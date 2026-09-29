using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Alisflyt.Application.Services;
using Alisflyt.Application.Tests.Fakes;
using Alisflyt.Domain.Forms;
using Alisflyt.Application.Models;
using Xunit;

namespace Alisflyt.Application.Tests
{
    public class GrantCasesPersistenceTests
    {
        private static GrantCaseApplicationService Service(FakeGrantCaseRepository repo) =>
            new(repo, new FakeCaseNumberGenerator("T-1"), TimeProvider.System);

        private static GrantApplicationData SampleApplicationData()
        {
            return new GrantApplicationData
            {
                HprNumber = "123456",
                DoctorName = "Dr Test",
                DoctorProfessions = new List<string> { "Allmennlege", "Undervisning" },
                GrantType = GrantType.AlisAgreementIncludingSupervision,
                FirstRegularGpOrLocumDate = new DateOnly(2022,1,1),
                SelectedPositionTypes = new List<PositionType> { PositionType.RegularGpOrLocum, PositionType.IntroductoryDoctor },
                EmploymentPeriods = new List<EmploymentPeriodInputModel>
                {
                    new() { PositionType = PositionType.RegularGpOrLocum, PositionPercentage = 50, EmploymentStartDate = new DateOnly(2023,1,1), FundingFrom = new DateOnly(2023,1,1), FundingThrough = new DateOnly(2023,12,31) },
                    new() { PositionType = PositionType.IntroductoryDoctor, PositionPercentage = 20, EmploymentStartDate = new DateOnly(2022,6,1), FundingFrom = new DateOnly(2022,6,1), FundingThrough = new DateOnly(2022,12,31) }
                },
                AbsenceCompensation = 100m,
                LearningActivityExpenses = 200m,
                SupervisionExpenses = 300m,
                HasAdditionalSupervisionCosts = true,
                AdditionalSupervisionCosts = 400m,
                IsCentralityGrade6 = true,
                CentralitySupplementRequestedAmount = 75000m,
                Certificate = new SupervisionCertificateViewModel
                {
                    SupervisorName = "Supervisor",
                    DoctorName = "Dr Test",
                    DoctorSigningPlace = "Kommune",
                    DoctorSigningDate = new DateOnly(2024,1,1),
                    SupervisorSigningPlace = "Kommune",
                    SupervisorSigningDate = new DateOnly(2024,1,2),
                    Sessions = new System.Collections.Generic.List<SupervisionSessionInputModel>
                    {
                        new() { Date = new DateOnly(2024,2,1), Hours = 1.5m, Topic = "Tema A" },
                        new() { Date = new DateOnly(2024,3,1), Hours = 2m, Topic = "Tema B" }
                    }
                }
            };
        }

        [Fact]
        public async Task Create_Save_Get_Update_Get_PersistsAllApplicationData()
        {
            var repo = new FakeGrantCaseRepository();
            var svc = Service(repo);
            var data = SampleApplicationData();
            var createReq = new CreateGrantCaseRequest { ApplicationData = data, HprNumber = data.HprNumber };

            var created = await svc.CreateDraftAsync(createReq);
            var id = created.Id;

            var loaded1 = await svc.GetByIdAsync(id);
            var a1 = loaded1.ApplicationData!;

            // basic checks
            Assert.Equal(data.HprNumber, a1.HprNumber);
            Assert.Equal(data.DoctorName, a1.DoctorName);
            Assert.Equal(data.DoctorProfessions, a1.DoctorProfessions);
            Assert.Equal(data.GrantType, a1.GrantType);
            Assert.Equal(data.FirstRegularGpOrLocumDate, a1.FirstRegularGpOrLocumDate);
            Assert.Equal(data.SelectedPositionTypes, a1.SelectedPositionTypes);

            Assert.Equal(data.EmploymentPeriods.Count, a1.EmploymentPeriods.Count);
            for (int i=0;i<data.EmploymentPeriods.Count;i++){
                var expected = data.EmploymentPeriods[i];
                var actual = a1.EmploymentPeriods[i];
                Assert.Equal(expected.PositionType, actual.PositionType);
                Assert.Equal(expected.PositionPercentage, actual.PositionPercentage);
                Assert.Equal(expected.EmploymentStartDate, actual.EmploymentStartDate);
                Assert.Equal(expected.FundingFrom, actual.FundingFrom);
                Assert.Equal(expected.FundingThrough, actual.FundingThrough);
            }

            Assert.Equal(data.AbsenceCompensation, a1.AbsenceCompensation);
            Assert.Equal(data.LearningActivityExpenses, a1.LearningActivityExpenses);
            Assert.Equal(data.SupervisionExpenses, a1.SupervisionExpenses);
            Assert.Equal(data.HasAdditionalSupervisionCosts, a1.HasAdditionalSupervisionCosts);
            Assert.Equal(data.AdditionalSupervisionCosts, a1.AdditionalSupervisionCosts);
            Assert.Equal(data.IsCentralityGrade6, a1.IsCentralityGrade6);
            Assert.Equal(data.CentralitySupplementRequestedAmount, a1.CentralitySupplementRequestedAmount);

            // Certificate
            Assert.Equal(data.Certificate.SupervisorName, a1.Certificate.SupervisorName);
            Assert.Equal(data.Certificate.DoctorName, a1.Certificate.DoctorName);
            Assert.Equal(data.Certificate.DoctorSigningPlace, a1.Certificate.DoctorSigningPlace);
            Assert.Equal(data.Certificate.DoctorSigningDate, a1.Certificate.DoctorSigningDate);
            Assert.Equal(data.Certificate.SupervisorSigningPlace, a1.Certificate.SupervisorSigningPlace);
            Assert.Equal(data.Certificate.SupervisorSigningDate, a1.Certificate.SupervisorSigningDate);
            Assert.Equal(2, a1.Certificate.Sessions.Count);
            Assert.Equal(data.Certificate.Sessions[0].Date, a1.Certificate.Sessions[0].Date);
            Assert.Equal(data.Certificate.Sessions[0].Hours, a1.Certificate.Sessions[0].Hours);
            Assert.Equal(data.Certificate.Sessions[0].Topic, a1.Certificate.Sessions[0].Topic);

            // Now update: change a few fields and add a session
            a1.DoctorName = "Dr Changed";
            a1.Certificate.Sessions.Add(new SupervisionSessionInputModel { Date = new DateOnly(2024,4,1), Hours = 1m, Topic = "Tema C" });
            var updateReq = new UpdateGrantCaseDraftRequest { ApplicationData = a1 };
            var updated = await svc.UpdateDraftAsync(id, updateReq);

            var loaded2 = await svc.GetByIdAsync(id);
            var a2 = loaded2.ApplicationData!;
            Assert.Equal("Dr Changed", a2.DoctorName);
            Assert.Equal(3, a2.Certificate.Sessions.Count);
            Assert.Equal("Tema C", a2.Certificate.Sessions[2].Topic);
        }

        [Fact]
        public async Task EmploymentPeriods_And_CertificateSessions_RemoveMiddleRow_PersistsCorrectly()
        {
            var repo = new FakeGrantCaseRepository();
            var svc = Service(repo);
            var data = SampleApplicationData();

            // extend to 3 employment periods and 3 sessions
            data.EmploymentPeriods.Insert(1, new EmploymentPeriodInputModel { PositionType = PositionType.GeneralPractitionerOutsideRegularGpScheme, PositionPercentage = 10, EmploymentStartDate = new DateOnly(2021,1,1), FundingFrom = new DateOnly(2021,1,1), FundingThrough = new DateOnly(2021,12,31) });
            data.Certificate.Sessions.Insert(1, new SupervisionSessionInputModel { Date = new DateOnly(2024,5,1), Hours = 0.5m, Topic = "Mid" });

            var created = await svc.CreateDraftAsync(new CreateGrantCaseRequest { ApplicationData = data, HprNumber = data.HprNumber });
            var id = created.Id;

            var loaded1 = await svc.GetByIdAsync(id);
            var a1 = loaded1.ApplicationData!;
            Assert.Equal(3, a1.EmploymentPeriods.Count);
            Assert.Equal(3, a1.Certificate.Sessions.Count);

            // remove middle elements
            a1.EmploymentPeriods.RemoveAt(1);
            a1.Certificate.Sessions.RemoveAt(1);

            await svc.UpdateDraftAsync(id, new UpdateGrantCaseDraftRequest { ApplicationData = a1 });

            var loaded2 = await svc.GetByIdAsync(id);
            var a2 = loaded2.ApplicationData!;
            Assert.Equal(2, a2.EmploymentPeriods.Count);
            Assert.Equal(2, a2.Certificate.Sessions.Count);
            // ensure order preserved: remaining should be original indices 0 and 2
            Assert.Equal(data.EmploymentPeriods[0].PositionType, a2.EmploymentPeriods[0].PositionType);
            Assert.Equal(data.EmploymentPeriods[2].PositionType, a2.EmploymentPeriods[1].PositionType);
            Assert.Equal(data.Certificate.Sessions[0].Topic, a2.Certificate.Sessions[0].Topic);
            Assert.Equal(data.Certificate.Sessions[2].Topic, a2.Certificate.Sessions[1].Topic);
        }
    }
}
