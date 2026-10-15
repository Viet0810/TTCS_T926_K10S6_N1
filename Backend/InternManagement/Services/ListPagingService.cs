using System.Data;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;
namespace InternManagement.Services;

// Read-only paging; existing CRUD services and array endpoints remain unchanged.
public sealed class ListPagingService(IConfiguration config)
{
    private readonly string connectionString=config.GetConnectionString("InternManagement")!;
    public async Task<PageResponse<InternResponse>> InternsAsync(PageRequest page,InternFilterRequest? filter,CancellationToken ct)
        =>await ReadAsync("Interns",InternRecordMapper.Columns,page,filter,InternRecordMapper.Map,ct);
    public async Task<PageResponse<UserResponse>> UsersAsync(PageRequest page,CancellationToken ct,string? requesterRole=null)
        =>await ReadAsync("Users","Id,Username,FullName,Email,Role,MustChangePassword",page,null,
            r=>new UserResponse(r.GetInt32(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetBoolean(5)),ct,requesterRole);
    private async Task<PageResponse<T>> ReadAsync<T>(string table,string columns,PageRequest page,InternFilterRequest? filter,Func<SqlDataReader,T> map,CancellationToken ct,string? requesterRole=null)
    {
        await using var connection=new SqlConnection(connectionString);await connection.OpenAsync(ct);
        await using var cmd=connection.CreateCommand();var where=" WHERE 1=1";
        if(table=="Users")
        {
            where+=" AND Role <> 'ADMIN'";
            if(string.Equals(requesterRole,"HR",StringComparison.OrdinalIgnoreCase))where+=" AND Role = 'MENTOR'";
        }
        var search=filter?.Search??page.Search;
        if(!string.IsNullOrWhiteSpace(search))
        {
            where+=table=="Users"?" AND (FullName LIKE @search OR Email LIKE @search OR Role LIKE @search)":
                " AND (FullName LIKE @search OR Email LIKE @search OR Phone LIKE @search OR School LIKE @search OR Major LIKE @search OR StudentCode LIKE @search OR ClassName LIKE @search OR Organization LIKE @search OR Position LIKE @search OR Mentor LIKE @search)";
            cmd.Parameters.Add("@search",SqlDbType.NVarChar,256).Value="%"+search.Trim()+"%";
        }
        if(table=="Interns") foreach(var (column,value) in new[]{("School",filter?.School),("Major",filter?.Major),("Status",filter?.Status)})
            if(!string.IsNullOrWhiteSpace(value)){where+=$" AND {column}=@{column}";cmd.Parameters.Add("@"+column,SqlDbType.NVarChar,200).Value=value.Trim();}
        cmd.CommandText=$"SELECT COUNT(*) FROM dbo.{table}{where}";
        var total=Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));var pages=(int)Math.Ceiling(total/(double)page.PageSize);var current=Math.Min(page.Page,Math.Max(1,pages));
        cmd.Parameters.Add("@offset",SqlDbType.Int).Value=(current-1)*page.PageSize;cmd.Parameters.Add("@size",SqlDbType.Int).Value=page.PageSize;
        cmd.CommandText=$"SELECT {columns} FROM dbo.{table}{where} ORDER BY Id DESC OFFSET @offset ROWS FETCH NEXT @size ROWS ONLY";
        await using var reader=await cmd.ExecuteReaderAsync(ct);var items=new List<T>();while(await reader.ReadAsync(ct))items.Add(map(reader));
        return new(items,current,page.PageSize,total,pages);
    }
    public async Task<object> InternOptionsAsync(CancellationToken ct)
    {
        await using var connection=new SqlConnection(connectionString);await connection.OpenAsync(ct);await using var cmd=connection.CreateCommand();
        cmd.CommandText="SELECT COUNT(*),COUNT(DISTINCT NULLIF(School,'')),COUNT(DISTINCT NULLIF(Major,'')) FROM dbo.Interns;SELECT DISTINCT School FROM dbo.Interns WHERE School<>'' ORDER BY School;SELECT DISTINCT Major FROM dbo.Interns WHERE Major<>'' ORDER BY Major";
        await using var reader=await cmd.ExecuteReaderAsync(ct);await reader.ReadAsync(ct);var total=reader.GetInt32(0);var schoolCount=reader.GetInt32(1);var majorCount=reader.GetInt32(2);
        var schools=new List<string>();await reader.NextResultAsync(ct);while(await reader.ReadAsync(ct))schools.Add(reader.GetString(0));
        var majors=new List<string>();await reader.NextResultAsync(ct);while(await reader.ReadAsync(ct))majors.Add(reader.GetString(0));
        return new{totalItems=total,schoolCount,majorCount,schools,majors};
    }
}
