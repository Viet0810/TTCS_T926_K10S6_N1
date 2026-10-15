using Microsoft.Data.SqlClient;
namespace InternManagement.Infrastructure;
public static class EvaluationAuditMigration {
 public static async Task ApplyAsync(SqlConnection c,CancellationToken ct) {
  await using var q=new SqlCommand("""
   IF COL_LENGTH('dbo.FinalEvaluations','CreatedByUserId') IS NULL
    ALTER TABLE dbo.FinalEvaluations ADD CreatedByUserId INT NULL REFERENCES dbo.Users(Id),EvaluationSource VARCHAR(10) NOT NULL DEFAULT 'LEGACY',UpdatedAt DATETIMEOFFSET NULL;
   """,c);await q.ExecuteNonQueryAsync(ct);
 }
}
