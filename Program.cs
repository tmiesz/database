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

    var command = PrepareInput(input);

    if (command is null)
        Console.WriteLine($"Unrecognized keyword at start of '{input}'.");
}

static Kind? PrepareInput(string input)
{
    if (input.Equals("select", StringComparison.OrdinalIgnoreCase))
        return Kind.SELECT;
    if (input.Equals("insert", StringComparison.OrdinalIgnoreCase))
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
