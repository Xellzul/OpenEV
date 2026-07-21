namespace OpenEV.Override.Ports.Dialog.Model;

// The fixed-BSS "dialog scratch" globals the in-game dialog functions share
// (only one such dialog is open at a time, so several cells are reused by
// different dialogs — the name records the dominant / structural role).
//
// MANAGED: the scalar cells, dialog/window/handle pointer cells and
// the PICT-handle arrays now live as typed C# fields below. The STRING/TEXT
// buffers and the Rect scratch are managed too — plain C# string / short[4]
// fields passed directly into the real toolbox text/rect routines (GetIndString,
// TETextBox, DrawString, OffsetRect, …); they were the last EvoMemory-backed
// address consts here before that migration.
public static class DialogScratch
{
    // Test-only: when set, BarDialogFilter's mash timer does NOT fire the bar-person
    // encounter (item 6). Lets the EVO_SPACEPORTBAR button test drive the 4 bar buttons
    // without the person popping the mission dialog mid-test. Inert in normal play; the
    // bar-person flow itself is covered by EVO_MISSIONOFFER.
    public static bool SuppressBarMashTimer;

    // 0x10080bdc (UI foreground colour) — migrated to Graphics.Model.UiColors.DialogFore.

    // ── 0x10085dxx group (spaceport/death/bribe dialogs) — MANAGED ──
    // &DAT_10085d50 PICT-handle array (spaceport/BBS button picts, 12 ints d50..d7c).
    public static readonly int[] SpaceportPicts = new int[12];
    // Bribe-dialog button picts (d80..d8c).
    public static readonly int[] BribeBtnPicts = new int[4];
    public static int BribeBtnPictB0 { get => BribeBtnPicts[0]; set => BribeBtnPicts[0] = value; }
    public static int BribeBtnPictB0Sel { get => BribeBtnPicts[1]; set => BribeBtnPicts[1] = value; }
    public static int BribeBtnPictB1 { get => BribeBtnPicts[2]; set => BribeBtnPicts[2] = value; }
    public static int BribeBtnPictB1Sel { get => BribeBtnPicts[3]; set => BribeBtnPicts[3] = value; }
    public static int BarNewsPictHandle;        // _DAT_10085d90, the bar news-terminal PICT 9000 (RunBarNewsDialog)
    public static int SpaceportDialogRecord;    // _DAT_10085d94, spaceport / alert-text dialog record
    public static int BarNewsDialogWindow;      // _DAT_10085d98, the bar news-terminal dialog window
    public static int BribeDialogPtr;           // _DAT_10085d9c, bribe dialog record

    // ── Text buffers — managed strings (were EvoMemory-backed address buffers) ──
    // 0x10085da0 (AlertTextBuffer) -> MANAGED BarDescText below.
    // The spaceport-bar dësc text (was the shared alert/desc C-string buffer
    // 0x10085da0; RunSpaceportBarDialog fills, RedrawBarDialog TETextBoxes).
    public static string BarDescText = "";
    // The hailed ship's class/type + govt labels (were the Pascal buffers
    // 0x10086e84 / 0x10086f84; SpaceportPersonDialog fills from 'STR '/STR#,
    // DrawOutfitterItemPanel draws).
    public static string SpaceportNameText = "";
    public static string SpaceportGovtText = "";
    // 0x100861a0 ("DeathMsgText2" misname) -> MANAGED Dialog.Model.SpaceportGlobals.BarNewsLineB
    // 0x100865a0 ("DeathMsgText1" misname) -> MANAGED Dialog.Model.SpaceportGlobals.BarNewsLineA
    // &DAT_100866a0 scrollbar-strip Rect scratch (DrawScrollbarPict) — MANAGED short[4]
    // {top,left,bottom,right}.
    public static readonly short[] ScrollbarStripRect = new short[4];
    // 0x100866b6 ("BbsMissionListBuffer") -> MANAGED Dialog.Model.SpaceportGlobals.BarPersonQueue
    // The shared comm/hail message (was the Pascal scratch &DAT_10086b84;
    // LoadIndexedSpobString/LoadIndexedRebellionString fill it, the comm-dialog
    // panels read it).
    public static string SpaceportHailText = "";
    // The bar-person greeting (was the buffer 0x10086c84; BuildBarDescription
    // fills, SpaceportPersonDialog copies into the hail text).
    public static string SpaceportGreetText = "";
    // The spob hail/desc text (was the 256-byte buffer 0x10086d84 / toc-0x18dc).
    public static string SpaceportDescText = "";

    // ── Comm-face / slot-machine reel state (0x100866a8..66b5) — MANAGED ──
    public static readonly short[] CommFaceX = new short[3];  // &DAT_100866a8 + i*2, reel scroll offsets
    public static readonly short[] CommFaceTimer = new short[3];  // &DAT_100866ae + i*2, reel timers
    public static short SpaceportMashCounter;   // _DAT_100866b4, auto-trade button-mash counter (SHORT; earlier Int forms spanned the BBS buffer)

    // ── 0x10086axx group (active in-game dialog state; union — reused) — MANAGED ──
    public static short SpaceportCommFaceIndex; // 0x10086ab6, comm-face cycle index
    public static int SpaceportCommFacePtrA;  // 0x10086ab8 -> comm-face image Ptr (GetResource 0x1cc)
    public static int SpaceportCommFacePtrB;  // 0x10086abc -> comm-face image Ptr (GetResource 0x1cd)
    public static int SpaceportCommDialogRecord; // _DAT_10086ac0 GetNewDialog(0x3ef); spaceport & comm dialogs
    // Spaceport-person dialog (FUN_1000f4f8, DLOG 0x3ef) modal-filter proc key —
    // the PEF-relocated UPP source cell 0x10080d68 holds FUN_100108b0
    // (Dialog.SpaceportPersonDialogFilter).
    public const int PersonFilterProc = 0x100108b0;
    // Planet DOMINATION/tribute comm dialog (FUN_10010f70, DLOG 0x3f1) modal-filter
    // proc key — the PEF-relocated UPP source cell 0x10080d28 holds FUN_100120cc
    // (Outfit.TradeDockRefuelDialogFilter).
    public const int DominateFilterProc = 0x100120cc;
    public static int BuyShipDialogRecord;    // _DAT_10086ac4 GetNewDialog(0x3f0); buyship / game-speed / comm-status
    // Modal-filter proc key for the 0x3f0 two-button dialog family (the
    // PEF-relocated UPP source cell 0x10080cf8 holds FUN_100134dc =
    // Dialog.TwoButtonDialogFilter).
    public const int TwoButtonFilterProc = 0x100134dc;
    public static int BoardingDialogRecord;     // _DAT_10086ac8; boarding / six-button dialogs
    public static short BuyShipMode;            // 0x10086acc: 1=sell, 2=buy
    public static short SpaceportGreetIndex;    // 0x10086ace
    public static short SpaceportSelCellA;      // 0x10086ad0, reset -1 at spaceport-comm open (toc-0x1b90)
    public static short SpaceportSelCellB;      // 0x10086ad2, reset -1 at spaceport-comm open (toc-0x1b8e)
    // ad4..ade are ADJACENT SHORTS — the earlier ReadInt/WriteInt forms on them were
    // width bugs that spanned the neighbouring cell.
    public static short BoardingSalvageCargoIndex; // 0x10086ad4
    public static short BoardingSalvageCargoQty;     // 0x10086ad6
    public static short BoardingSalvageAmmoType; // 0x10086ad8
    public static short BoardingSalvageAmmoQty;  // 0x10086ada
    public static short BoardingSalvageFuel;      // 0x10086adc
    public static short BoardingCaptureChance; // 0x10086ade
    public static short SpaceportBribeRoll;     // 0x10086ae0, rng(100) bribe-willingness roll (<0 = unrolled)
    public static int BoardingSalvageCredits;        // 0x10086ae4 (int)
    // SpaceportBribeFine (0x10086ae8) + BuyShipPriceCell (0x10086aec) live in
    // Core.Model.GameData.BribeFine / .BuyShipPriceCell.
    public static float SpaceportBribeAmount;   // _DAT_10086af0
    public static byte CommHailGateFlag;        // 0x10086af4: comm/hail button gating (tocBase-0x1b6c)
    public static byte SpaceportCanBribeFlag;   // 0x10086af5
    public static byte SpaceportFlag;           // 0x10086af6
    public static byte SpaceportNoTradeFlag;    // 0x10086af7
    public static byte SpaceportHiredFlag;      // 0x10086af8
    // Comm-dialog button picts (&DAT_10086afc, 10 ints afc..b20).
    public static readonly int[] CommButtonPicts = new int[10];

    // The 2-button confirm (DLOG 0x3fa) PICT handles — was the live-heap array
    // behind ptr cell 0x10080c60. Filled by RunConfirmYesNoDialog (PICTs
    // 0x1bca/0x1bcb/0x1bc8/0x1bc9), drawn by
    // Render2ButtonRow: [idx*2] = normal, [idx*2+1] = selected, per button.
    public static readonly int[] ConfirmButtonPicts = new int[4];

    // The alert/briefing OK-button PICT pair — was the 2-entry heap array behind
    // ptr cell 0x10080c1c (0x1b8e normal / 0x1b8f pressed). Loaded by
    // RunAboutDialog + DoSceneTransition, drawn by Graphics.DrawButtonPressed.
    public static readonly int[] ButtonPictPair = new int[2];

    // The picture-alert's PICT handle — was the cell behind ptr cell 0x10080fdc
    // (RunAboutDialog stores GetPicture(0x96); RedrawPictureAlertDialog draws it).
    public static int AlertPictHandle;

    // The generic-alert DialogPtr — was the cell behind ptr cell 0x10080fe8
    // (ShowGenericAlert stores GetNewDialog(0x82); DisposeCurrentAlertDialog disposes).
    public static int GenericAlertDialog;
    public static int CommBtnPictB2Sel { get => CommButtonPicts[1]; set => CommButtonPicts[1] = value; }
    public static int CommBtnPictB1Act { get => CommButtonPicts[2]; set => CommButtonPicts[2] = value; }
    public static int CommBtnPictB1ActSel { get => CommButtonPicts[3]; set => CommButtonPicts[3] = value; }
    public static int CommBtnPictB1 { get => CommButtonPicts[4]; set => CommButtonPicts[4] = value; }
    public static int CommBtnPictB1Sel { get => CommButtonPicts[5]; set => CommButtonPicts[5] = value; }
    public static int CommBtnPictB2Act { get => CommButtonPicts[6]; set => CommButtonPicts[6] = value; }  // GetPicture(0x1bac)
    public static int CommBtnPictB2ActSel { get => CommButtonPicts[7]; set => CommButtonPicts[7] = value; }  // GetPicture(0x1bad)
    public static int CommBtnPictHail0 { get => CommButtonPicts[8]; set => CommButtonPicts[8] = value; }
    public static int CommBtnPictHail1 { get => CommButtonPicts[9]; set => CommButtonPicts[9] = value; }
    // Buy-ship / game-speed button picts (&DAT_10086b3c, 4 ints — the SAME cells,
    // the dialogs are never open simultaneously).
    public static readonly int[] BuyShipPicts = new int[4];
    public static int[] GameSpeedButtonPicts => BuyShipPicts;
    // Boarding / six-button picts (&DAT_10086b4c, 12 ints b4c..b78).
    public static readonly int[] BoardingPicts = new int[12];
    public static int SpaceportPersonPict;      // 0x10086b7c, person-portrait PICT handle
    public static int DialogShipPtr;            // _DAT_10086b80, current person / comm-target ship record ptr
}
