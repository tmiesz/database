using System.Text;

if (args.Length < 1)
{
    Console.WriteLine("Must supply a database filename.");
    Environment.Exit(1);
}

var pager = new Pager(args[0]);
var table = new Table(pager);

while (true)
{
    PrintPrompt();
    HandleInput(ReadInput());
}

void HandleInput(string input)
{
    if (input.StartsWith('.'))
    {
        HandleMetaInput(input, table);
        return;
    }

    var result = PrepareInput(input, out var statement);

    switch (result)
    {
        case Outcome.Success:
            break;
        case Outcome.SyntaxError:
            Console.WriteLine("Syntax error. Could not parse statement.");
            return;
        case Outcome.UnrecognizedKeyword:
            Console.WriteLine($"Unrecognized keyword at start of '{input}'.");
            return;
        case Outcome.SizeError:
            Console.WriteLine("String is too long.");
            return;
        case Outcome.NegativeId:
            Console.WriteLine("ID must be positive.");
            return;

    }

    Execute(statement, table);
}

static void Execute(Statement statement, Table table)
{
    switch (statement.Kind)
    {
        case Kind.INSERT:
            {
                var result = table.Insert(statement.Row!);

                if (result == InsertOutcome.Duplicate)
                    Console.WriteLine("Error: Duplicate key.");
                else Console.WriteLine("Executed.");

                break;
            }
        case Kind.SELECT:
            {
                table.Select();
                Console.WriteLine("Executed.");
                break;
            }
        case Kind.BEGIN:
            {
                var result = table.Pager.Begin();
                if (result == TransactionOutcome.AlreadyOpen)
                    Console.WriteLine("Error: Transaction already open.");
                else Console.WriteLine("Executed.");
                break;
            }
        case Kind.COMMIT:
            {
                var result = table.Pager.Commit();
                if (result == TransactionOutcome.NotOpen)
                    Console.WriteLine("Error: No transaction open.");
                else Console.WriteLine("Executed.");
                break;
            }
        case Kind.ROLLBACK:
            {
                var result = table.Pager.Rollback();
                if (result == TransactionOutcome.NotOpen)
                    Console.WriteLine("Error: No transaction open.");
                else Console.WriteLine("Executed.");
                break;
            }
        default:
            Console.WriteLine("Unrecognized command.");
            break;
    }

    table.Pager.CompleteStatement();
}

static Outcome PrepareInput(string input, out Statement statement)
{
    statement = null!;

    var parts = input.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    if (parts.Length == 0)
        return Outcome.UnrecognizedKeyword;

    if (parts[0].Equals("select", StringComparison.OrdinalIgnoreCase))
    {
        statement = new(Kind.SELECT, null);
        return Outcome.Success;
    }

    if (parts[0].Equals("insert", StringComparison.OrdinalIgnoreCase))
    {
        if (parts.Length != 4)
            return Outcome.SyntaxError;

        if (!long.TryParse(parts[1], out var id))
            return Outcome.SyntaxError;
        if (id < 0) return Outcome.NegativeId;
        if (id > uint.MaxValue) return Outcome.SyntaxError;

        if (!Row.TryCreate((uint)id, parts[2], parts[3], out Row row))
        {
            return Outcome.SizeError;
        }

        statement = new(Kind.INSERT, row);
        return Outcome.Success;
    }

    if (parts[0].Equals("begin", StringComparison.OrdinalIgnoreCase))
    {
        if (parts.Length != 1)
            return Outcome.SyntaxError;

        statement = new(Kind.BEGIN, null);
        return Outcome.Success;
    }

    if (parts[0].Equals("commit", StringComparison.OrdinalIgnoreCase))
    {
        if (parts.Length != 1)
            return Outcome.SyntaxError;

        statement = new(Kind.COMMIT, null);
        return Outcome.Success;
    }

    if (parts[0].Equals("rollback", StringComparison.OrdinalIgnoreCase))
    {
        if (parts.Length != 1)
            return Outcome.SyntaxError;

        statement = new(Kind.ROLLBACK, null);
        return Outcome.Success;
    }

    return Outcome.UnrecognizedKeyword;
}

static void HandleMetaInput(string input, Table table)
{
    var command = input[1..];

    switch (command)
    {
        case "exit":
            table.Close();
            Environment.Exit(0);
            break;
        case "constants":
            Constants.Print();
            break;
        case "btree":
            Console.WriteLine("Tree:");
            table.PrintTree(table.RootPageNumber, 0);
            break;
        case "dirty":
            var dirtyPages = table.Pager.GetDirtyPages();
            Console.WriteLine(dirtyPages.Count > 0
                    ? "Dirty pages: " + string.Join(", ", dirtyPages)
                    : "Dirty pages: none");
            break;
        default:
            Console.WriteLine($"Unrecognized command '{input}'.");
            return;
    }

}

static string ReadInput()

{
    var input = Console.ReadLine();

    if (input is null)
    {
        Console.WriteLine("Error reading input");
        Environment.Exit(1);

        return null;
    }

    else return input;
}

static void PrintPrompt()
{
    Console.Write("db > ");
}

enum Kind
{
    SELECT,
    INSERT,
    BEGIN,
    COMMIT,
    ROLLBACK
}

enum Outcome
{
    Success,
    SyntaxError,
    UnrecognizedKeyword,
    SizeError,
    NegativeId
}

enum InsertOutcome
{
    Success,
    Duplicate,
}

enum TransactionOutcome
{
    Success,
    AlreadyOpen,
    NotOpen
}
enum NodeType : byte
{
    Internal,
    Leaf
}

static class Constants
{
    private const int NodeTypeSize = 1;
    private const int IsRootFlagSize = 1;
    private const int ParentPointerSize = 4;
    private const int CellCountSize = 4;
    private const int KeySize = 4;
    private const int InternalNodeNumKeysSize = 4;
    private const int InternalNodeChildPointerSize = 4;
    private const int LeafNodeNextLeafSize = 4;

    public const int COMMON_NODE_HEADER_SIZE = NodeTypeSize + IsRootFlagSize + ParentPointerSize;
    public const int NODE_TYPE_OFFSET = 0;
    public const int IS_ROOT_OFFSET = NODE_TYPE_OFFSET + NodeTypeSize;
    public const int PARENT_POINTER_OFFSET = IS_ROOT_OFFSET + IsRootFlagSize;
    public const int INVALID_PAGE_NUMBER = -1;

    public const int LEAF_NODE_NEXT_LEAF_SIZE = LeafNodeNextLeafSize;
    public const int LEAF_NODE_NEXT_LEAF_OFFSET = LEAF_NODE_CELL_COUNT_OFFSET + CellCountSize;
    public const int INTERNAL_NODE_NUM_KEYS_SIZE = InternalNodeNumKeysSize;
    public const int INTERNAL_NODE_NUM_KEYS_OFFSET = COMMON_NODE_HEADER_SIZE;
    public const int INTERNAL_NODE_RIGHT_CHILD_SIZE = InternalNodeChildPointerSize;
    public const int INTERNAL_NODE_RIGHT_CHILD_OFFSET = COMMON_NODE_HEADER_SIZE + INTERNAL_NODE_NUM_KEYS_SIZE;
    public const int INTERNAL_NODE_HEADER_SIZE = COMMON_NODE_HEADER_SIZE + INTERNAL_NODE_NUM_KEYS_SIZE + INTERNAL_NODE_RIGHT_CHILD_SIZE;
    public const int INTERNAL_NODE_CELL_CHILD_SIZE = InternalNodeChildPointerSize;
    public const int INTERNAL_NODE_KEY_OFFSET = INTERNAL_NODE_CELL_CHILD_SIZE;
    public const int INTERNAL_NODE_CELL_SIZE = INTERNAL_NODE_CELL_CHILD_SIZE + KeySize;
    public const int INTERNAL_NODE_MAX_CELLS = 3;
    public const int INTERNAL_NODE_CAPACITY = (PAGE_SIZE - INTERNAL_NODE_HEADER_SIZE) / INTERNAL_NODE_CELL_SIZE;

    public const int LEAF_NODE_CELL_COUNT_OFFSET = COMMON_NODE_HEADER_SIZE;
    public const int LEAF_NODE_VALUE_OFFSET = KeySize;
    public const int LEAF_NODE_HEADER_SIZE = COMMON_NODE_HEADER_SIZE + CellCountSize + LEAF_NODE_NEXT_LEAF_SIZE;
    public const int LEAF_NODE_CELL_SIZE = KeySize + Row.ROW_SIZE;
    public const int LEAF_NODE_SPACE_FOR_CELLS = PAGE_SIZE - LEAF_NODE_HEADER_SIZE;
    public const int LEAF_NODE_MAX_CELLS = LEAF_NODE_SPACE_FOR_CELLS / LEAF_NODE_CELL_SIZE;
    public const int LEAF_NODE_RIGHT_SPLIT_COUNT = (LEAF_NODE_MAX_CELLS + 1) / 2;
    public const int LEAF_NODE_LEFT_SPLIT_COUNT = LEAF_NODE_MAX_CELLS + 1 - LEAF_NODE_RIGHT_SPLIT_COUNT;

    public const int MAX_PAGES = 100;
    public const int PAGE_SIZE = 4096;

    internal static void Print()
    {
        Console.WriteLine("Constants:");
        Console.WriteLine($"{nameof(Row.ROW_SIZE)}: {Row.ROW_SIZE}");
        Console.WriteLine($"{nameof(COMMON_NODE_HEADER_SIZE)}: {COMMON_NODE_HEADER_SIZE}");
        Console.WriteLine($"{nameof(LEAF_NODE_HEADER_SIZE)}: {LEAF_NODE_HEADER_SIZE}");
        Console.WriteLine($"{nameof(LEAF_NODE_CELL_SIZE)}: {LEAF_NODE_CELL_SIZE}");
        Console.WriteLine($"{nameof(LEAF_NODE_SPACE_FOR_CELLS)}: {LEAF_NODE_SPACE_FOR_CELLS}");
        Console.WriteLine($"{nameof(LEAF_NODE_MAX_CELLS)}: {LEAF_NODE_MAX_CELLS}");
        Console.WriteLine($"{nameof(LEAF_NODE_RIGHT_SPLIT_COUNT)}: {LEAF_NODE_RIGHT_SPLIT_COUNT}");
        Console.WriteLine($"{nameof(LEAF_NODE_LEFT_SPLIT_COUNT)}: {LEAF_NODE_LEFT_SPLIT_COUNT}");
        Console.WriteLine($"{nameof(INTERNAL_NODE_HEADER_SIZE)}: {INTERNAL_NODE_HEADER_SIZE}");
        Console.WriteLine($"{nameof(INTERNAL_NODE_CELL_SIZE)}: {INTERNAL_NODE_CELL_SIZE}");
        Console.WriteLine($"{nameof(INTERNAL_NODE_MAX_CELLS)}: {INTERNAL_NODE_MAX_CELLS}");
        Console.WriteLine($"{nameof(INTERNAL_NODE_CAPACITY)}: {INTERNAL_NODE_CAPACITY}");
    }
}

record Statement(Kind Kind, Row? Row);

class Row
{
    const int ID_SIZE = 4;
    const int USERNAME_SIZE = 32;
    const int EMAIL_SIZE = 255;

    const int ID_OFFSET = 0;
    const int USERNAME_OFFSET = ID_OFFSET + ID_SIZE;
    const int EMAIL_OFFSET = USERNAME_OFFSET + USERNAME_SIZE;
    public const int ROW_SIZE = ID_SIZE + USERNAME_SIZE + EMAIL_SIZE;

    public uint Id { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }

    private Row(uint id, string userName, string email)
    {
        Id = id;
        UserName = userName;
        Email = email;
    }

    public void Serialize(byte[] buffer, int start)
    {
        Array.Clear(buffer, start, ROW_SIZE);

        var idBytes = BitConverter.GetBytes(Id);
        Array.Copy(idBytes, 0, buffer, start, idBytes.Length);

        var userNameBytes = Encoding.UTF8.GetBytes(UserName);
        Array.Copy(userNameBytes, 0, buffer, start + USERNAME_OFFSET, userNameBytes.Length);

        var emailBytes = Encoding.UTF8.GetBytes(Email);
        Array.Copy(emailBytes, 0, buffer, start + EMAIL_OFFSET, emailBytes.Length);
    }

    public static Row Deserialize(byte[] buffer, int start)
    {
        var id = BitConverter.ToUInt32(buffer.AsSpan(start + ID_OFFSET, ID_SIZE));

        var nameSlot = buffer.AsSpan(start + USERNAME_OFFSET, USERNAME_SIZE);
        var nameZero = nameSlot.IndexOf((byte)0);
        var nameLength = nameZero == -1 ? USERNAME_SIZE : nameZero;
        var userName = Encoding.UTF8.GetString(nameSlot[..nameLength]);

        var emailSlot = buffer.AsSpan(start + EMAIL_OFFSET, EMAIL_SIZE);
        var emailZero = emailSlot.IndexOf((byte)0);
        var emailLength = emailZero == -1 ? EMAIL_SIZE : emailZero;
        var email = Encoding.UTF8.GetString(emailSlot[..emailLength]);

        var row = new Row(id, userName, email);

        return row;
    }

    public static bool TryCreate(uint id, string userName, string email, out Row row)
    {
        row = null!;

        if (Encoding.UTF8.GetByteCount(userName) > USERNAME_SIZE)
            return false;
        if (Encoding.UTF8.GetByteCount(email) > EMAIL_SIZE)
            return false;

        row = new Row(id, userName, email);

        return true;
    }

    public void PrintRow()
    {
        Console.WriteLine($"({Id}, {UserName}, {Email})");
    }
}

class Table
{
    public Pager Pager { get; }
    public int RootPageNumber { get; } = 0;

    public Table(Pager pager)
    {
        Pager = pager;
        if (pager.PageCount == 0)
        {
            var page = pager.RetrievePage(RootPageNumber);
            var root = new LeafNode(page);
            root.Initialize();

            root.IsRoot = true;

            Pager.CompleteStatement();
        }
    }

    public InsertOutcome Insert(Row row)
    {
        var cursor = Cursor.CreateAtKey(this, row.Id);
        var leaf = cursor.Leaf;

        var cellNumber = cursor.CellNumber;
        if (cellNumber < leaf.CellCount && leaf.GetKey((int)cellNumber) == row.Id)
            return InsertOutcome.Duplicate;

        if (leaf.CellCount >= Constants.LEAF_NODE_MAX_CELLS)
        {
            SplitAndInsert(cursor, row);
            return InsertOutcome.Success;
        }

        leaf.MakeRoomAt((int)cellNumber);
        leaf.SetKeyForCell((int)cellNumber, row.Id);
        leaf.StoreRow(row, (int)cellNumber);

        leaf.CellCount++;

        return InsertOutcome.Success;
    }

    public void Select()
    {
        var cursor = Cursor.CreateAtStart(this);
        while (!cursor.EndOfTable)
        {
            var row = cursor.GetValue();
            row.PrintRow();
            cursor.Advance();
        }
    }

    public void Close()
    {
        for (int i = 0; i < Pager.PageCount; i++)
        {
            if (!Pager.IsPageLoaded(i))
                continue;
            Pager.Flush(i);
        }

        Pager.FileStream.Dispose();
    }

    public void SplitAndInsert(Cursor cursor, Row row)
    {
        var oldLeaf = cursor.Leaf;
        var oldMax = oldLeaf.MaxKey;
        var newPageNumber = Pager.GetUnusedPageNumber();

        var page = Pager.RetrievePage(newPageNumber);
        var newLeaf = new LeafNode(page);

        newLeaf.Initialize();
        newLeaf.NextLeaf = oldLeaf.NextLeaf;
        oldLeaf.NextLeaf = newPageNumber;
        newLeaf.Parent = oldLeaf.Parent;

        int insertCell = (int)cursor.CellNumber;

        LeafNode destination;
        int cellInDestination;
        for (int i = Constants.LEAF_NODE_MAX_CELLS; i >= 0; i--)
        {

            if (i >= Constants.LEAF_NODE_LEFT_SPLIT_COUNT)
                destination = newLeaf;
            else destination = oldLeaf;

            cellInDestination = i % Constants.LEAF_NODE_LEFT_SPLIT_COUNT;

            if (i == insertCell)
            {
                destination.SetKeyForCell(cellInDestination, row.Id);
                destination.StoreRow(row, cellInDestination);
            }
            else if (i > insertCell)
            {
                oldLeaf.CopyCellTo(i - 1, destination, cellInDestination);
            }
            else
            {
                oldLeaf.CopyCellTo(i, destination, cellInDestination);
            }
        }

        oldLeaf.CellCount = Constants.LEAF_NODE_LEFT_SPLIT_COUNT;
        newLeaf.CellCount = Constants.LEAF_NODE_RIGHT_SPLIT_COUNT;

        if (oldLeaf.IsRoot)
            CreateNewRoot(newPageNumber);
        else
        {
            var parentPageNumber = oldLeaf.Parent;
            var newMax = oldLeaf.MaxKey;
            var parent = new InternalNode(Pager.RetrievePage(parentPageNumber));

            parent.UpdateKey(oldMax, newMax);
            InternalNodeInsert(parentPageNumber, newPageNumber);
        }
    }

    public void CreateNewRoot(int rightChildPageNumber)
    {
        var rootPage = Pager.RetrievePage(RootPageNumber);

        var rightChildPage = Pager.RetrievePage(rightChildPageNumber);
        var rootIsInternal = Node.GetNodeType(rootPage) == NodeType.Internal;

        if (rootIsInternal)
            new InternalNode(rightChildPage).Initialize();

        var leftChildPageNumber = Pager.GetUnusedPageNumber();
        var leftChildPage = Pager.RetrievePage(leftChildPageNumber);

        leftChildPage.MarkDirty();
        Array.Copy(rootPage.Data, leftChildPage.Data, Constants.PAGE_SIZE);

        if (rootIsInternal)
        {
            var leftNode = new InternalNode(leftChildPage);

            for (int i = 0; i < leftNode.NumKeys; i++)
            {
                var childPageNumber = leftNode.GetChild(i);
                Node.SetParent(Pager.RetrievePage(childPageNumber), leftChildPageNumber);
            }

            Node.SetParent(Pager.RetrievePage(leftNode.RightChild), leftChildPageNumber);
        }

        Node.SetIsRoot(leftChildPage, false);
        Node.SetParent(leftChildPage, RootPageNumber);
        Node.SetParent(rightChildPage, RootPageNumber);

        var root = new InternalNode(rootPage);
        root.Initialize();
        root.IsRoot = true;
        root.NumKeys = 1;
        root.SetChild(0, leftChildPageNumber);
        root.SetKeyForCell(0, GetNodeMaxKey(leftChildPage));
        root.RightChild = rightChildPageNumber;
    }

    public void InternalNodeInsert(int parentPageNumber, int childPageNumber)
    {
        var parent = new InternalNode(Pager.RetrievePage(parentPageNumber));
        var child = Pager.RetrievePage(childPageNumber);
        var childMaxKey = GetNodeMaxKey(child);
        var index = parent.FindChildNumber(childMaxKey);
        var originalNumKeys = parent.NumKeys;

        if (originalNumKeys >= Constants.INTERNAL_NODE_MAX_CELLS)
        {
            InternalNodeSplitAndInsert(parentPageNumber, childPageNumber);
            return;
        }

        var rightChildPageNumber = parent.RightChild;

        if (rightChildPageNumber == Constants.INVALID_PAGE_NUMBER)
        {
            parent.RightChild = childPageNumber;
            return;
        }

        parent.NumKeys = originalNumKeys + 1;
        var rightChild = Pager.RetrievePage(rightChildPageNumber);
        var rightChildMaxKey = GetNodeMaxKey(rightChild);

        if (childMaxKey > rightChildMaxKey)
        {
            parent.SetChild((int)originalNumKeys, rightChildPageNumber);
            parent.SetKeyForCell((int)originalNumKeys, rightChildMaxKey);
            parent.RightChild = childPageNumber;
        }
        else
        {
            for (int i = (int)originalNumKeys; i > index; i--)
                parent.CopyCell(i - 1, i);

            parent.SetChild(index, childPageNumber);
            parent.SetKeyForCell(index, childMaxKey);
        }
    }

    public void InternalNodeSplitAndInsert(int parentPageNumber, int childPageNumber)
    {
        var oldPageNumber = parentPageNumber;
        var oldPage = Pager.RetrievePage(oldPageNumber);
        var oldNode = new InternalNode(oldPage);
        var oldMax = GetNodeMaxKey(oldPage);

        var childPage = Pager.RetrievePage(childPageNumber);
        var childMax = GetNodeMaxKey(childPage);

        var newPageNumber = Pager.GetUnusedPageNumber();

        var splittingRoot = oldNode.IsRoot;

        InternalNode parent;

        if (splittingRoot)
        {
            CreateNewRoot(newPageNumber);
            parent = new InternalNode(Pager.RetrievePage(RootPageNumber));
            oldPageNumber = parent.GetChild(0);
            oldNode = new InternalNode(Pager.RetrievePage(oldPageNumber));
        }
        else
        {
            parent = new InternalNode(Pager.RetrievePage(oldNode.Parent));
            new InternalNode(Pager.RetrievePage(newPageNumber)).Initialize();
        }

        var currentPageNumber = oldNode.RightChild;
        InternalNodeInsert(newPageNumber, currentPageNumber);
        Node.SetParent(Pager.RetrievePage(currentPageNumber), newPageNumber);
        oldNode.RightChild = Constants.INVALID_PAGE_NUMBER;

        for (int i = Constants.INTERNAL_NODE_MAX_CELLS - 1; i > Constants.INTERNAL_NODE_MAX_CELLS / 2; i--)
        {
            currentPageNumber = oldNode.GetChild(i);
            InternalNodeInsert(newPageNumber, currentPageNumber);
            Node.SetParent(Pager.RetrievePage(currentPageNumber), newPageNumber);
            oldNode.NumKeys--;
        }

        oldNode.RightChild = oldNode.GetChild((int)oldNode.NumKeys - 1);
        oldNode.NumKeys--;

        var maxAfterSplit = GetNodeMaxKey(Pager.RetrievePage(oldPageNumber));
        var destinationPageNumber = childMax < maxAfterSplit ? oldPageNumber : newPageNumber;

        InternalNodeInsert(destinationPageNumber, childPageNumber);
        Node.SetParent(childPage, destinationPageNumber);

        var newMax = GetNodeMaxKey(Pager.RetrievePage(oldPageNumber));
        parent.UpdateKey(oldMax, newMax);

        if (!splittingRoot)
        {
            var upperPageNumber = oldNode.Parent;
            Node.SetParent(Pager.RetrievePage(newPageNumber), upperPageNumber);
            InternalNodeInsert(upperPageNumber, newPageNumber);
        }
    }

    public uint GetNodeMaxKey(Page page)
    {
        var nodeType = Node.GetNodeType(page);
        if (nodeType is NodeType.Leaf)
            return new LeafNode(page).MaxKey;

        var rightChildPageNumber = new InternalNode(page).RightChild;
        return GetNodeMaxKey(Pager.RetrievePage(rightChildPageNumber));
    }

    public void PrintTree(int pageNumber, int indentationLevel)
    {
        var page = Pager.RetrievePage(pageNumber);

        var nodeType = Node.GetNodeType(page);

        switch (nodeType)
        {
            case NodeType.Internal:
                var node = new InternalNode(page);
                Indent(indentationLevel);
                Console.WriteLine($"- internal (size {node.NumKeys})");
                if (node.NumKeys > 0)
                {
                    for (int i = 0; i < node.NumKeys; i++)
                    {
                        PrintTree(node.GetChild(i), indentationLevel + 1);
                        Indent(indentationLevel + 1);
                        Console.WriteLine($"- key {node.GetKey(i)}");
                    }
                    PrintTree(node.RightChild, indentationLevel + 1);
                }
                break;
            case NodeType.Leaf:
                var leaf = new LeafNode(page);
                Indent(indentationLevel);
                Console.WriteLine($"- leaf (size {leaf.CellCount})");
                for (int i = 0; i < leaf.CellCount; i++)
                {
                    Indent(indentationLevel + 1);
                    Console.WriteLine($"- {leaf.GetKey(i)}");
                }
                break;
            default:
                Console.WriteLine("Unexpected node type.");
                Environment.Exit(1);
                break;
        }
    }

    private static void Indent(int level)
    {
        for (int i = 0; i < level; i++)
            Console.Write("  ");
    }
}

class Cursor(Table table, int pageNumber, uint initialCellNumber)
{
    public Table Table { get; } = table;
    public LeafNode Leaf { get; private set; } = new(table.Pager.RetrievePage(pageNumber));
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
                Leaf = new LeafNode(Table.Pager.RetrievePage(PageNumber));
                CellNumber = 0;
            }
        }
    }

    private static Cursor FindInPage(Table table, int pageNumber, uint key)
    {
        var page = table.Pager.RetrievePage(pageNumber);
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
