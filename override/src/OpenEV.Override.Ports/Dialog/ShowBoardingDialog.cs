using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.EvoMath;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Outfit;
using OpenEV.Override.Ports.Outfit.Model;
using OpenEV.Override.Ports.Pilot.Model;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;
using OpenEV.Override.Ports.Title;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Dialog;

// FUN_100138c8 (EV Override-11.c lines 9796-10199) — the BOARDING/salvage
// six-button dialog (DLOG 0x3f3, "Select what to salvage..."): take cargo (2),
// steal credits (3), take ammo (4), transfer fuel (6), attempt capture (7),
// leave (1). A patience counter starts at rng(26)+15 and is SCALED after
// every action; when rng(100) <= patience the boarded ship's self-destruct
// fires (the post-loop punishment path). Formerly named ShowSpaceportBarterDialog —
// "Barter"/"Spaceport" were early transcription misnames (it is the in-combat boarding
// dialog, not a spaceport trade); renamed to ShowBoardingDialog 2026-07-14.
//
// Dialog 4-rules rewrite (B8): message building -> C# strings (stack bufs
// gone); data-seg doubles/strings dumped to literals; filter UPP cell read ->
// typed BoardingDialogFilter registration; win+0x10 -> GetDialogPortRect.
public static class ShowBoardingDialog
{
    // Modal-filter proc key — the UPP source cell 0x10080cf0 held the PEF-relocated
    // FUN_100153a0 TVector (= BoardingDialogFilter, the six-button
    // boarding filter; typed in B7, registration flipped here in B8).
    public const int BoardingFilterProc = 0x100153a0;

    // Port bridge for the modal-filter UPP — typed MacEvent shape (dialog 4-rules B8).
    private static int FilterAdapter(int dialog, MacEvent evt)
    {
        short itemHit = 0;
        int r = BoardingDialogFilter.Run(dialog, evt, ref itemHit); evt.ItemHit = itemHit; return r;
    }

    // ── Data-seg tunables (dumped via tools/dump_dataseg.py) ──
    // NOTE: a prior-batch flag claimed "patience × ReadDouble(PriceLinearThresholdConst)
    // ≈ 6.05e12" — that was wrong. The cell this code reads, 0x10081c70 (toc-0x69f0,
    // = CommodityPricing.PriceLinearThresholdConst), holds plain double 2.0.
    // The 6.05e12 figure came from misreading the NEIGHBOUR cell 0x10081c60 — a
    // FLOAT 75.0f whose 8-byte double interpretation is 6.0473e12.
    private const double PatienceScaleTrade = 2.0;    // *0x10081c70 (toc-0x69f0) — cargo/ammo salvage
    private const double PatienceScaleSteal = 1.25;   // *0x10081c68 (toc-0x69f8) — credits steal
    private const double PatienceScaleFuel = 1.5;    // *0x10081c90 (toc-0x69d0) — fuel transfer
    // *0x10081c78 (toc-0x69e8) dumps as 8 zero bytes; the 4-byte bit-copy the
    // decompile writes into ship +0x1c is therefore float 0.0f.
    private const float DisabledTimerReset = 0f;
    private const float EscortExitDistance = 75.0f;  // float @0x10081c60 (toc-0x6a00)

    // ── Data-seg strings (dumped; MacRoman 0xd5 = ’) ──
    private const string MsgNoCargoRoom = "You couldn’t store any of the cargo you salvaged from this ship.";   // P @0x10082a3b (toc-0x5c25)
    private const string MsgSalvagedPrefix = "You salvaged ";                                                      // C @0x10082a7c (toc-0x5be4)
    private const string MsgTonsOf = " tons of ";                                                          // C @0x10082a8a (toc-0x5bd6)
    private const string MsgFromThisShip = " from this ship.";                                                   // C @0x10082a94 (toc-0x5bcc)
    private const string MsgStoleCredits = "You stole all the credits from this ship.";                          // P @0x10082aa5 (toc-0x5bbb)
    private const string MsgNoAmmoRoom = "You couldn’t store any of the ammo you salvaged from this ship.";    // P @0x10082af0 (toc-0x5b70)
    private const string MsgWordSeparator = " ";                                                                  // C @0x10081c24 (toc-0x6a3c)
    private const string MsgTanksFilled = "You filled your tanks with fuel from this ship.";                    // P @0x10082b30 (toc-0x5b30)
    private const string MsgFuelDrained = "You transferred all of this ship’s fuel to your tanks.";             // P @0x10082b60 (toc-0x5b00)
    private const string MsgNoFuelRoom = "You couldn’t store any of the fuel you transferred from this ship."; // P @0x10082b97 (toc-0x5ac9)
    private const string MsgCaptureFailed = "Your attempt to capture this ship was unsuccessful.";                // P @0x10082ccc (toc-0x5994)
    private const string MsgSelfDestructSp = " Oops! You tripped this ship’s security self-destruct mechanism.";   // P @0x10082bda (toc-0x5a86) — leading space in the data seg
    private const string MsgMaxEscorts = "You already have your maximum number of escorts.";                   // P @0x10082c9b (toc-0x59c5)
    private const string MsgEscortAssigned = "You assigned this ship to your fleet of escorts.";                   // P @0x10082c6a (toc-0x59f6)
    private const string MsgRenamePrompt = "Now rename this captured ship:";                                     // P @0x10082c1b (toc-0x5a45)
    private const string MsgCaptureDeclined = "You decided not to capture this ship after all.";                   // P @0x10082c3a (toc-0x5a26)
    private const string MsgSelfDestruct = "Oops! You tripped this ship’s security self-destruct mechanism.";    // P @0x100829fb (toc-0x5c65)
    // Number words for the ammo-salvage count (C strings).
    private const string WordOne = "one";    // @0x10081c14 (toc-0x6a4c)
    private const string WordTwo = "two";    // @0x10081c18 (toc-0x6a48)
    private const string WordThree = "three";  // @0x10082acf (toc-0x5b91)
    private const string WordFour = "four";   // @0x10082ad5 (toc-0x5b8b)
    private const string WordFive = "five";   // @0x10082ada (toc-0x5b86)
    private const string WordSix = "six";    // @0x10081c1c (toc-0x6a44)
    private const string WordSeven = "seven";  // @0x10082adf (toc-0x5b81)
    private const string WordEight = "eight";  // @0x10082ae5 (toc-0x5b7b)
    private const string WordNine = "nine";   // @0x10082aeb (toc-0x5b75)
    private const string WordTen = "ten";    // @0x10081c20 (toc-0x6a40)

    public static void Run()
    {
        int patience;     // iVar3 — bartender/boarded-crew patience counter
        short count;      // sVar5 — rng rolls / loop counts / fuel qty
        short ammoOutfit; // sVar6 — matched Ammo-type outfit index
        short freeMass;   // sVar7
        byte chk;         // cVar8 — reused yes/no predicate result (decompile shape kept)
        bool patienceArmed;  // bVar9 — a scaled patience value is pending the rng(100) check
        bool exitLoop;       // bVar10
        int idx;          // iVar11
        string msg;       // replaces the auStack_178/auStack_278 C-string staging
        short itemHit = 0;   // local_62[0]

        // NOTE: decompile passes PTR_DAT_10080b84 (the chatter-text RGBColor record POINTER) straight through as
        // the chatter colour arg; that record is now UiColors.ChatterText, so every
        // chatter call below passes UiColors.ChatterText directly rather than re-deriving the pointer.
        patienceArmed = false;
        exitLoop = false;
        int modalUpp = MacToolbox.NewRoutineDescriptor(BoardingFilterProc, 0xfd0, 1);
        MacToolbox.RegisterModalFilter(BoardingFilterProc, FilterAdapter);
        patience = (int)(SeedEvoRng.Run(26));
        patience = patience + 15;
        InitTradeSession.Run();
        MacToolbox.ShowCursor();
        DialogScratch.BoardingDialogRecord = 0;
        DialogScratch.BoardingDialogRecord = MacToolbox.GetNewDialog(0x3f3, 0, -1);   // behind = (WindowPtr)-1 (frontmost)
        if (DialogScratch.BoardingDialogRecord != 0)
        {
            // Fills only the first half of the 12-slot BoardingPicts array (matches the
            // decompile's fixed 0x8 bound, a resource-ID split point, not BoardingPicts.Length).
            for (idx = 0; (short)idx < 8; idx = idx + 1)
            {
                DialogScratch.BoardingPicts[(short)idx] = MacToolbox.GetPicture(idx + 0x1ba4);   // PICTs 0x1ba4..0x1bab
            }
            for (idx = 8; (short)idx < DialogScratch.BoardingPicts.Length; idx = idx + 1)
            {
                DialogScratch.BoardingPicts[(short)idx] = MacToolbox.GetPicture(idx + 0x1ba6);   // PICTs 0x1bae..0x1bb1
            }
            RecenterWindowIntoPlayArea.Run(DialogScratch.BoardingDialogRecord);
            MacToolbox.ShowWindow(DialogScratch.BoardingDialogRecord);
            MacToolbox.SelectWindow(DialogScratch.BoardingDialogRecord);
            MacToolbox.SetPort(DialogScratch.BoardingDialogRecord);
            MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
            while (true)
            {
                MacToolbox.ModalDialog(modalUpp, ref itemHit);
                if (itemHit == 1)
                {
                    patience = -1;
                    patienceArmed = false;
                    Render6ButtonRow.Run(-1);
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[2], 1, 128, 128);
                    exitLoop = true;
                }
                if (patienceArmed)
                {
                    count = (short)(SeedEvoRng.Run(100));
                    if (count <= (short)patience) break;   // caught — punishment path below the loop
                }
                patienceArmed = false;
                if (itemHit == 2)
                {
                    Render6ButtonRow.Run(-1);
                    if (DialogScratch.BoardingSalvageCargoIndex == -1)
                    {
                        SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                    }
                    else
                    {
                        count = (short)(TotalMassWithEscorts.Run());
                        freeMass = (short)(ShipDerivedStats.TotalMassCarried(ShipTable.Player));
                        if ((int)count - (int)freeMass < (int)DialogScratch.BoardingSalvageCargoQty)
                        {
                            count = (short)(TotalMassWithEscorts.Run());
                            DialogScratch.BoardingSalvageCargoQty = (short)(ShipDerivedStats.TotalMassCarried(ShipTable.Player));
                            DialogScratch.BoardingSalvageCargoQty = (short)(count - DialogScratch.BoardingSalvageCargoQty);
                        }
                        if (DialogScratch.BoardingSalvageCargoQty < 1)
                        {
                            SndPlay.Run(CombatSoundCells.UiSoundBankA[2], 1, 128, 128);
                            EnqueueChatterEvent.Run(MsgNoCargoRoom, 240, 0, 12, UiColors.ChatterText, 0, 0);
                            RepaintGameWindow.Run();
                            TwoStepRepaintGameWindow.Run();
                        }
                        else
                        {
                            // Decompile 9872-9882: strcpy/NumToString/strcat staging through two
                            // stack C-string buffers + c2pstr — built as a C# string now. The
                            // commodity name is entry idx of the STR# 0xfa1 heap table (Pascal
                            // strings 0x100 apart behind the ptr cell 0x10080bc4).
                            msg = MsgSalvagedPrefix + DialogScratch.BoardingSalvageCargoQty.ToString() + MsgTonsOf +
                                  ResourceGlobals.NamesStr0fa1[DialogScratch.BoardingSalvageCargoIndex] +
                                  MsgFromThisShip;
                            SndPlay.Run(CombatSoundCells.UiSoundBankA[2], 1, 128, 128);
                            EnqueueChatterEvent.Run(msg, 240, 0, 12, UiColors.ChatterText, 0, 0);
                            RepaintGameWindow.Run();
                            TwoStepRepaintGameWindow.Run();
                            idx = DialogScratch.BoardingSalvageCargoIndex;
                            GameData.Ships[0].CargoHold[idx] = (short)(GameData.Ships[0].CargoHold[idx] + DialogScratch.BoardingSalvageCargoQty);
                            WorldState.HudStatusPanelDirty = 1;
                            DialogScratch.BoardingSalvageCargoQty = 0;
                            DialogScratch.BoardingSalvageCargoIndex = -1;
                            MacToolbox.SetPort(DialogScratch.BoardingDialogRecord);
                            MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(DialogScratch.BoardingDialogRecord));   // win+0x10 portRect
                        }
                        DialogScratch.BoardingSalvageCargoIndex = -1;
                        patience = (int)((double)(short)patience * PatienceScaleTrade);   // decompile 9896-9898 signed i2d
                        // (decompile 9899 `local_58 = (double)(longlong)iVar3;` — dead store, dropped)
                        patienceArmed = true;
                    }
                }
                if (itemHit == 3)
                {
                    Render6ButtonRow.Run(-1);
                    if (DialogScratch.BoardingSalvageCredits < 1)
                    {
                        SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                    }
                    else
                    {
                        SndPlay.Run(CombatSoundCells.UiSoundBankA[2], 1, 128, 128);
                        EnqueueChatterEvent.Run(MsgStoleCredits, 240, 0, 12, UiColors.ChatterText, 0, 0);
                        RepaintGameWindow.Run();
                        TwoStepRepaintGameWindow.Run();
                        GameData.Ships[0].Credits = GameData.Ships[0].Credits + DialogScratch.BoardingSalvageCredits;
                        patience = (int)((double)(short)patience * PatienceScaleSteal);   // decompile 9915-9916 signed i2d
                        // (decompile 9918 dead store dropped)
                        patienceArmed = true;
                        WorldState.HudStatusPanelDirty = 1;
                        TickHudRedrawScheduler.Run();
                        DialogScratch.BoardingSalvageCredits = 0;
                        MacToolbox.SetPort(DialogScratch.BoardingDialogRecord);
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(DialogScratch.BoardingDialogRecord));   // win+0x10 portRect
                    }
                }
                if (itemHit == 4)
                {
                    Render6ButtonRow.Run(-1);
                    RebuildOwnedOutfitsFromMarket.Run();
                    // Scan for the Ammo-type outfit whose ModValue matches the selected commodity type
                    // (was a comma-tuple `.Item2` transcription of the decompile's comma-operator while-header).
                    for (count = 0; ; count = (short)(count + 1))
                    {
                        ammoOutfit = -1;
                        if (OutfitTable.Count - 1 < count) break;
                        if (OutfitTable.Store[count].ModType[0] == OutfitModType.Ammo)
                        {
                            ammoOutfit = count;
                            if (DialogScratch.BoardingSalvageAmmoType == OutfitTable.Store[count].ModValue[0]) break;
                        }
                    }
                    if ((ammoOutfit == -1) || (DialogScratch.BoardingSalvageAmmoType == -1))
                    {
                        SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                    }
                    else
                    {
                        for (count = 0; ; count = (short)(count + 1))
                        {
                            freeMass = (short)(ShipDerivedStats.FreeMassSpace());
                            if (!(OutfitTable.Store[ammoOutfit].Mass <= freeMass &&
                                  count < DialogScratch.BoardingSalvageAmmoQty)) break;
                            idx = DialogScratch.BoardingSalvageAmmoType;
                            GameData.Ships[0].WeaponSlotAmmo[idx] = (short)(GameData.Ships[0].WeaponSlotAmmo[idx] + 1);
                            RebuildOwnedOutfitsFromMarket.Run();
                        }
                        if (count < 1)
                        {
                            SndPlay.Run(CombatSoundCells.UiSoundBankA[2], 1, 128, 128);
                            EnqueueChatterEvent.Run(MsgNoAmmoRoom, 240, 0, 12, UiColors.ChatterText, 0, 0);
                            RepaintGameWindow.Run();
                            TwoStepRepaintGameWindow.Run();
                        }
                        else
                        {
                            // Decompile 9954-10000: the same two-buffer C-string staging, with the
                            // count rendered as a number WORD for 1..10 (NumToString digits above).
                            string countWord;
                            if (count == 1)
                            {
                                countWord = WordOne;
                            }
                            else if (count == 2)
                            {
                                countWord = WordTwo;
                            }
                            else if (count == 3)
                            {
                                countWord = WordThree;
                            }
                            else if (count == 4)
                            {
                                countWord = WordFour;
                            }
                            else if (count == 5)
                            {
                                countWord = WordFive;
                            }
                            else if (count == 6)
                            {
                                countWord = WordSix;
                            }
                            else if (count == 7)
                            {
                                countWord = WordSeven;
                            }
                            else if (count == 8)
                            {
                                countWord = WordEight;
                            }
                            else if (count == 9)
                            {
                                countWord = WordNine;
                            }
                            else if (count == 10)
                            {
                                countWord = WordTen;
                            }
                            else
                            {
                                countWord = count.ToString();   // NumToString + p2cstr
                            }
                            // Outfit name: entry ammoOutfit of STR# 0x138c (singular, <2) or
                            // 0x138d (plural) — Pascal strings 0x100 apart behind the heap ptrs.
                            string outfitName = count < 2
                              ? ResourceGlobals.NamesStr138c[ammoOutfit]
                              : ResourceGlobals.NamesStr138d[ammoOutfit];
                            msg = MsgSalvagedPrefix + countWord + MsgWordSeparator + outfitName + MsgFromThisShip;
                            SndPlay.Run(CombatSoundCells.UiSoundBankA[2], 1, 128, 128);
                            EnqueueChatterEvent.Run(msg, 240, 0, 12, UiColors.ChatterText, 0, 0);
                            RepaintGameWindow.Run();
                            TwoStepRepaintGameWindow.Run();
                        }
                        WorldState.HudWeaponPanelDirty = 1;
                        patience = (int)((double)(short)patience * PatienceScaleTrade);   // decompile 10007-10008 signed i2d
                        // (decompile 10010 dead store dropped)
                        patienceArmed = true;
                        DialogScratch.BoardingSalvageAmmoQty = 0;
                        DialogScratch.BoardingSalvageAmmoType = -1;
                        MacToolbox.SetPort(DialogScratch.BoardingDialogRecord);
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(DialogScratch.BoardingDialogRecord));   // win+0x10 portRect
                    }
                }
                if (itemHit == 6)
                {
                    Render6ButtonRow.Run(-1);
                    if (DialogScratch.BoardingSalvageFuel < 1)
                    {
                        SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                    }
                    else
                    {
                        SndPlay.Run(CombatSoundCells.UiSoundBankA[2], 1, 128, 128);
                        count = (short)(ShipDerivedStats.EffectiveFuelMax(ShipTable.Player));
                        // NOTE: this cast must keep the i2d CONCAT44 magic-pack idiom (here and at the two
                        // sites below) — dropping it leaves plain value - 2^52ish, i.e. hugely negative.
                        idx = (int)((float)count - GameData.Ships[0].Fuel);   // decompile 10026-10027 signed i2d
                        // (decompile 10029 dead store dropped)
                        count = (short)idx;
                        if (DialogScratch.BoardingSalvageFuel < count)
                        {
                            count = DialogScratch.BoardingSalvageFuel;
                        }
                        if (count < 1)
                        {
                            EnqueueChatterEvent.Run(MsgNoFuelRoom, 240, 0, 12, UiColors.ChatterText, 0, 0);
                            RepaintGameWindow.Run();
                            TwoStepRepaintGameWindow.Run();
                        }
                        else
                        {
                            GameData.Ships[0].Fuel = GameData.Ships[0].Fuel + (float)count;   // decompile 10040-10043 signed i2d
                            count = (short)(ShipDerivedStats.EffectiveFuelMax(ShipTable.Player));
                            if (GameData.Ships[0].Fuel < (float)count)
                            {   // decompile 10045-10047 signed i2d
                                EnqueueChatterEvent.Run(MsgFuelDrained, 240, 0, 12, UiColors.ChatterText, 0, 0);
                            }
                            else
                            {
                                EnqueueChatterEvent.Run(MsgTanksFilled, 240, 0, 12, UiColors.ChatterText, 0, 0);
                            }
                            RepaintGameWindow.Run();
                            TwoStepRepaintGameWindow.Run();
                        }
                        WorldState.ShieldEnergyBarDirty = 1;
                        TickHudRedrawScheduler.Run();
                        patience = (int)((double)(short)patience * PatienceScaleFuel);   // decompile 10058-10059 signed i2d
                        // (decompile 10061 dead store dropped)
                        patienceArmed = true;
                        DialogScratch.BoardingSalvageFuel = 0;
                        MacToolbox.SetPort(DialogScratch.BoardingDialogRecord);
                        MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(DialogScratch.BoardingDialogRecord));   // win+0x10 portRect
                    }
                }
                if (itemHit == 7)
                {
                    Render6ButtonRow.Run(-1);
                    {
                        int t = GameData.Ships[0].TargetSlot;
                        if ((GameData.Ships[t].Govt != -1) &&
                           ((GameData.Governments[GameData.Ships[t].Govt].Flags & GovtFlags.StartDisabledOrDerelict) != 0))
                        {
                            DialogScratch.BoardingCaptureChance = -1;
                        }
                        count = (short)(SeedEvoRng.Run(100));
                        if ((DialogScratch.BoardingCaptureChance < count) || (DialogScratch.BoardingCaptureChance < 1))
                        {
                            exitLoop = true;
                            SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                            EnqueueChatterEvent.Run(MsgCaptureFailed, 240, 0, 12, UiColors.ChatterText, 0, 0);
                            RepaintGameWindow.Run();
                            TwoStepRepaintGameWindow.Run();
                            MacToolbox.SetPort(DialogScratch.BoardingDialogRecord);
                        }
                        else
                        {
                            count = (short)(SeedEvoRng.Run(10));
                            if (count == 0)
                            {
                                exitLoop = true;
                                // NOTE: decompile stores int 0xffff8300 (= -32000) into the +0x68 Shield cell; this
                                // port's Shield convention is numeric (assign the float value directly, (int)Shield
                                // reads it back), not a bit-punned Int32BitsToSingle store.
                                GameData.Ships[t].Shield = -32000f;
                                GameData.Ships[t].DeathTimer = DisabledTimerReset;   // 4-byte bit-copy of *(toc-0x69e8) = 0x00000000 = 0.0f
                                EnqueueChatterEvent.Run(MsgSelfDestructSp, 240, 0, 12, UiColors.ChatterText, 0, 0);
                                RepaintGameWindow.Run();
                                TwoStepRepaintGameWindow.Run();
                                MacToolbox.SetPort(DialogScratch.BoardingDialogRecord);
                            }
                            else
                            {
                                SndPlay.Run(CombatSoundCells.UiSoundBankA[2], 1, 128, 128);
                                chk = (byte)(EscortRoomAvailable.Run() ? 1 : 0);
                                if (chk == 0)
                                {
                                    SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);
                                    EnqueueChatterEvent.Run(MsgMaxEscorts, 240, 0, 12, UiColors.ChatterText, 0, 0);
                                    RepaintGameWindow.Run();
                                    TwoStepRepaintGameWindow.Run();
                                    MacToolbox.SetPort(DialogScratch.BoardingDialogRecord);
                                }
                                else
                                {
                                    if (!RunConfirmYesNoDialog.Run())
                                    {
                                        exitLoop = true;
                                        GameData.Ships[t].AiBehaviorType = ShipAiType.Escort;
                                        GameData.Ships[t].OwnerSlot = 0;
                                        GameData.Ships[t].IsCarriedFighter = 0;
                                        GameData.Ships[t].Shield = 0f;
                                        GameData.Ships[t].Govt = -1;
                                        GameData.Ships[t].PersIndex = -1;
                                        GameData.Ships[t].TargetSlot = -1;
                                        GameData.Ships[t].SalvageClaimed = 1;
                                        GameData.Ships[t].TargetSlot = -1; // duplicated write in the original too
                                        int exitHeading = (int)(SeedEvoRng.Run(360));
                                        EvMath.OffsetByHeading((double)EscortExitDistance, exitHeading,
                                                 ref GameData.Ships[t].PosX, ref GameData.Ships[t].PosY);
                                        ShipAi.SetStateHyperWindupAndPropagate(ShipTable.Ships[t]);
                                        EnqueueChatterEvent.Run(MsgEscortAssigned, 240, 0, 12, UiColors.ChatterText, 0, 0);
                                        RepaintGameWindow.Run();
                                        TwoStepRepaintGameWindow.Run();
                                        MacToolbox.SetPort(DialogScratch.BoardingDialogRecord);
                                        WorldState.HudStatusPanelDirty = 1;
                                    }
                                    else
                                    {
                                        exitLoop = true;
                                        // decompile copies the ship-class name (class +0x3e, a Pascal blob) into auStack_178 then hands it
                                        // to the alert; the name is a managed string now — pass it directly.
                                        chk = (byte)(AlertModal_ThreeButton.Run(MsgRenamePrompt,
                                                 GameData.ShipClasses[GameData.Ships[t].ShipClass].Name, 20));
                                        MacToolbox.SetPort(DialogScratch.BoardingDialogRecord);
                                        if (chk == 0)
                                        {
                                            EnqueueChatterEvent.Run(MsgCaptureDeclined, 240, 0, 12, UiColors.ChatterText, 0, 0);
                                            MacToolbox.SetPort(DialogScratch.BoardingDialogRecord);
                                        }
                                        else
                                        {
                                            PilotIdentity.ShipName = PilotIdentity.CapturedNameEntry;
                                            MacToolbox.SetPort(DialogScratch.BoardingDialogRecord);
                                            RunShipCaptureSwap.Run(ShipTable.Ships[t], 0);
                                            MacToolbox.SetPort(DialogScratch.BoardingDialogRecord);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                if (exitLoop) goto LAB_10014a34;
                RedrawPilotInfoPanel.Run();
                TickFlashEffectCountdown.Run();
                MacToolbox.SetPort(DialogScratch.BoardingDialogRecord);
            }
            {
                int t = GameData.Ships[0].TargetSlot;
                count = (short)(ShipDerivedStats.EffectiveArmorMax(ShipTable.Ships[t])); // FLAG: passes NPC ptr to helper
                // NOTE: decompile stores the int -(armorMax+1) into the +0x68 Shield cell; this port's
                // Shield convention is numeric, not a bit-punned Int32BitsToSingle store.
                GameData.Ships[t].Shield = -(count + 1);
                GameData.Ships[t].DeathTimer = DisabledTimerReset;   // 4-byte bit-copy of *(toc-0x69e8) = 0.0f
            }
            SndPlay.Run(CombatSoundCells.UiSoundBankA[2], 1, 128, 128);
            EnqueueChatterEvent.Run(MsgSelfDestruct, 240, 0, 12, UiColors.ChatterText, 0, 0);
            UpdateWindowRegionLayout.Run(false);
            MacToolbox.SetPort(DialogScratch.BoardingDialogRecord);
        LAB_10014a34:
            for (count = 0; count < DialogScratch.BoardingPicts.Length; count = (short)(count + 1))
            {
                if (DialogScratch.BoardingPicts[count] != 0)
                {
                    MacToolbox.HUnlock(DialogScratch.BoardingPicts[count]);
                    MacToolbox.HPurge(DialogScratch.BoardingPicts[count]);
                    MacToolbox.ReleaseResource(DialogScratch.BoardingPicts[count]);
                }
            }
            SetGamePortAndDevice.Run();
            MacToolbox.DisposeRoutineDescriptor(modalUpp);
            MacToolbox.DisposeDialog(DialogScratch.BoardingDialogRecord);
            SetGamePortAndDevice.Run();
            MacToolbox.HideCursor();
            RepaintGameWindow.Run();
        }
        return;
    }
}
