namespace SpreadsheetFilterApp.Infrastructure.Scripting.Roslyn;

public sealed class SandboxSecurityPolicy
{
    private static readonly string[] ForbiddenTokens =
    [
        "System.IO",
        "System.Net",
        "System.Reflection",
        "Environment",
        "Process",
        "Thread",
        "Task.Run",
        "File.",
        "Directory.",
        "Activator.",
        "Assembly."
    ];

    public void AssertSafe(string linqCode)
    {
        foreach (var token in ForbiddenTokens)
        {
            if (linqCode.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Forbidden API in sandbox: {token}");
            }
        }
    }
}
