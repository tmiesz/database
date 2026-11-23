internal class TreeLayout(int keySize, int valueSize, int internalMaxCells)
{
    public int KeySize { get; } = keySize;
    public int ValueSize { get; } = valueSize;
    public int InternalMaxCells { get; } = internalMaxCells;

    public int LeafCellSize => KeySize + ValueSize;
    public int LeafCellOffset => KeySize;
    public int LeafMaxCells => Constants.LEAF_NODE_SPACE_FOR_CELLS / LeafCellSize;

    public int RightSplitCount => (LeafMaxCells + 1) / 2;
    public int LeftSplitCount => LeafMaxCells + 1 - RightSplitCount;

    public int InternalCellSize => Constants.INTERNAL_NODE_CELL_CHILD_SIZE + KeySize;
    public int InternalCapacity => (Constants.PAGE_SIZE - Constants.INTERNAL_NODE_HEADER_SIZE) / InternalCellSize;
}
