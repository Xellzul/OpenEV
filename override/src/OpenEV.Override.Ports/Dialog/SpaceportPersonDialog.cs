using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Mission;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Pilot.Model;
using OpenEV.Override.Ports.Resource;
using OpenEV.Override.Ports.Ship;
using OpenEV.Override.Ports.Ship.Model;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;
using OpenEV.Override.Ports.Text;
using OpenEV.Platform.EvoData.Resources.Flags;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000f4f8 (EV Override-11.c lines 8051-8550) — the spaceport BAR person
// encounter comm dialog (DLOG 0x3ef): greet a 'përs' ship met in the bar, roll
// a bribe willingness + fine amount, then run the leave/greet/refuel modal loop
// (item 1 = leave, 2 = greet/bribe/hire, 3 = name exchange).
public static class SpaceportPersonDialog
{
    // ── Data-seg tunables (dumped via tools/dump_dataseg.py, big-endian) ──
    private const double BribeBasePerCredit = 0.01;   // *0x10081ce0 (toc-0x6980)
    private const float BribeJitter = 0.5f;   // *0x10081cd8 (toc-0x6988, FLOAT read of the cell)
    private const double FineRollCreditScale = 5e-07;  // *0x10081cd0 (toc-0x6990)
    private const double FineRollCreditScaleWanted = 0.0001; // *0x10081cc8 (toc-0x6998, govt flag 0x8000)
    private const double FineCapCreditFraction = 0.333;  // *0x10081cc0 (toc-0x69a0)
    private const double BribeFairLow = 0.8;    // *0x10081cb8 (toc-0x69a8)
    private const double BribeFairHigh = 1.2;    // *0x10081cb0 (toc-0x69b0)
    private const float FuelNearFullThreshold = 100.0f; // *0x10081ca8 (toc-0x69b8, FLOAT read of the cell)
    private const string ShipNameSuffix = ".";    // C string @0x10081c10 (toc-0x6a50)

    // Bridges the registered filter-proc's raw (int, MacEvent) shape to
    // SpaceportPersonDialogFilter's typed signature.
    private static int FilterAdapter(int dialog, MacEvent evt)
    {
        short itemHit = 0;
        int r = SpaceportPersonDialogFilter.Run(dialog, evt, ref itemHit);
        evt.ItemHit = itemHit;
        return r;
    }

    // Shared by the 4 near-identical bribe-fairness verdict blocks in the modal
    // loop below (decompile 8304-8327, 8350-8373, 8381-8404, 8445-8471): "too low"
    // (0x17) / fair (fairStringIndex) / "too generous" (0x18).
    private static void ShowBribeFairnessResult(int fairStringIndex)
    {
        if (BribeFairLow <= (double)DialogScratch.SpaceportBribeAmount)
        {
            if ((double)DialogScratch.SpaceportBribeAmount <= BribeFairHigh)
            {
                LoadIndexedSpobString.Run(fairStringIndex);
            }
            else
            {
                LoadIndexedSpobString.Run(0x18);
            }
        }
        else
        {
            LoadIndexedSpobString.Run(0x17);
        }
    }

    public static void Run(short personIndex)
    {
        // NOTE the UPP descriptor is built BEFORE the -1 early-out, exactly like
        // the decompile (the UPP leaks on that path — original quirk kept).
        int modalUpp = MacToolbox.NewRoutineDescriptor(DialogScratch.PersonFilterProc, 0xfd0, 1);
        MacToolbox.RegisterModalFilter(DialogScratch.PersonFilterProc, FilterAdapter);
        if (personIndex == -1)
        {
            return;
        }
        if (GameData.Ships[personIndex].IsActive != 0)
        {
            // The decompile reuses one register per role across the rest of this
            // function: sVar8 -> RNG rolls, the refuel/bribe verdict, and the
            // closing purge-loop counter; cVar9 -> yes/no ship-state predicate
            // result. Kept as single locals to match.
            short roll;
            byte chk;
            DialogScratch.DialogShipPtr = ShipTable.Base + personIndex * 0xa82;
            var person = ShipTable.FromPtr(DialogScratch.DialogShipPtr);
            DialogScratch.SpaceportCanBribeFlag = 0;
            DialogScratch.SpaceportFlag = 0;
            DialogScratch.SpaceportHiredFlag = 0;
            if (person.Govt == -1)
            {
                DialogScratch.SpaceportCanBribeFlag = 0x01;
            }
            else
            {
                if ((person.AiBehaviorType < ShipAiType.Warship) &&
                   ((GameData.Governments[person.Govt].Flags & GovtFlags.FreightersTakeBribes) != 0))
                {
                    DialogScratch.SpaceportCanBribeFlag = 0x01;
                }
                if ((ShipAiType.BraveTrader < person.AiBehaviorType) &&
                   ((GameData.Governments[person.Govt].Flags & GovtFlags.WarshipsTakeBribes) != 0))
                {
                    DialogScratch.SpaceportCanBribeFlag = 0x01;
                }
            }
            DialogScratch.SpaceportGreetIndex = (short)(SeedEvoRng.Run(5));
            roll = (short)(SeedEvoRng.Run(41));
            DialogScratch.SpaceportBribeAmount = (float)(BribeBasePerCredit * (double)(roll + 80));
            roll = (short)(SeedEvoRng.Run(5));
            if (roll == 0)
            {
                DialogScratch.SpaceportBribeAmount = DialogScratch.SpaceportBribeAmount - BribeJitter;
            }
            else
            {
                roll = (short)(SeedEvoRng.Run(5));
                if (roll == 0)
                {
                    DialogScratch.SpaceportBribeAmount = DialogScratch.SpaceportBribeAmount + BribeJitter;
                }
            }
            int fineRoll = (int)(FineRollCreditScale * (double)GameData.Player.Credits);
            // ORIGINAL BUG (kept, bug-for-bug parity): 16-bit clamp — only the LOW
            // short of fineRoll is range-tested. Unreachable at this scale (needs
            // Credits > 65.5B, beyond int range); see the HighBribeDemands clamp
            // below for the reachable instance of this same bug.
            if ((short)fineRoll < 1)
            {
                fineRoll = 1;
            }
            roll = (short)(SeedEvoRng.Run((short)fineRoll));
            GameData.BribeFine = (int)(DialogScratch.SpaceportBribeAmount * (float)(roll * 1000 + 3000));
            if ((person.Govt != -1) &&
               ((GameData.Governments[person.Govt].Flags & GovtFlags.HighBribeDemands) != 0))
            {
                fineRoll = (int)(FineRollCreditScaleWanted * (double)GameData.Player.Credits);
                // ORIGINAL BUG (kept, bug-for-bug parity): same 16-bit clamp, reachable
                // here — Credits >= ~327.68M wraps fineRoll's low short negative/zero,
                // defeating the clamp.
                if ((short)fineRoll < 1)
                {
                    fineRoll = 1;
                }
                roll = (short)(SeedEvoRng.Run((short)fineRoll));
                GameData.BribeFine = (int)(DialogScratch.SpaceportBribeAmount * (float)(roll * 1000 + 10000));
            }
            if (FineCapCreditFraction * (double)GameData.Player.Credits <
                (double)GameData.BribeFine)
            {
                fineRoll = (int)(FineCapCreditFraction * (double)GameData.Player.Credits);
                GameData.BribeFine = fineRoll;
            }
            GameData.BribeFine = GameData.BribeFine / 1000;
            GameData.BribeFine = GameData.BribeFine * 1000;
            if (20000 < GameData.BribeFine)
            {
                GameData.BribeFine = 20000;
            }
            if (GameData.BribeFine < 1000)
            {
                GameData.BribeFine = 1000;
            }
            DialogScratch.SpaceportNoTradeFlag = 0;
            if ((person.Govt != -1) &&
               ((GameData.Governments[person.Govt].Flags & GovtFlags.Xenophobic) != 0))
            {
                DialogScratch.SpaceportNoTradeFlag = 0x01;
            }
            DialogScratch.SpaceportPersonPict = MacToolbox.GetPicture((short)(person.ShipClass + 0x14b4));
            if (person.Govt == -1)
            {
                DialogScratch.SpaceportNameText =
                    TryLoadStr.RunString((short)(person.ShipClass + 0xf3c))
                    ?? MacToolbox.GetIndString(0x138e, (short)(person.ShipClass + 1));
            }
            else
            {
                DialogScratch.SpaceportNameText =
                    TryLoadStr.RunString((short)(person.ShipClass + 0xed8))
                    ?? MacToolbox.GetIndString(0x1772, (short)(person.ShipClass + 1));
                DialogScratch.SpaceportGovtText =
                    TryLoadStr.RunString((short)(person.Govt + 0x1068))
                    ?? MacToolbox.GetIndString(0x1771, (short)(person.Govt + 1));
            }
            SndPlay.Run(CombatSoundCells.UiSoundBankA[4], 1, 0x80, 0x80);
            MacToolbox.ShowCursor();
            DialogScratch.CommButtonPicts[0] = MacToolbox.GetPicture(0x1b7c);
            DialogScratch.CommBtnPictB2Sel = MacToolbox.GetPicture(0x1b7d);
            DialogScratch.CommBtnPictB1Act = MacToolbox.GetPicture(0x1b88);
            DialogScratch.CommBtnPictB1ActSel = MacToolbox.GetPicture(0x1b89);
            DialogScratch.CommBtnPictHail0 = MacToolbox.GetPicture(0x1b7a);
            DialogScratch.CommBtnPictHail1 = MacToolbox.GetPicture(0x1b7b);
            bool hasFlow = false;
            if ((person.GrudgeMissionIndex != -1) &&
               (GameData.MissionStates[person.GrudgeMissionIndex].IsActive != 0))
            {
                short grudgeBehavior;
                for (grudgeBehavior = GameData.Missions[person.GrudgeMissionIndex].ShipBehavior;
                    8 < grudgeBehavior; grudgeBehavior = (short)(grudgeBehavior + -10))
                {
                }
                if (grudgeBehavior == 1)
                {
                    hasFlow = true;
                }
            }
            if ((person.OwnerSlot != 0) || (person.AiBehaviorType != ShipAiType.Escort) || hasFlow)
            {
                DialogScratch.CommBtnPictB1 = MacToolbox.GetPicture(0x1b80);
                DialogScratch.CommBtnPictB1Sel = MacToolbox.GetPicture(0x1b81);
            }
            else
            {
                DialogScratch.CommBtnPictB1 = MacToolbox.GetPicture(0x1b9c);
                DialogScratch.CommBtnPictB1Sel = MacToolbox.GetPicture(0x1b9d);
            }
            BuildBarDescription.Run();
            LoadIndexedSpobString.Run(0);
            if ((person.GrudgeMissionIndex == -1) ||
               ((chk = (byte)(ShipDerivedStats.IsDisabled(person) ? 1 : 0)) != 0))
            {
                chk = (byte)(ShipDerivedStats.IsDisabled(person) ? 1 : 0);
                if (chk == 0)
                {
                    chk = (byte)(ShipAi.HasEngagedAllyOrCarrier(person) ? 1 : 0);
                    if (chk == 0)
                    {
                        if (person.OwnerSlot == 0)
                        {
                            if (person.AiBehaviorType == ShipAiType.NavalFighter)
                            {
                                LoadIndexedSpobString.Run(5);
                            }
                            if (person.AiBehaviorType == ShipAiType.Escort)
                            {
                                LoadIndexedSpobString.Run(4);
                            }
                        }
                        else
                        {
                            chk = (byte)(IsPlayerEngagementTarget.Run(person) ? 1 : 0);
                            if (chk == 0)
                            {
                                LoadIndexedSpobString.Run(2);
                            }
                            else
                            {
                                LoadIndexedSpobString.Run(0);
                            }
                        }
                    }
                    else
                    {
                        LoadIndexedSpobString.Run(2);
                    }
                }
            }
            else if (GameData.MissionStates[person.GrudgeMissionIndex].IsActive != 0)
            {
                if (GameData.Missions[person.GrudgeMissionIndex].MissionGoalType == MissionGoalKind.DestroyAll)
                {
                    LoadIndexedSpobString.Run(2);
                }
                if (GameData.Missions[person.GrudgeMissionIndex].MissionGoalType == MissionGoalKind.Escort)
                {
                    LoadIndexedSpobString.Run(8);
                    DialogScratch.SpaceportHailText += PilotIdentity.ShipName + ShipNameSuffix;
                }
                if (GameData.Missions[person.GrudgeMissionIndex].ShipBehavior == 0)
                {
                    LoadIndexedSpobString.Run(2);
                }
                if (GameData.Missions[person.GrudgeMissionIndex].ShipBehavior == 1)
                {
                    LoadIndexedSpobString.Run(4);
                }
            }
            DialogScratch.SpaceportCommDialogRecord = 0;
            DialogScratch.SpaceportCommDialogRecord = MacToolbox.GetNewDialog(0x3ef, 0, -1);
            if (DialogScratch.SpaceportCommDialogRecord != 0)
            {
                var itemType = new short[1];
                var itemHandle = new int[1];
                var itemRect = new short[4];
                short itemHit = default;
                bool done = false;
                NewDialogHook.Run(DialogScratch.SpaceportCommDialogRecord, 0);
                RecenterWindowIntoPlayArea.Run(DialogScratch.SpaceportCommDialogRecord);
                MacToolbox.ShowWindow(DialogScratch.SpaceportCommDialogRecord);
                MacToolbox.SelectWindow(DialogScratch.SpaceportCommDialogRecord);
                MacToolbox.SetPort(DialogScratch.SpaceportCommDialogRecord);
                MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
                MacToolbox.InvalRect(MacToolbox.GetDialogPortRect(DialogScratch.SpaceportCommDialogRecord));
                do
                {
                    MacToolbox.ModalDialog(modalUpp, ref itemHit);
                    if (itemHit == 1)
                    {
                        done = true;
                    }
                    if (itemHit == 2)
                    {
                        SndPlay.Run(CombatSoundCells.UiSoundBankA[1], 1, 0x80, 0x80);
                        chk = (byte)(ShipDerivedStats.IsDisabled(person) ? 1 : 0);
                        if (chk == 0)
                        {
                            if ((person.OwnerSlot != 0) || (person.AiBehaviorType != ShipAiType.Escort) || hasFlow)
                            {
                                chk = (byte)(ShipAi.HasEngagedAllyOrCarrier(person) ? 1 : 0);
                                if (chk == 0)
                                {
                                    chk = (byte)(IsPlayerEngagementTarget.Run(person) ? 1 : 0);
                                    if ((chk == 0) || (DialogScratch.SpaceportNoTradeFlag != 0))
                                    {
                                        LoadIndexedSpobString.Run(0x13);
                                    }
                                    else
                                    {
                                        chk = (byte)(ShipAi.HasGovtAlliesAlive(person) ? 1 : 0);
                                        if ((chk == 0) && ((chk = (byte)(ShipAi.IsStateActiveCombatPhase(person) ? 1 : 0)) == 0))
                                        {
                                            chk = (byte)(AnyShipEngaged.Run() ? 1 : 0);
                                            if (chk == 0)
                                            {
                                                if (FuelNearFullThreshold <= GameData.Player.Fuel)
                                                {
                                                    LoadIndexedSpobString.Run(0xe);
                                                }
                                                else if (DialogScratch.SpaceportNoTradeFlag == 0)
                                                {
                                                    if (person.AiBehaviorType < ShipAiType.NavalFighter)
                                                    {
                                                        ShowBribeFairnessResult(0x1c);
                                                        DrawOutfitterItemPanel.Run();
                                                        roll = (short)ConfirmBribeFinePayment.Run();
                                                        MacToolbox.SetPort(DialogScratch.SpaceportCommDialogRecord);
                                                        if (roll == 1)
                                                        {
                                                            LoadIndexedSpobString.Run(0x1d);
                                                            ShipAi.SetStateJumpingOut(person);
                                                        }
                                                        else if (roll == 0)
                                                        {
                                                            LoadIndexedSpobString.Run(0x1f);
                                                        }
                                                        else if (roll == -1)
                                                        {
                                                            LoadIndexedSpobString.Run(0xc);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        LoadIndexedSpobString.Run(0x19);
                                                    }
                                                }
                                                else
                                                {
                                                    LoadIndexedSpobString.Run(0x13);
                                                }
                                            }
                                            else
                                            {
                                                chk = (byte)(HasEngagedEnemyInWindow.Run(person) ? 1 : 0);
                                                if (chk == 0)
                                                {
                                                    LoadIndexedSpobString.Run(0x13);
                                                }
                                                else if (DialogScratch.SpaceportNoTradeFlag == 0)
                                                {
                                                    if (person.AiBehaviorType == ShipAiType.WimpyTrader)
                                                    {
                                                        LoadIndexedSpobString.Run(0x11);
                                                    }
                                                    else if (person.AiBehaviorType == ShipAiType.BraveTrader)
                                                    {
                                                        if ((person.SlotIndex == (person.SlotIndex / 3) * 3) &&
                                                           (person.Govt == -1))
                                                        {
                                                            ShowBribeFairnessResult(0x12);
                                                            DrawOutfitterItemPanel.Run();
                                                            roll = (short)ConfirmBribeFinePayment.Run();
                                                            MacToolbox.SetPort(DialogScratch.SpaceportCommDialogRecord);
                                                            if (roll == 1)
                                                            {
                                                                LoadIndexedSpobString.Run(0x1d);
                                                                PickRetreatTarget.Run(person);
                                                            }
                                                            else if (roll == 0)
                                                            {
                                                                LoadIndexedSpobString.Run(0x1f);
                                                            }
                                                            else if (roll == -1)
                                                            {
                                                                LoadIndexedSpobString.Run(0xc);
                                                            }
                                                        }
                                                        else
                                                        {
                                                            LoadIndexedSpobString.Run(0x11);
                                                        }
                                                    }
                                                    else if ((person.AiBehaviorType == ShipAiType.Warship) ||
                                                            (person.AiBehaviorType == ShipAiType.Interceptor))
                                                    {
                                                        ShowBribeFairnessResult(0x12);
                                                        DrawOutfitterItemPanel.Run();
                                                        roll = (short)ConfirmBribeFinePayment.Run();
                                                        MacToolbox.SetPort(DialogScratch.SpaceportCommDialogRecord);
                                                        if (roll == 1)
                                                        {
                                                            LoadIndexedSpobString.Run(0x1d);
                                                            PickRetreatTarget.Run(person);
                                                        }
                                                        else if (roll == 0)
                                                        {
                                                            LoadIndexedSpobString.Run(0x1f);
                                                        }
                                                        else if (roll == -1)
                                                        {
                                                            LoadIndexedSpobString.Run(0xc);
                                                        }
                                                    }
                                                    else
                                                    {
                                                        LoadIndexedSpobString.Run(0x1d);
                                                        ShipAi.PickRandomCombatTarget(person);
                                                    }
                                                }
                                                else
                                                {
                                                    LoadIndexedSpobString.Run(0x13);
                                                }
                                            }
                                        }
                                        else if (person.AiBehaviorType < ShipAiType.NavalFighter)
                                        {
                                            LoadIndexedSpobString.Run(0x10);
                                        }
                                        else
                                        {
                                            LoadIndexedSpobString.Run(0x16);
                                        }
                                    }
                                }
                                else
                                {
                                    bool isCommodity = false;
                                    // decompile iVar10 (the fine-roll scratch above) is reused here
                                    // for an unrelated ShipBehavior modulo check; given its own local
                                    // instead of reusing fineRoll since the two roles are otherwise
                                    // unrelated (a readability choice, not a scoping necessity —
                                    // fineRoll is still in scope here).
                                    short shipBehaviorMod;
                                    if ((person.GrudgeMissionIndex != -1) &&
                                        (GameData.MissionStates[person.GrudgeMissionIndex].IsActive != 0) &&
                                       ((shipBehaviorMod = GameData.Missions[person.GrudgeMissionIndex].ShipBehavior) ==
                                        (shipBehaviorMod / 10) * 10))
                                    {
                                        isCommodity = true;
                                    }
                                    if (isCommodity)
                                    {
                                        LoadIndexedSpobString.Run(0x13);
                                    }
                                    else if (person.DefendedSpobIndex == -1)
                                    {
                                        if (person.PersIndex == ShipRecord.EngagePlayerPersIndex)
                                        {
                                            LoadIndexedSpobString.Run(0x13);
                                        }
                                        else if (DialogScratch.SpaceportCanBribeFlag == 0)
                                        {
                                            LoadIndexedSpobString.Run(0x13);
                                        }
                                        else
                                        {
                                            ShowBribeFairnessResult(0x12);
                                            DrawOutfitterItemPanel.Run();
                                            roll = (short)ConfirmBribeFinePayment.Run();
                                            MacToolbox.SetPort(DialogScratch.SpaceportCommDialogRecord);
                                            if (roll == 1)
                                            {
                                                LoadIndexedSpobString.Run(0x14);
                                                ShipAi.SetStateInert(person);
                                                person.AiBehaviorType = ShipAiType.WimpyTrader;
                                            }
                                            else if (roll == 0)
                                            {
                                                LoadIndexedSpobString.Run(0x1e);
                                                ShipAi.CallForDefendersAndEngagePlayer(person);
                                            }
                                            else if (roll == -1)
                                            {
                                                LoadIndexedSpobString.Run(0xc);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        LoadIndexedSpobString.Run(0x13);
                                    }
                                }
                            }
                            else
                            {
                                DialogScratch.SpaceportHiredFlag = 1;
                                LoadIndexedSpobString.Run(0x26);
                                MacToolbox.GetDialogItem(DialogScratch.SpaceportCommDialogRecord, 0xc, itemType, itemHandle, itemRect);
                                MacToolbox.InvalRect(itemRect);
                            }
                        }
                        else
                        {
                            LoadIndexedSpobString.Run(1);
                        }
                    }
                    if (itemHit == 3)
                    {
                        SndPlay.Run(CombatSoundCells.UiSoundBankA[1], 1, 0x80, 0x80);
                        chk = (byte)(ShipDerivedStats.IsDisabled(person) ? 1 : 0);
                        if (chk == 0)
                        {
                            chk = (byte)(ShipAi.HasEngagedAllyOrCarrier(person) ? 1 : 0);
                            if (chk == 0)
                            {
                                chk = (byte)(IsPlayerEngagementTarget.Run(person) ? 1 : 0);
                                if (chk == 0)
                                {
                                    LoadIndexedSpobString.Run(0xd);
                                }
                                else
                                {
                                    DialogScratch.SpaceportHailText = TextScratch.Trunc(DialogScratch.SpaceportGreetText, 254);
                                }
                            }
                            else
                            {
                                LoadIndexedSpobString.Run(0xd);
                            }
                        }
                        else
                        {
                            LoadIndexedSpobString.Run(1);
                        }
                    }
                    MacToolbox.SetPort(DialogScratch.SpaceportCommDialogRecord);
                    MacToolbox.GetDialogItem(DialogScratch.SpaceportCommDialogRecord, 10, itemType, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    MacToolbox.GetDialogItem(DialogScratch.SpaceportCommDialogRecord, 0xc, itemType, itemHandle, itemRect);
                    MacToolbox.InvalRect(itemRect);
                    RenderCommButtonRow.Run(-1);
                } while (!done);
                MacToolbox.HideCursor();
                for (roll = 0; roll < DialogScratch.CommButtonPicts.Length; roll = (short)(roll + 1))
                {
                    if (DialogScratch.CommButtonPicts[roll] != 0)
                    {
                        MacToolbox.HPurge(DialogScratch.CommButtonPicts[roll]);
                        MacToolbox.ReleaseResource(DialogScratch.CommButtonPicts[roll]);
                    }
                }
                if (DialogScratch.SpaceportPersonPict != 0)
                {
                    MacToolbox.HPurge(DialogScratch.SpaceportPersonPict);
                    MacToolbox.ReleaseResource(DialogScratch.SpaceportPersonPict);
                }
                SndPlay.Run(CombatSoundCells.UiSoundBankA[2], 1, 0x80, 0x80);
                if (DialogScratch.SpaceportHiredFlag != 0)
                {
                    RedistributeCargoAmongShips.Run(person.SlotIndex);
                    person.OwnerSlot = -1;
                    person.AiBehaviorType = GameData.ShipClasses[person.ShipClass].InherentAI;
                    ShipAi.SetStateInert(person);
                    WorldState.HudStatusPanelDirty = 1;
                }
                // ORIGINAL dead store (kept, Rule 11): decompile 8539-8540 computes the
                // dialog's window half-height into a local that's never read afterward.
                // ASM srawi+addze = signed truncating division, so /2 here, not >>1.
                short[] finalPortRect = MacToolbox.GetDialogPortRect(DialogScratch.SpaceportCommDialogRecord);
                short windowHalfHeight = (short)((finalPortRect[2] - finalPortRect[0]) / 2);
                MacToolbox.DisposeRoutineDescriptor(modalUpp);
                MacToolbox.DisposeDialog(DialogScratch.SpaceportCommDialogRecord);
                DialogScratch.DialogShipPtr = 0;
                SetGamePortAndDevice.Run();
                RepaintGameWindow.Run();
            }
            return;
        }
        return;
    }
}
