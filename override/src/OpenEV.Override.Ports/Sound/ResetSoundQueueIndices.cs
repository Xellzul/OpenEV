using OpenEV.Override.Ports.Sound.Model;
namespace OpenEV.Override.Ports.Sound;

// Decompile: EV Override-11.c lines 50093-50100.
// Empties the pending-sound ring: Count, WriteIndex, ReadIndex = 0 (in that
// original cell order 0x10081a5c / a58 / a54).
// The initial transcription wrote int 0 OVER the three pointer CELLS (WriteInt at the cell
// addresses) instead of zeroing the shorts behind them — it nulled the cells.
public static class ResetSoundQueueIndices
{
    public static void Run()
    {
        SoundQueueRing.Count = 0;
        SoundQueueRing.WriteIndex = 0;
        SoundQueueRing.ReadIndex = 0;
        return;
    }
}
