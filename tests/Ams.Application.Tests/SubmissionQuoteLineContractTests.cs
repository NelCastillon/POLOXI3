using System.ComponentModel.DataAnnotations;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Submissions;
using Xunit;

namespace Ams.Application.Tests;

public sealed class SubmissionQuoteLineContractTests
{
    [Fact]
    public void QuoteComparison_LineCount_Reflects_Persisted_Lines()
    {
        var quote = new QuoteComparisonDto
        {
            Lines =
            [
                new SubmissionQuoteLineDto { QuoteLineId = Guid.NewGuid(), LineOfBusiness = "Property", QuotedPremium = 18000m },
                new SubmissionQuoteLineDto { QuoteLineId = Guid.NewGuid(), LineOfBusiness = "General Liability", QuotedPremium = 7000m }
            ]
        };

        Assert.Equal(2, quote.LineCount);
        Assert.Equal(25000m, quote.Lines.Sum(line => line.QuotedPremium));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(1000, 101)]
    public void QuoteLineTerm_Rejects_Invalid_Premium_Or_Commission(decimal premium, decimal commission)
    {
        var request = new SubmissionQuoteLineTermRequest(
            Guid.NewGuid(), "Property", "Quoted", premium, null, null, commission,
            null, null, null, null, null, null, null, null, false, null);

        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true);

        Assert.False(isValid);
        Assert.NotEmpty(validationResults);
    }

    [Fact]
    public void QuoteLineTerm_Preserves_Unspecified_Tria()
    {
        var request = new SubmissionQuoteLineTermRequest(
            Guid.NewGuid(), "Property", "Quoted", 1000m, null, null, 10m,
            null, null, null, null, null, null, null, null, false, null);

        var validationResults = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), validationResults, true);

        Assert.True(isValid);
        Assert.Null(request.TriaIncluded);
        Assert.Empty(validationResults);
    }
}