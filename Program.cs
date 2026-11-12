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
            var result = table.Insert(statement.Row!);

            if (result == InsertOutcome.Error)
                Console.WriteLine("Error: Table full.");
            if (result == InsertOutcome.Duplicate)
                Console.WriteLine("Error: Duplicate key.");
            else Console.WriteLine("Executed.");

            break;

        case Kind.SELECT:
            table.Select();
            Console.WriteLine("Executed.");
            break;

        default:
            Console.WriteLine("Unrecognized command.");
            break;
    }
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

class LeafNode(byte[] page)
{
    private readonly byte[] page = page;
    private static int CellStart(int cell) => Constants.LEAF_NODE_HEADER_SIZE + (cell * Constants.LEAF_NODE_CELL_SIZE);
    private static int RowStart(int cell) => CellStart(cell) + Constants.LEAF_NODE_VALUE_OFFSET;
    public uint MaxKey => GetKey((int)CellCount - 1);

    public bool IsRoot
    {
        get => page[Constants.IS_ROOT_OFFSET] != 0;
        set => page[Constants.IS_ROOT_OFFSET] = value ? (byte)1 : (byte)0;
    }
    public NodeType NodeType
    {
        get => (NodeType)page[Constants.NODE_TYPE_OFFSET];
        set => page[Constants.NODE_TYPE_OFFSET] = (byte)value;
    }

    public uint CellCount
    {
        get => BitConverter.ToUInt32(page, Constants.LEAF_NODE_CELL_COUNT_OFFSET);
        set
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, page, Constants.LEAF_NODE_CELL_COUNT_OFFSET, bytes.Length);
        }
    }

    public uint GetKey(int cell) =>
        BitConverter.ToUInt32(page, CellStart(cell));

    public void SetKeyForCell(int cell, uint key)
    {
        var bytes = BitConverter.GetBytes(key);
        Array.Copy(bytes, 0, page, CellStart(cell), bytes.Length);
    }

    public Row GetRow(int cell)
    {
        return Row.Deserialize(page, RowStart(cell));
    }

    public void StoreRow(Row row, int cell)
    {
        row.Serialize(page, RowStart(cell));
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
            Array.Copy(page, CellStart(i), page, CellStart(i + 1), Constants.LEAF_NODE_CELL_SIZE);
        }
    }

    public void Initialize()
    {
        NodeType = NodeType.Leaf;
        IsRoot = false;
        CellCount = 0;
    }

    public void CopyCellTo(int fromCell, LeafNode destination, int toCell)
    {
        Array.Copy(page, CellStart(fromCell), destination.page, CellStart(toCell), Constants.LEAF_NODE_CELL_SIZE);
    }
}

class InternalNode(byte[] page)
{
    private static int CellStart(int cell) => Constants.INTERNAL_NODE_HEADER_SIZE + (cell * Constants.INTERNAL_NODE_CELL_SIZE);

    public bool IsRoot
    {
        get => page[Constants.IS_ROOT_OFFSET] != 0;
        set => page[Constants.IS_ROOT_OFFSET] = value ? (byte)1 : (byte)0;
    }
    public NodeType NodeType
    {
        get => (NodeType)page[Constants.NODE_TYPE_OFFSET];
        set => page[Constants.NODE_TYPE_OFFSET] = (byte)value;
    }
    public uint NumKeys
    {
        get => BitConverter.ToUInt32(page, Constants.INTERNAL_NODE_NUM_KEYS_OFFSET);
        set
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, page, Constants.INTERNAL_NODE_NUM_KEYS_OFFSET, bytes.Length);
        }
    }
    public int RightChild
    {
        get => BitConverter.ToInt32(page, Constants.INTERNAL_NODE_RIGHT_CHILD_OFFSET);
        set
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Copy(bytes, 0, page, Constants.INTERNAL_NODE_RIGHT_CHILD_OFFSET, bytes.Length);
        }
    }

    public void Initialize()
    {
        NodeType = NodeType.Internal;
        IsRoot = false;
        NumKeys = 0;
    }

    public uint GetKey(int cell) =>
        BitConverter.ToUInt32(page, CellStart(cell) + Constants.INTERNAL_NODE_KEY_OFFSET);

    public void SetKeyForCell(int cell, uint key)
    {
        var bytes = BitConverter.GetBytes(key);
        Array.Copy(bytes, 0, page, CellStart(cell) + Constants.INTERNAL_NODE_KEY_OFFSET, bytes.Length);
    }

    public int GetChild(int childNumber)
    {
        if (childNumber > NumKeys)
        {
            Console.WriteLine($"Tried to access child number {childNumber} > num keys {NumKeys}");
            Environment.Exit(1);
            return 0;
        }
        if (childNumber == NumKeys)
        {
            return RightChild;
        }
        return BitConverter.ToInt32(page, CellStart(childNumber));
    }

    public void SetChild(int childNumber, int pageNumber)
    {
        if (childNumber > NumKeys)
        {
            Console.WriteLine($"Tried to access child number {childNumber} > num keys {NumKeys}");
            Environment.Exit(1);
        }
        else if (childNumber == NumKeys)
        {
            RightChild = pageNumber;
        }
        else
        {
            var bytes = BitConverter.GetBytes(pageNumber);
            Array.Copy(bytes, 0, page, CellStart(childNumber), bytes.Length);
        }
    }
}

enum Kind
{
    SELECT,
    INSERT
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
    Error
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

    public const int COMMON_NODE_HEADER_SIZE = NodeTypeSize + IsRootFlagSize + ParentPointerSize;
    public const int NODE_TYPE_OFFSET = 0;
    public const int IS_ROOT_OFFSET = NODE_TYPE_OFFSET + NodeTypeSize;

    public const int INTERNAL_NODE_NUM_KEYS_SIZE = InternalNodeNumKeysSize;
    public const int INTERNAL_NODE_NUM_KEYS_OFFSET = COMMON_NODE_HEADER_SIZE;
    public const int INTERNAL_NODE_RIGHT_CHILD_SIZE = InternalNodeChildPointerSize;
    public const int INTERNAL_NODE_RIGHT_CHILD_OFFSET = COMMON_NODE_HEADER_SIZE + INTERNAL_NODE_NUM_KEYS_SIZE;
    public const int INTERNAL_NODE_HEADER_SIZE = COMMON_NODE_HEADER_SIZE + INTERNAL_NODE_NUM_KEYS_SIZE + INTERNAL_NODE_RIGHT_CHILD_SIZE;
    public const int INTERNAL_NODE_CELL_CHILD_SIZE = InternalNodeChildPointerSize;
    public const int INTERNAL_NODE_KEY_OFFSET = INTERNAL_NODE_CELL_CHILD_SIZE;
    public const int INTERNAL_NODE_CELL_SIZE = INTERNAL_NODE_CELL_CHILD_SIZE + KeySize;
    public const int INTERNAL_NODE_MAX_CELLS = (PAGE_SIZE - INTERNAL_NODE_HEADER_SIZE) / INTERNAL_NODE_CELL_SIZE;

    public const int LEAF_NODE_CELL_COUNT_OFFSET = COMMON_NODE_HEADER_SIZE;
    public const int LEAF_NODE_VALUE_OFFSET = KeySize;
    public const int LEAF_NODE_HEADER_SIZE = COMMON_NODE_HEADER_SIZE + CellCountSize;
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
        var newPageNumber = Pager.GetUnusedPageNumber();

        var page = Pager.RetrievePage(newPageNumber);
        var newLeaf = new LeafNode(page);

        newLeaf.Initialize();

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
            Console.WriteLine("TODO: Need to implement updating parent after split");
            Environment.Exit(1);
        }
    }

    public void CreateNewRoot(int rightChildPageNumber)
    {
        var rootPage = Pager.RetrievePage(RootPageNumber);
        var leftChildPageNumber = Pager.GetUnusedPageNumber();
        var leftChildPage = Pager.RetrievePage(leftChildPageNumber);

        Array.Copy(rootPage, leftChildPage, Constants.PAGE_SIZE);
        var leftChild = new LeafNode(leftChildPage)
        {
            IsRoot = false
        };
        var root = new InternalNode(rootPage);
        root.Initialize();
        root.IsRoot = true;
        root.NumKeys = 1;
        root.SetChild(0, leftChildPageNumber);
        root.SetKeyForCell(0, leftChild.MaxKey);
        root.RightChild = rightChildPageNumber;
    }

    public void PrintTree(int pageNumber, int indentationLevel)
    {
        var page = Pager.RetrievePage(pageNumber);

        var nodeType = new LeafNode(page).NodeType;

        switch (nodeType)
        {
            case NodeType.Internal:
                var node = new InternalNode(page);
                Indent(indentationLevel);
                Console.WriteLine($"- internal (size {node.NumKeys})");
                for (int i = 0; i < node.NumKeys; i++)
                {
                    PrintTree(node.GetChild(i), indentationLevel + 1);
                    Indent(indentationLevel + 1);
                    Console.WriteLine($"- key {node.GetKey(i)}");
                }
                PrintTree(node.RightChild, indentationLevel + 1);
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

class Pager
{
    public string FileName { get; }
    public FileStream FileStream { get; }
    private byte[][] Pages { get; } = new byte[Constants.MAX_PAGES][];
    public long Length { get; }
    public int PageCount { get; private set; }

    public Pager(string fileName)
    {
        FileName = fileName;
        FileStream = new FileStream(
                FileName,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite
                );
        Length = FileStream.Length;
        if (Length % Constants.PAGE_SIZE != 0)
        {
            Console.WriteLine("Db file is not a whole number of pages. Corrupt file.");
            Environment.Exit(1);
        }
        PageCount = (int)(Length / Constants.PAGE_SIZE);
    }

    public byte[] RetrievePage(int pageNumber)
    {
        if (pageNumber >= Constants.MAX_PAGES)
        {
            Console.WriteLine($"Tried to fetch page number out of bonds. {pageNumber} > {Constants.MAX_PAGES - 1}");
            Environment.Exit(1);
        }

        byte[] buffer;

        if (Pages[pageNumber] is null)
        {
            buffer = new byte[Constants.PAGE_SIZE];

            var offset = pageNumber * Constants.PAGE_SIZE;
            FileStream.Seek(offset, SeekOrigin.Begin);

            _ = FileStream.Read(buffer, 0, Constants.PAGE_SIZE);

            Pages[pageNumber] = buffer;

            if (pageNumber >= PageCount)
                PageCount = pageNumber + 1;

            return buffer;
        }

        return Pages[pageNumber];
    }

    public bool IsPageLoaded(int pageNumber)
    {
        if (Pages[pageNumber] != null)
            return true;

        else return false;
    }

    public void Flush(int pageNumber)
    {
        if (Pages[pageNumber] is null)
        {
            Console.WriteLine($"Page {pageNumber} is not loaded");
            Environment.Exit(1);
        }
        var offset = pageNumber * Constants.PAGE_SIZE;
        FileStream.Seek(offset, SeekOrigin.Begin);

        FileStream.Write(Pages[pageNumber], 0, Constants.PAGE_SIZE);
        FileStream.Flush();
    }

    public int GetUnusedPageNumber()
    {
        return PageCount;
    }
}

class Cursor(Table table, int pageNumber, uint initialCellNumber)
{
    public Table Table { get; } = table;
    public LeafNode Leaf { get; } = new(table.Pager.RetrievePage(pageNumber));
    public uint CellNumber { get; private set; } = initialCellNumber;
    public bool EndOfTable => CellNumber >= Leaf.CellCount;

    public static Cursor CreateAtStart(Table table)
    {
        return new Cursor(table, table.RootPageNumber, 0);
    }

    public static Cursor CreateAtKey(Table table, uint key)
    {
        var leaf = new LeafNode(table.Pager.RetrievePage(table.RootPageNumber));
        if (leaf.NodeType != NodeType.Leaf)
        {
            Console.WriteLine("Internal nodes don't exists yet.");
            Environment.Exit(1);
        }

        var cell = leaf.FindCell(key);

        return new Cursor(table, table.RootPageNumber, (uint)cell);
    }

    public Row GetValue() => Leaf.GetRow((int)CellNumber);
    public void Advance() => CellNumber++;
}
