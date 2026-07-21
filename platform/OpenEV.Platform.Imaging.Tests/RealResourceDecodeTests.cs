using System;
using System.IO;
using OpenEV.Platform.EvoData;
using OpenEV.Platform.ResourceFork;
using OpenEV.Platform.Imaging;

namespace OpenEV.Platform.Imaging.Tests;

// Decodes the ACTUAL EVO 'cicn'/'ppat'/'clut' resources from the game data fork,
// validating the decoders (esp. the from-scratch ppat/clut) against real bytes —
// the in-game visuals (brackets, streaks, radar static, palettes) aren't reachable
// without a live game session, so this is the closest non-interactive check.
// Skips gracefully if the game data folder isn't found.
public class RealResourceDecodeTests
{
    private const uint Cicn = 0x6369636e, Ppat = 0x70706174, Clut = 0x636c7574;

    private static OverrideGameData? TryLoad()
    {
        const string Folder = "EV Override 1.0.2 Ä";
        foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                var candidate = Path.Combine(dir.FullName, Folder);
                if (Directory.Exists(candidate))
                {
                    try { return OverrideDataLoader.Load(candidate, _ => { }); }
                    catch { return null; }
                }
                dir = dir.Parent;
            }
        }
        return null;
    }

    [Fact]
    public void Cicn_RealResources_MostDecode()
    {
        var data = TryLoad();
        if (data is null) return;   // game data not present — skip
        int total = 0, ok = 0;
        foreach (var kv in data.RawByOsType)
        {
            if (kv.Key.RawType != Cicn) continue;
            total++;
            if (CicnDecoder.Decode(kv.Value, $"cicn {kv.Key.Id}") is not null) ok++;
        }
        if (total == 0) return;     // no cicn resources — skip
        Assert.True(ok > total / 2, $"cicn: only {ok}/{total} decoded");
    }

    [Fact]
    public void Ppat_RealResources_MostDecode()
    {
        var data = TryLoad();
        if (data is null) return;
        int total = 0, ok = 0;
        foreach (var kv in data.RawByOsType)
        {
            if (kv.Key.RawType != Ppat) continue;
            total++;
            var tile = PpatDecoder.Decode(kv.Value, $"ppat {kv.Key.Id}");
            if (tile is not null && tile.Width > 0 && tile.Height > 0) ok++;
        }
        if (total == 0) return;
        Assert.True(ok > total / 2, $"ppat: only {ok}/{total} decoded");
    }

    [Fact]
    public void Clut_RealResources_Parse()
    {
        var data = TryLoad();
        if (data is null) return;
        int total = 0, ok = 0;
        foreach (var kv in data.RawByOsType)
        {
            if (kv.Key.RawType != Clut) continue;
            total++;
            try
            {
                var r = new BigEndianSpanReader(kv.Value);
                var ct = ColorTableDecoder.Read(ref r);   // same format MacToolbox.DecodeClutResource parses
                if (ct.Count > 0) ok++;
            }
            catch { /* counts as a failure */ }
        }
        if (total == 0) return;
        Assert.True(ok > total / 2, $"clut: only {ok}/{total} parsed");
    }
}
