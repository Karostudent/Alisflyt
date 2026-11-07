using Alisflyt.Application.Models;
using Alisflyt.Application.Services;
using Alisflyt.Domain.Entities;
using Alisflyt.Domain.Forms;
using Xunit;

namespace Alisflyt.Application.Tests;

public class ApplicationCalculationConnectionTests
{
    private static GrantRateSet Rates() => GrantRateSet.Create(Guid.NewGuid(), "Demo", new(2025, 6, 1), new(2026, 5, 31),
        1375m, .60m, 160m, 14000m, 125000m, 24, 1.15m, 57.75m, .05m, 200000m, DateTimeOffset.UtcNow);
    private static GrantApplicationData Form() => new()
    {
        GrantType = GrantType.AlisAgreementIncludingSupervision,
        FirstRegularGpOrLocumDate = new(2025, 6, 1),
        EmploymentPeriods = [new() { PositionType = PositionType.RegularGpOrLocum, PositionPercentage = 100,
            EmploymentStartDate = new(2025, 6, 1), FundingFrom = new(2025, 6, 1), FundingThrough = new(2026, 5, 31) }]
    };

    [Fact]
    public void Additional_supervision_costs_do_not_change_automatic_total()
    {
        var form = Form();
        var service = new GrantCalculationService();
        var withoutAdditionalCosts = service.Calculate(GrantCalculationInputMapper.From(form), Rates());

        form.AdditionalSupervisionCosts = 25_000m;

        var withAdditionalCosts = service.Calculate(GrantCalculationInputMapper.From(form), Rates());

        Assert.Equal(withoutAdditionalCosts.TotalAmount, withAdditionalCosts.TotalAmount);
        Assert.Equal(withoutAdditionalCosts.GuidanceAmount, withAdditionalCosts.GuidanceAmount);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(50000, 50000)]
    [InlineData(100000, 91317.1875)]
    public void Guidance_uses_actual_expenses_with_cap_regardless_of_sessions(decimal expense, decimal expected)
    {
        var form = Form(); form.SupervisionExpenses = expense;
        var service = new GrantCalculationService();
        var withoutSessions = service.Calculate(GrantCalculationInputMapper.From(form), Rates());
        form.Certificate.Sessions = Enumerable.Range(0, 30).Select(i => new SupervisionSessionInputModel
            { Date = new(2025, 7, 1), Hours = 2, Topic = "Demo" }).ToList();
        var withSessions = service.Calculate(GrantCalculationInputMapper.From(form), Rates());
        Assert.Equal(expected, withSessions.GuidanceAmount);
        Assert.Equal(withoutSessions.TotalAmount, withSessions.TotalAmount);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 4348.4375)]
    [InlineData(6, 13045.3125)]
    [InlineData(28, 60878.125)]
    [InlineData(42, 91317.1875)]
    public void Guidance_honorarium_uses_registered_hours_and_excel_factor(decimal hours, decimal expected)
    {
        var form = Form();
        form.Certificate.Sessions = [new() { Date = new(2025, 7, 1), Hours = hours }];

        var result = new GrantCalculationService().Calculate(GrantCalculationInputMapper.From(form), Rates());

        Assert.Equal(hours, result.RegisteredGuidanceHours);
        Assert.Equal(hours * 1.375m, result.GuidancePaidHours);
        Assert.Equal(1375m * 1.15m, result.GuidanceHourlyRate);
        Assert.Equal(expected, result.GuidanceHonorariumAmount);
        Assert.Equal(Math.Min(expected, 91317.1875m), result.GuidanceHonorariumCappedAmount);
    }

    [Fact]
    public void Guidance_honorarium_counts_in_period_sessions_once_across_overlapping_periods()
    {
        var form = Form();
        form.EmploymentPeriods.Add(new()
        {
            PositionType = PositionType.IntroductoryDoctor,
            PositionPercentage = 20,
            EmploymentStartDate = new(2025, 6, 1),
            FundingFrom = new(2025, 6, 1),
            FundingThrough = new(2026, 5, 31)
        });
        form.Certificate.Sessions =
        [
            new() { Date = new(2025, 5, 31), Hours = 3 },
            new() { Date = new(2025, 6, 1), Hours = 2 },
            new() { Date = new(2026, 5, 31), Hours = 4 },
            new() { Date = new(2026, 6, 1), Hours = 3 },
            new() { Hours = 5 }
        ];

        var result = new GrantCalculationService().Calculate(GrantCalculationInputMapper.From(form), Rates());

        Assert.Equal(6m, result.RegisteredGuidanceHours);
        Assert.Equal(13045.3125m, result.GuidanceHonorariumAmount);
    }

    [Fact]
    public void Guidance_honorarium_ignores_non_positive_session_hours()
    {
        var form = Form();
        form.Certificate.Sessions =
        [
            new() { Date = new(2025, 7, 1), Hours = 0 },
            new() { Date = new(2025, 7, 2), Hours = -2 }
        ];

        var result = new GrantCalculationService().Calculate(GrantCalculationInputMapper.From(form), Rates());

        Assert.Equal(0m, result.RegisteredGuidanceHours);
        Assert.Equal(0m, result.GuidanceHonorariumAmount);
    }

    [Fact]
    public void Guidance_honorarium_does_not_change_guidance_amount_or_total()
    {
        var form = Form();
        form.SupervisionExpenses = 50_000m;
        form.Certificate.Sessions = [new() { Date = new(2025, 7, 1), Hours = 6 }];

        var result = new GrantCalculationService().Calculate(GrantCalculationInputMapper.From(form), Rates());

        Assert.Equal(50_000m, result.GuidanceAmount);
        Assert.Equal(50_000m, result.TotalAmount - result.PracticeCompensationAmount - result.LearningActivitiesAmount - result.ProductivityAmount - result.FacilitationAmount - result.CentralitySupplementAmount);
        Assert.NotEqual(result.GuidanceAmount, result.GuidanceHonorariumAmount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10000)]
    [InlineData(200000)]
    public void Historical_requested_productivity_does_not_change_automatic_amount(decimal requested)
    {
        var form = Form(); form.ProductivityRequestedAmount = requested;
        var service = new GrantCalculationService();
        Assert.Equal(125000m, service.Calculate(GrantCalculationInputMapper.From(form), Rates()).ProductivityAmount);
        form.EmploymentPeriods[0].PositionType = PositionType.IntroductoryDoctor;
        Assert.Equal(0m, service.Calculate(GrantCalculationInputMapper.From(form), Rates()).ProductivityAmount);
    }
}
