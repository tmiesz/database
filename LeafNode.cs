class LeafNode(Page page) : Node(page)
{
    private static int CellStart(int cell) => Constants.LEAF_NODE_HEADER_SIZE + (cell * Constants.LEAF_NODE_CELL_SIZE);
    private static int RowStart(int cell) => CellStart(cell) + Constants.LEAF_NODE_VALUE_OFFSET;
    public uint MaxKey => GetKey((int)CellCount - 1);

    public int NextLeaf
    {
        get => BitConverter.ToInt32(Page.Data, Constants.LEAF_NODE_NEXT_LEAF_OFFSET);
        set
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, Page.Data, Constants.LEAF_NODE_NEXT_LEAF_OFFSET, bytes.Length);
        }
    }

    public uint CellCount
    {
        get => BitConverter.ToUInt32(Page.Data, Constants.LEAF_NODE_CELL_COUNT_OFFSET);
        set
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, Page.Data, Constants.LEAF_NODE_CELL_COUNT_OFFSET, bytes.Length);
        }
    }

    public uint GetKey(int cell) =>
        BitConverter.ToUInt32(Page.Data, CellStart(cell));

    public void SetKeyForCell(int cell, uint key)
    {
        var bytes = BitConverter.GetBytes(key);
        Array.Copy(bytes, 0, Page.Data, CellStart(cell), bytes.Length);
    }

    public Row GetRow(int cell)
    {
        return Row.Deserialize(Page.Data, RowStart(cell));
    }

    public void StoreRow(Row row, int cell)
    {
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
        for (int i = (int)CellCount - 1; i >= cell; i--)
        {
            Array.Copy(Page.Data, CellStart(i), Page.Data, CellStart(i + 1), Constants.LEAF_NODE_CELL_SIZE);
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
        Array.Copy(Page.Data, CellStart(fromCell), destination.Page.Data, CellStart(toCell), Constants.LEAF_NODE_CELL_SIZE);
    }
}

