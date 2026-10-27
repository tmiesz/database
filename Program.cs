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

    if (PrepareInput(input) is not Kind kind)
    {
        Console.WriteLine($"Unrecognized keyword at start of '{input}'.");
        return;
    }

    Execute(kind);
    Console.WriteLine("Executed.");
}

static void Execute(Kind kind) =>
    Console.WriteLine(kind switch
    {
        Kind.INSERT => "This is where we would do an insert.",
        Kind.SELECT => "This is where we would do a select.",
        _ => "Unrecognized command."
    });

static Kind? PrepareInput(string input)
{
    var keyword = input.Split(' ', 2)[0];

    if (keyword.Equals("select", StringComparison.OrdinalIgnoreCase))
        return Kind.SELECT;
    if (keyword.Equals("insert", StringComparison.OrdinalIgnoreCase))
        return Kind.INSERT;

    return null;
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
