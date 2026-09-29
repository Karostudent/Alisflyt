using Alisflyt.Application.Models;
using Xunit;

namespace Alisflyt.Application.Tests;

public class GrantCalculationResultTests
{
    [Fact]
    public void TotalAmount_IsSumOfAllCalculationElements()
    {
        var result = new GrantCalculationResult
        {
            PracticeCompensationAmount = 10_000m,
            LearningActivitiesAmount = 20_000m,
            ProductivityAmount = 30_000m,
            GuidanceAmount = 40_000m,
            FacilitationAmount = 50_000m,
            CentralitySupplementAmount = 60_000m
        };

        Assert.Equal(210_000m, result.TotalAmount);
    }
}