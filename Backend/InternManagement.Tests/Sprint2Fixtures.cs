using Microsoft.Data.SqlClient;
internal static class Sprint2Fixtures
{
    // Isolated SQL fixtures for RBAC/assignment tests; review transitions are tested separately via API.
    public static async Task ApprovedAsync(string connectionString,int internId,bool contract=false)
    {
        await using var connection=new SqlConnection(connectionString);await connection.OpenAsync();
        await using var command=connection.CreateCommand();
        command.CommandText="""
            INSERT dbo.InternDocuments(InternId,Kind,FileName,Content,ReviewStatus,ReviewedAt)
            VALUES(@id,'application','application.pdf',0x255044462D,'approved',SYSUTCDATETIME());
            IF @contract=1 INSERT dbo.InternContracts(InternId,FileName,Content,UploadedBy)
                SELECT @id,'contract.pdf',0x255044462D,Id FROM dbo.Users WHERE Username='permission-hr';
            """;
        command.Parameters.AddWithValue("@id",internId);command.Parameters.AddWithValue("@contract",contract);await command.ExecuteNonQueryAsync();
    }
}
