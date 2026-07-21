using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Text;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Mission;

// Port of FUN_1004e648 (EV Override-11.c lines 32194-32281). Per-mission
// location-trigger handler, driven each tick by TickGovtEncounters over the
// active-mission table. When the player's current system matches the mission's
// trigger or destination system it fires the matching arrival/departure event:
// marks the mission state and, when the event carries a description id, plays
// its movie and posts the news text.
//
// DEVIATION (faithful): the decompile's 3-byte prefix copy (FUN_10076178 into the
// desc scratch buffer, lines 32223/32243/32265) is a dead write, since the next
// LoadDescriptionText copy overwrites the buffer from offset 0 before it can be
// read. The port skips it; do not re-add it.
public static class CheckMissionEncounter
{
    public static void Run(int missionIndex)
    {
        short missionIdx = (short)missionIndex;
        var rec = GameData.Missions[missionIdx];

        // ---- Player is IN the mission's trigger system ----
        if (GameData.Player.NavTargetSpob == rec.TargetSpob)
        {
            GameData.MissionStates[missionIdx].ArrivedAtTarget = 1;

            // ARRIVAL: arrive-state, not yet picked up, and cargo fits.
            if (rec.PickupMode == MissionCargoPickupMode.AtDestination && rec.CargoPickedUp == 0)
            {
                bool hasCargoSpace = ValidateMissionCargoSpace.Run(rec.CargoStringIndex, rec.CargoMass);
                if (hasCargoSpace)
                {
                    rec.CargoPickedUp = 1;
                    FireDescription(missionIdx, rec.LoadCargoText, hideCursorBeforeRefresh: false);
                }
            }

            // DEPARTURE: no longer arrive-state but cargo was picked up.
            if (rec.DropOffMode == 0 && rec.CargoPickedUp != 0)
            {
                rec.CargoPickedUp = 0;
                FireDescription(missionIdx, rec.DumpCargoText, hideCursorBeforeRefresh: false);
                WorldState.HudStatusPanelDirty = 1;
            }
        }

        // ---- Player reached the mission's DESTINATION system ----
        if (GameData.Player.NavTargetSpob == rec.ReturnSpob && rec.DropOffMode == 1)
        {
            rec.CargoPickedUp = 0;
            FireDescription(missionIdx, rec.DumpCargoText, hideCursorBeforeRefresh: true);
            WorldState.HudStatusPanelDirty = 1;
        }
    }

    // Arrival/departure/destination are identical except that the destination
    // path hides the cursor again before the final refresh.
    private static void FireDescription(short missionIdx, short descId, bool hideCursorBeforeRefresh)
    {
        if (descId == -1) return;
        if (WorldState.IsCursorHiddenByGame) MacToolbox.ShowCursor();
        // PlayMovieById returns nonzero UNLESS the movie is a one-shot it already
        // consumed; nonzero means the caller should present its own text fallback.
        byte showFallbackText = (byte)PlayMovieById.Run(descId, 1);
        if (showFallbackText != 0)
        {
            TextScratch.Text = LoadDescriptionText.Load(descId);
            SubstituteMissionDescTags.Run(0, missionIdx);   // expand mission tokens
            AlertText.Message = TextScratch.Text;             // post as the news text
            DoSceneTransition.Run(0, 0);                      // post the news entry
        }
        RepaintGameWindow.Run();
        if (hideCursorBeforeRefresh && WorldState.IsCursorHiddenByGame) MacToolbox.HideCursor();
        PlayMovieById.Run(descId, 0);                         // passive refresh of the movie window
    }
}
