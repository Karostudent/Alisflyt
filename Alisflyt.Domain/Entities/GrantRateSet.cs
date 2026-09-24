using System;

namespace Alisflyt.Domain.Entities
{
    public class GrantRateSet
    {
        public Guid Id { get; private set; }
        public string Name { get; private set; }

        public DateOnly ValidFrom { get; private set; }
        public DateOnly ValidTo { get; private set; }

        // Practice compensation
        public decimal SalaryRate { get; private set; }
        public decimal PracticeCompensationRate { get; private set; }
        public decimal PracticeCompensationMaxHours { get; private set; }

        // Learning activities
        public decimal LearningActivitiesMaxAmount { get; private set; }

        // Lower productivity
        public decimal ProductivityMaxAmount { get; private set; }

        // Guidance
        public decimal GuidanceRate { get; private set; }
        public decimal GuidanceHoursPerYear { get; private set; }

        // Practical facilitation
        public decimal FacilitationRate { get; private set; }

        // Centrality supplement
        public decimal CentralitySupplementMaxAmount { get; private set; }

        public bool IsActive { get; private set; }

        public DateTimeOffset CreatedAtUtc { get; private set; }
        public DateTimeOffset LastModifiedAtUtc { get; private set; }

        private GrantRateSet(
            Guid id,
            string name,
            DateOnly validFrom,
            DateOnly validTo,
            decimal salaryRate,
            decimal practiceCompensationRate,
            decimal practiceCompensationMaxHours,
            decimal learningActivitiesMaxAmount,
            decimal productivityMaxAmount,
            decimal guidanceRate,
            decimal guidanceHoursPerYear,
            decimal facilitationRate,
            decimal centralitySupplementMaxAmount,
            DateTimeOffset createdAtUtc)
        {
            Id = id;
            Name = name;
            ValidFrom = validFrom;
            ValidTo = validTo;

            SalaryRate = salaryRate;
            PracticeCompensationRate = practiceCompensationRate;
            PracticeCompensationMaxHours = practiceCompensationMaxHours;

            LearningActivitiesMaxAmount = learningActivitiesMaxAmount;
            ProductivityMaxAmount = productivityMaxAmount;

            GuidanceRate = guidanceRate;
            GuidanceHoursPerYear = guidanceHoursPerYear;

            FacilitationRate = facilitationRate;
            CentralitySupplementMaxAmount = centralitySupplementMaxAmount;

            IsActive = true;

            CreatedAtUtc = createdAtUtc;
            LastModifiedAtUtc = createdAtUtc;
        }

        public static GrantRateSet Create(
            Guid id,
            string name,
            DateOnly validFrom,
            DateOnly validTo,
            decimal salaryRate,
            decimal practiceCompensationRate,
            decimal practiceCompensationMaxHours,
            decimal learningActivitiesMaxAmount,
            decimal productivityMaxAmount,
            decimal guidanceRate,
            decimal guidanceHoursPerYear,
            decimal facilitationRate,
            decimal centralitySupplementMaxAmount,
            DateTimeOffset createdAtUtc)
        {
            if (id == Guid.Empty)
                throw new ArgumentException(
                    "Id must not be empty.",
                    nameof(id));

            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(
                    "Name must be provided.",
                    nameof(name));

            if (validTo < validFrom)
                throw new ArgumentException(
                    "ValidTo cannot be before ValidFrom.",
                    nameof(validTo));

            if (salaryRate < 0)
                throw new ArgumentOutOfRangeException(nameof(salaryRate));

            if (practiceCompensationRate < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(practiceCompensationRate));

            if (practiceCompensationMaxHours < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(practiceCompensationMaxHours));

            if (learningActivitiesMaxAmount < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(learningActivitiesMaxAmount));

            if (productivityMaxAmount < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(productivityMaxAmount));

            if (guidanceRate < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(guidanceRate));

            if (guidanceHoursPerYear < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(guidanceHoursPerYear));

            if (facilitationRate < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(facilitationRate));

            if (centralitySupplementMaxAmount < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(centralitySupplementMaxAmount));

            return new GrantRateSet(
                id,
                name.Trim(),
                validFrom,
                validTo,
                salaryRate,
                practiceCompensationRate,
                practiceCompensationMaxHours,
                learningActivitiesMaxAmount,
                productivityMaxAmount,
                guidanceRate,
                guidanceHoursPerYear,
                facilitationRate,
                centralitySupplementMaxAmount,
                createdAtUtc);
        }
    }
}