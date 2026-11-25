class Table
{
    public Btree Tree { get; }
    public int RootPageNumber => Tree.RootPageNumber;

    public Table(Btree tree)
    {
        Tree = tree;

        if (Tree.Pager.PageCount == 0)
        {
            var page = Tree.Pager.RetrievePage(RootPageNumber);
            var root = Tree.GetLeafNode(page);
            root.Initialize();

            root.IsRoot = true;

            Tree.Pager.CompleteStatement();
        }
    }

    public InsertOutcome Insert(Row row)
    {
        var key = TableKey.FromId(row.Id);
        var cursor = Cursor.CreateAtKey(this, key);
        var leaf = cursor.Leaf;
        var value = new byte[Row.ROW_SIZE];
        row.Serialize(value, 0);

        var cellNumber = cursor.CellNumber;
        if (cellNumber < leaf.CellCount && leaf.GetKey((int)cellNumber).SequenceEqual(key))
            return InsertOutcome.Duplicate;

        if (leaf.CellCount >= Tree.Layout.LeafMaxCells)
        {
            Tree.SplitAndInsert(cursor, key, value);
            return InsertOutcome.Success;
        }

        leaf.MakeRoomAt((int)cellNumber);
        leaf.SetKeyForCell((int)cellNumber, key);
        leaf.SetValueForCell((int)cellNumber, value);

        leaf.CellCount++;

        return InsertOutcome.Success;
    }

    public void Select()
    {
        var cursor = Cursor.CreateAtStart(this);

        while (!cursor.EndOfTable)
        {
            var row = Row.Deserialize(cursor.GetValue());
            row.PrintRow();
            cursor.Advance();
        }
    }

    public void Close()
    {
        Tree.Pager.Close();
    }
}

