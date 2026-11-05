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
        Assert.Equal(33090.410958904109589041095892m, result.PracticeCompensationMaximumAmount);
        Assert.False(result.IsPracticeCompensationCapped);
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

        Assert.Equal(33090.410958904109589041095892m, result.PracticeCompensationAmount);
        Assert.Equal(33090.410958904109589041095892m, result.PracticeCompensationMaximumAmount);
        Assert.True(result.IsPracticeCompensationCapped);
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

        Assert.Equal(3509.5890410958904109589041098m, result.LearningActivitiesAmount);
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
    public void Productivity_IsFullAnnualMaximum_For100PercentRegularGpYear()
    {
        var service = new GrantCalculationService();
        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            FirstRegularGpOrLocumDate = new DateOnly(2025, 6, 1),
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(PositionType.RegularGpOrLocum, 100m, new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 1), new DateOnly(2026, 5, 31))
            ]
        };

        var result = service.Calculate(input, CreateRateSet());

        Assert.Equal(125_000m, result.ProductivityAmount);
    }

    [Fact]
    public void Productivity_UsesRegularGpPercentage_For60PercentCombinedPosition()
    {
        var service = new GrantCalculationService();
        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            FirstRegularGpOrLocumDate = new DateOnly(2025, 6, 1),
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(PositionType.RegularGpOrLocum, 60m, new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 1), new DateOnly(2026, 5, 31)),
                new GrantCalculationEmploymentPeriod(PositionType.IntroductoryDoctor, 40m, new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 1), new DateOnly(2026, 5, 31))
            ]
        };

        var result = service.Calculate(input, CreateRateSet());

        Assert.Equal(75_000m, result.ProductivityAmount);
    }

    [Fact]
    public void Productivity_IsZero_WhenOutsideFirst24Months()
    {
        var service = new GrantCalculationService();
        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            FirstRegularGpOrLocumDate = new DateOnly(2023, 1, 1),
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(PositionType.RegularGpOrLocum, 100m, new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 1), new DateOnly(2026, 5, 31))
            ]
        };

        var result = service.Calculate(input, CreateRateSet());

        Assert.Equal(0m, result.ProductivityAmount);
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

        Assert.Equal(31506.849315068493150684931506m, result.ProductivityAmount);
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

        Assert.Equal(31506.849315068493150684931512m, result.ProductivityAmount);
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

    [Fact]
    public void Guidance_UsesActualExpense_WhenBelowMaximum()
    {
        var service = new GrantCalculationService();

        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            SupervisionExpenses = 20_000m,
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

        Assert.Equal(20_000m, result.GuidanceAmount);
    }

    [Fact]
    public void Guidance_IsCappedAtMaximum()
    {
        var service = new GrantCalculationService();

        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            SupervisionExpenses = 100_000m,
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

        Assert.Equal(91_317.1875m, result.GuidanceAmount);
    }

    [Fact]
    public void Guidance_IsProratedByPositionAndPeriod()
    {
        var service = new GrantCalculationService();

        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            SupervisionExpenses = 100_000m,
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

        Assert.Equal(22891.842893835616438356164385m, result.GuidanceAmount);
    }

    [Fact]
    public void Facilitation_IsFivePercentOfStandardElements()
    {
        var service = new GrantCalculationService();

        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            AbsenceCompensation = 10_000m,
            LearningActivityExpenses = 10_000m,
            SupervisionExpenses = 10_000m,
            FirstRegularGpOrLocumDate = new DateOnly(2024, 8, 1),
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(
                PositionType.RegularGpOrLocum,
                100m,
                new DateOnly(2024, 8, 1),
                new DateOnly(2025, 8, 1),
                new DateOnly(2026, 1, 31))
            ]
        };

        var rateSet = CreateRateSet();

        var result = service.Calculate(input, rateSet);

        var expectedStandardElements =
            result.PracticeCompensationAmount
            + result.LearningActivitiesAmount
            + result.ProductivityAmount
            + result.GuidanceAmount;

        Assert.Equal(
            expectedStandardElements * 0.05m,
            result.FacilitationAmount);
    }

    [Fact]
    public void Facilitation_IsZero_WhenStandardElementsAreZero()
    {
        var service = new GrantCalculationService();

        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            EmploymentPeriods = []
        };

        var rateSet = CreateRateSet();

        var result = service.Calculate(input, rateSet);

        Assert.Equal(0m, result.FacilitationAmount);
    }

    [Fact]
    public void CentralitySupplement_UsesRequestedAmount_WhenBelowMaximum()
    {
        var service = new GrantCalculationService();

        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            IsCentralityGrade6 = true,
            CentralitySupplementRequestedAmount = 35_000m,
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

        Assert.Equal(35_000m, result.CentralitySupplementAmount);
        Assert.True(!string.IsNullOrWhiteSpace(result.CentralityExplanation));
    }

    [Fact]
    public void CentralitySupplement_IsCappedAtMaximum()
    {
        var service = new GrantCalculationService();

        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            IsCentralityGrade6 = true,
            CentralitySupplementRequestedAmount = 70_000m,
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

        Assert.Equal(50136.986301369863013698630140m, result.CentralitySupplementAmount);
        Assert.True(!string.IsNullOrWhiteSpace(result.CentralityExplanation));
    }

    [Fact]
    public void CentralitySupplement_IsZero_WhenMunicipalityIsNotGrade6()
    {
        var service = new GrantCalculationService();

        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            IsCentralityGrade6 = false,
            CentralitySupplementRequestedAmount = 70_000m,
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

        Assert.Equal(0m, result.CentralitySupplementAmount);
    }

    [Fact]
    public void CentralitySupplement_IsZero_ForSupervisionOnlyGrant()
    {
        var service = new GrantCalculationService();

        var input = new GrantCalculationInput
        {
            GrantType = GrantType.SupervisionOnly,
            IsCentralityGrade6 = true,
            CentralitySupplementRequestedAmount = 70_000m,
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

        Assert.Equal(0m, result.CentralitySupplementAmount);
    }

    [Fact]
    public void SupervisionOnly_IgnoresAgreementAmounts_AndCalculatesGuidanceOnly()
    {
        var service = new GrantCalculationService();
        var input = new GrantCalculationInput
        {
            GrantType = GrantType.SupervisionOnly,
            AbsenceCompensation = 132_000m,
            LearningActivityExpenses = 14_000m,
            FirstRegularGpOrLocumDate = new DateOnly(2025, 1, 1),
            SupervisionExpenses = 100_000m,
            IsCentralityGrade6 = true,
            CentralitySupplementRequestedAmount = 200_000m,
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

        var result = service.Calculate(input, CreateRateSet());

        Assert.Equal(0m, result.PracticeCompensationAmount);
        Assert.Equal(0m, result.PracticeCompensationMaximumAmount);
        Assert.False(result.IsPracticeCompensationCapped);
        Assert.Equal(0m, result.LearningActivitiesAmount);
        Assert.Equal(0m, result.ProductivityAmount);
        Assert.Equal(91_317.1875m, result.GuidanceAmount);
        Assert.Equal(0m, result.FacilitationAmount);
        Assert.Equal(0m, result.CentralitySupplementAmount);
        Assert.Equal(result.GuidanceAmount, result.TotalAmount);
    }

    [Fact]
    public void DayProration_UsesAll365Days_ForFullRatePeriod()
    {
        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            LearningActivityExpenses = decimal.MaxValue,
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(
                    PositionType.RegularGpOrLocum,
                    100m,
                    new DateOnly(2025, 6, 1),
                    new DateOnly(2025, 6, 1),
                    new DateOnly(2026, 5, 31))
            ]
        };

        var result = new GrantCalculationService().Calculate(input, CreateRateSet());

        Assert.Equal(14_000m, result.LearningActivitiesAmount);
    }

    [Fact]
    public void DayProration_Counts182InclusiveDays()
    {
        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            LearningActivityExpenses = decimal.MaxValue,
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(
                    PositionType.RegularGpOrLocum,
                    100m,
                    new DateOnly(2025, 6, 1),
                    new DateOnly(2025, 6, 1),
                    new DateOnly(2025, 11, 29))
            ]
        };

        var result = new GrantCalculationService().Calculate(input, CreateRateSet());

        Assert.Equal(6980.8219178082191780821917804m, result.LearningActivitiesAmount);
    }

    [Fact]
    public void DayProration_AppliesSixtyPercentPosition()
    {
        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            LearningActivityExpenses = decimal.MaxValue,
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(
                    PositionType.RegularGpOrLocum,
                    60m,
                    new DateOnly(2025, 6, 1),
                    new DateOnly(2025, 6, 1),
                    new DateOnly(2026, 5, 31))
            ]
        };

        var result = new GrantCalculationService().Calculate(input, CreateRateSet());

        Assert.Equal(8400m, result.LearningActivitiesAmount);
    }

    [Fact]
    public void Productivity_UsesOnlyTheFastlegeShareInCombinedPeriods()
    {
        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            FirstRegularGpOrLocumDate = new DateOnly(2025, 6, 1),
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(PositionType.RegularGpOrLocum, 60m, new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 1), new DateOnly(2026, 5, 31)),
                new GrantCalculationEmploymentPeriod(PositionType.IntroductoryDoctor, 40m, new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 1), new DateOnly(2026, 5, 31))
            ]
        };

        var result = new GrantCalculationService().Calculate(input, CreateRateSet());

        Assert.Equal(75_000m, result.ProductivityAmount);
    }

    [Fact]
    public void Productivity_ProrationUsesOnlyTheOverlapWithFirst24Months()
    {
        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            FirstRegularGpOrLocumDate = new DateOnly(2024, 8, 1),
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(
                    PositionType.RegularGpOrLocum,
                    100m,
                    new DateOnly(2026, 1, 1),
                    new DateOnly(2026, 1, 1),
                    new DateOnly(2026, 12, 31))
            ]
        };

        var result = new GrantCalculationService().Calculate(input, CreateRateSet());

        Assert.Equal(72602.739726027397260273972600m, result.ProductivityAmount);
    }

    [Fact]
    public void DayProration_SumsNonOverlappingPeriods()
    {
        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            LearningActivityExpenses = decimal.MaxValue,
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(PositionType.RegularGpOrLocum, 100m, new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 1), new DateOnly(2025, 6, 30)),
                new GrantCalculationEmploymentPeriod(PositionType.RegularGpOrLocum, 100m, new DateOnly(2025, 7, 1), new DateOnly(2025, 7, 1), new DateOnly(2025, 7, 31))
            ]
        };

        var result = new GrantCalculationService().Calculate(input, CreateRateSet());

        Assert.Equal(2339.7260273972602739726027394m, result.LearningActivitiesAmount);
    }

    [Fact]
    public void DayProration_Uses365DenominatorForLeapYear()
    {
        var input = new GrantCalculationInput
        {
            GrantType = GrantType.AlisAgreementIncludingSupervision,
            LearningActivityExpenses = decimal.MaxValue,
            EmploymentPeriods =
            [
                new GrantCalculationEmploymentPeriod(
                    PositionType.RegularGpOrLocum,
                    100m,
                    new DateOnly(2024, 1, 1),
                    new DateOnly(2024, 1, 1),
                    new DateOnly(2024, 12, 31))
            ]
        };

        var result = new GrantCalculationService().Calculate(input, CreateRateSet());

        Assert.Equal(14038.356164383561643835616438m, result.LearningActivitiesAmount);
    }
}
