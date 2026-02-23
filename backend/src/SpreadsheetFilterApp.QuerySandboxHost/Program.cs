using System.Text.Json;

namespace SpreadsheetFilterApp.QuerySandboxHost;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args.Length < 2)
        {
            Console.Error.WriteLine("Usage: QuerySandboxHost <input.json> <output.json>");
            return 1;
        }

        var inputPath = args[0];
        var outputPath = args[1];

        var json = await File.ReadAllTextAsync(inputPath);
        var request = JsonSerializer.Deserialize<SandboxRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        if (request is null)
        {
            await File.WriteAllTextAsync(outputPath, JsonSerializer.Serialize(new SandboxResponse
            {
                Success = false,
                Diagnostics =
                [
                    new SandboxDiagnostic
                    {
                        Message = "Invalid request payload.",
                        Severity = "error",
                        Line = 1,
                        Column = 1
                    }
                ]
            }));
            return 2;
        }

        var runner = new QueryRunner();
        var response = await runner.RunAsync(request, CancellationToken.None);

        var outJson = JsonSerializer.Serialize(response, new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = false
        });
        await File.WriteAllTextAsync(outputPath, outJson);
        return response.Success ? 0 : 3;
    }
}
