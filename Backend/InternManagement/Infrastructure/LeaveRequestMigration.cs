using Microsoft.Data.SqlClient;

namespace InternManagement.Infrastructure;

public static class LeaveRequestMigration
{
    public static async Task ApplyAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID(N'dbo.LeaveRequests', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.LeaveRequests (
                    Id BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LeaveRequests PRIMARY KEY,
                    InternId INT NOT NULL CONSTRAINT FK_LeaveRequests_Interns REFERENCES dbo.Interns(Id) ON DELETE CASCADE,
                    UserId INT NOT NULL CONSTRAINT FK_LeaveRequests_Users REFERENCES dbo.Users(Id),
                    LeaveType VARCHAR(30) NOT NULL CONSTRAINT CK_LeaveRequests_Type CHECK (LeaveType IN ('SICK_LEAVE', 'ACADEMIC_LEAVE', 'PERSONAL_LEAVE', 'OTHER')),
                    DurationType VARCHAR(20) NOT NULL CONSTRAINT CK_LeaveRequests_Duration CHECK (DurationType IN ('FULL_DAY', 'MORNING', 'AFTERNOON', 'MULTI_DAY')),
                    StartDate DATE NOT NULL,
                    EndDate DATE NOT NULL,
                    TotalDays DECIMAL(3,1) NOT NULL CONSTRAINT CK_LeaveRequests_Days CHECK (TotalDays > 0 AND TotalDays <= 30.0),
                    Reason NVARCHAR(1000) NOT NULL,
                    Status VARCHAR(20) NOT NULL CONSTRAINT DF_LeaveRequests_Status DEFAULT 'PENDING' CONSTRAINT CK_LeaveRequests_Status CHECK (Status IN ('PENDING', 'APPROVED', 'REJECTED', 'CANCELLED')),
                    ReviewerId INT NULL CONSTRAINT FK_LeaveRequests_Reviewer REFERENCES dbo.Users(Id),
                    ReviewerNote NVARCHAR(1000) NULL,
                    ReviewedAt DATETIME2 NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_LeaveRequests_CreatedAt DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_LeaveRequests_UpdatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT CK_LeaveRequests_DateRange CHECK (StartDate <= EndDate)
                );

                CREATE NONCLUSTERED INDEX IX_LeaveRequests_InternId_Status_Dates 
                ON dbo.LeaveRequests (InternId, Status, StartDate, EndDate)
                INCLUDE (LeaveType, DurationType, TotalDays, CreatedAt);
            END;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
