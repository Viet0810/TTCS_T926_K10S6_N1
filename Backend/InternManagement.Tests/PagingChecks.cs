using InternManagement.DTOs;
using InternManagement.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using System.ComponentModel.DataAnnotations;
internal static class PagingChecks
{
    public static async Task RunAsync(string connectionString,Action<bool,string> check)
    {
        var prefix="Paging-"+Guid.NewGuid().ToString("N");
        await using var connection=new SqlConnection(connectionString);await connection.OpenAsync();
        await using var cmd=connection.CreateCommand();cmd.Parameters.AddWithValue("@prefix",prefix);
        cmd.CommandText="""
            DECLARE @n INT=1;
            WHILE @n<=27 BEGIN
              INSERT dbo.Interns(FullName,Email,Phone,School,Major)
              VALUES(@prefix+CAST(@n AS VARCHAR),@prefix+CAST(@n AS VARCHAR)+'@example.invalid','0912345678','Paging School','Paging Major');
              INSERT dbo.Users(Username,FullName,Email,PasswordHash,Role)
              VALUES(@prefix+CAST(@n AS VARCHAR),@prefix+CAST(@n AS VARCHAR),@prefix+CAST(@n AS VARCHAR)+'@example.invalid','Test-fixture-hash','HR');
              SET @n=@n+1;
            END
            """;
        await cmd.ExecuteNonQueryAsync();
        try
        {
            var service=new ListPagingService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:InternManagement",connectionString}}).Build());
            var first=await service.InternsAsync(new(){Search=prefix},null,default);
            var next=await service.InternsAsync(new(){Page=2,Search=prefix},null,default);
            check(first.Items.Count==10&&first.TotalItems==27&&first.TotalPages==3,"intern server paging is bounded and reports totals");
            check(!first.Items.Select(x=>x.Id).Intersect(next.Items.Select(x=>x.Id)).Any(),"stable paging does not repeat rows across pages");
            var last=await service.InternsAsync(new(){Page=99,Search=prefix},null,default);
            check(last.Page==3&&last.Items.Count==7,"out-of-range page clamps to existing last page");
            var empty=await service.InternsAsync(new(){Page=3,Search=prefix},new(){Search=prefix,School="Missing School"},default);
            check(empty.Page==1&&empty.TotalItems==0&&empty.Items.Count==0,"filter updates totals and clamps empty page to one");
            var users=await service.UsersAsync(new(){Page=2,Search=prefix},default);
            check(users.Page==2&&users.Items.Count==10&&users.TotalItems==27&&users.Items.All(x=>x.Role=="HR"),"account paging returns only requested rows without password hash");
            foreach(var query in new[]{new PageRequest{Page=0},new PageRequest{PageSize=101}})
                check(!Validator.TryValidateObject(query,new ValidationContext(query),[],true),"invalid pagination bounds rejected");
        }
        finally{cmd.CommandText="DELETE dbo.Interns WHERE FullName LIKE @prefix+'%';DELETE dbo.Users WHERE FullName LIKE @prefix+'%'";await cmd.ExecuteNonQueryAsync();}
    }
}
