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

    [Theory]
    [InlineData(10, 15812.5)]
    [InlineData(60, 91317.1875)]
    public void Certificate_hours_feed_guidance_and_total_with_annual_cap(int hours, decimal expected)
    {
        var form = Form();
        // Use multiple valid sessions for the 60-hour sample.
        form.Certificate.Sessions = Enumerable.Range(0, hours / 2)
            .Select(i => new SupervisionSessionInputModel { Date = new(2025, 7, 1), Hours = 2 }).ToList();
        var result = new GrantCalculationService().Calculate(GrantCalculationInputMapper.From(form), Rates());
        Assert.Equal(expected, result.GuidanceAmount);
        Assert.Equal(expected * 1.05m + 125000m * 1.05m, result.TotalAmount);
    }

    [Fact]
    public void Sessions_must_be_dated_and_within_period_and_are_counted_once()
    {
        var form = Form();
        form.EmploymentPeriods.Add(new() { PositionType = PositionType.IntroductoryDoctor, PositionPercentage = 20,
            EmploymentStartDate = new(2025, 6, 1), FundingFrom = new(2025, 6, 1), FundingThrough = new(2026, 5, 31) });
        form.Certificate.Sessions = [new() { Date = new(2025, 5, 31), Hours = 3 },
            new() { Date = new(2025, 6, 1), Hours = 2 }, new() { Date = new(2026, 5, 31), Hours = 4 },
            new() { Date = new(2026, 6, 1), Hours = 3 }, new() { Hours = 5 }];
        Assert.Equal(6m, GrantCalculationInputMapper.From(form).SupervisionHours);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(10000, 10000)]
    [InlineData(200000, 125000)]
    public void Requested_productivity_is_capped_by_eligibility(decimal requested, decimal expected)
    {
        var form = Form(); form.ProductivityRequestedAmount = requested;
        var result = new GrantCalculationService().Calculate(GrantCalculationInputMapper.From(form), Rates());
        Assert.Equal(expected, result.ProductivityAmount);
        form.EmploymentPeriods[0].PositionType = PositionType.IntroductoryDoctor;
        Assert.Equal(0, new GrantCalculationService().Calculate(GrantCalculationInputMapper.From(form), Rates()).ProductivityAmount);
    }

    [Fact]
    public void Out_of_period_sessions_do_not_fall_back_to_legacy_expenses()
    {
        var form = Form(); form.SupervisionExpenses = 50000;
        form.Certificate.Sessions = [new() { Date = new(2026, 6, 1), Hours = 4 }];
        Assert.Equal(0, new GrantCalculationService().Calculate(GrantCalculationInputMapper.From(form), Rates()).GuidanceAmount);
        form.Certificate.Sessions.Clear();
        Assert.Equal(50000, new GrantCalculationService().Calculate(GrantCalculationInputMapper.From(form), Rates()).GuidanceAmount);
    }
}
