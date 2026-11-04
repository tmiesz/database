using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace database.tests;

public class DatabaseTests
{
    static string ProjectPath([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "database.csproj"));

    static async Task<(string[] Lines, int ExitCode, string Error)> RunScript(params string[] commands)
    {
        var info = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { "run", "--project", ProjectPath() },
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
        Assert.Equal("db > (1, user1, person1@example.com)", lines[^3]);
    }

    [Fact]
    public async Task InsertsOverTableSize()
    {
        var commands = Enumerable.Range(1, 1401)
            .Select(i => $"insert {i} user{i} person{i}@example.com")
            .Append(".exit")
            .ToArray();

        var (lines, exitCode, error) = await RunScript(commands);

        Assert.True(exitCode == 0, error);
        Assert.Equal("db > Error: Table full.", lines[^2]);
    }

    [Fact]
    public async Task InsertsMaximumLength()
    {
        var maxName = new string('a', 32);
        var maxMail = new string('a', 255);
        var (lines, exitCode, error) = await RunScript(
            $"insert 1 {maxName} {maxMail}",
            "select",
            ".exit");

        Assert.True(exitCode == 0, error);
        Assert.Equal($"db > (1, {maxName}, {maxMail})", lines[^3]);
    }

    [Fact]
    public async Task InsertsOverMaximumLength()
    {
        var maxName = new string('a', 33);
        var maxMail = new string('a', 256);
        var (lines, exitCode, error) = await RunScript(
            $"insert 1 {maxName} {maxMail}",
            "select",
            ".exit");

        Assert.True(exitCode == 0, error);
        Assert.Equal("db > String is too long.", lines[^3]);
    }

    [Fact]
    public async Task InsertsNegativeId()
    {
        var (lines, exitCode, error) = await RunScript(
            "insert -1 user1 person1@example.com",
            "select",
            ".exit");

        Assert.True(exitCode == 0, error);
        Assert.Equal("db > ID must be positive.", lines[^3]);
    }
}
