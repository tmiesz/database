while (true)
{
    PrintPrompt();
    HandleInput(ReadInput());
}

void HandleInput(string input)
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
    }

    Execute(statement);
    Console.WriteLine("Executed.");
}

static void PrintRow(Row record)
{
    Console.WriteLine($"({record.Id}, {record.UserName}, {record.Email})");
}

static void Execute(Statement statement)
{
    switch (statement.Kind)
    {
        case Kind.INSERT:
            PrintRow(statement.Row!);
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
    var parts = input.Split(' ', 4);
    statement = null!;

    if (parts[0].Equals("select", StringComparison.OrdinalIgnoreCase))
    {
        statement = new(Kind.SELECT, null);
        return Outcome.Success;
    }

    if (parts[0].Equals("insert", StringComparison.OrdinalIgnoreCase))
    {
        if (parts.Length < 4)
            return Outcome.SyntaxError;

        if (!uint.TryParse(parts[1], out var id))
            return Outcome.SyntaxError;

        statement = new(Kind.INSERT, new Row(id, parts[2], parts[3]));
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
    UnrecognizedKeyword
}

record Statement(Kind Kind, Row? Row);
record Row(uint Id, string UserName, string Email);
