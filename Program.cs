using System.Text;

var table = new Table();

for (uint i = 1; i < 1410; i++)
{
    Row.TryCreate(i, "test", "test@test.com", out Row r);
    var result = table.Insert(r);

    if (result != InsertOutcome.Success)
        Console.WriteLine($"{i}:!!!");
}

while (true)
{
    PrintPrompt();
    HandleInput(ReadInput());
}

static void HandleInput(string input)
{
    if (input.StartsWith('.'))
    {
        HandleMetaInput(input);
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
            Console.WriteLine("Size is too big");
            return;

    }

    Execute(statement);
    Console.WriteLine("Executed.");
}

static void Execute(Statement statement)
{
    switch (statement.Kind)
    {
        case Kind.INSERT:
            statement.Row!.PrintRow();
            break;

        case Kind.SELECT:
            Console.WriteLine("This is where we would do a select.");
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

        if (!uint.TryParse(parts[1], out var id))
            return Outcome.SyntaxError;

        if (!Row.TryCreate(id, parts[2], parts[3], out Row row))
        {
            return Outcome.SizeError;
        }

        statement = new(Kind.INSERT, row);
        return Outcome.Success;
    }

    return Outcome.UnrecognizedKeyword;
}

static void HandleMetaInput(string input)
{
    var command = input[1..];

    switch (command)
    {
        case "exit":
            Environment.Exit(0);
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
    INSERT
}

enum Outcome
{
    Success,
    SyntaxError,
    UnrecognizedKeyword,
    SizeError
}

enum InsertOutcome
{
    Success,
    Error
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

        TryCreate(id, userName, email, out var row);

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

class Table()
{
    public byte[][] Pages { get; } = new byte[TABLE_MAX_PAGES][];
    public int RowCount { get; private set; } = 0;

    const int PAGE_SIZE = 4096;
    const int ROWS_PER_PAGE = PAGE_SIZE / Row.ROW_SIZE;
    const int TABLE_MAX_PAGES = 100;
    const int TABLE_MAX_ROWS = ROWS_PER_PAGE * TABLE_MAX_PAGES;

    public (byte[] page, int start) GetSlot(int rowNumber)
    {
        var pageNumber = rowNumber / ROWS_PER_PAGE;
        var boxNumber = rowNumber % ROWS_PER_PAGE;
        var start = boxNumber * Row.ROW_SIZE;

        if (Pages[pageNumber] is null)
        {
            Pages[pageNumber] = new byte[PAGE_SIZE];
        }

        return (Pages[pageNumber], start);
    }

    public InsertOutcome Insert(Row row)
    {
        if (RowCount == TABLE_MAX_ROWS)
            return InsertOutcome.Error;

        var (page, start) = GetSlot(RowCount);

        row.Serialize(page, start);
        RowCount++;

        return InsertOutcome.Success;
    }
}
