using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace database.tests;

public class DatabaseTests
{
    static string ProjetcPath([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "database.csproj"));

    static async Task<(string[] Lines, int ExitCode, string Error)> RunScript(params string[] commands)
    {
        var info = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { "run", "--project", ProjetcPath() },
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(info);
        var errorTask = process!.StandardError.ReadToEndAsync();

        foreach (var command in commands)
            await process.StandardInput.WriteLineAsync(command);
        process.StandardInput.Close();

        var output = await process.StandardOutput.ReadToEndAsync();
        await process.WaitForExitAsync();

        return (output.Split('\n'), process.ExitCode, await errorTask);
    }

    [Fact]
    public async Task InsertsAndRetrievesRow()
    {
        var (lines, exitCode, error) = await RunScript(
                "insert 1 user1 person1@example.com",
                "select",
                ".exit");

        Assert.True(exitCode == 0, error);
        Assert.Equal(new[]{
                "db > Executed.",
                "db > (1, user1, person1@example.com)",
                "Executed.",
                "db > "
                }, lines);
    }
}
