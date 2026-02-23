using SpreadsheetFilterApp.Application.Common;
using SpreadsheetFilterApp.Application.Mapping;
using SpreadsheetFilterApp.Domain.ValueObjects;

namespace SpreadsheetFilterApp.Application.Tests;

public sealed class ApplicationCoreTests
{
    [Theory]
    [InlineData("arquivo.csv", SpreadsheetFormat.Csv)]
    [InlineData("CSV", SpreadsheetFormat.Csv)]
    [InlineData("planilha.xlsx", SpreadsheetFormat.Xlsx)]
    [InlineData("xlsx", SpreadsheetFormat.Xlsx)]
    public void SchemaMapper_ToFormat_MapsKnownFormats(string input, SpreadsheetFormat expected)
    {
        var result = SchemaMapper.ToFormat(input);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void SchemaMapper_ToFormat_InvalidFormat_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => SchemaMapper.ToFormat("arquivo.txt"));
    }

    [Fact]
    public void Guards_NotNullOrWhiteSpace_ThrowsForEmptyValue()
    {
        var ex = Assert.Throws<ArgumentException>(() => Guards.NotNullOrWhiteSpace(" ", "linqCode"));
        Assert.Contains("linqCode is required.", ex.Message);
    }
}
