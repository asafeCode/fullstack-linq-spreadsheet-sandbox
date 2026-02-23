using SpreadsheetFilterApp.QuerySandboxHost;

namespace SpreadsheetFilterApp.QuerySandboxHost.Tests;

public sealed class QueryRunnerTests
{
    private readonly QueryRunner _runner = new();

    [Fact]
    public async Task Blocks_ForbiddenApi_From_SystemIo()
    {
        var request = BuildRequest("return rows.Where(r => System.IO.File.Exists(\"x\")).ToList();");

        var result = await _runner.RunAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains(result.Diagnostics, d => d.Message.Contains("Forbidden token", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Allows_Whitelisted_Linq_Where_Select_Take()
    {
        var request = BuildRequest("return rows.Where(r => r.Int(\"progresso\") >= 50).Select(r => new { nome = r.Str(\"nome\") }).Take(10).ToList();");

        var result = await _runner.RunAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, result.Rows.Count);
    }

    [Fact]
    public async Task Join_With_SemCs_Fallback_Works()
    {
        var request = new SandboxRequest
        {
            Code = """
                return sheet1.Rows
                    .Join(sheet2!.Rows,
                        r1 => r1.Str("name_norm"),
                        r2 => r2.Str("name_norm"),
                        (r1, r2) => new { cs = r2.Str("cs_responsavel") ?? "SEM_CS" })
                    .GroupBy(x => x.cs)
                    .Select(g => new { cs_responsavel = g.Key, total = g.Count() })
                    .ToList();
                """,
            Sheet1 = new SheetPayload
            {
                Headers = ["name_norm"],
                Rows =
                [
                    new Dictionary<string, string?> { ["name_norm"] = "ana" },
                    new Dictionary<string, string?> { ["name_norm"] = "bruno" }
                ]
            },
            Sheet2 = new SheetPayload
            {
                Headers = ["name_norm", "cs_responsavel"],
                Rows =
                [
                    new Dictionary<string, string?> { ["name_norm"] = "ana", ["cs_responsavel"] = null },
                    new Dictionary<string, string?> { ["name_norm"] = "bruno", ["cs_responsavel"] = "CS_1" }
                ]
            },
            MaxRows = 100,
            HardLimitRows = 1000,
            TimeoutMs = 2000
        };

        var result = await _runner.RunAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Contains(result.Rows, x => Equals(x["cs_responsavel"], "SEM_CS") && Equals(x["total"], 1));
        Assert.Contains(result.Rows, x => Equals(x["cs_responsavel"], "CS_1") && Equals(x["total"], 1));
    }

    [Fact]
    public async Task HardLimit_Is_Enforced()
    {
        var request = BuildRequest("return rows.Select(r => new { nome = r.Str(\"nome\") }).ToList();", maxRows: 3, hardLimit: 3);

        var result = await _runner.RunAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(3, result.Rows.Count);
        Assert.True(result.Truncated);
    }

    [Fact]
    public async Task Allows_Normalization_Only_Through_RowRef_Methods()
    {
        var request = new SandboxRequest
        {
            Code = """
                return sheet1.Rows
                    .Where(r => r.EqualsNorm("nome", "Abner da Silva Costa"))
                    .Join(sheet2!.Rows,
                        p => p.Norm("nome"),
                        c => c.Norm("nome"),
                        (p, c) => new { nome = p.Str("nome"), cs = c.Str("cs_responsavel") ?? "Nenhum" })
                    .ToList();
                """,
            Sheet1 = new SheetPayload
            {
                Headers = ["nome"],
                Rows =
                [
                    new Dictionary<string, string?> { ["nome"] = " Abner   da  Silva Costa " },
                    new Dictionary<string, string?> { ["nome"] = "Outro Nome" }
                ]
            },
            Sheet2 = new SheetPayload
            {
                Headers = ["nome", "cs_responsavel"],
                Rows =
                [
                    new Dictionary<string, string?> { ["nome"] = "abner da silva costa", ["cs_responsavel"] = "Christiam" }
                ]
            },
            MaxRows = 100,
            HardLimitRows = 1000,
            TimeoutMs = 2000
        };

        var result = await _runner.RunAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Rows);
        Assert.Equal("Christiam", result.Rows[0]["cs"]);
    }

    [Fact]
    public async Task Allows_StringComparison_OrdinalIgnoreCase_Methods()
    {
        var request = BuildRequest("""
            return rows
                .Where(r => (r.Str("nome") ?? "").Contains("an", StringComparison.OrdinalIgnoreCase))
                .Where(r => (r.Str("nome") ?? "").StartsWith("a", StringComparison.OrdinalIgnoreCase))
                .Select(r => new { nome = r.Str("nome") ?? "" })
                .ToList();
            """);

        var result = await _runner.RunAsync(request, CancellationToken.None);

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Single(result.Rows);
        Assert.Equal("Ana", result.Rows[0]["nome"]);
    }

    [Fact]
    public async Task Allows_Dictionary_Projection_From_Query()
    {
        var request = BuildRequest("""
            return rows
                .Select(r => new Dictionary<string, object?>
                {
                    ["nome"] = r.Str("nome"),
                    ["progresso"] = r.Str("progresso")
                })
                .ToList();
            """);

        var result = await _runner.RunAsync(request, CancellationToken.None);

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.NotEmpty(result.Rows);
        Assert.True(result.Rows[0].ContainsKey("nome"));
        Assert.True(result.Rows[0].ContainsKey("progresso"));
    }

    [Fact]
    public async Task Allows_Helper_Methods_Declared_In_Submission()
    {
        var request = BuildRequest("""
            static string GetNome(RowRef r)
            {
                return r.Str("nome") ?? "";
            }

            return rows
                .Select(r => new { nome = GetNome(r) })
                .ToList();
            """);

        var result = await _runner.RunAsync(request, CancellationToken.None);

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.NotEmpty(result.Rows);
    }

    [Fact]
    public async Task Allows_String_IsNullOrWhiteSpace()
    {
        var request = BuildRequest("""
            return rows
                .Where(r => !string.IsNullOrWhiteSpace(r.Str("nome")))
                .Select(r => new { nome = r.Str("nome") ?? "" })
                .ToList();
            """);

        var result = await _runner.RunAsync(request, CancellationToken.None);

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.NotEmpty(result.Rows);
    }

    [Fact]
    public async Task Allows_PropertyStyle_Normalize_Syntax()
    {
        var request = new SandboxRequest
        {
            Code = """
                return rows
                    .Where(r => r.nome.Normalize() == r.nome.Normalize())
                    .Select(r => new { nome = r.nome })
                    .ToList();
                """,
            Sheet1 = new SheetPayload
            {
                Headers = ["nome"],
                Rows =
                [
                    new Dictionary<string, string?> { ["nome"] = "Ana" },
                    new Dictionary<string, string?> { ["nome"] = "Bruno" }
                ]
            },
            MaxRows = 50,
            HardLimitRows = 1000,
            TimeoutMs = 2000
        };

        var result = await _runner.RunAsync(request, CancellationToken.None);

        Assert.True(result.Success, string.Join(" | ", result.Diagnostics.Select(d => d.Message)));
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal("Ana", result.Rows[0]["nome"]);
    }

    [Fact]
    public async Task Does_Not_Block_ProfileField_By_File_Substring()
    {
        var request = new SandboxRequest
        {
            Code = """
                var col = "pro" + "fi" + "le_field_telefone";
                return rows.Select(r => new { telefone = r.Str(col) }).Take(2).ToList();
                """,
            Sheet1 = new SheetPayload
            {
                Headers = ["profile_field_telefone"],
                Rows =
                [
                    new Dictionary<string, string?> { ["profile_field_telefone"] = "11999999999" },
                    new Dictionary<string, string?> { ["profile_field_telefone"] = "11888888888" }
                ]
            },
            MaxRows = 10,
            HardLimitRows = 100,
            TimeoutMs = 2000
        };

        var result = await _runner.RunAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.DoesNotContain(result.Diagnostics, d => d.Message.Contains("Forbidden token: File", StringComparison.OrdinalIgnoreCase));
    }

    private static SandboxRequest BuildRequest(string code, int maxRows = 200, int hardLimit = 1000)
    {
        var rows = new List<Dictionary<string, string?>>
        {
            new() { ["nome"] = "Ana", ["progresso"] = "50" },
            new() { ["nome"] = "Bruno", ["progresso"] = "72" },
            new() { ["nome"] = "Carla", ["progresso"] = "10" },
            new() { ["nome"] = "Daniel", ["progresso"] = "5" },
            new() { ["nome"] = "Erica", ["progresso"] = "90" }
        };

        return new SandboxRequest
        {
            Code = code,
            Sheet1 = new SheetPayload
            {
                Headers = ["nome", "progresso"],
                Rows = rows
            },
            MaxRows = maxRows,
            HardLimitRows = hardLimit,
            TimeoutMs = 2000
        };
    }
}
