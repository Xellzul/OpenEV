namespace OpenEV.Extraction.SitExtract;

/// <summary>
/// Prefix-code (Huffman) decoder ported from XADMaster's <c>XADPrefixCode</c>
/// (© Dag Ågren / MacPaw, LGPL 2.1+, https://github.com/MacPaw/XADMaster), minus the
/// lookup-table acceleration — a plain tree walk is fast enough for a 7 MB archive.
/// The tree is decoded with <see cref="BitReader"/>'s low-bit-first stream; codes
/// built from lengths are canonical with the shortest code being all zero bits and
/// their most significant code bit transmitted first (XAD's
/// <c>shortestCodeIsZeros:YES</c> + <c>CSInputNextSymbolUsingCodeLE</c> pairing).
/// </summary>
internal sealed class HuffmanDecoder
{
    // Node branches; values >= 0 are child node indices (or the symbol, at a leaf).
    // A fresh node is (-1, -2): unequal negatives, so it is neither a leaf (left ==
    // right) nor complete. Leaves store the symbol in both branches, exactly like
    // XADPrefixCode, and the leaf check happens before any descent.
    private readonly List<(int Left, int Right)> _nodes = [(-1, -2)];

    private bool IsLeaf(int node) => _nodes[node].Left == _nodes[node].Right;

    private int Branch(int node, int bit) => bit == 0 ? _nodes[node].Left : _nodes[node].Right;

    private void SetBranch(int node, int bit, int target)
    {
        var (left, right) = _nodes[node];
        _nodes[node] = bit == 0 ? (target, right) : (left, target);
    }

    /// <summary>Insert a code whose most significant bit is transmitted first.</summary>
    public void AddCodeHighBitFirst(int symbol, uint code, int length)
    {
        int node = 0;
        for (int bitPos = length - 1; bitPos >= 0; bitPos--)
        {
            int bit = (int)(code >> bitPos) & 1;
            if (IsLeaf(node))
                throw new InvalidDataException("Prefix-code conflict: code passes through a leaf.");
            if (Branch(node, bit) < 0)
            {
                _nodes.Add((-1, -2));
                SetBranch(node, bit, _nodes.Count - 1);
            }
            node = Branch(node, bit);
        }
        if (_nodes[node] != (-1, -2))
            throw new InvalidDataException("Prefix-code conflict: leaf position already occupied.");
        _nodes[node] = (symbol, symbol);
    }

    /// <summary>
    /// Insert a code given in transmission order with its LOW bit first (the form the
    /// hardcoded method-13 meta-code table uses), by reversing it to high-bit-first.
    /// </summary>
    public void AddCodeLowBitFirst(int symbol, uint code, int length)
    {
        uint reversed = 0;
        for (int i = 0; i < length; i++)
            reversed |= ((code >> i) & 1) << (length - 1 - i);
        AddCodeHighBitFirst(symbol, reversed, length);
    }

    /// <summary>
    /// Canonical construction from per-symbol code lengths (XAD's
    /// <c>initWithLengths:...shortestCodeIsZeros:YES</c>): symbols are assigned codes
    /// in order of increasing length, ties broken by symbol index, starting from the
    /// all-zeros code. Lengths &lt;= 0 mean "symbol absent".
    /// </summary>
    public static HuffmanDecoder FromLengths(ReadOnlySpan<int> lengths, int maxLength = 32)
    {
        var decoder = new HuffmanDecoder();
        uint code = 0;
        int symbolsLeft = lengths.Length;
        for (int length = 1; length <= maxLength; length++)
        {
            for (int i = 0; i < lengths.Length; i++)
            {
                if (lengths[i] != length) continue;
                decoder.AddCodeHighBitFirst(i, code, length);
                code++;
                if (--symbolsLeft == 0) return decoder;
            }
            code <<= 1;
        }
        return decoder;
    }

    /// <summary>Read one symbol by walking the tree with low-bit-first stream bits.</summary>
    public int DecodeSymbol(BitReader reader)
    {
        int node = 0;
        while (!IsLeaf(node))
        {
            int bit = reader.ReadBit();
            if (Branch(node, bit) < 0)
                throw new InvalidDataException("Invalid prefix code in bitstream.");
            node = Branch(node, bit);
        }
        return _nodes[node].Left;
    }
}
