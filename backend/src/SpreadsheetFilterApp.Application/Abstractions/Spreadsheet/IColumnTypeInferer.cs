using System.Collections.Generic;
using SpreadsheetFilterApp.Application.DTOs;

namespace SpreadsheetFilterApp.Application.Abstractions.Spreadsheet;

public interface IColumnTypeInferer
{
    IReadOnlyDictionary<string, string> Infer(
        IReadOnlyList<IReadOnlyDictionary<string, string?>> rows,
        IReadOnlyList<string> headers,
        int sampleSize = 200);
}
