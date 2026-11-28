internal class Table(Btree tree)
{
    public Btree Tree { get; } = tree;
    public int RootPageNumber => Tree.RootPageNumber;

    public InsertOutcome Insert(Row row)
    {
        var key = TableKey.FromId(row.Id);
        var value = new byte[Row.ROW_SIZE];
        row.Serialize(value, 0);

        return Tree.Insert(key, value);
    }

    public void Select()
    {
        var cursor = Cursor.CreateAtStart(Tree);

        while (!cursor.EndOfTable)
        {
            var row = Row.Deserialize(cursor.GetValue());
            row.PrintRow();
            cursor.Advance();
        }
    }
}

