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
