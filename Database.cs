internal class Database
{
    public Pager Pager { get; }
    public Table Table { get; }

    private Database(Pager pager, Table table)
    {
        Pager = pager;
        Table = table;
    }

    public static Database Open(string fileName)
    {
        var pager = new Pager(fileName);
        var layout = new TreeLayout(TableKey.SIZE, Row.ROW_SIZE, Constants.INTERNAL_NODE_MAX_CELLS);

        int tableRoot;

        if (pager.PageCount == 0)
        {
            var header = new Header(pager.RetrievePage(Constants.DB_HEADER_PAGE_NUMBER));
            header.Initialize();

            tableRoot = Constants.DB_HEADER_PAGE_NUMBER + 1;
            header.SetRoot(Constants.TABLE_TREE, tableRoot);

            new Btree(pager, layout, tableRoot).InitializeEmpty();
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

            if (tableRoot <= Constants.DB_HEADER_PAGE_NUMBER || tableRoot >= pager.PageCount)
            {
                Console.WriteLine("Damaged database header.");
                Environment.Exit(1);
            }
        }

        var tree = new Btree(pager, layout, tableRoot);
        return new Database(pager, new Table(tree));
    }

    public void Close()
    {
        Pager.Close();
    }
}
