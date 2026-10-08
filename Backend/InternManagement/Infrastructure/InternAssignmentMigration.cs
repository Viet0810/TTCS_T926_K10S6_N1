using Microsoft.Data.SqlClient;

namespace InternManagement.Infrastructure;

public static class InternAssignmentMigration
{
    public static async Task ApplyAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF COL_LENGTH(N'dbo.Interns', N'MentorUserId') IS NULL
                ALTER TABLE dbo.Interns ADD MentorUserId INT NULL;
            IF COL_LENGTH(N'dbo.Interns', N'InternshipProgramId') IS NULL
                ALTER TABLE dbo.Interns ADD InternshipProgramId INT NULL;
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
        command.CommandText = """
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Interns_MentorUser')
                ALTER TABLE dbo.Interns ADD CONSTRAINT FK_Interns_MentorUser
                    FOREIGN KEY (MentorUserId) REFERENCES dbo.Users(Id) ON DELETE SET NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_Interns_InternshipProgram')
                ALTER TABLE dbo.Interns ADD CONSTRAINT FK_Interns_InternshipProgram
                    FOREIGN KEY (InternshipProgramId) REFERENCES dbo.InternshipPrograms(Id) ON DELETE SET NULL;
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Interns') AND name = 'IX_Interns_MentorUserId')
                CREATE INDEX IX_Interns_MentorUserId ON dbo.Interns(MentorUserId);
            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID('dbo.Interns') AND name = 'IX_Interns_InternshipProgramId')
                CREATE INDEX IX_Interns_InternshipProgramId ON dbo.Interns(InternshipProgramId);
            """;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
