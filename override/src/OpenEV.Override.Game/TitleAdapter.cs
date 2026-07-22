using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using OpenEV.Platform.ResourceFork;
using OpenEV.Platform.Toolbox;
using OpenEV.Override.Ports.Title;
using OpenEV.Platform.EvoData;
using OpenEV.Override.Ports.Boot;
using OpenEV.Override.Ports.Combat;
using OpenEV.Override.Ports.Combat.Model;
using OpenEV.Override.Ports.Core.Model;
using OpenEV.Override.Ports.Dialog;
using OpenEV.Override.Ports.Graphics.Model;
using OpenEV.Override.Ports.Misc;

namespace OpenEV.Override.Game;

// Runs the game's transcribed TitleMainLoop on a background thread, so the original do/while
// WaitNextEvent + while-Button loops execute unchanged. The host thread samples input and drains
// the Canvas command queue each frame.
internal static class TitleAdapter
{
    private static Thread? _titleThread;
    private static string? _gameDirForLaunch;
    private static OverrideGameData? _data;
    private static TextureCache? _textures;
    private static SpriteCache? _sprites;
    private static SoundEngine? _sound;

    public static void Setup(string? gameDir)
    {
        // The PEF data-segment load ran in Program.Main before this call, mirroring the Mac CFM
        // loader relocating the fragment before `.start`. Running it before TitleMemory.Init lets
        // the title/boot steps still override the mutable globals they manage.
        TitleMemory.Init(OverrideGameHost.VirtualWidth, OverrideGameHost.VirtualHeight);
        // The game opts in to the Window-Manager update-event model: InvalRect → one updateEvt from
        // WaitNextEvent. The title paints its pilot-info panel ONLY through that path (updateEvt →
        // DrawPilotInfo), same as the Mac. (The Register app shares the pump but never opts in.)
        MacToolbox.UpdateEventsEnabled = true;

        if (gameDir is not null)
        {
            try
            {
                _data = OverrideDataLoader.Load(gameDir, msg => Console.WriteLine($"[loader] {msg}"));
                _textures = new TextureCache(_data);
                _sprites = new SpriteCache(_data);
                MacToolbox.PictResolver = id => _textures.GetPict(id);
                MacToolbox.GetPictureImpl = id => _data.Picts.ContainsKey(id) ? id : 0;
                // LoadSpriteSheetsAndGWorlds (FUN_1001d634, boot step 36) calls this right after
                // building each 'spïn' band's frame table — see WireFrameRange for why it patches
                // per-band rather than reusing AllocateSlotBitmapHeader's slot-GWorld-PixBase math.
                MacToolbox.WireSpriteBand = WireFrameRange;

                // The shareware nag's "Register" button (ShowSharewareNagDialog →
                // LaunchApplicationByFSSpec) Process.Starts the built OpenEV.Register exe.
                _gameDirForLaunch = gameDir;
                MacToolbox.AppLauncher = LaunchRegisterApp;

                // Enter-Ship render-world setup — invoked by RunGameSessionLauncher (FUN_10045778)
                // when a session starts. FAITHFUL: the launcher does NOT call
                // FUN_10053ab0 (InitGameWorldState) — New/Open Pilot run it BEFORE loading the pilot.
                MacToolbox.OnEnterGameWorld = () =>
                {
                    try { BuildShipSpriteTable(); }
                    catch (Exception ex) { Console.WriteLine($"[ENTERSHIP] sprite-table build threw: {ex.Message}\n{ex.StackTrace}"); }
                };

                // Resource Manager: serve the raw Mac resource payload by its OSType — what the
                // universe loader (ship/spob/syst/weap/outfit at byte offsets) and GetIndString
                // (STR#) expect from GetResource.
                MacToolbox.GetResourceImpl = (type, id) =>
                    _data.RawByOsType.TryGetValue((type, id), out var raw) ? raw : null;
                // GetResInfo name lookup — the loader copies these into the system/ship/spob/outfit
                // name fields (HUD, target display).
                MacToolbox.GetResNameImpl = (type, id) =>
                    _data.NameByOsType.TryGetValue((type, id), out var n) ? n : null;
                // CountResources/GetIndResource — the syst/spob loops early-exit once they've loaded
                // this many; LoadCargoResources walks 'dëqt' this way. Indexed once by OverrideGameData.
                MacToolbox.CountResourcesImpl = _data.CountResources;
                MacToolbox.GetIndResourceIdImpl = _data.GetIndResourceId;
                Console.WriteLine($"[TitleAdapter] Data loaded: {_data.Picts.Count} PICTs, {_data.Snds.Count} snds; raw resources={_data.RawByOsType.Count}.");

                // Give the boot's OpenResFile (OpenPluginResourceFiles / FUN_10015b4c) its real
                // outcome: a distinct positive refNum per STR# 130 fork actually opened, -1 for absent.
                // A missing required fork then trips the game's fade + "couldn't locate its <…>" alert
                // + ExitToShell faithfully.
                var resRefByName = new Dictionary<string, int>(_data.OpenedDataFiles.Count);
                int nextResRef = 2;   // 0/-1 reserved (present-but-0 / absent); real refNums positive
                foreach (string name in _data.OpenedDataFiles) resRefByName[name] = nextResRef++;
                MacToolbox.ResFileOpener = name => resRefByName.TryGetValue(name, out int r) ? r : -1;

                // QuickTime movie files (PlayQuickTimeMovie / FUN_10060504): the original
                // resolves the 'dëqt' record's file name in the EV Plug-Ins folder. Data
                // fork only — the movies are flattened .mov files. Without a game dir the
                // resolver stays null and EnterMovies keeps reporting -1 (a Mac without
                // QuickTime), so movies degrade to their dësc-text fallback.
                string movieDir = Path.Combine(gameDir, "EV Plug-Ins");
                MacToolbox.MovieFileResolver = name =>
                {
                    try
                    {
                        string p = Path.Combine(movieDir, name);
                        return File.Exists(p) ? File.ReadAllBytes(p) : null;
                    }
                    catch { return null; }
                };

                // Dialog Manager: serve DLOG/DITL templates to GetNewDialog from the parsed records.
                // Window bounds + each item's DITL-local rect/kind/text/id pass through; GetNewDialog
                // centres the window and offsets items to global coords. Unknown ids return null.
                MacToolbox.GetDialogTemplateImpl = dlogId =>
                {
                    if (!_data.TryGetDialogAndItems(dlogId, out var dl, out var items)) return null;
                    var tmpl = new MacToolbox.DlgTemplate
                    {
                        Top = dl.Top, Left = dl.Left, Bottom = dl.Bottom, Right = dl.Right,
                        ProcId = dl.ProcId, PositionType = dl.PositionType, ItemsId = dl.ItemsId,
                        Visible = dl.Visible,
                    };
                    foreach (var it in items)
                        tmpl.Items.Add(new MacToolbox.DlgTemplateItem
                        {
                            Kind = (MacToolbox.DitlItemKind)(byte)it.Kind,
                            Top = it.Top, Left = it.Left, Bottom = it.Bottom, Right = it.Right,
                            Enabled = it.Enabled, Text = it.Text, ResourceId = it.ResourceId,
                        });
                    return tmpl;
                };

                // Sound bridge. The Mac Sound Manager + software mixer aren't portable, so route the
                // high-level sound traps to the host SoundEngine. SndPlay (FUN_10060288) decodes the
                // snd-handle sentinel from FUN_10075450; the music paths bridge from SndStartFilePlay
                // (title 30000) and LoadAndStartSoundPair (credits 30001).
                _sound = new SoundEngine(_data);
                MacToolbox.SndPlayer        = (id, vol) => _sound.PlaySfx(id, vol);
                MacToolbox.BeepPlayer       = ()        => _sound.PlayBeep();
                MacToolbox.FileMusicPlayer  = id        => _sound.StartFileMusic(id);
                MacToolbox.FileMusicStopper = ()        => _sound.StopFileMusic();
                MacToolbox.PairMusicPlayer  = (a, b)    => _sound.StartPairMusic(a, b);
                MacToolbox.PairMusicStopper = ()        => _sound.StopPairMusic();
                MacToolbox.SfxStopper       = id        => _sound.StopSfx(id);
                MacToolbox.SfxStopAll       = ()        => _sound.StopAllSfx();
                MacToolbox.MasterVolumeSetter = v       => _sound.MasterVolume = v;
                Console.WriteLine($"[TitleAdapter] Sound bridge wired ({_data.Snds.Count} snds; title=30000 about=30001 click=600).");

                // The backdrop (0x1008f6ee) and anim (0x1008f700) pixmap keys are real writable
                // RenderTargets (registered by the host in Initialize); the title composes into them
                // via DrawPicture/CopyBits like the Mac, so no static PICT textures are registered here.
                // The hover orb blits the real spïn-900 masked records, wired post-boot in TitleThreadEntry.

                // Prefs persistence: WritePrefsToDisk (FUN_1001a3b8) drives FSMakeFSSpec →
                // FSpCreateResFile → FSpOpenResFile → AddResource → CloseResFile; the File Manager
                // bridge round-trips a genuine 'Mp¨Ä' id-0x80 resource fork on disk. Scoped to the
                // prefs filename; every other FSSpec caller keeps its no-op.
                try
                {
                    // Host substrate (Mac-invisible): the portable data root, beside the executable.
                    string prefsDir = EvoPaths.DataRoot;
                    MacToolbox.HfsDataForkBaseDir = prefsDir;
                    string PrefsPath(string name) => Path.Combine(prefsDir, name);
                    // The original saves pilots in a "Pilots" subfolder of the Preferences folder
                    // (FUN_1001e940 creates it from binary string toc-0x58fe; the pilot FSSpec uses
                    // dirID toc+0x1e94, distinct from the prefs-folder dirID toc+0x1e90). prefsDir is
                    // the prefs-folder analog, so pilots live in prefsDir/Pilots; the prefs file itself
                    // stays at the prefsDir root.
                    string pilotsDir = Path.Combine(prefsDir, "Pilots");
                    // Set precisely once STR# 130/7 is read below; anything else is a pilot.
                    string prefsFileName = "Override Prefs";
                    var win1252 = Encoding.GetEncoding(1252);
                    uint TypeCode(string s)
                    {
                        var b = win1252.GetBytes(s);
                        return (uint)((b[0] << 24) | (b[1] << 16) | (b[2] << 8) | b[3]);
                    }
                    MacToolbox.ForkParser = raw =>
                    {
                        var list = new List<MacToolbox.ForkResource>();
                        foreach (var e in MacResourceFork.Read(raw))
                            list.Add(new MacToolbox.ForkResource { Type = TypeCode(e.EvoType()), Id = e.Id, Data = e.Data, Name = e.Name });
                        return list;
                    };
                    MacToolbox.ForkSerializer = res =>
                    {
                        var resources = res.ConvertAll(r =>
                            new ForkResource(r.Type, (short)r.Id, r.Name, r.Data));
                        return MacResourceFork.Write(resources);
                    };
                    // A picked pilot (Open Pilot) carries an explicit path override; the prefs file
                    // lives at the prefsDir root; every other managed name is a pilot in Pilots/.
                    string ResolvePath(string name) =>
                        MacToolbox.ManagedForkPathOverride.TryGetValue(name, out var p) ? p
                        : string.Equals(name, prefsFileName, StringComparison.Ordinal) ? PrefsPath(name)
                        : Path.Combine(pilotsDir, name);
                    MacToolbox.ForkFileReader = name =>
                        File.Exists(ResolvePath(name)) ? File.ReadAllBytes(ResolvePath(name)) : null;
                    MacToolbox.ForkFileWriter = (name, bytes) =>
                    {
                        var path = ResolvePath(name);
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        File.WriteAllBytes(path, bytes);
                    };
                    MacToolbox.ForkFileDeleter = name =>
                    {
                        var path = ResolvePath(name);
                        if (File.Exists(path)) File.Delete(path);
                    };
                    // Open Pilot file picker (StandardGetFile). Native dialog runs on a dedicated STA
                    // thread (the title thread is MTA), defaulting to the Pilots subfolder.
                    Directory.CreateDirectory(pilotsDir);
                    MacToolbox.PilotFilePicker = () => PickPilotFile(pilotsDir);
                    // Boot's Pilots-folder guard (OpenPluginResourceFiles / FUN_10015b4c) probes
                    // ":Pilots" via FSMakeFSSpec — answer noErr/fnfErr for that name, decline every
                    // other so unrelated callers keep their no-FS noErr. (The host just created
                    // pilotsDir above, so this always reports present — faithful mechanism, inert here.)
                    MacToolbox.FsSpecByNameProbe = name => name == ":Pilots"
                        ? (short)(Directory.Exists(pilotsDir) ? 0 : -43)
                        : (short?)null;
                    // Register the prefs file under the name the ported code asks for (STR# 130/0x82
                    // index 7) so FSMakeFSSpec recognises it; fall back to the classic name if absent.
                    prefsFileName = MacToolbox.GetIndString((short)0x82, (short)7);
                    if (string.IsNullOrEmpty(prefsFileName)) prefsFileName = "Override Prefs";
                    MacToolbox.RegisterManagedForkFile(prefsFileName);
                    // Register the "Last Pilot" pointer so FSMakeFSSpec recognises it at boot — the
                    // last-pilot auto-load (FUN_1001b56c) probes it via
                    // PilotFileExistsOnDefaultVolume("Last Pilot") and resumes the pilot it points at.
                    MacToolbox.RegisterManagedForkFile(MacToolbox.LastPilotPointerName);
                    Console.WriteLine($"[TitleAdapter] Prefs → {PrefsPath(prefsFileName)} ; Pilots → {pilotsDir}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[TitleAdapter] Prefs persistence wiring failed: {ex.Message}");
                }

                // Set Prefs global/TOC slots, default key bindings, keybind-capture modal filter.
                PrefsMemory.Init();
                Console.WriteLine("[TitleAdapter] GWorld targets: SCREEN/BACKDROP/ANIM registered by host; backdrop+animFrames+splashes=preloaded");

                // Fonts. Geneva TTF reads the extracted sfnt from <exe dir>/Fonts/.
                GenevaFont.Init();
                MacToolbox.Font = GenevaFont.System;
                // Sillycon (Mac font ID 2020 / TextFont 0x7e4) — the misnamed extracted
                // Geneva_9295.ttf, kept separate so Geneva (ID 3) gets a real Geneva/Verdana above.
                SillyconFont.Init();
                MacToolbox.SillyconFont = SillyconFont.System;
                // Times (Mac font ID 20) — the About-EVÉ credits roll. EVO doesn't bundle Times; use
                // the Windows metric-equivalent.
                TimesFont.Init();
                MacToolbox.TimesFont = TimesFont.System;
                // Chicago for the Mac SYSTEM font (family ID 0) — dialog statText/buttons/TextEdit.
                // Bundled public-domain ChicagoFLF; null would fall back to Geneva + faux-bold.
                SystemFont.Init();
                MacToolbox.SystemFont = SystemFont.System;
                Console.WriteLine($"[TitleAdapter] Fonts — Geneva: {GenevaFont.Available} (src={GenevaFont.Source}); Chicago: {SystemFont.Available} (src={SystemFont.Source}); Times: {TimesFont.Available} (src={TimesFont.Source}); Sillycon: {SillyconFont.Available} (src={SillyconFont.Source})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TitleAdapter] Data load failed: {ex.Message}");
            }
        }

        // The title thread runs the pre-title boot splash + InitTitleBackdrop + the transcribed title
        // logic in an infinite loop; the quit flag breaks out. Hosted here (not the host render thread)
        // so the boot splash's Thread.Sleep between PICTs can hold each frame without blocking Draw.
        _titleThread = new Thread(TitleThreadEntry) { IsBackground = true, Name = "EVO-Title" };
        _titleThread.Start();
    }

    // Launch the standalone "Register EV Override" port (installed as MacToolbox.AppLauncher).
    // Resolves the built OpenEV.Register exe near the game binary or in the register/ build output,
    // then Process.Starts it with the game dir. Returns noErr(0) on launch, fnfErr(-43) otherwise.
    private static int LaunchRegisterApp(string appName)
    {
        try
        {
            string exeName = OperatingSystem.IsWindows() ? "OpenEV.Register.exe" : "OpenEV.Register";
            var candidates = new List<string>
            {
                Path.Combine(AppContext.BaseDirectory, exeName),
            };
            // Walk up from the game binary to the repo root and probe the register build output.
            var dir = new System.IO.DirectoryInfo(AppContext.BaseDirectory);
            for (int up = 0; up < 8 && dir is not null; up++, dir = dir.Parent)
            {
                string reg = Path.Combine(dir.FullName, "register", "src", "OpenEV.Register.App", "bin");
                if (!Directory.Exists(reg)) continue;
                // bin/{Debug,Release}/{tfm}/exe — enumerate the TFM subdir rather than hardcoding one,
                // so a framework bump (net8→net10→…) doesn't silently break this dev-tree fallback.
                foreach (var cfg in new[] { "Debug", "Release" })
                {
                    string cfgDir = Path.Combine(reg, cfg);
                    if (!Directory.Exists(cfgDir)) continue;
                    foreach (var tfmDir in Directory.GetDirectories(cfgDir))
                    {
                        string p = Path.Combine(tfmDir, exeName);
                        if (File.Exists(p)) candidates.Add(p);
                    }
                }
            }
            string? exe = candidates.Find(File.Exists);
            if (exe is null)
            {
                Console.WriteLine($"[AppLauncher] OpenEV.Register exe not found for '{appName}' (built register/ first); candidates: {string.Join(" ; ", candidates)}");
                return -43;   // fnfErr
            }
            var psi = new ProcessStartInfo(exe) { UseShellExecute = false };
            if (_gameDirForLaunch is not null) psi.ArgumentList.Add(_gameDirForLaunch);
            Process.Start(psi);
            Console.WriteLine($"[AppLauncher] launched OpenEV.Register: {exe}");
            return 0;   // noErr
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppLauncher] launch failed: {ex.GetType().Name}: {ex.Message}");
            return -43;
        }
    }

    private static void TitleThreadEntry()
    {
        Console.WriteLine("[TitleThread] entered");
        try
        {
            // Run the pre-title boot sequence (FUN_10061bb0 → Boot.GameBootSequence). RunPreTitle
            // walks the Mac 46-step boot order: the Ambrosia/Override splash (steps 24+34) and the
            // title backdrop init (step 44) run; every Mac-only / subsystem-gated step is an explicit
            // no-op documented in GameBootSequence. On this background thread because the boot's
            // ScreenFade ramps block (Thread.Sleep per step) while the Draw pump keeps running.
            try
            {
                GameBootSequence.RunPreTitle();
                Console.WriteLine("[TitleThread] GameBootSequence.RunPreTitle done — splash shown, InnerArenaRect populated.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TitleThread] GameBootSequence threw: {ex.Message} {ex.StackTrace}");
            }

            // Host substrate: pin RenderMode to the direct-colour path for the title. Boot's
            // InitRenderWindow → CacheCurrentDeviceFields set it to the device depth (32), which routes
            // sprite blits (the hover orb included) into the Mac per-depth compose-port pipeline the
            // host doesn't model (its CopyMask src is the unresolvable ComposeScratchPort → no-op). The
            // in-game path already pins 0 in BuildShipSpriteTable; this is the same pin post-boot.
            GlobalState.RenderMode = 0;

            // Title hover-orb frames were built by boot step 36 (LoadSpriteSheetsAndGWorlds →
            // LoadSheetBand(900)) and re-pointed at host-decoded spïn-900 cell textures inline there
            // via MacToolbox.WireSpriteBand (see WireFrameRange). No separate wiring step needed here.

            // Steps 46-48 of `.start`: block in the title loop until quit, restore the cursor, then
            // GracefulExit → ExitToShell (step 48) ends the process. So PanicExit.Run(0) below is dead
            // code on a real quit — kept faithfully, exactly as the decompile keeps `.start`'s trailing
            // PanicExit after the non-returning FUN_10061bb0 (line 49850).
            GameBootSequence.RunTitleLoop();
            PanicExit.Run(0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TitleThread] EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
        Console.WriteLine("[TitleThread] exit");
    }

    // Re-point ONE 'spïn' band's boot-built frame records at host-decoded, cache-registered cell
    // textures. Installed as MacToolbox.WireSpriteBand and called by LoadSpriteSheetsAndGWorlds
    // (FUN_1001d634) right after it builds each band via AllocateSlotBitmapHeader — whose ColorRef
    // math is faithful but reads a slot-GWorld PixBase that's always 0 in the software renderer, so
    // this only patches the host-only ColorRef/Bounds on the SAME record, never a parallel one.
    // table[baseIdx+f] is genuinely 0 only if boot skipped the sprite-sheet load (dev-only 'ëbug'
    // SkipSpriteSheetLoad bit, ships off) or the spïn didn't decode; Register() is the defensive
    // fallback for that. Keys derive from spinId (0x100 spacing covers the largest 36-frame band), so
    // this is safe to call per band with no external key bookkeeping.
    private static int WireFrameRange(int spinId, int[] table, int baseIdx, int maxFrames)
    {
        var frames = _sprites?.GetSpinFrames(spinId);
        if (frames is not { Length: > 0 }) return 0;
        int keyBase = 0x20000000 + spinId * 0x100;
        int wired = 0;
        for (int f = 0; f < frames.Length && f < maxFrames && baseIdx + f < table.Length; f++)
        {
            if (frames[f] is null) continue;
            int key = keyBase + f * 4;
            MacToolbox.SetScratchPixmap(key, frames[f]);
            int handle = table[baseIdx + f];
            var rec = handle != 0
                ? SpriteFrames.At(handle)
                : SpriteFrames.Register();
            rec.ColorRef = key;                               // CopyMask srcBits key (host ResolveTexture)
            rec.BoundsBottom = (short)frames[f].Height;
            rec.BoundsRight  = (short)frames[f].Width;
            table[baseIdx + f] = rec.Handle;
            wired++;
        }
        return wired;
    }

    // Pilot-file selection for StandardGetFile (Open Pilot). On Windows, pops a native open-file
    // dialog (NFD) on a DEDICATED STA thread. Two threads it must NOT use: the host/render thread
    // (marshalling NFD onto it deadlocks the render loop) and the title thread (MTA; NFD's
    // IFileOpenDialog needs STA). The title thread blocks on Join while the dialog is up —
    // faithful modal behaviour (Mac StandardGetFile is modal too) while the host keeps compositing.
    // Cancel returns null so OpenPilot loads nothing. Non-Windows (macOS NSOpenPanel is main-thread-
    // only; Linux GTK isn't thread-safe off the main thread) falls back to most-recent, hang-free.
    private static string? PickPilotFile(string startDir)
    {
        if (OperatingSystem.IsWindows() && TryPickPilotFileNative(startDir, out string? picked))
            return picked;                   // null = user cancelled → do not load
        return MostRecentPilot(startDir);    // no native dialog on this platform
    }

    // Runs the NFD open dialog on a dedicated STA thread. Returns true if the dialog ran to completion
    // (picked = chosen path, or null on Cancel); false if the native dialog was unavailable (no
    // bundled NFD native for the RID), so PickPilotFile can fall back to most-recent.
    private static bool TryPickPilotFileNative(string startDir, out string? picked)
    {
        string? initial = Directory.Exists(startDir) ? startDir : null;
        string? result = null;
        bool ran = false;
        var t = new Thread(() =>
        {
            try
            {
                var r = NativeFileDialogSharp.Dialog.FileOpen(null, initial);
                if (r.IsOk) { result = r.Path; ran = true; }
                else if (r.IsCancelled) { ran = true; }   // cancelled → null, no fallback
                else if (r.IsError) Console.WriteLine($"[TitleAdapter] Open Pilot dialog error: {r.ErrorMessage}");
            }
            catch (Exception ex)
            {
                // No bundled NFD native for this RID → let PickPilotFile fall back.
                Console.WriteLine($"[TitleAdapter] native Open-Pilot dialog unavailable ({ex.GetType().Name}); using most-recent.");
            }
        }) { IsBackground = true, Name = "EVO-OpenPilotDialog" };
        if (OperatingSystem.IsWindows()) t.SetApartmentState(ApartmentState.STA);
        t.Start();
        t.Join();
        picked = result;
        if (ran)
            Console.WriteLine(result is not null
                ? $"[TitleAdapter] Open Pilot → {result}"
                : "[TitleAdapter] Open Pilot → cancelled");
        return ran;
    }

    // Fallback when no native dialog is available: the most recently written file across the standard
    // pilot directories (null if none).
    private static string? MostRecentPilot(string startDir)
    {
        try
        {
            var dirs = new List<string>();
            if (Directory.Exists(startDir)) dirs.Add(startDir);
            string pilots = EvoPaths.Pilots;
            if (Directory.Exists(pilots)) dirs.Add(pilots);

            string? best = null;
            DateTime bestTime = DateTime.MinValue;
            foreach (var d in dirs)
                foreach (var f in Directory.GetFiles(d))
                {
                    var t = File.GetLastWriteTimeUtc(f);
                    if (t > bestTime) { bestTime = t; best = f; }
                }

            Console.WriteLine(best is not null
                ? $"[TitleAdapter] PickPilotFile fallback → most-recent pilot: {best}"
                : "[TitleAdapter] PickPilotFile: no pilot files found.");
            return best;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TitleAdapter] MostRecentPilot failed: {ex.Message}");
            return null;
        }
    }

    // Enter-Ship render init. The in-game path drives sprite rendering itself: each frame FUN_10061d74
    // creates a render node per active ship, the dispatcher FUN_1007d8bc calls its update UPP
    // (→ UpdateShipSlotTick) which sets node+0x16=sprite and node+2/+4=camera-relative pos, then
    // BlitSpriteByDepth→CopyMask blits it. Frame textures are wired at BOOT (LoadSpriteSheetsAndGWorlds
    // via WireSpriteBand), and the port-rect/camera-centre are set by TitleMemory.Init +
    // GWorldPort.ShowGameWindow — so this only sets up pieces that need Enter-Ship timing. Called ONCE
    // before the loop starts.
    private static void BuildShipSpriteTable()
    {
        // (1) Per-type node-update UPP tokens (the _DAT_1008124x globals copied into node+0x1a). A UPP
        //     is represented by the original routine address; InvokeNodeUpdateUpp dispatches on it. Seeded
        //     at boot and re-seeded here (idempotent).
        SpriteNodeUppCells.SeedDispatchTokens();

        // (2) The bounds-box-scale seed is the managed literal ShipStatConstants.SpriteBoundsScale (0.25).

        // (3) Compositing route — the offscreen game GWorld. window+0 (active port; sprite CopyMask
        //     dst) and window+0x1e (the offscreen GWorld FUN_1005ff4c composites) both point at
        //     GamePixmapSentinel, whose +2 key maps to the host _gameTarget RenderTarget. The frame
        //     draws into that RT and the host flushes it to the visible port each frame (FUN_1005ff4c's
        //     offscreen→screen CopyBits). window+0x72 < 2 selects the simple CopyMask path.
        GlobalState.ActivePortPixmap    = MacToolbox.GamePixmapSentinel;
        GlobalState.OffscreenGameGWorld = MacToolbox.GamePixmapSentinel;
        // window+0x38 = the backdrop/anim-scratch GWorld (the dirty-rect ERASE source). Point it at the
        // anim RT so the ported black PaintRect (RunMainGameLoop) lands in a real surface and
        // UpdateWindowRegionLayout can restore backdrop→_gameTarget under moving sprites (fixes trailing).
        GlobalState.AnimScratchPort = OverrideGameHost.AnimPixmapSentinel;
        GlobalState.RenderMode = 0;
        EvoGlobals.GameWorldActive = true;

        // (4) In-game status-panel offscreen GWorlds. InitGameOffscreenBuffers (FUN_100526cc, deferred)
        //     creates two offscreen GWorlds and draws the panel PICTs into them: PICT 128 = right-side
        //     status-panel BACKGROUND at slot 0x1008f6d0, PICT 160 = a secondary panel element at
        //     0x1008f708. RefreshStatusPanel (FUN_10054f28) then CopyBits the 144px strip from
        //     0x1008f6d0 each entry. Without these the HUD panel never draws. Each GWorld is a scratch
        //     pixmap at slot+2, with the slot self-referencing (ReadInt(slot)+2 == slot+2 == key).
        WirePanelGWorld(RenderGlobals.StatusPanelPort, 0x80);     // status-panel background (PICT 128)
        WirePanelGWorld(RenderGlobals.SecondaryPanelPort, 0xa0);  // secondary panel element (PICT 160)

        bool shipTableReady = OriginalGameStateTotalBytes.GameTablesAllocated;
        int playerClass = shipTableReady ? GameData.Ships[0].ShipClass : -1;
        int classPresent = playerClass >= 0
            ? GameData.ShipClasses[playerClass].Cost : -1;
        Console.WriteLine($"[ENTERSHIP] world entered. playerClass={playerClass} class+0x36(spritePresent)=0x{classPresent:x8} " +
            $"scrCentre=({WorldFlags.CameraCentreX},{WorldFlags.CameraCentreY})");

        // (5) HUD/target-reticle overlay nodes (FUN_10052b38). The original creates these ONCE at boot
        //     and they persist for the session. World setup can run per game-world enter, so guard on
        //     the persistent reticle-node pointer to create exactly one set. Runs after the ship sprite
        //     table (0x1008a748) the reticle sizes its brackets from; the bracket update UPP
        //     (FUN_10020ad4 → TickEscortTractor) is dispatched per frame by TickSpriteSystem.
        if (EscortSpawnRecord.ReticleNode == 0)
        {
            try { SpawnHudOverlayNodes.Run(); }
            catch (Exception ex) { Console.WriteLine($"[ENTERSHIP] HUD overlay nodes (FUN_10052b38) threw: {ex.Message}"); }
        }
    }

    // Wire one in-game status-panel offscreen GWorld: register the panel PICT as a scratch pixmap at
    // the port's key (port.Handle + 2 == the legacy slot+2) and stamp the PICT dims into its portRect —
    // RefreshStatusPanel's CopyBits uses the key as src and the portRect as srcRect.
    private static void WirePanelGWorld(MacGrafPort port, int pictId)
    {
        var tex = _textures?.GetPict(pictId);
        if (tex is null)
        {
            Console.WriteLine($"[ENTERSHIP] panel GWorld 0x{port.Handle:x}: PICT {pictId} MISSING — HUD strip will be blank");
            return;
        }
        MacToolbox.SetScratchPixmap(port.PixmapKey, tex);
        port.RectTop = 0;
        port.RectLeft = 0;
        port.RectBottom = (short)tex.Height;
        port.RectRight = (short)tex.Width;
        Console.WriteLine($"[ENTERSHIP] panel GWorld 0x{port.Handle:x}: PICT {pictId} {tex.Width}x{tex.Height} wired");
    }

    public static void Stop()
    {
        _titleThread?.Join(500);
        // DEVIATION (host substrate): the port never disposed the SoundEngine. Close the audio device
        // before the host's Dispose calls SDL_Quit. Reached on the window-close path; the in-game Quit exits via ExitToShell on the title thread before this runs.
        _sound?.Dispose();
        _sound = null;   // the MacToolbox sound delegates capture this; don't leave them on a dead device
    }
}
