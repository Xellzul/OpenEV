using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Combat.Model;

namespace OpenEV.Override.Ports.Graphics;

// Port of FUN_1001d634 (EV Override-11.c lines 13206-13525): the in-game sprite-sheet
// loader (GameBootSequence). For every 'spïn' band it claims a colour+mask GWorld slot
// pair (LoadIconPairForSlot → the managed SlotGWorlds records), then carves the sheet
// into per-cell bitmap headers (AllocateSlotBitmapHeader → managed SpriteFrames handles)
// which land in the managed frame tables (CombatGraphicsTables). Bands: 300.. planets,
// 128.. ship classes (with sheet-id dedup), 200.. weapons, 400..402 explosions, 500
// carried ships, 700 debris pair, 800/801 misc, 900 hover orb; then PICT 3000.. comm
// faces, PixPats, and the cicn sprite groups.
public static class LoadSpriteSheetsAndGWorlds
{
    private static void ReadSheetHeader(int resHandle, out short f0, out short f1, out short f2,
                                        out short f3, out short f4, out short f5)
    {
        f0 = MacToolbox.ReadResourceShort(resHandle, 0);
        f1 = MacToolbox.ReadResourceShort(resHandle, 2);
        f2 = MacToolbox.ReadResourceShort(resHandle, 4);
        f3 = MacToolbox.ReadResourceShort(resHandle, 6);
        f4 = MacToolbox.ReadResourceShort(resHandle, 8);
        f5 = MacToolbox.ReadResourceShort(resHandle, 10);
    }

    public static void Run()
    {
        SpriteFrameTables.CTable1BitHandle = MacToolbox.GetCTable(1);
        SpriteFrameTables.CTable8BitHandle = MacToolbox.GetCTable(8);
        MacToolbox.HNoPurge(SpriteFrameTables.CTable1BitHandle);
        MacToolbox.HNoPurge(SpriteFrameTables.CTable8BitHandle);
        if (Core.Model.BugBits.IsSet(Core.Model.BugBit.SkipSpriteSheetLoad))
            return;
        SpriteFrameTables.HiResFlag = (byte)(Core.Model.BugBits.IsSet(Core.Model.BugBit.HiRes) ? 1 : 0);

        short[] spriteIdDedupTable = new short[WeaponGraphicsTable.RecCount];
        for (int i = 0; i < WeaponGraphicsTable.RecCount; i++)
            spriteIdDedupTable[i] = -1;
        RenderGlobals.SpriteLoadSlotIndex = 0;

        // ── 'spïn' 300..: planet sprites — one header per graphic. ──
        for (int idx = 0; idx < PlanetSpriteRecordTable.Count; idx++)
        {
            RenderGlobals.SpriteSheetResHandle = 0;   // faithful dead store (decompile clears then reloads the scratch cell)
            RenderGlobals.SpriteSheetResHandle = MacToolbox.GetResource(MacResType.Sprite, idx + 300);
            if (RenderGlobals.SpriteSheetResHandle == 0)
                continue;
            MacToolbox.HLock(RenderGlobals.SpriteSheetResHandle);
            short cellW = MacToolbox.ReadResourceShort(RenderGlobals.SpriteSheetResHandle, 0);
            short cellH = MacToolbox.ReadResourceShort(RenderGlobals.SpriteSheetResHandle, 2);
            short gridW = MacToolbox.ReadResourceShort(RenderGlobals.SpriteSheetResHandle, 4);
            short gridH = MacToolbox.ReadResourceShort(RenderGlobals.SpriteSheetResHandle, 6);
            MacToolbox.HUnlock(RenderGlobals.SpriteSheetResHandle);
            MacToolbox.ReleaseResource(RenderGlobals.SpriteSheetResHandle);
            LoadIconPairForSlot.Run(cellW, cellH, gridW, gridH, 1, 1);
            PlanetSpriteRecordTable.Store[idx] = Misc.AllocateSlotBitmapHeader.Run(gridW, gridH, 0, 0);
            if (PlanetSpriteRecordTable.Store[idx] == 0)
                Misc.FatalGraphicsResourceExit.Run();
            MacToolbox.WireSpriteBand?.Invoke(idx + 300, PlanetSpriteRecordTable.Store, idx, 1);
            AdvanceCreditsScrollProgress.Run(1.0);   // *(double*)(toc-0x6968)
        }

        // ── 'spïn' 128..: ship-class heading sheets (dedup by sheet id). ──
        for (int cls = 0; cls < WeaponGraphicsTable.RecCount; cls++)
        {
            RenderGlobals.SpriteSheetResHandle = MacToolbox.GetResource(MacResType.Sprite, cls + 128);
            if (RenderGlobals.SpriteSheetResHandle == 0)
                continue;
            short frameCount = 0;
            ReadSheetHeader(RenderGlobals.SpriteSheetResHandle,
                            out short sheetId, out short hdr1, out short cellW, out short cellH,
                            out short cols, out short rows);
            if ((sheetId != -1) && (hdr1 != -1))
            {
                bool dedupFound = false;
                for (short scan = 0; scan < cls; scan++)
                {
                    if (MacToolbox.ReadResourceShort(RenderGlobals.SpriteSheetResHandle, 0) != spriteIdDedupTable[scan])
                        continue;
                    for (short f = 0; f < WeaponGraphicsTable.FrameCount; f++)
                        WeaponGraphicsTable.Store[cls * WeaponGraphicsTable.FrameCount + f] = WeaponGraphicsTable.Store[scan * WeaponGraphicsTable.FrameCount + f];
                    dedupFound = true;
                    break;
                }
                if (!dedupFound)
                {
                    LoadIconPairForSlot.Run(sheetId, hdr1, cellW, cellH, cols, rows);
                    short frame = 0;
                    for (int row = 0; row < rows; row++)
                        for (int col = 0; col < cols; col++)
                        {
                            WeaponGraphicsTable.Store[cls * WeaponGraphicsTable.FrameCount + frame] =
                                Misc.AllocateSlotBitmapHeader.Run(cellW, cellH, (short)(col * cellW), (short)(row * cellH));
                            frame++;
                        }
                    // Only wire textures for a class that just built its OWN records — a deduped
                    // class (above) shares the earlier class's handles and is already wired.
                    MacToolbox.WireSpriteBand?.Invoke(cls + 128, WeaponGraphicsTable.Store,
                        cls * WeaponGraphicsTable.FrameCount, WeaponGraphicsTable.FrameCount);
                    spriteIdDedupTable[cls] = sheetId;
                    frameCount = (short)(frameCount + cols * rows);
                }
            }
            MacToolbox.ReleaseResource(RenderGlobals.SpriteSheetResHandle);
            AdvanceCreditsScrollProgress.Run(frameCount);
        }

        // ── 'spïn' 200..: per-weapon-graphic sheets. ──
        for (int graphic = 0; graphic < WeaponDefTable.RecCount; graphic++)
        {
            RenderGlobals.SpriteSheetResHandle = MacToolbox.GetResource(MacResType.Sprite, graphic + 200);
            if (RenderGlobals.SpriteSheetResHandle == 0)
                continue;
            ReadSheetHeader(RenderGlobals.SpriteSheetResHandle,
                            out short h0, out short h1, out short cellW, out short cellH,
                            out short cols, out short rows);
            MacToolbox.ReleaseResource(RenderGlobals.SpriteSheetResHandle);
            LoadIconPairForSlot.Run(h0, h1, cellW, cellH, cols, rows);
            short frame = 0;
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                {
                    WeaponDefTable.Store[graphic * WeaponDefTable.FrameCount + frame] =
                        Misc.AllocateSlotBitmapHeader.Run(cellW, cellH, (short)(col * cellW), (short)(row * cellH));
                    frame++;
                }
            MacToolbox.WireSpriteBand?.Invoke(graphic + 200, WeaponDefTable.Store,
                graphic * WeaponDefTable.FrameCount, WeaponDefTable.FrameCount);
            AdvanceCreditsScrollProgress.Run(frame);
        }

        // ── 'spïn' 400..402: explosion types. ──
        for (int type = 0; type < ExplosionGraphicsTable.TypeCount; type++)
        {
            RenderGlobals.SpriteSheetResHandle = MacToolbox.GetResource(MacResType.Sprite, type + 400);
            if (RenderGlobals.SpriteSheetResHandle == 0)
                continue;
            ReadSheetHeader(RenderGlobals.SpriteSheetResHandle,
                            out short h0, out short h1, out short cellW, out short cellH,
                            out short cols, out short rows);
            MacToolbox.ReleaseResource(RenderGlobals.SpriteSheetResHandle);
            LoadIconPairForSlot.Run(h0, h1, cellW, cellH, cols, rows);
            short frame = 0;
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < cols; col++)
                {
                    ExplosionGraphicsTable.Store[type * ExplosionGraphicsTable.FrameCount + frame] =
                        Misc.AllocateSlotBitmapHeader.Run(cellW, cellH, (short)(col * cellW), (short)(row * cellH));
                    frame++;
                }
            MacToolbox.WireSpriteBand?.Invoke(type + 400, ExplosionGraphicsTable.Store,
                type * ExplosionGraphicsTable.FrameCount, ExplosionGraphicsTable.FrameCount);
            AdvanceCreditsScrollProgress.Run(frame);
        }

        // ── 'spïn' 500: carried-ship frames. ──
        LoadSheetBand(500, SpriteFrameDimTable.Store);

        // ── 'spïn' 700: the two debris frames. ──
        RenderGlobals.SpriteSheetResHandle = MacToolbox.GetResource(MacResType.Sprite, 700);
        if (RenderGlobals.SpriteSheetResHandle != 0)
        {
            ReadSheetHeader(RenderGlobals.SpriteSheetResHandle,
                            out short h0, out short h1, out short cellW, out short cellH,
                            out short cols, out short rows);
            MacToolbox.ReleaseResource(RenderGlobals.SpriteSheetResHandle);
            LoadIconPairForSlot.Run(h0, h1, cellW, cellH, cols, rows);
            DockingDebrisFrameTables.DebrisPair[0] = Misc.AllocateSlotBitmapHeader.Run(cellW, cellH, 0, 0);
            DockingDebrisFrameTables.DebrisPair[1] = Misc.AllocateSlotBitmapHeader.Run(cellW, cellH, cellW, 0);
            MacToolbox.WireSpriteBand?.Invoke(700, DockingDebrisFrameTables.DebrisPair, 0, DockingDebrisFrameTables.DebrisPair.Length);
            AdvanceCreditsScrollProgress.Run(2.0);   // *(double*)(toc-0x6970)
        }

        // ── 'spïn' 800 / 801 / 900 bands. ──
        LoadSheetBand(800, SpriteFrameTables.Spin800Frames);
        LoadSheetBand(801, SpriteFrameTables.Spin801Frames);
        LoadSheetBand(900, SpriteFrameTables.HoverOrbFrames);

        // ── PICT 3000..: comm faces. ──
        for (int i = 0; i < SpriteFrameTables.CommFacePicts.Length; i++)
            SpriteFrameTables.CommFacePicts[i] = MacToolbox.GetPicture(i + 3000);

        // ── PixPats (ptr-slot tables — pixpat slice not yet divorced). ──
        for (int i = 0; i < RenderGlobals.RadarJamColorTable.Length; i++)
        {
            int pat = MacToolbox.GetPixPat(i + 128);
            // toc-0x78d0 = the radar-jam static-colour table (DrawRadarHud reads the same slots).
            RenderGlobals.RadarJamColorTable[i] = pat;
        }
        // toc-0x78d4 = the armor-bar PixPat ptr cell (DrawPlayerShieldBar reads it).
        RenderGlobals.ArmorBarPixPat = MacToolbox.GetPixPat(200);

        // ── cicn sprite groups. ──
        for (int i = 0; i < DockingDebrisFrameTables.DockingRingDim.Length; i++)
        {
            DockingDebrisFrameTables.DockingRingDim[i] = LoadCIconToSprite.Run((short)(i + 10000));
            DockingDebrisFrameTables.DockingRingLit[i] = LoadCIconToSprite.Run((short)(i + 10004));
        }
        for (int row = 0; row < 8; row++)
            for (int col = 0; col < 8; col++)
                SpriteFrameTables.StreakFrames[row * 8 + col] = LoadCIconToSprite.Run((short)(row * 8 + col + 1000));
        for (int i = 0; i < SpriteFrameTables.HudOrbFrames.Length; i++)
            SpriteFrameTables.HudOrbFrames[i] = LoadCIconToSprite.Run((short)(i + 18000));
        for (int row = 0; row < 4; row++)
            for (int col = 0; col < 4; col++)
                SpriteFrameTables.TargetBrackets[row * 4 + col] = LoadCIconToSprite.Run((short)(row * 4 + col + 10008));
        DockingDebrisFrameTables.Cicn20000Cell[0] = LoadCIconToSprite.Run(20000);

        if (Core.Model.BugBits.IsSet(Core.Model.BugBit.DebugStackSpaceDump))
        {
            MacToolbox.DebugStr($"After loading graphics, stackspace is {MacToolbox.StackSpace()} bytes");
        }
        SetGamePortAndDevice.Run();
        MacToolbox.ForeColor(QuickDrawColor.Black);
        MacToolbox.MaxMem(0);
    }

    // Shared shape of the 800/801/900 bands: one sheet, frames walked col-major into `table`.
    private static void LoadSheetBand(int spinId, int[] table)
    {
        RenderGlobals.SpriteSheetResHandle = MacToolbox.GetResource(MacResType.Sprite, spinId);
        if (RenderGlobals.SpriteSheetResHandle == 0)
            return;
        ReadSheetHeader(RenderGlobals.SpriteSheetResHandle,
                        out short h0, out short h1, out short cellW, out short cellH,
                        out short cols, out short rows);
        MacToolbox.ReleaseResource(RenderGlobals.SpriteSheetResHandle);
        LoadIconPairForSlot.Run(h0, h1, cellW, cellH, cols, rows);
        short frame = 0;
        for (int col = 0; col < rows; col++)
            for (int row = 0; row < cols; row++)
            {
                table[frame] = Misc.AllocateSlotBitmapHeader.Run(cellW, cellH, (short)(row * cellW), (short)(col * cellH));
                frame++;
            }
        MacToolbox.WireSpriteBand?.Invoke(spinId, table, 0, table.Length);
        AdvanceCreditsScrollProgress.Run(frame);
    }
}
