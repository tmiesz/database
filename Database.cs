internal class Database
{
    public Pager Pager { get; }
    public Table Table { get; }
    public Btree UsernameIndex { get; }

    private Database(Pager pager, Table table, Btree usernameIndex)
    {
        Pager = pager;
        Table = table;
        UsernameIndex = usernameIndex;
    }

    public static Database Open(string fileName)
    {
        var pager = new Pager(fileName);
        var tableLayout = new TreeLayout(TableKey.SIZE, Row.ROW_SIZE, Constants.INTERNAL_NODE_MAX_CELLS);
        var indexLayout = new TreeLayout(UsernameKey.SIZE, 0, Constants.INTERNAL_NODE_MAX_CELLS);

        int tableRoot;
        int indexRoot;

        if (pager.PageCount == 0)
        {
            var header = new Header(pager.RetrievePage(Constants.DB_HEADER_PAGE_NUMBER));
            header.Initialize();

            tableRoot = Constants.DB_HEADER_PAGE_NUMBER + 1;
            indexRoot = tableRoot + 1;
            header.SetRoot(Constants.TABLE_TREE, tableRoot);
            header.SetRoot(Constants.USERNAME_INDEX_TREE, indexRoot);

            new Btree(pager, tableLayout, tableRoot).InitializeEmpty();
            new Btree(pager, indexLayout, indexRoot).InitializeEmpty();
            pager.CompleteStatement();
        }
        else
        {
            var header = new Header(pager.RetrievePage(Constants.DB_HEADER_PAGE_NUMBER));

            if (!header.IsValid)
            {
                Console.WriteLine("Not a valid database file.");
                Environment.Exit(1);
            }

            tableRoot = header.GetRoot(Constants.TABLE_TREE);
            indexRoot = header.GetRoot(Constants.USERNAME_INDEX_TREE);

            if (tableRoot <= Constants.DB_HEADER_PAGE_NUMBER || tableRoot >= pager.PageCount ||
                indexRoot <= Constants.DB_HEADER_PAGE_NUMBER || indexRoot >= pager.PageCount ||
                indexRoot == tableRoot)
            {
                Console.WriteLine("Damaged database header.");
                Environment.Exit(1);
            }
        }

        var tableTree = new Btree(pager, tableLayout, tableRoot);
        var indexTree = new Btree(pager, indexLayout, indexRoot);

        return new Database(pager, new Table(tableTree), indexTree);
    }

    public InsertOutcome Insert(Row row)
    {
        var outcome = Table.Insert(row);

        if (outcome is InsertOutcome.Duplicate)
            return outcome;

        var key = UsernameKey.Create(row.UserName, row.Id);
        var indexOutcome = UsernameIndex.Insert(key, []);

        if (indexOutcome is InsertOutcome.Duplicate)
            throw new InvalidOperationException("Table and username index disagree.");

        return InsertOutcome.Success;
    }

    public void Close()
    {
        Pager.Close();
    }
}
