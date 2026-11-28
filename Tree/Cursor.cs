internal class Cursor(Btree tree, int pageNumber, uint initialCellNumber)
{
    public Btree Tree { get; } = tree;
    public LeafNode Leaf { get; private set; } = tree.GetLeafNode(pageNumber);
    public uint CellNumber { get; private set; } = initialCellNumber;
    public int PageNumber { get; private set; } = pageNumber;
    public bool EndOfTable => CellNumber >= Leaf.CellCount;

    public static Cursor CreateAtStart(Btree tree)
    {
        return FindInPage(tree, tree.RootPageNumber, new byte[tree.Layout.KeySize]);
    }

    public static Cursor CreateAtKey(Btree tree, ReadOnlySpan<byte> key)
    {
        return FindInPage(tree, tree.RootPageNumber, key);
    }

    public ReadOnlySpan<byte> GetValue() => Leaf.GetValue((int)CellNumber);

    public void Advance()
    {
        CellNumber++;

        if (CellNumber >= Leaf.CellCount)
        {
            var nextPageNumber = Leaf.NextLeaf;

            if (nextPageNumber != 0)
            {
                PageNumber = nextPageNumber;
                Leaf = Tree.GetLeafNode(PageNumber);
                CellNumber = 0;
            }
        }
    }

    private static Cursor FindInPage(Btree tree, int pageNumber, ReadOnlySpan<byte> key)
    {
        var page = tree.Pager.RetrievePage(pageNumber);
        var type = Node.GetNodeType(page);

        switch (type)
        {
            case NodeType.Leaf:
                var leaf = tree.GetLeafNode(page);
                var cell = leaf.FindCell(key);
                return new Cursor(tree, pageNumber, (uint)cell);
            case NodeType.Internal:
                var node = tree.GetInternalNode(page);
                var childNumber = node.FindChildNumber(key);
                var childPageNumber = node.GetChild(childNumber);
                return FindInPage(tree, childPageNumber, key);
            default:
                throw new InvalidOperationException("Unexpected node type.");
        }
    }
}
