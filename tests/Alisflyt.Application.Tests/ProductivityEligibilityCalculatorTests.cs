using Alisflyt.Application.Models;
using Xunit;

namespace Alisflyt.Application.Tests;

public class ProductivityEligibilityCalculatorTests
{
    [Fact]
    public void Calculate_WhenFundingPeriodIsFullyWithinEligibility_ReturnsWholeFundingPeriod()
    {
        // Arrange
        var firstRegularGpDate = new DateOnly(2025, 1, 1);
        var fundingFrom = new DateOnly(2025, 6, 1);
        var fundingThrough = new DateOnly(2025, 12, 31);

        // Act
        var result = ProductivityEligibilityCalculator.Calculate(
            firstRegularGpDate,
            24,
            fundingFrom,
            fundingThrough);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(fundingFrom, result.From);
        Assert.Equal(fundingThrough, result.Through);
    }

    [Fact]
    public void Calculate_WhenFundingPeriodCrossesEligibilityEnd_ClipsEndDate()
    {
        // Arrange
        var firstRegularGpDate = new DateOnly(2024, 8, 1);
        var fundingFrom = new DateOnly(2026, 1, 1);
        var fundingThrough = new DateOnly(2026, 12, 31);

        // Act
        var result = ProductivityEligibilityCalculator.Calculate(
            firstRegularGpDate,
            24,
            fundingFrom,
            fundingThrough);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2026, 1, 1), result.From);
        Assert.Equal(new DateOnly(2026, 7, 31), result.Through);
    }

    [Fact]
    public void Calculate_WhenFundingPeriodIsAfterEligibility_ReturnsNull()
    {
        // Arrange
        var firstRegularGpDate = new DateOnly(2024, 8, 1);
        var fundingFrom = new DateOnly(2026, 8, 1);
        var fundingThrough = new DateOnly(2026, 12, 31);

        // Act
        var result = ProductivityEligibilityCalculator.Calculate(
            firstRegularGpDate,
            24,
            fundingFrom,
            fundingThrough);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Calculate_WhenFundingPeriodStartsBeforeEligibility_ClipsStartDate()
    {
        // Arrange
        var firstRegularGpDate = new DateOnly(2025, 3, 1);
        var fundingFrom = new DateOnly(2025, 1, 1);
        var fundingThrough = new DateOnly(2025, 6, 30);

        // Act
        var result = ProductivityEligibilityCalculator.Calculate(
            firstRegularGpDate,
            24,
            fundingFrom,
            fundingThrough);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(new DateOnly(2025, 3, 1), result.From);
        Assert.Equal(new DateOnly(2025, 6, 30), result.Through);
    }
}