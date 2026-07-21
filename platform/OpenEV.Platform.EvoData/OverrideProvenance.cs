namespace OpenEV.Platform.EvoData;

/// <summary>Which kind of resource-fork file a layer came from, low precedence → high.</summary>
public enum OverrideLayerKind
{
    Application, // the app fork "EV Override" — open from launch, lowest precedence
    DataFile,    // one of the six "Override *" data files (STR# 130 order)
    Plugin,      // a file in EV Plug-Ins/.rsrc — opened last, highest precedence
}

/// <summary>
/// One file's definition of a single (type,id) resource. <see cref="LoadOrder"/> is the file's
/// position in the open sequence (0 = app fork); a higher LoadOrder shadows a lower one because
/// the loader (like the Mac Resource Manager) is last-write-wins.
/// </summary>
public sealed record OverrideLayer(
    int LoadOrder, string FileName, OverrideLayerKind Kind,
    uint RawType, int Id, byte[] Payload, string? Name);

/// <summary>
/// All files that define one (type,id), in load order (lowest precedence first). The last entry
/// is the <see cref="Winner"/> — the version that actually reaches the game.
/// </summary>
public sealed class OverrideChain
{
    public IReadOnlyList<OverrideLayer> Layers { get; }
    public OverrideChain(IReadOnlyList<OverrideLayer> layers) { Layers = layers; }

    public OverrideLayer Winner => Layers[Layers.Count - 1];
    public bool HasMultipleLayers => Layers.Count > 1;
}

/// <summary>
/// The flattened <see cref="OverrideGameData"/> plus per-(type,id) provenance: which files defined
/// each resource and which one won. Produced by <see cref="OverrideDataLoader.LoadWithProvenance"/>.
/// The chain <see cref="OverrideChain.Winner"/> payload is the same array stored in
/// <see cref="OverrideGameData.RawByOsType"/>, so "what the chain shows" == "what reaches the game".
/// </summary>
public sealed class OverrideProvenanceData
{
    public OverrideGameData Data { get; }
    public IReadOnlyDictionary<(uint RawType, int Id), OverrideChain> Chains { get; }
    public IReadOnlyList<string> FileOrder { get; }

    public OverrideProvenanceData(OverrideGameData data,
        IReadOnlyDictionary<(uint RawType, int Id), OverrideChain> chains, IReadOnlyList<string> fileOrder)
    {
        Data = data;
        Chains = chains;
        FileOrder = fileOrder;
    }
}
