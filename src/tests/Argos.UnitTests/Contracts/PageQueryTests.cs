using System.ComponentModel.DataAnnotations;
using Argos.Contracts.Common;

namespace Argos.UnitTests.Contracts;

/// <summary>
/// Paginação comum (D-53; plan 15.8): padrão de 25 por página, máximo de 100 e mensagens em pt-BR (Art. XIII.2).
/// </summary>
public sealed class PageQueryTests
{
    [Fact]
    public void Defaults_FirstPageOf25Ascending_D53()
    {
        var query = new PageQuery();

        Assert.Equal(1, query.Page);
        Assert.Equal(25, query.PageSize);
        Assert.Null(query.Sort);
        Assert.Equal(SortDirection.Asc, query.Direction);
        Assert.Empty(Validate(query));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public void PageSize_WithinRange_IsValid_D53(int pageSize)
    {
        Assert.Empty(Validate(new PageQuery { PageSize = pageSize }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void PageSize_OutOfRange_IsRejectedInPortuguese_D53(int pageSize)
    {
        var error = Assert.Single(Validate(new PageQuery { PageSize = pageSize }));

        Assert.Equal([nameof(PageQuery.PageSize)], error.MemberNames);
        Assert.Equal("O tamanho da página deve estar entre 1 e 100.", error.ErrorMessage);
    }

    [Fact]
    public void Page_Zero_IsRejectedInPortuguese_D53()
    {
        var error = Assert.Single(Validate(new PageQuery { Page = 0 }));

        Assert.Equal("A página deve ser 1 ou maior.", error.ErrorMessage);
    }

    private static List<ValidationResult> Validate(PageQuery query)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(query, new ValidationContext(query), results, validateAllProperties: true);
        return results;
    }
}
