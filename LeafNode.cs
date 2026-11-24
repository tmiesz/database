class LeafNode(Page page, TreeLayout layout) : Node(page)
{
    private int CellStart(int cell) => Constants.LEAF_NODE_HEADER_SIZE + (cell * Layout.LeafCellSize);
    private int RowStart(int cell) => CellStart(cell) + Layout.LeafValueOffset;
    public byte[] MaxKey => GetKey((int)CellCount - 1).ToArray();

    public int NextLeaf
    {
        get => BitConverter.ToInt32(Page.Data, Constants.LEAF_NODE_NEXT_LEAF_OFFSET);
        set
        {
            Page.MarkDirty();
            var bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, Page.Data, Constants.LEAF_NODE_NEXT_LEAF_OFFSET, bytes.Length);
        }
    }

    public uint CellCount
    {
        get => BitConverter.ToUInt32(Page.Data, Constants.LEAF_NODE_CELL_COUNT_OFFSET);
        set
        {
            Page.MarkDirty();
            var bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, Page.Data, Constants.LEAF_NODE_CELL_COUNT_OFFSET, bytes.Length);
        }
    }

    private TreeLayout Layout { get; } = layout;

    public ReadOnlySpan<byte> GetKey(int cell) =>
        Page.Data.AsSpan(CellStart(cell), Layout.KeySize);

    public void SetKeyForCell(int cell, ReadOnlySpan<byte> key)
    {
        if (key.Length != Layout.KeySize)
            throw new ArgumentException("Key length does not match the layout.", nameof(key));

        Page.MarkDirty();
        key.CopyTo(Page.Data.AsSpan(CellStart(cell), Layout.KeySize));
    }

    public Row GetRow(int cell)
    {
        return Row.Deserialize(Page.Data, RowStart(cell));
    }

    public void StoreRow(Row row, int cell)
    {
        Page.MarkDirty();
        row.Serialize(Page.Data, RowStart(cell));
    }

    public int FindCell(ReadOnlySpan<byte> key)
    {
        var min = 0;
        var onePastMax = (int)CellCount;

        while (min != onePastMax)
        {
            var middle = (min + onePastMax) / 2;
            var comparison = key.SequenceCompareTo(GetKey(middle));

            if (comparison == 0) return middle;
            if (comparison < 0) onePastMax = middle;
            else min = middle + 1;
        }

        return min;
    }

    public void MakeRoomAt(int cell)
    {
        Page.MarkDirty();
        for (int i = (int)CellCount - 1; i >= cell; i--)
        {
            Array.Copy(Page.Data, CellStart(i), Page.Data, CellStart(i + 1), Layout.LeafCellSize);
        }
    }

    public void Initialize()
    {
        NodeType = NodeType.Leaf;
        IsRoot = false;
        NextLeaf = 0;
        CellCount = 0;
    }

    public void CopyCellTo(int fromCell, LeafNode destination, int toCell)
    {
        destination.Page.MarkDirty();
        Array.Copy(Page.Data, CellStart(fromCell), destination.Page.Data, CellStart(toCell), Layout.LeafCellSize);
    }
}

