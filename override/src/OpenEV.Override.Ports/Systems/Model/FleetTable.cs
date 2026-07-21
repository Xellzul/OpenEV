namespace OpenEV.Override.Ports.Systems.Model;

// The 'flët' FLEET definition table — 128 records, formerly 0x20 bytes each in
// the heap behind PTR slot 0x1008a534 (toc+0x1ed4, alloc 0x1000). LoadFleetResources
// fills it from 'flët' 0x80..; SpawnRandomEligibleFleet/SpawnFleet walk it. Records
// are typed managed now; the slot + heap range are retired (OriginalGameStateTotalBytes).
public static class FleetTable
{
    public const int Count = 128;   // resource IDs 128..255 ('flët' 0x80..)

    public static readonly FleetRecord[] Store = CreateStore();
    private static FleetRecord[] CreateStore()
    {
        var s = new FleetRecord[Count];
        for (int i = 0; i < s.Length; i++) s[i] = new FleetRecord();
        return s;
    }
}

// One fleet definition (offsets = the old record layout; resource offsets noted).
public sealed class FleetRecord
{
    public const int EscortGroupCount = 4;   // the fleet's escort groups (one ship class + count range each)

    public short LeadShipType;   // +0x00  res+0 − 0x80 (ship class of the lead ship; -1 = no fleet)
    public short Govt;       // +0x02  res+0x1a, − 0x80 normalized when > 0x7f (govt id shared by the fleet's ships)
    public short[] EscortType = new short[EscortGroupCount];  // +0x04  res+0x2..   − 0x80 (escort ship classes)
    public short[] EscortMin = new short[EscortGroupCount];  // +0x0c  res+0x0a.. (min escort count per type)
    public short[] EscortMax = new short[EscortGroupCount];  // +0x14  res+0x12.. (max escort count per type)
    public short LinkSyst;   // +0x1c  res+0x1c — banded system/govt selector (SpawnRandomEligibleFleet.IsLinkEligible
                             //        decodes it): exact match = direct syst link; 128..9999 = sibling-syst alias
                             //        (syst = link-128); 10000.. = govt id; 15000.. = govt ally; 20000.. = govt
                             //        not-self; 25000.. = govt enemy; -1 = always eligible.
    public short MissionBit;  // +0x1e  ControlBits gate (< 512 = bit index, 1000..1511 = alias; -1 = always)
}
