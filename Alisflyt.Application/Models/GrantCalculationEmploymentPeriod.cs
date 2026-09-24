using Alisflyt.Domain.Forms;
using System;
using System.Collections.Generic;
using System.Text;

namespace Alisflyt.Application.Models;

   public sealed record GrantCalculationEmploymentPeriod(
    PositionType PositionType,
    decimal PositionPercentage,
    DateOnly EmploymentStartDate,
    DateOnly FundingFrom,
    DateOnly FundingThrough);
