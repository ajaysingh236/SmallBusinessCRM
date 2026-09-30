-- The ASP.NET Core Identity tables and CRM entities are created by EF Core migrations:
-- 1) InitialIdentityRbac
-- 2) RealTimePipelineAuditAndMail
-- 3) AddConcurrencyTokensAndHardening
--
-- Run: dotnet ef database update
--
-- -------------------------------------------------------------
-- SQL Server Performance Indexes (with filtered index syntax):
-- -------------------------------------------------------------
-- CREATE INDEX IX_Tasks_DueDateUtc_Open ON Tasks(DueDateUtc) WHERE IsCompleted = 0;
-- CREATE INDEX IX_ActivityLogs_LeadId_OccurredUtc ON ActivityLogs(LeadId, OccurredUtc DESC);
-- CREATE INDEX IX_Leads_AssignedUserId ON Leads(AssignedUserId);

-- -------------------------------------------------------------
-- SQLite Compatible Performance Indexes:
-- -------------------------------------------------------------
-- CREATE INDEX IF NOT EXISTS IX_Tasks_DueDateUtc_Open ON Tasks(DueDateUtc) WHERE IsCompleted = 0;
-- CREATE INDEX IF NOT EXISTS IX_ActivityLogs_LeadId_OccurredUtc ON ActivityLogs(LeadId, OccurredUtc DESC);
-- CREATE INDEX IF NOT EXISTS IX_Leads_AssignedUserId ON Leads(AssignedUserId);

-- Administrative audit example: Application code uses IgnoreQueryFilters() only
-- after role authorization has completed.
SELECT Id, Title, Value, AssignedUserId, CreatedAt
FROM Leads;
