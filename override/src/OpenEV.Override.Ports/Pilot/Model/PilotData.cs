using System;
using System.Text;
using OpenEV.Platform.Toolbox;

namespace OpenEV.Override.Ports.Pilot.Model;

// One pilot-save serialization block as a managed big-endian byte[].
// SavePilotFile fills the blocks field-by-field and hands the bytes to
// AddResource via MacToolbox.NewHandleFromBytes; LoadPluginPilotData copies the
// loaded resource's bytes back in via LoadFrom.
public sealed class PilotBlock
{
    public readonly byte[] Data;
    public PilotBlock(int size) => Data = new byte[size];

    public byte ByteAt(int off) => Data[off];
    public short ShortAt(int off) => BigEndian.ReadInt16(Data, off);
    public int IntAt(int off) => BigEndian.ReadInt32(Data, off);
    public void SetByte(int off, byte v) => Data[off] = v;
    public void SetShort(int off, short v) => BigEndian.WriteInt16(Data, off, v);
    public void SetInt(int off, int v) => BigEndian.WriteInt32(Data, off, v);

    // Pascal string (length byte + chars + NUL) inside the block — the in-record
    // govt Name/MissionName buffers.
    public void SetPascal(int off, string s, int maxLen)
    {
        int len = s.Length;
        if (len > 255) len = 255;
        if (len > maxLen - 1) len = maxLen - 1;
        if (len < 0) len = 0;
        Data[off] = (byte)len;
        for (int i = 0; i < len; i++) Data[off + 1 + i] = (byte)s[i];
        Data[off + 1 + len] = 0;
    }
    public string PascalAt(int off)
    {
        int len = Data[off];
        if (len == 0) return string.Empty;
        if (len > Data.Length - off - 1) len = Data.Length - off - 1;
        return MacToolbox.MacRomanToString(Data, off + 1, len);   // Mac-Roman, not Windows-1252
    }

    /// Copy a loaded resource's bytes into the block (anything beyond a
    /// truncated resource stays zero, matching the Mac's fresh-handle reads).
    public void LoadFrom(byte[] src)
    {
        int n = src.Length < Data.Length ? src.Length : Data.Length;
        Array.Copy(src, Data, n);
        if (n < Data.Length) Array.Clear(Data, n, Data.Length - n);
    }
}

// The two in-memory pilot save blocks: the MAIN record (0x26ee = 9966 bytes,
// OpïL id 0x80 "Pilot Data") and the AUX/galaxy block (0x22fe = 8958 bytes,
// OpïL id 0x81, named after the player ship). They used to live behind Mac
// Handles in the global slots 0x100870c4 (record) / 0x100870c8 (aux); the
// Handle ints inside SavePilotFile/LoadPluginPilotData are transient
// resource-manager handles only.
public static class PilotData
{
    public const int RecordSize = 0x26ee;
    public const int AuxSize = 0x22fe;

    public static readonly PilotBlock RecordBlock = new(RecordSize);
    public static readonly PilotBlock AuxBlock = new(AuxSize);

    // Typed facades (named fields at their save offsets) — ALL access to the
    // blocks outside PilotRec/PilotAuxRec goes through these, never raw offsets.
    public static PilotRec Record => new PilotRec(RecordBlock);
    public static PilotAuxRec Aux => new PilotAuxRec(AuxBlock);
}
