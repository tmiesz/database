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
