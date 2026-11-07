-- Run against AlisflytDevelopment using sqlcmd -b -i scripts/refresh-development-demo-data.sql.
-- Keep the original form example and three detailed ALIS demo cases.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF DB_NAME() <> N'AlisflytDevelopment'
    THROW 50000, 'This script is only for AlisflytDevelopment.', 1;

DECLARE @sourceId uniqueidentifier = '11111111-1111-1111-1111-111111111111';
IF NOT EXISTS (SELECT 1 FROM dbo.GrantCases WHERE Id = @sourceId
    AND HprNumber = N'HPR-DEM-1' AND ISJSON(ApplicationDataJson) = 1)
    THROW 50001, 'The ALIS-DEM-001 demo application is missing.', 1;

DECLARE @examples TABLE (
    Id uniqueidentifier, CaseNumber nvarchar(100), HprNumber nvarchar(50),
    DoctorName nvarchar(100), SupervisorName nvarchar(100),
    SupervisorHprNumber nvarchar(50), EmploymentPercentage decimal(5,2),
    AbsenceCompensation decimal(18,2), LearningActivityExpenses decimal(18,2),
    SupervisionExpenses decimal(18,2), CalculationBasisJson nvarchar(max)
);
INSERT INTO @examples VALUES
('66666666-6666-4666-8666-666666666661', N'ALIS-DEMO-BASIS-001', N'123456789', N'Anna Hansen', N'Ingrid Solheim', N'HPR-2711-2012', 100, 37150, 8000, 42000,
 N'{"LearningActivities":[{"Description":"Grunnkurs B - Bodø","Date":"2025-09-16","Hours":null,"Days":5},{"Description":"Emnekurs - Endokrinologi i allmennpraksis","Date":"2025-10-22","Hours":null,"Days":2},{"Description":"Hospitering ved sykehus","Date":"2025-11-11","Hours":4,"Days":null}],"StudyAndSpecializationDays":[{"Description":"Svangerskapsoppfølging","Date":"2025-10-16","Hours":4,"Days":null},{"Description":"ALM033 - lest artikkel og litteratur","Date":"2025-11-13","Hours":2,"Days":null}],"Expenses":[{"Description":"Emnekurs - Endokrinologi i allmennpraksis","Date":"2025-10-22","TravelExpense":0,"CourseExpense":6000},{"Description":"Grunnkurs B Bodø","Date":"2025-09-16","TravelExpense":2000,"CourseExpense":0}],"IndividualGuidanceHours":42,"GroupGuidanceHours":null}'),
('66666666-6666-4666-8666-666666666662', N'ALIS-DEMO-BASIS-002', N'123456789', N'Anna Hansen', N'Lars Nilsen', N'HPR-2712-2013', 80, 19800, 4500, 25500,
 N'{"LearningActivities":[{"Description":"Kurs i akuttmedisin","Date":"2025-08-12","Hours":6,"Days":null},{"Description":"Hospitering på legevakt","Date":"2025-09-05","Hours":8,"Days":null}],"StudyAndSpecializationDays":[{"Description":"Fordypning: kroniske sykdommer","Date":"2025-10-03","Hours":4,"Days":null}],"Expenses":[{"Description":"Kurs i akuttmedisin","Date":"2025-08-12","TravelExpense":1200,"CourseExpense":4500}],"IndividualGuidanceHours":24,"GroupGuidanceHours":2}'),
('66666666-6666-4666-8666-666666666663', N'ALIS-DEMO-BASIS-003', N'123456789', N'Anna Hansen', N'Kari Holm', N'HPR-2713-2014', 60, 0, 12500, 18000,
 N'{"LearningActivities":[],"StudyAndSpecializationDays":[{"Description":"Studie- og fordypningsdag: veiledning","Date":"2025-12-01","Hours":8,"Days":null},{"Description":"Studie- og fordypningsdag: fagmøte","Date":"2026-01-15","Hours":8,"Days":null}],"Expenses":[{"Description":"Fagmøte og kursmateriell","Date":"2026-01-15","TravelExpense":3500,"CourseExpense":9000}],"IndividualGuidanceHours":12,"GroupGuidanceHours":4}');

DELETE FROM dbo.GrantCases
WHERE Id IN (
    '66666666-6666-4666-8666-666666666661',
    '66666666-6666-4666-8666-666666666662',
    '66666666-6666-4666-8666-666666666663'
);

INSERT INTO dbo.GrantCases (Id, CaseNumber, HprNumber, EmploymentPercentage,
    EmploymentStartDate, EmploymentEndDate, Status, CreatedAtUtc, LastModifiedAtUtc,
    ApplicationDataJson, ReturnReason, ReturnedAtUtc)
SELECT e.Id, e.CaseNumber, e.HprNumber, e.EmploymentPercentage,
    source.EmploymentStartDate, source.EmploymentEndDate, source.Status,
    TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'), TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'),
    JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(
    JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(source.ApplicationDataJson,
        '$.HprNumber', e.HprNumber),
        '$.DoctorName', e.DoctorName),
        '$.DoctorProfessions', JSON_QUERY(N'["Lege"]')),
        '$.Certificate.DoctorName', e.DoctorName),
        '$.Certificate.SupervisorName', e.SupervisorName),
        '$.Certificate.SupervisorHprNumber', e.SupervisorHprNumber),
        '$.EmploymentPeriods[0].PositionPercentage', e.EmploymentPercentage),
        '$.AbsenceCompensation', e.AbsenceCompensation),
        '$.LearningActivityExpenses', e.LearningActivityExpenses),
        '$.SupervisionExpenses', e.SupervisionExpenses),
        '$.CalculationBasis', JSON_QUERY(e.CalculationBasisJson)),
        '$.SupervisorApprovedAtUtc', NULL),
    NULL, NULL
FROM @examples e
CROSS JOIN dbo.GrantCases source
WHERE source.Id = @sourceId
    AND NOT EXISTS (SELECT 1 FROM dbo.GrantCases existing
        WHERE existing.Id = e.Id OR existing.CaseNumber = e.CaseNumber);

-- Only the three inspected legacy test records without application forms.
DELETE FROM dbo.GrantCases
WHERE ApplicationDataJson IS NULL AND (
    (Id = 'bbe9d2a8-2c6a-453a-9d7d-49ec4238f8ed' AND CaseNumber = N'ALIS-30B64850') OR
    (Id = '2a97c877-ef2a-4793-817b-53b72144f2bf' AND CaseNumber = N'ALIS-12712519') OR
    (Id = '8847174c-3925-4d8a-8878-ef669e3b5235' AND CaseNumber = N'ALIS-BDBCEA0D')
);

COMMIT TRANSACTION;

SELECT CaseNumber, HprNumber, EmploymentPercentage, Status
FROM dbo.GrantCases ORDER BY CaseNumber;
