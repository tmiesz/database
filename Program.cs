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

record Statement(Kind Kind, Row? Row);
