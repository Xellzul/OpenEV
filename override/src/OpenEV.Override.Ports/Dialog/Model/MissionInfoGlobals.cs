using OpenEV.Override.Ports.Mission.Model;

namespace OpenEV.Override.Ports.Dialog.Model;

// Managed home for the ACTIVE-MISSIONS info dialog globals (the FUN_1004fa88
// family: RunMissionInfoDialog + MissionSelectDialogFilter +
// BuildMissionsListBox + RedrawMissionSelectDialog + the 2-button row).
public static class MissionInfoGlobals
{
    // *0x10080c44 (toc-0x7a1c, ptr -> int cell): the open mission-info dialog window.
    public static int DialogWindow;

    // *0x10080c3c: the 2 {normal, pressed} PICT pairs of the button row
    // (0x1b62/0x1b63 leave, 0x1bb4/0x1bb5 abort).
    public static readonly int[] Picts = new int[4];

    // *0x10081134 (toc-0x752c, ptr -> cell): the List Manager ListHandle of the
    // missions list box (the port's List Manager is a stub — LNew returns 0).
    public static int ListHandle;

    // *0x10081128 (toc-0x7538, ptr -> short): selected list row (-1 = none).
    public static short SelectedRow;

    // *0x10081138: row -> active-mission slot (MissionTable/MissionStateTable index)
    // map for the 8 mission slots (-1 = empty row).
    public static readonly short[] RowToMissionSlot = new short[MissionStateTable.Count];

    // Modal-filter proc key (was UPP source cell 0x1008112c -> FUN_10050230).
    public const int FilterProc = 0x10050230;
}
