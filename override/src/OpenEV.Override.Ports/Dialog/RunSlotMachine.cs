using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog.Model;
using OpenEV.Override.Ports.Graphics;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;
using OpenEV.Override.Ports.Sound;
using OpenEV.Override.Ports.Sound.Model;

namespace OpenEV.Override.Ports.Dialog;

// FUN_1000af9c (EV Override-11.c lines 5800-6069) — the bar slot machine (DLOG
// 0x3f7, sharing the bribe-dialog scratch cells): loads the four bet-button
// PICTs (0x1bc0..0x1bc3) and the reel click/stop snds (0x1cc/0x1cd), randomizes
// the three reels (CommFaceX = symbol*64 px scroll, CommFaceTimer = 200..399
// ticks), tracks the 1000/5000-credit bet buttons, spins the reels down with an
// 8px/tick → eased deceleration, tallies the stopped symbols (reel scroll / 64,
// clamped 0..5), pays 1.3× the bet for a pair / 3× for a triple, and TETextBoxes
// the win/lose message (STR# 0x1fd8 taunts 1..3) into the widened item-4 rect.
//
// Dialog 4-rules (B9): C# strings end-to-end (the auStack_398/_299/_199 Pascal
// buffers + NumToString/p2cstr/c2pstr/Concat/StrncpyPad/CStringLength collapse
// into one `message` string), managed GetDialogItem outs, GetMouse() packed
// point, plain (double) casts for the int->double bit-packs, and the
// data-seg doubles / C-strings dumped to literals:
//   toc-0x6a60 = 0x10081c00 -> 8.0     (reel deceleration scale)
//   toc-0x6a68 = 0x10081bf8 -> 100.0   (reel timer range)
//   toc-0x6a70 = 0x10081bf0 -> 1.3     (pair payout multiplier)
//   toc-0x6a78 = 0x10081be8 -> 1000.0  (thousands divisor)
//   toc-0x6a80 = 0x10081be0 -> 3.0     (triple payout multiplier)
//   toc-0x6a88 = 0x10081bd8 -> the standard i2d bias (PpcMagic.I2dBias)
//   toc-0x6a90 = 0x10081bd0 -> ","   /  toc-0x6a8e = 0x10081bd2 -> "0"
//   0x1008285a -> "Congratulations - you win "   /   0x10082875 -> " credits!"
public static class RunSlotMachine
{
    public static void Run()
    {
        bool done;
        short betAmount = default; // NOTE: untracked decompile register (unaff_r26); always assigned (1000 or 5000) before use at LAB_1000b208 — default 0 is never reached.

        done = false;
        for (short pictIdx = 0; pictIdx < DialogScratch.BribeBtnPicts.Length; pictIdx = (short)(pictIdx + 1))
        {
            DialogScratch.BribeBtnPicts[pictIdx] = MacToolbox.GetPicture(pictIdx + 0x1bc0);
        }
        DialogScratch.SpaceportCommFacePtrA = LoadSndResource.Run(0x1cc);
        DialogScratch.SpaceportCommFacePtrB = LoadSndResource.Run(0x1cd);
        DialogScratch.BribeDialogPtr = 0;
        DialogScratch.BribeDialogPtr = MacToolbox.GetNewDialog(0x3f7, 0, -1);
        if (DialogScratch.BribeDialogPtr != 0)
        {
            NewDialogHook.Run(DialogScratch.BribeDialogPtr, 0);
            RecenterWindowIntoPlayArea.Run(DialogScratch.BribeDialogPtr);
            MacToolbox.ShowWindow(DialogScratch.BribeDialogPtr);
            MacToolbox.SelectWindow(DialogScratch.BribeDialogPtr);
            MacToolbox.SetPort(DialogScratch.BribeDialogPtr);
            MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
            for (short reel = 0; reel < DialogScratch.CommFaceX.Length; reel = (short)(reel + 1))
            {
                DialogScratch.CommFaceX[reel] = (short)(SeedEvoRng.Run(6) << 6);
                DialogScratch.CommFaceTimer[reel] = (short)(SeedEvoRng.Run(200) + 200);
            }
            // PaintRect/FrameRect(dialog + 0x10) — the dialog window's portRect.
            var dlgPortRect = MacToolbox.GetDialogPortRect(DialogScratch.BribeDialogPtr);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            MacToolbox.PaintRect(dlgPortRect);
            MacToolbox.RGBForeColor((uint)UiColors.DialogFore);
            MacToolbox.FrameRect(dlgPortRect);
            MacToolbox.ForeColor(QuickDrawColor.Black);
            DrawScrollbarPict.Run();
            DrawSlotMachineReels.Run();
            RenderBribeButtons.Run(-1);
            while (true)
            {
                short clicked;
                while (true)
                {
                    // Wait for a press (original quirk: never waits for release,
                    // so a held button re-fires immediately).
                    // DEVIATION (faithful): the ASM busy-waits (bl .Button; beq loc_B15C) with
                    // no Delay; this port yields via Sleep(16) instead — Button() reads the
                    // host-updated input bridge, and a pure spin would starve the host thread
                    // that refreshes it (livelock, same class as the title-click freeze).
                    while (!MacToolbox.Button()) System.Threading.Thread.Sleep(16);
                    clicked = (short)TrackBuyDialog.Run(MacToolbox.GetMouse());
                    if (clicked != 0) break;
                    if (999 < GameData.Player.Credits)
                    {
                        betAmount = 1000;
                        goto LAB_1000b208;
                    }
                    SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);   // "can't afford" buzz
                }
                if (clicked != 1)
                {
                    done = true;
                    goto LAB_1000b208;
                }
                if (4999 < GameData.Player.Credits) break;
                SndPlay.Run(CombatSoundCells.UiSoundBankA[3], 1, 128, 128);   // "can't afford" buzz
            }
            betAmount = 5000;
        LAB_1000b208:
            if (!done)
            {
                GameData.Player.Credits = GameData.Player.Credits - betAmount;
                WorldState.HudStatusPanelDirty = 1;
                TickHudRedrawScheduler.Run();
                MacToolbox.SetPort(DialogScratch.BribeDialogPtr);
                done = false;
                int[] reelPaceTicks = new int[1];   // Delay out-tick, unused (only here for the pace below)
                while (!done)
                {
                    done = true;
                    // DEVIATION (faithful): deliberate host-substrate pacing, accepted; NOT in
                    // the decompile — FUN_1000af9c's spin loop, EV Override-11.c 5888-5936, has NO Delay/
                    // WaitNextEvent/tick-cap of any kind). In the original the reels animated
                    // only because FUN_1000bb44's CopyBits drew straight to the screen
                    // synchronously — one visible frame per pass, paced by the blit's real cost.
                    // This port defers every draw to the host's async queue (drained by a
                    // separate thread), so an unpaced loop enqueues all ~200-460 reel positions
                    // in a few ms and the host only ever presents the last — the reels jumped
                    // straight to the result ("Gamble isn't animating", bug class #27).
                    //   Restoring the animation needs a per-frame yield the source didn't have:
                    // wrap each pass in a draw batch (so all 3 reels present together, not torn)
                    // and Delay(1) after it (≈ 1/60 s, the port's 60 Hz present rate — the same
                    // cadence sibling ZoomInWindowAnimation uses per frame). This adds ONLY the
                    // presentation cadence: the reel maths below (8px/tick, the eased stop, the
                    // 200-399 timers) is byte-for-byte the decompile, untouched. Visible cost:
                    // the spin now runs ~5-8 s (a fast Mac was quicker, but the host can't
                    // present faster than 60 Hz, and this way every reel position is seen).
                    MacToolbox.BeginDrawBatch();
                    for (short reel = 0; reel < DialogScratch.CommFaceX.Length; reel = (short)(reel + 1))
                    {
                        // (x % 64) — the decompile's >>6-plus-negative-odd-carry pairs are
                        // a signed round-toward-zero div/mod by 64 (one symbol = 64px).
                        if (0 < DialogScratch.CommFaceTimer[reel] ||
                            1 < DialogScratch.CommFaceX[reel] % 64)
                        {
                            // Near a symbol boundary: play the reel click (snd 0x1cc)
                            // unless one is already sounding.
                            if (DialogScratch.CommFaceX[reel] % 64 < 2 &&
                                (short)CountMatchingSoundVoices.Run(DialogScratch.SpaceportCommFacePtrA) == 0)
                            {
                                SndPlay.Run(DialogScratch.SpaceportCommFacePtrA, 1, 128, 128);
                            }
                            if (DialogScratch.CommFaceTimer[reel] < 100)
                            {
                                if (DialogScratch.CommFaceTimer[reel] < 2)
                                {
                                    DialogScratch.CommFaceX[reel] -= 1;
                                }
                                else
                                {
                                    // x = -(8.0 * timer/100.0 - x) — ease toward the stop as
                                    // the timer runs down. (The decompile's (double)(longlong)
                                    // round-trip stores local_50..68 were dead decompile artifacts.)
                                    int eased = (int)-(8.0 * ((double)DialogScratch.CommFaceTimer[reel] / 100.0)
                                                       - (double)DialogScratch.CommFaceX[reel]);
                                    DialogScratch.CommFaceX[reel] = (short)eased;
                                }
                            }
                            else
                            {
                                DialogScratch.CommFaceX[reel] -= 8;   // full-speed scroll
                            }
                            if (DialogScratch.CommFaceX[reel] < 0)
                            {
                                DialogScratch.CommFaceX[reel] += 384;   // wrap (6 symbols × 64px)
                            }
                            DialogScratch.CommFaceTimer[reel] -= 1;
                            done = false;
                            // Reel just stopped on a boundary: the stop thunk (snd 0x1cd).
                            if (DialogScratch.CommFaceTimer[reel] < 1 &&
                                DialogScratch.CommFaceX[reel] % 64 < 2)
                            {
                                SndPlay.Run(DialogScratch.SpaceportCommFacePtrB, 9, 128, 128);
                            }
                        }
                    }
                    DrawSlotMachineReels.Run();
                    MacToolbox.EndDrawBatch();                 // the batch + Delay(1) are the added pace — see loop-top note
                    MacToolbox.Delay(1, reelPaceTicks);        // present this frame (host-substrate pace, see loop-top note)
                }
                // Tally the stopped symbols: scroll / 64 (signed toward-zero), clamp 0..5.
                var symbolCounts = new short[10]; // local_7c[10] — only [0..5] are used
                const short SymbolCount = 6;
                for (short s = 0; s < SymbolCount; s = (short)(s + 1))
                {
                    symbolCounts[s] = 0;
                }
                for (short reel = 0; reel < DialogScratch.CommFaceX.Length; reel = (short)(reel + 1))
                {
                    short symbol = (short)(DialogScratch.CommFaceX[reel] / 64);
                    if (symbol < 0) symbol = 0;
                    if (5 < symbol) symbol = 5;
                    symbolCounts[symbol] = (short)(symbolCounts[symbol] + 1);
                }
                MacToolbox.SetPort(DialogScratch.BribeDialogPtr);
                // Default message: one of the three STR# 0x1fd8 lose taunts.
                string message = MacToolbox.GetIndString(0x1fd8, (short)(SeedEvoRng.Run(3) + 1));
                for (short s = 0; s < SymbolCount; s = (short)(s + 1))
                {
                    if (symbolCounts[s] == 2)
                    {
                        message = BuildPayoutMessage(betAmount, 1.3, CombatSoundCells.WeaponHitSnd[0]);   // pair pays 1.3×
                        break;
                    }
                    if (symbolCounts[s] == 3)
                    {
                        message = BuildPayoutMessage(betAmount, 3.0, CombatSoundCells.WeaponHitSnd[3]);   // triple pays 3×
                        break;
                    }
                }
                WorldState.HudStatusPanelDirty = 1;
                TickHudRedrawScheduler.Run();
                MacToolbox.SetPort(DialogScratch.BribeDialogPtr);
                MacToolbox.ForeColor(QuickDrawColor.Black);
                var messageRect = new short[4];   // auStack_84 (+local_7e) — item-4 message rect
                var item5Rect = new short[4];   // auStack_8c (+local_86) — item-5 rect
                MacToolbox.GetDialogItem(DialogScratch.BribeDialogPtr, 4, 0, 0, messageRect);
                MacToolbox.GetDialogItem(DialogScratch.BribeDialogPtr, 5, 0, 0, item5Rect);
                // decompile `local_7e = local_86` — widen the item-4 message rect's RIGHT
                // edge to item 5's right (the decompile split each 8-byte Rect into a
                // 6-byte array + trailing short).
                messageRect[3] = item5Rect[3];
                MacToolbox.PaintRect(messageRect);
                // FUN_10076178 (StrncpyPad) + p2cstr + FUN_1007613c (CStringLength) on the
                // auStack_398 staging copy collapse into the C# message string.
                SetPortAndDevice.Run(RenderGlobals.BackdropGWorld, 0);
                MacToolbox.ForeColor(QuickDrawColor.Black);
                MacToolbox.TextFont(3);
                MacToolbox.TextSize(9);
                MacToolbox.TETextBox(message, messageRect, 0);
                MacToolbox.InvertRect(messageRect);
                SetGamePortAndDevice.Run();
                MacToolbox.SetPort(DialogScratch.BribeDialogPtr);
                MacToolbox.ForeColor(QuickDrawColor.Black);
                // CopyBits(*(toc+0x708c)+2 -> dialog+2): blit the composed message from
                // the backdrop GWorld onto the dialog (numeric `+2` pixmap keys).
                MacToolbox.CopyBits(RenderGlobals.BackdropGWorld + 2, DialogScratch.BribeDialogPtr + 2,
                                    messageRect, messageRect, 0, 0);
                while (!MacToolbox.Button()) System.Threading.Thread.Sleep(16);   // DEVIATION (faithful): dismiss-click wait, same held-button quirk + host-yield pacing as above
            }
            for (short pictIdx = 0; pictIdx < DialogScratch.BribeBtnPicts.Length; pictIdx = (short)(pictIdx + 1))
            {
                if (DialogScratch.BribeBtnPicts[pictIdx] != 0)
                {
                    MacToolbox.HPurge(DialogScratch.BribeBtnPicts[pictIdx]);
                    MacToolbox.ReleaseResource(DialogScratch.BribeBtnPicts[pictIdx]);
                }
            }
            if (DialogScratch.SpaceportCommFacePtrA != 0)
            {
                FlushMixQueueEntries.Run(DialogScratch.SpaceportCommFacePtrA);
                MacToolbox.DisposePtr(DialogScratch.SpaceportCommFacePtrA);
            }
            if (DialogScratch.SpaceportCommFacePtrB != 0)
            {
                FlushMixQueueEntries.Run(DialogScratch.SpaceportCommFacePtrB);
                MacToolbox.DisposePtr(DialogScratch.SpaceportCommFacePtrB);
            }
            MacToolbox.DisposeDialog(DialogScratch.BribeDialogPtr);
            MacToolbox.FlushEvents(EventMask.EveryEvent & ~EventMask.DiskMask, 0);
        }
        return;
    }

    // FUN_1000af9c 5958-6017 — pair/triple win payout: multiply the bet, play the
    // matching hit-snd cue, credit the player, and build the "Congratulations - you
    // win N,NNN credits!" message (thousands via the 1000.0 divide, remainder
    // zero-padded with "0" appends). The two decompile blocks are identical except
    // for the multiplier (1.3/3.0) and the WeaponHitSnd slot (0/3).
    private static string BuildPayoutMessage(short betAmount, double multiplier, int hitSndSlot)
    {
        short payout = (short)(int)((double)betAmount * multiplier);   // short truncation = original
        SndPlay.Run(hitSndSlot, 10, 128, 128);
        GameData.Player.Credits = GameData.Player.Credits + payout;
        string message = "Congratulations - you win ";
        message += (int)((double)payout / 1000.0);           // NumToString(thousands)
        message += ",";
        short remainder = (short)(payout % 1000);
        if (remainder < 100) message += "0";
        if (remainder < 10) message += "0";
        message += remainder;                                // NumToString(remainder)
        message += " credits!";
        return message;
    }
}
