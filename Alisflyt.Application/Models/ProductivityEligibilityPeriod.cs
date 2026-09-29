using System;
using System.Collections.Generic;
using System.Text;

namespace Alisflyt.Application.Models
{
    public sealed record ProductivityEligibilityPeriod(
    DateOnly From,
    DateOnly Through);
}
