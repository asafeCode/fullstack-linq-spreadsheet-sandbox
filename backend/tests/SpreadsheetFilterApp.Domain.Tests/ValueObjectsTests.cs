using SpreadsheetFilterApp.Domain.Exceptions;
using SpreadsheetFilterApp.Domain.ValueObjects;

namespace SpreadsheetFilterApp.Domain.Tests;

public sealed class ValueObjectsTests
{
    [Fact]
    public void ColumnName_EmptyValue_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => _ = new ColumnName(" "));
    }

    [Fact]
    public void ColumnName_TrimmedValue_IsStored()
    {
        var value = new ColumnName("  Nome  ");
        Assert.Equal("Nome", value.Value);
    }

    [Fact]
    public void NormalizedColumnName_EmptyValue_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => _ = new NormalizedColumnName(string.Empty));
    }

    [Fact]
    public void NormalizedColumnName_TrimmedValue_IsStored()
    {
        var value = new NormalizedColumnName("  nome_col  ");
        Assert.Equal("nome_col", value.Value);
    }
}
