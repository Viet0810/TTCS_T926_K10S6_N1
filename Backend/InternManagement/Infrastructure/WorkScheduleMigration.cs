using Microsoft.Data.SqlClient;
namespace InternManagement.Infrastructure;
public static class WorkScheduleMigration
{
    public static async Task ApplyAsync(SqlConnection connection,CancellationToken ct)
    {
        await using var tx=(SqlTransaction)await connection.BeginTransactionAsync(System.Data.IsolationLevel.Serializable,ct);
        await using var q=new SqlCommand("""
            IF OBJECT_ID('dbo.WorkSchedules','U') IS NULL
            CREATE TABLE dbo.WorkSchedules(
                Id INT IDENTITY PRIMARY KEY,InternId INT NOT NULL REFERENCES dbo.Interns(Id),
                WorkDate DATE NOT NULL,StartTime TIME(0) NOT NULL,EndTime TIME(0) NOT NULL,Note NVARCHAR(1000) NULL,
                CreatedBy INT NOT NULL REFERENCES dbo.Users(Id),CreatedAt DATETIMEOFFSET NOT NULL DEFAULT SYSDATETIMEOFFSET(),
                UpdatedAt DATETIMEOFFSET NULL,CONSTRAINT UQ_WorkSchedules_InternDate UNIQUE(InternId,WorkDate),
                CONSTRAINT CK_WorkSchedules_Time CHECK(StartTime<EndTime));
            IF COL_LENGTH('dbo.AttendanceRecords','ScheduledStart') IS NULL
                ALTER TABLE dbo.AttendanceRecords ADD ScheduledStart TIME(0) NULL,ScheduledEnd TIME(0) NULL;
            IF NOT EXISTS(SELECT 1 FROM dbo.PermissionMigrations WITH(UPDLOCK,HOLDLOCK) WHERE Name='US23WorkSchedules')
            BEGIN
                INSERT dbo.RolePermissions(Role,Permission)
                SELECT r.Role,p.Permission FROM (VALUES('ADMIN'),('HR'),('INTERN')) r(Role)
                CROSS JOIN (VALUES('WORK_SCHEDULE_READ'),('WORK_SCHEDULE_CREATE'),('WORK_SCHEDULE_UPDATE')) p(Permission)
                WHERE (r.Role<>'INTERN' OR p.Permission='WORK_SCHEDULE_READ')
                  AND NOT EXISTS(SELECT 1 FROM dbo.RolePermissions x WHERE x.Role=r.Role AND x.Permission=p.Permission);
                INSERT dbo.PermissionMigrations(Name) VALUES('US23WorkSchedules');
            END
            """,connection,tx);
        await q.ExecuteNonQueryAsync(ct);await tx.CommitAsync(ct);
    }
}