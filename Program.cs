while (true)
{
    PrintPrompt();
    var input = ReadInput();

    if (input is ".exit")
        return 0;

    else Console.WriteLine($"Unrecognized command '{input}'.");
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
