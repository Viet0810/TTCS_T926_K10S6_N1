using Microsoft.Data.SqlClient;
namespace InternManagement.Infrastructure;
public static class MentorScheduleMigration
{
 public static async Task ApplyAsync(SqlConnection connection,CancellationToken ct){await using var command=connection.CreateCommand();command.CommandText="""
 IF OBJECT_ID('dbo.MentorScheduleSeries','U') IS NULL BEGIN
 CREATE TABLE dbo.MentorScheduleSeries(Id INT IDENTITY PRIMARY KEY,MentorUserId INT NOT NULL REFERENCES dbo.Users(Id),Title NVARCHAR(200) NOT NULL,ScheduleType VARCHAR(20) NOT NULL CHECK(ScheduleType IN('WORK','MEETING','TRAINING','OTHER')),StartDate DATE NOT NULL,EndDate DATE NOT NULL,StartTime TIME(0) NOT NULL,EndTime TIME(0) NOT NULL,RecurrenceType VARCHAR(20) NOT NULL CHECK(RecurrenceType IN('NONE','WEEKLY','CUSTOM')),WeekDays VARCHAR(20) NULL,Note NVARCHAR(1000) NULL,Status VARCHAR(20) NOT NULL CHECK(Status IN('ACTIVE','PAUSED','CANCELLED')),CreatedBy INT NOT NULL REFERENCES dbo.Users(Id),CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),UpdatedBy INT NULL REFERENCES dbo.Users(Id),UpdatedAt DATETIMEOFFSET NULL,CONSTRAINT CK_MentorScheduleSeries_Date CHECK(StartDate<=EndDate),CONSTRAINT CK_MentorScheduleSeries_Time CHECK(StartTime<EndTime));
 CREATE INDEX IX_MentorScheduleSeries_MentorDates ON dbo.MentorScheduleSeries(MentorUserId,StartDate,EndDate); END;
 IF OBJECT_ID('dbo.MentorScheduleExceptions','U') IS NULL BEGIN
 CREATE TABLE dbo.MentorScheduleExceptions(Id INT IDENTITY PRIMARY KEY,SeriesId INT NOT NULL REFERENCES dbo.MentorScheduleSeries(Id) ON DELETE CASCADE,OccurrenceDate DATE NOT NULL,IsCancelled BIT NOT NULL DEFAULT 0,OverrideTitle NVARCHAR(200) NULL,OverrideStartTime TIME(0) NULL,OverrideEndTime TIME(0) NULL,OverrideNote NVARCHAR(1000) NULL,CreatedBy INT NOT NULL REFERENCES dbo.Users(Id),CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),CONSTRAINT UQ_MentorScheduleExceptions UNIQUE(SeriesId,OccurrenceDate)); END;
 """;await command.ExecuteNonQueryAsync(ct);}
}
