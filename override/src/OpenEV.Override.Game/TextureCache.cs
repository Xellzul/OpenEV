using System.Collections.Generic;
using OpenEV.Platform.EvoData;
using OpenEV.Platform.Imaging;

namespace OpenEV.Override.Game;

// Minimal PICT → Rgba8Image cache for the game. MacToolbox.PictResolver calls
// .GetPict(id). Decoding is pure managed (no GPU/thread affinity), so it is
// safe from any thread — but the resolver is invoked BOTH from the host drain
// (DrawPicture/CopyBits closures) AND synchronously from the title thread
// (RectCenter / DrawTransitionSplashPict), so the dictionary is guarded by a lock.
internal sealed class TextureCache
{
    private readonly OverrideGameData _data;
    private readonly Dictionary<int, Rgba8Image?> _picts = new();
    private readonly object _lock = new();

    public TextureCache(OverrideGameData data)
    {
        _data = data;
    }

    public Rgba8Image? GetPict(int id)
    {
        lock (_lock)
        {
            if (_picts.TryGetValue(id, out var cached)) return cached;
            Rgba8Image? img = null;
            if (_data.Picts.TryGetValue(id, out var bytes))
                img = PictDecoder.Decode(bytes, $"PICT {id}");
            _picts[id] = img;
            return img;
        }
    }
}
