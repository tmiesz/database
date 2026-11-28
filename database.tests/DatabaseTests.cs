using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace database.tests;

public class DatabaseTests
{
    static string ProjectPath([CallerFilePath] string thisFile = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(thisFile)!, "..", "database.csproj"));

    private static string GenerateDatabasePath() =>
        Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.db");

    static Task<(string[] Lines, int ExitCode, string Error)> RunScript(
        string databasePath,
        params string[] commands)
        => RunScriptWithCrash(databasePath, null, commands);

    static async Task<(string[] Lines, int ExitCode, string Error)> RunScriptWithCrash(
        string databasePath,
        string? crashAt,
        params string[] commands)
    {
        var info = new ProcessStartInfo
        {
            FileName = "dotnet",
            ArgumentList = { "run", "--project", ProjectPath(), databasePath },
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        if (crashAt is not null)
            info.Environment["DB_CRASH_AT"] = crashAt;

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

    [Fact]
    public async Task InsertsIntoRightLeafAfterSplit()
    {
        var db = GenerateDatabasePath();

        var (lines, exitCode, error) = await RunScript(
            db,
            [.. Enumerable.Range(1, 15).Select(i => $"insert {i} user{i} person{i}@example.com"),
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
            "  - leaf (size 8)",
            .. Enumerable.Range(8, 8).Select(i => $"    - {i}"),
        ];

        Assert.Equal(expected, lines[^21..^1]);
    }

    [Fact]
    public async Task SelectsPrintsAllRows()
    {
        var db = GenerateDatabasePath();

        var (lines, exitCode, error) = await RunScript(
            db,
            [.. Enumerable.Range(1, 15).Select(i => $"insert {i} user{i} person{i}@example.com"),
            "select",
            ".exit"]);

        Assert.True(exitCode == 0, error);

        string[] expected =
        [
            "db > (1, user1, person1@example.com)",
            .. Enumerable.Range(2, 14).Select(i => $"({i}, user{i}, person{i}@example.com)"),
            "Executed.",
        ];

        Assert.Equal(expected, lines[^17..^1]);
    }

    [Fact]
    public async Task PrintsFourLeafTree()
    {
        var db = GenerateDatabasePath();

        int[] ids = [18, 7, 10, 29, 23, 4, 14, 30, 15, 26, 22, 19, 2, 1, 21, 11, 6, 20, 5, 8, 9, 3, 12, 27, 17, 16, 13, 24, 25, 28];
        var (lines, exitCode, error) = await RunScript(
            db,
            [.. ids.Select(i => $"insert {i} user{i} person{i}@example.com"),
            ".btree",
            ".exit"]);

        Assert.True(exitCode == 0, error);

        string[] expected =
        [
            "db > Tree:",
            "- internal (size 3)",
            "  - leaf (size 7)",
            .. Enumerable.Range(1, 7).Select(i => $"    - {i}"),
            "  - key 7",
            "  - leaf (size 8)",
            .. Enumerable.Range(8, 8).Select(i => $"    - {i}"),
            "  - key 15",
            "  - leaf (size 7)",
            .. Enumerable.Range(16, 7).Select(i => $"    - {i}"),
            "  - key 22",
            "  - leaf (size 8)",
            .. Enumerable.Range(23, 8).Select(i => $"    - {i}"),
        ];

        Assert.Equal(expected, lines[^40..^1]);
    }

    [Fact]
    public async Task PrintsSevenLeafTree()
    {
        var db = GenerateDatabasePath();

        int[] ids =
        [
            58, 56, 8, 54, 77, 7, 25, 71, 13, 22, 53, 51, 59, 32, 36, 79,
            10, 33, 20, 4, 35, 76, 49, 24, 70, 48, 39, 15, 47, 30, 86, 31,
            68, 37, 66, 63, 40, 78, 19, 46, 14, 81, 72, 6, 50, 85, 67, 2,
            55, 69, 5, 65, 52, 1, 29, 9, 43, 75, 21, 82, 12, 18, 60, 44
        ];

        var (lines, exitCode, error) = await RunScript(
            db,
            [.. ids.Select(i => $"insert {i} user{i} person{i}@example.com"),
            ".btree",
            ".exit"]);

        Assert.True(exitCode == 0, error);

        static string[] Leaf(string pad, params int[] keys) =>
            [$"{pad}- leaf (size {keys.Length})", .. keys.Select(k => $"{pad}  - {k}")];

        string[] expected =
        [
            "db > Tree:",
            "- internal (size 1)",
            "  - internal (size 2)",
            .. Leaf("    ", 1, 2, 4, 5, 6, 7, 8),
            "    - key 8",
            .. Leaf("    ", 9, 10, 12, 13, 14, 15, 18, 19, 20, 21, 22),
            "    - key 22",
            .. Leaf("    ", 24, 25, 29, 30, 31, 32, 33, 35),
            "  - key 35",
            "  - internal (size 3)",
            .. Leaf("    ", 36, 37, 39, 40, 43, 44, 46, 47, 48, 49, 50, 51),
            "    - key 51",
            .. Leaf("    ", 52, 53, 54, 55, 56, 58, 59, 60, 63, 65, 66),
            "    - key 66",
            .. Leaf("    ", 67, 68, 69, 70, 71, 72, 75),
            "    - key 75",
            .. Leaf("    ", 76, 77, 78, 79, 81, 82, 85, 86),
        ];

        Assert.Equal(expected, lines[^82..^1]);
    }

    [Fact]
    public async Task RollbackUndoesInsert()
    {
        var db = GenerateDatabasePath();
        var (lines, exitCode, error) = await RunScript(
            db,
            "insert 1 user1 person1@example.com",
            "begin",
            "insert 2 user2 person2@example.com",
            "rollback",
            "select",
            ".exit");

        Assert.True(exitCode == 0, error);

        string[] expected =
        [
            "db > (1, user1, person1@example.com)",
            "Executed.",
            "db > ",
        ];

        Assert.Equal(expected, lines[^3..]);
    }

    [Fact]
    public async Task ExitDropsOpenTransaction()
    {
        var db = GenerateDatabasePath();
        var (_, exitCode, error) = await RunScript(
            db,
            "insert 1 user1 person1@example.com",
            "begin",
            "insert 2 user2 person2@example.com",
            ".exit");

        Assert.True(exitCode == 0, error);

        var (lines, _, _) = await RunScript(db, "select", ".exit");

        string[] expected =
        [
            "db > (1, user1, person1@example.com)",
            "Executed.",
            "db > ",
        ];

        Assert.Equal(expected, lines);
    }

    [Fact]
    public async Task CommitCutShortIsRepairedOnNextStart()
    {
        var db = GenerateDatabasePath();
        var journal = db + ".journal";

        var (_, setupExit, setupError) = await RunScript(
            db,
            [.. Enumerable.Range(1, 13).Select(i => $"insert {i} user{i} person{i}@example.com"),
            ".exit"]);

        var (_, crashExit, crashError) = await RunScriptWithCrash(
            db,
            "after-database-written",
            "begin",
            "insert 14 user14 person14@example.com",
            "commit");

        Assert.True(File.Exists(journal));
        Assert.Equal(5 * 4096L, new FileInfo(db).Length);

        var (lines, exitCode, error) = await RunScript(db, "select", ".exit");

        string[] expected =
        [
            "db > (1, user1, person1@example.com)",
            .. Enumerable.Range(2, 12).Select(i => $"({i}, user{i}, person{i}@example.com)"),
            "Executed.",
        ];

        Assert.Equal(expected, lines[..^1]);
        Assert.False(File.Exists(journal));
        Assert.Equal(3 * 4096L, new FileInfo(db).Length);
    }
    [Fact]
    public async Task IndexIsSortedByUsername()
    {
        var db = GenerateDatabasePath();
        var (lines, exitCode, error) = await RunScript(
            db,
            "insert 3 zed zed@example.com",
            "insert 1 alice alice@example.com",
            "insert 2 bob bob2@example.com",
            "insert 5 bob bob5@example.com",
            ".index",
            ".exit");

        Assert.True(exitCode == 0, error);

        string[] expected =
        [
            "db > Index:",
            "- leaf (size 4)",
            "  - alice (1)",
            "  - bob (2)",
            "  - bob (5)",
            "  - zed (3)",
            "db > ",
    ];

        Assert.Equal(expected, lines[^7..]);
    }

    [Fact]
    public async Task RollbackDropsIndexEntry()
    {
        var db = GenerateDatabasePath();
        var (lines, exitCode, error) = await RunScript(
            db,
            "insert 1 alice alice@example.com",
            "begin",
            "insert 2 bob bob@example.com",
            "rollback",
            ".index",
            "select",
            ".exit");

        Assert.True(exitCode == 0, error);

        string[] expected =
        [
            "db > Index:",
            "- leaf (size 1)",
            "  - alice (1)",
            "db > (1, alice, alice@example.com)",
            "Executed.",
            "db > ",
    ];

        Assert.Equal(expected, lines[^6..]);
    }

    [Fact]
    public async Task CommitCutShortIsRepairedForBothTrees()
    {
        var db = GenerateDatabasePath();
        var journal = db + ".journal";

        var (_, setupExit, setupError) = await RunScript(
            db,
            ["begin",
            .. Enumerable.Range(1, 113).Select(i => $"insert {i} user{i} person{i}@example.com"),
            "commit",
            ".exit"]);

        Assert.True(setupExit == 0, setupError);

        var (_, crashExit, crashError) = await RunScriptWithCrash(
            db,
            "after-database-written",
            "begin",
            "insert 114 user114 person114@example.com",
            "commit");

        Assert.True(File.Exists(journal));

        var (lines, exitCode, error) = await RunScript(db, ".index", "select", ".exit");

        Assert.True(exitCode == 0, error);
        Assert.Contains("- leaf (size 113)", lines);
        Assert.DoesNotContain(lines, l => l.Contains("- internal"));
        Assert.Equal(113, lines.Count(l => l.Contains("@example.com")));
        Assert.False(File.Exists(journal));
    }
}
