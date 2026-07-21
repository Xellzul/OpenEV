using OpenEV.Platform.Toolbox;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Text;

namespace OpenEV.Override.Ports.Mission;

// FUN_1004c300 (EV Override-11.c lines 31398-31483). Resolves a mission slot on the
// mission-FAILED branch (sole caller TickGovtEncounters, the Failed != 0 case): fires
// the fail-bit control links (re-arming matching cron events), applies the
// system-status penalty, shows the fail movie + its dësc text, confiscates the
// pay-encoded outfit, then aborts and clears the slot.
public static class ApplyMissionFailure
{
    public static void Run(int missionIdx)
    {
        short m = (short)missionIdx;
        var mission = GameData.Missions[m];

        for (short i = 0; i < MissionRecord.FailBitCount; i = (short)(i + 1))
        {
            short link = i == 0 ? mission.FailBitA : mission.FailBitB;
            if (-1 < link && link < 512)
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
            if (999 < link && link < 1512)
            {
                ControlBits.Set(link - 1000, 0);
            }
        }

        // System-status penalty: every system governed by CargoType loses CargoQty / 2.
        // (CargoType/CargoQty are the loader's cargo fields, reused here as the reward
        // government and amount.)
        if (mission.CargoType != -1)
        {
            for (short i = 0; i < SystTable.Count; i = (short)(i + 1))
            {
                if (SystTable.Store[i].Govt == mission.CargoType)
                {
                    short reward = mission.CargoQty;
                    // reward / 2 is the ASM's srawi+addze signed truncating divide (rounds toward
                    // zero); NOT >> 1, which floors and diverges for a negative reward.
                    GalaxyMapGlobals.SetSystemStatus(i,
                        (short)(GalaxyMapGlobals.SystemStatus(i) - reward / 2));
                }
            }
        }

        GameData.MissionStates[m].IsActive = 0;

        // Fail movie + its dësc text into the alert box.
        // NO-OP: the decompile pre-copies a stub string into the scratch buffer here
        // (FUN_10076178) before this; dead, since LoadDescriptionText.Load below
        // unconditionally overwrites the whole buffer before it can be read. The port
        // skips it.
        if (mission.FailText != -1)
        {
            if (WorldState.IsCursorHiddenByGame)
            {
                MacToolbox.ShowCursor();
            }
            byte loaded = (byte)PlayMovieById.Run(mission.FailText, 1);
            if (loaded != 0)
            {
                TextScratch.Text = LoadDescriptionText.Load(mission.FailText);
                SubstituteMissionDescTags.Run(0, m);
                AlertText.Message = TextScratch.Text;
                DoSceneTransition.Run(0, 0);
            }
            RepaintGameWindow.Run();
            if (WorldState.IsCursorHiddenByGame)
            {
                MacToolbox.HideCursor();
            }
            PlayMovieById.Run(mission.FailText, 0);
        }

        // Pay-encoded confiscation: RemoveGrantedOutfitOnAbort with Pay < -30127;
        // |Pay| - 30128 names the outfit whose whole owned stack is removed.
        if ((mission.Flags & MisnFlags.RemoveGrantedOutfitOnAbort) != 0 && mission.Pay < -30127)
        {
            double absPay = EvMath.FloatAbs((double)(float)mission.Pay);
            short outfit = (short)(int)(-30128f + absPay);
            if (-1 < outfit && outfit < 128 && 0 < OwnedOutfitGrid.Store[outfit])
            {
                OwnedOutfitGrid.Store[outfit] = 0;
                RebuildMarketFromOwnedOutfits.Run();
            }
        }

        AbortMission.Run(m);
        SpaceportGlobals.BbsLastSpob = -1;
    }
}
