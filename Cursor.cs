class Cursor(Table table, int pageNumber, uint initialCellNumber)
{
    public Table Table { get; } = table;
    public LeafNode Leaf { get; private set; } = new(table.Tree.Pager.RetrievePage(pageNumber));
    public uint CellNumber { get; private set; } = initialCellNumber;
    public int PageNumber { get; private set; } = pageNumber;
    public bool EndOfTable => CellNumber >= Leaf.CellCount;

    public static Cursor CreateAtStart(Table table)
    {
        return FindInPage(table, table.RootPageNumber, 0);
    }

    public static Cursor CreateAtKey(Table table, uint key)
    {
        return FindInPage(table, table.RootPageNumber, key);
    }

    public Row GetValue() => Leaf.GetRow((int)CellNumber);

    public void Advance()
    {
        CellNumber++;

        if (CellNumber >= Leaf.CellCount)
        {
            var nextPageNumber = Leaf.NextLeaf;

            if (nextPageNumber != 0)
            {
                PageNumber = nextPageNumber;
                Leaf = new LeafNode(Table.Tree.Pager.RetrievePage(PageNumber));
                CellNumber = 0;
            }
        }
    }

    private static Cursor FindInPage(Table table, int pageNumber, uint key)
    {
        var page = table.Tree.Pager.RetrievePage(pageNumber);
        var type = Node.GetNodeType(page);

        switch (type)
        {
            case NodeType.Leaf:
                var leaf = new LeafNode(page);
                var cell = leaf.FindCell(key);
                return new Cursor(table, pageNumber, (uint)cell);
            case NodeType.Internal:
                var node = new InternalNode(page);
                var childNumber = node.FindChildNumber(key);
                var childPageNumber = node.GetChild(childNumber);
                return FindInPage(table, childPageNumber, key);
            default:
                throw new InvalidOperationException("Unexpected node type.");
        }
    }
}
