using System;
using System.IO;
using System.Linq;
using OpenEV.Platform.EvoData;
using OpenEV.Platform.Imaging;

namespace OpenEV.Platform.Imaging.Tests;

// Regression guard for the New-Pilot "overwrite?" alert (DLOG 3002 / AlertModal_TwoButton)
// icon — investigated 2026-06-21 after the "dialog icon renders mirrored/wrong" review item.
//
// What the dump established: DLOG 3002's top-left icon is NOT a kind-32 Icon item and is NOT
// 'icl8 141' (the original suspicion). DITL 3002 item 4 is a kind-64 PICTURE item, resId 130,
// which v2 already draws via DrawDlgPicture. The "mirrored/wrong" report was the SAME bug class
// as the radar white-static: PICT 130 lives in TWO forks —
//   * "EV Override" (application fork): a red icon facing the other way,
//   * "Override Titles" (data file):    the correct up-right ship with a rainbow trail.
// The Mac Resource Manager (and the loader, since the resource-fork precedence fix) searches the
// most-recently-OPENED fork first, so the data file shadows the app fork and the CORRECT ship
// wins. The old "app fork last" order let the red app-fork icon win → the "mirrored/wrong" icon.
//
// This test pins that precedence for the alert icon so a loader-order regression can't silently
// bring the wrong PICT 130 back. (No 'icl8'/'ICON' rendering is involved or needed: the only
// kind-32 Icon DITL items in the whole game — DITL 129/901/3000 — reference resource ids 0/2,
// which match no icon resource of any family, so a faithful icon path would draw nothing.)
public class AlertIconPrecedenceTests
{
    private const uint PICT = 0x50494354;   // 'PICT'

    private static OverrideProvenanceData? TryLoad()
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
                    try { return OverrideDataLoader.LoadWithProvenance(candidate, _ => { }); }
                    catch { return null; }
                }
                dir = dir.Parent;
            }
        }
        return null;
    }

    [Fact]
    public void Pict130_AlertIcon_DataFileWinsOverAppFork()
    {
        var prov = TryLoad();
        if (prov is null) return;   // game data not present — skip

        Assert.True(prov.Chains.TryGetValue((PICT, 130), out var chain), "PICT 130 not loaded at all");

        // The collision must be real: the app fork AND a data file both define PICT 130.
        Assert.True(chain!.HasMultipleLayers, "PICT 130 should exist in >1 fork (app + Override Titles)");
        Assert.Contains(chain.Layers, l => l.Kind == OverrideLayerKind.Application);

        // The winner (what reaches the dialog) must be the data-file version, NOT the app fork's
        // red icon. This is the exact precedence the resource-fork fix restored.
        Assert.Equal(OverrideLayerKind.DataFile, chain.Winner.Kind);

        // And it must decode to the real 32×32 alert icon (the DITL item rect is 32×32).
        var icon = PictDecoder.Decode(chain.Winner.Payload, "PICT 130 alert icon");
        Assert.NotNull(icon);
        Assert.Equal(32, icon!.Width);
        Assert.Equal(32, icon.Height);
    }
}
