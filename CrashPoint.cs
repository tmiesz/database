internal static class CrashPoint
{
    private const string VariableName = "DB_CRASH_AT";

    public static void At(string name)
    {
        var envVariable = Environment.GetEnvironmentVariable(VariableName);
        if (envVariable == name)
            Environment.Exit(1);
    }
}
