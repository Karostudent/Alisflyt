using Alisflyt.Application.Models;
using Alisflyt.Application.Services;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Forms;
using Xunit;

namespace Alisflyt.Application.Tests;

public class GrantCalculationServiceTests
{
    [Fact]
    public void PracticeCompensation_UsesActualExpense_WhenBelowMaximum()
    {
        var service = new GrantCalculationService();

        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            AbsenceCompensation = 28_000m,
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(
                    PositionType.RegularGpOrLocum,
                    50m,
                    new DateOnly(2025, 1, 1),
                    new DateOnly(2025, 6, 1),
                    new DateOnly(2025, 11, 30))
            ]
        };

        var rateSet = CreateRateSet();

        var result = service.Calculate(input, rateSet);

        Assert.Equal(28_000m, result.PracticeCompensationAmount);
    }

    [Fact]
    public void PracticeCompensation_IsCappedAtMaximum()
    {
        var service = new GrantCalculationService();

        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            AbsenceCompensation = 40_000m,
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(
                    PositionType.RegularGpOrLocum,
                    50m,
                    new DateOnly(2025, 1, 1),
                    new DateOnly(2025, 6, 1),
                    new DateOnly(2025, 11, 30))
            ]
        };

        var rateSet = CreateRateSet();

        var result = service.Calculate(input, rateSet);

        Assert.Equal(33_000m, result.PracticeCompensationAmount);
    }

    [Fact]
    public void PracticeCompensation_IsZero_ForGpOutsideRegularGpScheme()
    {
        var service = new GrantCalculationService();

        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            AbsenceCompensation = 40_000m,
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(
                    PositionType.GeneralPractitionerOutsideRegularGpScheme,
                    100m,
                    new DateOnly(2025, 1, 1),
                    new DateOnly(2025, 6, 1),
                    new DateOnly(2026, 5, 31))
            ]
        };

        var rateSet = CreateRateSet();

        var result = service.Calculate(input, rateSet);

        Assert.Equal(0m, result.PracticeCompensationAmount);
    }

    private static GrantRateSet CreateRateSet()
    {
        return GrantRateSet.Create(
            Guid.NewGuid(),
            "Test rate set",
            new DateOnly(2025, 6, 1),
            new DateOnly(2026, 5, 31),
            1_375m,
            0.60m,
            160m,
            14_000m,
            125_000m,
            24,
            1.15m,
            57.75m,
            0.05m,
            200_000m,
            DateTimeOffset.UtcNow);
    }

    [Fact]
    public void LearningActivities_UsesActualExpense_WhenBelowMaximum()
    {
        var service = new GrantCalculationService();

        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            LearningActivityExpenses = 3_000m,
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(
                PositionType.RegularGpOrLocum,
                50m,
                new DateOnly(2025, 1, 1),
                new DateOnly(2025, 6, 1),
                new DateOnly(2025, 11, 30))
            ]
        };

        var rateSet = CreateRateSet();

        var result = service.Calculate(input, rateSet);

        Assert.Equal(3_000m, result.LearningActivitiesAmount);
    }

    [Fact]
    public void LearningActivities_IsCappedAtMaximum()
    {
        var service = new GrantCalculationService();

        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            LearningActivityExpenses = 5_000m,
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(
                PositionType.RegularGpOrLocum,
                50m,
                new DateOnly(2025, 1, 1),
                new DateOnly(2025, 6, 1),
                new DateOnly(2025, 11, 30))
            ]
        };

        var rateSet = CreateRateSet();

        var result = service.Calculate(input, rateSet);

        Assert.Equal(3_500m, result.LearningActivitiesAmount);
    }

    [Fact]
    public void LearningActivities_UsesFullAnnualMaximum_ForFullPositionAndYear()
    {
        var service = new GrantCalculationService();

        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            LearningActivityExpenses = 20_000m,
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(
                PositionType.RegularGpOrLocum,
                100m,
                new DateOnly(2025, 1, 1),
                new DateOnly(2025, 6, 1),
                new DateOnly(2026, 5, 31))
            ]
        };

        var rateSet = CreateRateSet();

        var result = service.Calculate(input, rateSet);

        Assert.Equal(14_000m, result.LearningActivitiesAmount);
    }
    [Fact]
    public void Productivity_UsesEligiblePeriodWithinFirstMonths()
    {
        var service = new GrantCalculationService();

        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            FirstRegularGpOrLocumDate = new DateOnly(2024, 8, 1),
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(
                PositionType.RegularGpOrLocum,
                50m,
                new DateOnly(2024, 8, 1),
                new DateOnly(2025, 8, 1),
                new DateOnly(2026, 1, 31))
            ]
        };

        var rateSet = CreateRateSet();

        var result = service.Calculate(input, rateSet);

        Assert.Equal(31_250m, result.ProductivityAmount);
    }

    [Fact]
    public void Productivity_IsClippedAtEligibilityEnd()
    {
        var service = new GrantCalculationService();

        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            FirstRegularGpOrLocumDate = new DateOnly(2024, 8, 1),
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(
                PositionType.RegularGpOrLocum,
                100m,
                new DateOnly(2024, 8, 1),
                new DateOnly(2026, 5, 1),
                new DateOnly(2026, 10, 31))
            ]
        };

        var rateSet = CreateRateSet();

        var result = service.Calculate(input, rateSet);

        Assert.Equal(31_250m, result.ProductivityAmount);
    }

    [Fact]
    public void Productivity_IsZero_AfterEligibilityPeriod()
    {
        var service = new GrantCalculationService();

        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            FirstRegularGpOrLocumDate = new DateOnly(2024, 8, 1),
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(
                PositionType.RegularGpOrLocum,
                100m,
                new DateOnly(2024, 8, 1),
                new DateOnly(2026, 8, 1),
                new DateOnly(2026, 12, 31))
            ]
        };

        var rateSet = CreateRateSet();

        var result = service.Calculate(input, rateSet);

        Assert.Equal(0m, result.ProductivityAmount);
    }

    [Fact]
    public void Productivity_IsZero_ForNonRegularGpPosition()
    {
        var service = new GrantCalculationService();

        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            FirstRegularGpOrLocumDate = new DateOnly(2024, 8, 1),
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(
                PositionType.IntroductoryDoctor,
                100m,
                new DateOnly(2024, 8, 1),
                new DateOnly(2025, 8, 1),
                new DateOnly(2026, 1, 31))
            ]
        };

        var rateSet = CreateRateSet();

        var result = service.Calculate(input, rateSet);

        Assert.Equal(0m, result.ProductivityAmount);
    }
}