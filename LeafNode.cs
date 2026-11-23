class LeafNode(Page page, TreeLayout layout) : Node(page)
{
    private int CellStart(int cell) => Constants.LEAF_NODE_HEADER_SIZE + (cell * Layout.LeafCellSize);
    private int RowStart(int cell) => CellStart(cell) + Layout.LeafValueOffset;
    public uint MaxKey => GetKey((int)CellCount - 1);

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

    public uint GetKey(int cell) =>
        BitConverter.ToUInt32(Page.Data, CellStart(cell));

    public void SetKeyForCell(int cell, uint key)
    {
        Page.MarkDirty();
        var bytes = BitConverter.GetBytes(key);
        Array.Copy(bytes, 0, Page.Data, CellStart(cell), bytes.Length);
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

    public int FindCell(uint key)
    {
        var min = 0;
        var onePastMax = (int)CellCount;

        while (min != onePastMax)
        {
            var middle = (min + onePastMax) / 2;
            var keyAtMiddle = GetKey(middle);

            if (key == keyAtMiddle)
                return middle;
            if (key < keyAtMiddle)
                onePastMax = middle;
            else
                min = middle + 1;
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
        Array.Copy(Page.Data, CellStart(fromCell), destination.Page.Data, CellStart(toCell), Constants.LEAF_NODE_CELL_SIZE);
    }
}

