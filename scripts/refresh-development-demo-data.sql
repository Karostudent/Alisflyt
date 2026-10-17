-- Run against AlisflytDevelopment using sqlcmd -b -i scripts/refresh-development-demo-data.sql.
-- Keep the original form example and the four hard-coded ALIS-DEM cases.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF DB_NAME() <> N'AlisflytDevelopment'
    THROW 50000, 'This script is only for AlisflytDevelopment.', 1;

DECLARE @sourceId uniqueidentifier = '81252649-9ee1-4924-8a80-c762fd4e3da3';
IF NOT EXISTS (SELECT 1 FROM dbo.GrantCases WHERE Id = @sourceId
    AND HprNumber = N'HPR-1911-2003' AND ISJSON(ApplicationDataJson) = 1)
    THROW 50001, 'The original HPR-1911-2003 demo application is missing.', 1;

DECLARE @examples TABLE (
    Id uniqueidentifier, CaseNumber nvarchar(100), HprNumber nvarchar(50),
    DoctorName nvarchar(100), SupervisorName nvarchar(100),
    SupervisorHprNumber nvarchar(50), EmploymentPercentage decimal(5,2)
);
INSERT INTO @examples VALUES
('66666666-6666-4666-8666-666666666661', N'ALIS-DEMO-FORM-001', N'HPR-1912-2004', N'Anna Berg', N'Ingrid Solheim', N'HPR-2711-2012', 100),
('66666666-6666-4666-8666-666666666662', N'ALIS-DEMO-FORM-002', N'HPR-1913-2005', N'Martin Hansen', N'Lars Nilsen', N'HPR-2712-2013', 80),
('66666666-6666-4666-8666-666666666663', N'ALIS-DEMO-FORM-003', N'HPR-1914-2006', N'Sara Dahl', N'Kari Holm', N'HPR-2713-2014', 60),
('66666666-6666-4666-8666-666666666664', N'ALIS-DEMO-FORM-004', N'HPR-1915-2007', N'Jonas Vik', N'Per Strand', N'HPR-2714-2015', 90),
('66666666-6666-4666-8666-666666666665', N'ALIS-DEMO-FORM-005', N'HPR-1916-2008', N'Emma Lie', N'Marit Lund', N'HPR-2715-2016', 50);

INSERT INTO dbo.GrantCases (Id, CaseNumber, HprNumber, EmploymentPercentage,
    EmploymentStartDate, EmploymentEndDate, Status, CreatedAtUtc, LastModifiedAtUtc,
    ApplicationDataJson, ReturnReason, ReturnedAtUtc)
SELECT e.Id, e.CaseNumber, e.HprNumber, e.EmploymentPercentage,
    source.EmploymentStartDate, source.EmploymentEndDate, source.Status,
    TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'), TODATETIMEOFFSET(SYSUTCDATETIME(), '+00:00'),
    JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(
    JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(source.ApplicationDataJson,
        '$.HprNumber', e.HprNumber),
        '$.DoctorName', e.DoctorName),
        '$.DoctorProfessions', JSON_QUERY(N'["Lege"]')),
        '$.Certificate.DoctorName', e.DoctorName),
        '$.Certificate.SupervisorName', e.SupervisorName),
        '$.Certificate.SupervisorHprNumber', e.SupervisorHprNumber),
        '$.EmploymentPeriods[0].PositionPercentage', e.EmploymentPercentage),
        '$.AbsenceCompensation', CAST(25000 AS decimal(18,2))),
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
