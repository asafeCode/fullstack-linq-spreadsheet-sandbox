# SpreadsheetFilterApp Backend

Backend ASP.NET Core para LINQ sandbox em planilhas (CSV/XLSX).

## Como rodar

```bash
cd backend/src/SpreadsheetFilterApp.Web
dotnet restore
dotnet run
```

Swagger: `https://localhost:5001/swagger` (ou porta exibida no console).

## Endpoints

- `POST /api/spreadsheets/schema` (multipart/form-data com `file`)
- `POST /api/spreadsheets/validate`
- `POST /api/spreadsheets/query/preview`
- `POST /api/spreadsheets/query`

## Exemplos LINQ

```csharp
rows.Where(row => row.Idade >= 18)
```

```csharp
rows.Where(row => row.Status == "Ativo").OrderBy(row => row.Nome)
```

```csharp
rows.Select(row => new { row.Nome, Ano = row.DataNascimento.Year, Score = row.Pontos * 1.5m })
```

## Limitações de sandbox

- Bloqueio de tokens perigosos (`System.IO`, `System.Net`, `Reflection`, `Environment`, etc.).
- Timeout de execução de 5 segundos.
- Limite de linhas processadas em memória: 50.000.
- Limite de upload: 25 MB.
