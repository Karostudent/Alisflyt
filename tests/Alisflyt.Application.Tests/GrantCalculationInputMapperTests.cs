using System;
using System.Collections.Generic;
using Alisflyt.Application.Models;
using Alisflyt.Domain.Forms;
using Xunit;

namespace Alisflyt.Application.Tests
{
    public class GrantCalculationInputMapperTests
    {
        [Fact]
        public void From_MapsCompleteGrantApplicationData()
        {
            // Arrange
            var app = new GrantApplicationData
            {
                GrantType = GrantType.AlisAgreementIncludingSupervision,
                AbsenceCompensation = 100m,
                LearningActivityExpenses = 200m,
                SupervisionExpenses = 300m,
                AdditionalSupervisionCosts = 400m,
                EmploymentPeriods = new List<EmploymentPeriodInputModel>
                {
                    new EmploymentPeriodInputModel
                    {
                        PositionType = PositionType.RegularGpOrLocum,
                        PositionPercentage = 50m,
                        EmploymentStartDate = new DateOnly(2025, 6, 1),
                        FundingFrom = new DateOnly(2025, 7, 1),
                        FundingThrough = new DateOnly(2026, 5, 31)
                    }
                }
            };

            // Act
            var result = GrantCalculationInputMapper.From(app);

            // Assert
            Assert.Equal(
                GrantType.AlisAgreementIncludingSupervision,
                result.GrantType);

            Assert.Equal(100m, result.AbsenceCompensation);
            Assert.Equal(200m, result.LearningActivityExpenses);
            Assert.Equal(300m, result.SupervisionExpenses);
            Assert.Equal(400m, result.AdditionalSupervisionCosts);

            var period = Assert.Single(result.EmploymentPeriods);

            Assert.Equal(
                PositionType.RegularGpOrLocum,
                period.PositionType);

            Assert.Equal(50m, period.PositionPercentage);
            Assert.Equal(
                new DateOnly(2025, 6, 1),
                period.EmploymentStartDate);

            Assert.Equal(
                new DateOnly(2025, 7, 1),
                period.FundingFrom);

            Assert.Equal(
                new DateOnly(2026, 5, 31),
                period.FundingThrough);
        }

        [Fact]
        public void From_NullableCostFieldsBecomeZero()
        {
            // Arrange
            var app = new GrantApplicationData
            {
                GrantType = GrantType.AlisAgreementIncludingSupervision,
                AbsenceCompensation = null,
                LearningActivityExpenses = null,
                SupervisionExpenses = null,
                AdditionalSupervisionCosts = null,
                EmploymentPeriods = new List<EmploymentPeriodInputModel>
                {
                    new EmploymentPeriodInputModel
                    {
                        PositionType = PositionType.RegularGpOrLocum,
                        PositionPercentage = 100m,
                        EmploymentStartDate = new DateOnly(2025, 6, 1),
                        FundingFrom = new DateOnly(2025, 6, 1),
                        FundingThrough = new DateOnly(2026, 5, 31)
                    }
                }
            };

            // Act
            var result = GrantCalculationInputMapper.From(app);

            // Assert
            Assert.Equal(0m, result.AbsenceCompensation);
            Assert.Equal(0m, result.LearningActivityExpenses);
            Assert.Equal(0m, result.SupervisionExpenses);
            Assert.Equal(0m, result.AdditionalSupervisionCosts);
        }

        [Fact]
        public void From_MissingGrantType_ThrowsArgumentException()
        {
            // Arrange
            var app = new GrantApplicationData
            {
                GrantType = null,
                EmploymentPeriods = new List<EmploymentPeriodInputModel>()
            };

            // Act
            var exception = Assert.Throws<ArgumentException>(
                () => GrantCalculationInputMapper.From(app));

            // Assert
            Assert.Contains(
                "Grant type is required for calculation.",
                exception.Message);
        }
    }
}