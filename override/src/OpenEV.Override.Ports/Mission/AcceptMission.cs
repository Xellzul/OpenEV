using OpenEV.Platform.Toolbox;
using OpenEV.Platform.EvoData.Resources.Flags;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.GalaxyMap;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Mission.Model;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Text;

namespace OpenEV.Override.Ports.Mission;

// Port of FUN_1004a570 (EV Override-11.c 30826-31005) — accept the offered mission
// `missionIdx`: claims the first free mission slot, copies the mission's display name
// from the 'mïsn' NameTable, loads the 'mïsn' resource into the slot record
// (LoadMissionResource), advances the deadline date, flips the def-table ControlBits
// link, plays the OnAccept text/scene, grants a Pay-encoded outfit, and compacts the
// BBS/bar availability grids. Returns 1 on accept, 0 on no-slot/no-room.
public static class AcceptMission
{
    // A mission Pay in (-40000, -30127) encodes a granted outfit: Pay = -(30128 + outfitIdx).
    private const float OutfitGrantBias = -30128.0f;

    public static int Run(int missionIdx)
    {
        short slot = -1;
        for (short i = 0; i < MissionStateTable.Count; i = (short)(i + 1))
        {
            if (GameData.MissionStates[i].IsActive == 0)
            {
                slot = i;
                break;
            }
        }
        if (slot == -1)
        {
            return 0;
        }

        short defIdx = (short)missionIdx;
        if (0 < GameData.MissionDefs[defIdx].CargoQty)
        {
            short effectiveCargoMax = (short)ShipDerivedStats.EffectiveCargoMax();
            if (effectiveCargoMax < GameData.MissionDefs[defIdx].CargoQty)
            {
                AlertText.Message = "Your ship doesn’t have enough cargo space to accept this mission.";
                DoSceneTransition.Run(0, 0);
                RepaintGameWindow.Run();
                return 0;
            }
            short freeCargo = (short)FreeCargoSpaceWithMissions.Run();
            if (freeCargo < GameData.MissionDefs[defIdx].CargoQty)
            {
                AlertText.Message = "Your ship doesn’t have enough free cargo space to accept this mission. Sell or jettison some and try again.";
                DoSceneTransition.Run(0, 0);
                RepaintGameWindow.Run();
                return 0;
            }
        }

        var missionState = GameData.MissionStates[slot];
        var mission = GameData.Missions[slot];
        missionState.IsActive = 1;
        missionState.Failed = 0;
        missionState.ArrivedAtTarget = 0;
        missionState.ObjectiveComplete = 0;
        mission.DestroyedShipCount = 0;
        mission.DepartedShipCount = 0;
        mission.MissionShipsSpawnedCount = 0;
        mission.BoardedShipCount = 0;
        mission.DisabledShipCount = 0;
        // The display name BuildMissionsListBox reads, copied from the 'mïsn' NameTable;
        // LoadMissionResource then fills the rest of the slot record.
        mission.MissionName = TextScratch.Trunc(MissionBoardGlobals.Names[defIdx] ?? "", 127);
        LoadMissionResource.Run(missionIdx, slot);
        mission.MissionDefIndex = defIdx;
        if (0 < mission.TimeLimit)
        {
            var newDate = GameDate.AdvanceDays(mission.TimeLimit);
            if (newDate.HasValue)
            {
                missionState.DeadlineYear = newDate.Value.Year;
                missionState.DeadlineMonth = newDate.Value.Month;
                missionState.DeadlineDay = newDate.Value.Day;
            }
        }

        short defControlBit = GameData.MissionDefs[defIdx].ControlBitLink;
        if (defControlBit < 0 || 999 < defControlBit)
        {
            if (999 < defControlBit && defControlBit < 1512)
            {
                ControlBits.Set(defControlBit - 1000, 0);
            }
        }
        else
        {
            ControlBits.Set(defControlBit, 1);
        }

        if (MissionBoardGlobals.DialogWindow != 0)
        {
            MacToolbox.HideWindow(MissionBoardGlobals.DialogWindow);
        }
        if (mission.AcceptText != -1)
        {
            if (PlayMovieById.Run(mission.AcceptText, 1) == 0)
            {
                if (MissionBoardGlobals.DialogWindow != 0)
                {
                    MacToolbox.HideWindow(MissionBoardGlobals.DialogWindow);
                }
                if (SpaceportGlobals.DialogWindow != 0 && RenderGlobals.DrawGateFlag == 0)
                {
                    RedrawSpaceportDialog.Run();
                }
            }
            else
            {
                // Faithful omission: the decompile's 3-byte prefix copy into the desc
                // scratch buffer (line 30927) is a dead write — LoadDescriptionText
                // overwrites the buffer from offset 0 before it can be read. Skipped;
                // do not re-add it (see CheckMissionEncounter.cs for the same pattern).
                TextScratch.Text = LoadDescriptionText.Load(mission.AcceptText);
                SubstituteMissionDescTags.Run(0, slot);
                AlertText.Message = TextScratch.Trunc(TextScratch.Text, 1023);
                if (MissionBoardGlobals.DialogWindow != 0)
                {
                    MacToolbox.HideWindow(MissionBoardGlobals.DialogWindow);
                }
                GalaxyMapState.PreviewSystem = -1;
                GalaxyMapGlobals.MissionsDirty = 1;
                if ((mission.Flags & MisnFlags.ShowGreenArrowInBrief) != 0)
                {
                    if (mission.TargetSpob == -1)
                    {
                        if (mission.ReturnSpob != -1)
                        {
                            GalaxyMapState.PreviewSystem = GameData.Spobs[mission.ReturnSpob].System;
                        }
                    }
                    else
                    {
                        GalaxyMapState.PreviewSystem = GameData.Spobs[mission.TargetSpob].System;
                    }
                }
                if (SpaceportGlobals.DialogWindow != 0 && RenderGlobals.DrawGateFlag == 0)
                {
                    RedrawSpaceportDialog.Run();
                }
                DoSceneTransition.Run(1, 0);
            }
            RepaintGameWindow.Run();
            GalaxyMapState.PreviewSystem = -1;
            PlayMovieById.Run(mission.AcceptText, 0);
        }

        if (mission.PickupMode == MissionCargoPickupMode.AtMissionStart)
        {
            mission.CargoPickedUp = 1;
        }
        if (GameData.Ships[0].NavTargetSpob == mission.TargetSpob && MissionBoardGlobals.DialogWindow != 0
            || mission.TargetSpob == -1)
        {
            missionState.ArrivedAtTarget = 1;
        }

        if (mission.Pay < -30127 && -40000 < mission.Pay)
        {
            // The (float) models the decompile's double->float rounding step
            // (PPC `fsubs`) — don't drop it.
            double payAbs = EvMath.FloatAbs((double)(float)mission.Pay);
            short outfitIdx = (short)(int)(OutfitGrantBias + payAbs);
            if (-1 < outfitIdx && outfitIdx < 128 && OwnedOutfitGrid.Store[outfitIdx] < 1)
            {
                OwnedOutfitGrid.Store[outfitIdx] = 1;
                RebuildMarketFromOwnedOutfits.Run();
            }
        }

        if (RenderGlobals.DrawGateFlag == 0 && MissionBoardGlobals.DialogWindow != 0)
        {
            // Compact both availability grids: drop this mission and every person no
            // longer eligible, packing survivors to the front.
            short savedMode = SpaceportGlobals.InBarFlag;
            SpaceportGlobals.InBarFlag = 0;
            short[] compacted = new short[516];   // decompile local_460; only [0..511] are used
            while (SpaceportGlobals.InBarFlag < MissionAvailGrid.ByMode.Length)
            {
                short kept = 0;
                for (short j = 0; j < MissionAvailGrid.Count; j = (short)(j + 1))
                {
                    compacted[j] = -1;
                }
                foreach (short pers in MissionAvailGrid.ByMode[SpaceportGlobals.InBarFlag])
                {
                    if (defIdx != pers && pers != -1 && IsBarPersEligible.Run(pers))
                    {
                        compacted[kept] = pers;
                        kept = (short)(kept + 1);
                    }
                }
                for (short j = 0; j < MissionAvailGrid.Count; j = (short)(j + 1))
                {
                    MissionAvailGrid.ByMode[SpaceportGlobals.InBarFlag][j] = compacted[j];
                }
                SpaceportGlobals.InBarFlag = (short)(SpaceportGlobals.InBarFlag + 1);
            }
            SpaceportGlobals.InBarFlag = savedMode;
        }
        GameData.RandomOdds[defIdx] = 0;
        return 1;
    }
}
