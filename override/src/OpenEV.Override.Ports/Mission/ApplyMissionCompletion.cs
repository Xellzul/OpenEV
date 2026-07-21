using System;
using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Text;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Graphics;

namespace OpenEV.Override.Ports.Mission;

// FUN_1004ba48 (EV Override-11.c 31243-31392) — apply a completed mission's rewards:
// play the resolution movie + dësc text, deactivate the slot, fire the four CompletionBit
// links (re-arming matching crons), apply the system-status reward, then decode the PAY
// field into the credit / status-clear / outfit / fine bands handled inline below.
public static class ApplyMissionCompletion
{
    public static void Run(int missionIdx)
    {
        short g = (short)missionIdx;
        var mission = GameData.Missions[g];

        // Resolution movie + its dësc text into the alert box.
        if (mission.CompletionText != -1)
        {
            if (WorldState.IsCursorHiddenByGame)
            {
                MacToolbox.ShowCursor();
            }
            byte loaded = (byte)PlayMovieById.Run(mission.CompletionText, 1);
            if (loaded != 0)
            {
                // NO-OP: the decompile pre-copies a 3-byte stub string into TextScratch here
                // (FUN_10076178, unk_820D8) before this load — dead, since FUN_100197d8
                // (EV Override-11.c 11568-11587) unconditionally overwrites the whole buffer
                // via FUN_1007615c's strcpy (49639-49655) on both the found- and
                // missing-resource branches, so the stub can never survive to be read.
                TextScratch.Text = LoadDescriptionText.Load(mission.CompletionText);
                SubstituteMissionDescTags.Run(0, g);
                AlertText.Message = TextScratch.Trunc(TextScratch.Text, 1023);
                DoSceneTransition.Run(0, 0);
            }
            RepaintGameWindow.Run();
            if (WorldState.IsCursorHiddenByGame)
            {
                MacToolbox.HideCursor();
            }
            PlayMovieById.Run(mission.CompletionText, 0);
        }

        GameData.MissionStates[g].IsActive = 0;

        // Resolution control bits (CompletionBit A..D).
        for (short k = 0; k < MissionRecord.CompletionBitCount; k = (short)(k + 1))
        {
            short link = k switch { 0 => mission.CompletionBitA, 1 => mission.CompletionBitB, 2 => mission.CompletionBitC, _ => mission.CompletionBitD };
            if (-1 < link && link < ControlBits.Count)
            {
                ControlBits.Set(link, 1);
                foreach (var cron in GameData.Crons)
                {
                    if (cron.ControlBit == link && 0 < cron.DurationDays)
                    {
                        cron.StateCountdown = cron.DurationDays;
                    }
                }
            }
            if (999 < link && link < 1000 + ControlBits.Count)
            {
                ControlBits.Set(link - 1000, 0);
            }
        }

        // System-status reward (CargoType/CargoQty fields reused as reward-govt/amount):
        // matching-govt systems gain the full amount; enemy systems lose, allied systems gain
        // (half-CargoQty steps, see the FMA notes below).
        if (mission.CargoType != -1)
        {
            for (short s = 0; s < SystTable.Count; s = (short)(s + 1))
            {
                short systGovt = SystTable.Store[s].Govt;
                if (systGovt == mission.CargoType)
                {
                    GalaxyMapGlobals.SetSystemStatus(s, (short)(GalaxyMapGlobals.SystemStatus(s) + mission.CargoQty));
                }
                else if (systGovt != -1)
                {
                    if (mission.CargoType == GameData.Governments[systGovt].Enemy ||
                        systGovt == GameData.Governments[mission.CargoType].Enemy)
                    {
                        // ASM: loc_4BE68 fnmsub f0,f3(0.5),f2(CargoQty),f0(Status) = -(0.5*CargoQty − Status),
                        // one fused rounding — do not simplify to plain double arithmetic (double-rounds).
                        GalaxyMapGlobals.SetSystemStatus(s,
                             (short)(int)-Math.FusedMultiplyAdd(0.5, mission.CargoQty, -GalaxyMapGlobals.SystemStatus(s)));
                    }
                    else if (mission.CargoType == GameData.Governments[systGovt].Ally ||
                             systGovt == GameData.Governments[mission.CargoType].Ally)
                    {
                        // ASM: loc_4BF64 fmadd f0,f3(0.5),f2(CargoQty),f0(Status) = 0.5*CargoQty + Status,
                        // one fused rounding — do not simplify to plain double arithmetic (double-rounds).
                        GalaxyMapGlobals.SetSystemStatus(s,
                             (short)(int)Math.FusedMultiplyAdd(0.5, mission.CargoQty, GalaxyMapGlobals.SystemStatus(s)));
                    }
                }
            }
        }

        // Pay decode. Positive: straight credits award.
        if (0 < mission.Pay)
        {
            GameData.Player.Credits = GameData.Player.Credits + mission.Pay;
            WorldState.HudStatusPanelDirty = 1;
        }
        // -10000 band: (|pay| - 10128) names a govt whose systems' negative status clears.
        if (mission.Pay < -9999 && -20000 < mission.Pay)
        {
            double absPay = EvMath.FloatAbs((double)(float)mission.Pay);
            short statusGovt = (short)(int)(-10128f + absPay);
            for (short s = 0; s < SystTable.Count; s = (short)(s + 1))
            {
                if (statusGovt == SystTable.Store[s].Govt && GalaxyMapGlobals.SystemStatus(s) < 0)
                {
                    GalaxyMapGlobals.SetSystemStatus(s, 0);
                }
            }
        }
        // -20000 band: (|pay| - 20128) names an outfit awarded to the player.
        if (mission.Pay < -19999 && -30000 < mission.Pay)
        {
            double absPay = EvMath.FloatAbs((double)(float)mission.Pay);
            short outfit = (short)(int)(-20128f + absPay);
            if (-1 < outfit && outfit < OwnedOutfitGrid.Count)
            {
                OwnedOutfitGrid.Store[outfit] = (short)(OwnedOutfitGrid.Store[outfit] + 1);
                RebuildMarketFromOwnedOutfits.Run();
            }
        }
        // -40000 band (-40001..-40099): fine (|pay| - 40000) percent of the player's credits.
        if (mission.Pay < -40000 && -40100 < mission.Pay)
        {
            double absPay = EvMath.FloatAbs((double)(float)mission.Pay);
            short pct = (short)(int)(-40000f + absPay);
            double credits = GameData.Player.Credits;
            // ASM: loc_4C1C8 fmul f2,dbl_820E0(0.01),pct (0.01×pct, one rounding) then fnmsub
            // f0,f3(credits),f2,f0(credits) = -(credits*(0.01*pct) − credits), the multiply-subtract
            // fused (one rounding) — plain (credits*0.01)*pct double-rounds; don't collapse it.
            double fineFraction = 0.01 * pct;
            GameData.Player.Credits = (int)-Math.FusedMultiplyAdd(credits, fineFraction, -credits);
        }

        SpaceportGlobals.BbsLastSpob = -1;
    }
}
