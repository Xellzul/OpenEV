using OpenEV.Override.Ports.Systems.Model;
using OpenEV.Override.Ports.Core.Model;

namespace OpenEV.Override.Ports.Misc;

// Port of FUN_10024c38 (EV Override-11.c lines 15870-15955).

public static class RecomputeWorldVisibility
{
    public static void Run()
    {
        short systemIndex = 0;
        short historySlot;
    LAB_10024fa0:
        if (SystTable.Count <= systemIndex)
        {
            for (short spobIndex = 0; spobIndex < SpobTable.Count; spobIndex = (short)(spobIndex + 1))
            {
                GameData.Spobs[spobIndex].Visible = 0;
                short spobSyst = GameData.Spobs[spobIndex].System;
                if (-1 < spobSyst && spobSyst < SystTable.Count &&
                    SystTable.Store[spobSyst].ShownFlag != 0)
                {
                    GameData.Spobs[spobIndex].Visible = 1;
                }
            }
            return;
        }
        byte wasVisible = SystTable.Store[systemIndex].ShownFlag;
        SystTable.Store[systemIndex].ShownFlag = 0;
        if (-32000 < SystTable.Store[systemIndex].Govt)
        {
            if (SystTable.Store[systemIndex].Visibility < 0)
            {
                SystTable.Store[systemIndex].ShownFlag = 1;
            }
            else if (SystTable.Store[systemIndex].Visibility < ControlBits.Count &&
                    -1 < SystTable.Store[systemIndex].Visibility)
            {
                if (ControlBits.Get(SystTable.Store[systemIndex].Visibility) == 0)
                {
                    SystTable.Store[systemIndex].ShownFlag = 0;
                }
                else
                {
                    SystTable.Store[systemIndex].ShownFlag = 1;
                }
            }
            else if (999 < SystTable.Store[systemIndex].Visibility &&
                    SystTable.Store[systemIndex].Visibility < 1000 + ControlBits.Count)
            {
                // Same ControlBits array as the band above, offset by the alias base (see ControlBits
                // header) — polarity is intentionally INVERTED vs the band above (here, bit CLEAR means
                // shown; above, bit SET means shown). Not a copy-paste bug — don't "fix" it to match.
                if (ControlBits.Get(SystTable.Store[systemIndex].Visibility - 1000) == 0)
                {
                    SystTable.Store[systemIndex].ShownFlag = 1;
                }
                else
                {
                    SystTable.Store[systemIndex].ShownFlag = 0;
                }
            }
        }
        if (SystTable.Store[systemIndex].ShownFlag == 0)
        {
            if (wasVisible != 0)
            {
                if (GameData.Player.NavMode == 3 && GameData.Player.NavTargetSpob == systemIndex)
                {
                    GameData.Player.NavTargetSpob = -1;
                    WorldState.SpawnPulseDirty = 1;
                }
                for (historySlot = 0; historySlot < GalaxyMapGlobals.NavHistoryLength; historySlot = (short)(historySlot + 1))
                {
                    if (systemIndex == GalaxyMapGlobals.NavHistory[historySlot])
                        goto LAB_10024f7c;
                }
            }
        }
        else
        {
            for (short otherSystemIndex = 0; otherSystemIndex < SystTable.Count; otherSystemIndex = (short)(otherSystemIndex + 1))
            {
                if (otherSystemIndex != systemIndex &&
                    SystTable.Store[otherSystemIndex].XPos == SystTable.Store[systemIndex].XPos &&
                    SystTable.Store[otherSystemIndex].YPos == SystTable.Store[systemIndex].YPos &&
                    SystTable.Store[systemIndex].Visited < SystTable.Store[otherSystemIndex].Visited)
                {
                    SystTable.Store[systemIndex].Visited = SystTable.Store[otherSystemIndex].Visited;
                }
            }
        }
        goto LAB_10024f9c;
    LAB_10024f7c:
        for (; historySlot < GalaxyMapGlobals.NavHistoryLength; historySlot = (short)(historySlot + 1))
        {
            GalaxyMapGlobals.NavHistory[historySlot] = -1;
        }
    LAB_10024f9c:
        systemIndex = (short)(systemIndex + 1);
        goto LAB_10024fa0;
    }
}
