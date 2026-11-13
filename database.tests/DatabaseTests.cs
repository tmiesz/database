using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace database.tests;

public class DatabaseTests
{
    static string ProjectPath([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "database.csproj"));

    private static string GenerateDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");

    static async Task<(string[] Lines, int ExitCode, string Error)> RunScript(string databasePath, params string[] commands)
    {
        var info = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { "run", "--project", ProjectPath(), databasePath },
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
        var db = GenerateDatabasePath();
        var (lines, exitCode, error) = await RunScript(
                db,
                "insert 1 user1 person1@example.com",
                "select",
                ".exit");

        Assert.True(exitCode == 0, error);
        Assert.Equal("db > (1, user1, person1@example.com)", lines[^3]);
    }

    [Fact]
    public async Task DuplicateKeyFails()
    {
        var db = GenerateDatabasePath();
        var (lines, exitCode, error) = await RunScript(
                db,
                "insert 1 user1 person1@example.com",
                "insert 1 user1 person1@example.com",
                "select",
                ".exit");

        Assert.True(exitCode == 0, error);
        Assert.Equal("db > Error: Duplicate key.", lines[^4]);
    }

    [Fact]
    public async Task SortedOrder()
    {
        var db = GenerateDatabasePath();
        var (lines, exitCode, error) = await RunScript(
                db,
                "insert 3 user3 person3@example.com",
                "insert 1 user1 person1@example.com",
                "insert 2 user2 person2@example.com",
                "select",
                ".exit");

        Assert.True(exitCode == 0, error);
        Assert.Equal("(3, user3, person3@example.com)", lines[^3]);
        Assert.Equal("(2, user2, person2@example.com)", lines[^4]);
        Assert.Equal("db > (1, user1, person1@example.com)", lines[^5]);
    }

    [Fact]
    public async Task InsertsMaximumLength()
    {
        var db = GenerateDatabasePath();
        var maxName = new string('a', 32);
        var maxMail = new string('a', 255);
        var (lines, exitCode, error) = await RunScript(
            db,
            $"insert 1 {maxName} {maxMail}",
            "select",
            ".exit");

        Assert.True(exitCode == 0, error);
        Assert.Equal($"db > (1, {maxName}, {maxMail})", lines[^3]);
    }

    [Fact]
    public async Task InsertsOverMaximumLength()
    {
        var db = GenerateDatabasePath();
        var maxName = new string('a', 33);
        var maxMail = new string('a', 256);
        var (lines, exitCode, error) = await RunScript(
            db,
            $"insert 1 {maxName} {maxMail}",
            "select",
            ".exit");

        Assert.True(exitCode == 0, error);
        Assert.Equal("db > String is too long.", lines[^3]);
    }

    [Fact]
    public async Task InsertsNegativeId()
    {
        var db = GenerateDatabasePath();
        var (lines, exitCode, error) = await RunScript(
            db,
            "insert -1 user1 person1@example.com",
            "select",
            ".exit");

        Assert.True(exitCode == 0, error);
        Assert.Equal("db > ID must be positive.", lines[^3]);
    }

    [Fact]
    public async Task DataPersists()
    {
        var db = GenerateDatabasePath();
        var name = "user1";
        var email = "person1@example.com";
        var (lines, _, _) = await RunScript(
            db,
            $"insert 1 {name} {email}",
            ".exit");

        var (lines2, _, _) = await RunScript(
            db,
            "select",
            ".exit");

        Assert.Equal("db > Executed.", lines[^2]);
        Assert.Equal($"db > (1, {name}, {email})", lines2[^3]);
    }

    [Fact]
    public async Task PrintsTreeAfterSplit()
    {
        var db = GenerateDatabasePath();

        var (lines, exitCode, error) = await RunScript(
            db,
            [.. Enumerable.Range(1, 14).Select(i => $"insert {i} user{i} person{i}@example.com"),
            ".btree",
             ".exit"]);

        Assert.True(exitCode == 0, error);

        string[] expected =
        [
            "db > Tree:",
            "- internal (size 1)",
            "  - leaf (size 7)",
            .. Enumerable.Range(1, 7).Select(i => $"    - {i}"),
            "  - key 7",
            "  - leaf (size 7)",
            .. Enumerable.Range(8, 7).Select(i => $"    - {i}"),
        ];

        Assert.Equal(expected, lines[^20..^1]);
    }
}
