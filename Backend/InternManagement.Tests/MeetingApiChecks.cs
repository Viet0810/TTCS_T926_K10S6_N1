using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using InternManagement.Controllers;
using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Models;
using InternManagement.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

internal static class MeetingApiChecks
{
    private sealed class MailRecorder : IEmailService
    {
        public int Count;
        public void ValidateConfiguration() { }
        public Task SendAsync(string recipient,EmailContent content,CancellationToken ct)
        { EmailService.ValidateRecipient(recipient);Interlocked.Increment(ref Count);return Task.CompletedTask; }
    }

    public static async Task RunAsync(string connectionString,Action<bool,string> check)
    {
        var builder=WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string,string?>{{"ConnectionStrings:InternManagement",connectionString}});
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddControllers().AddApplicationPart(typeof(MeetingsController).Assembly);
        builder.Services.AddDataProtection();
        builder.Services.AddSingleton<AuthTokenService>();builder.Services.AddSingleton<RolePermissionService>();
        builder.Services.AddScoped<RequestAuthorizationService>();builder.Services.AddScoped<MeetingService>();
        builder.Services.AddSingleton(TimeProvider.System);
        var mail=new MailRecorder();builder.Services.AddSingleton<IEmailService>(mail);
        await using var app=builder.Build();app.MapControllers();await app.StartAsync();
        try
        {
            var address=app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            using var http=new HttpClient{BaseAddress=new Uri(address)};
            await using var sql=new SqlConnection(connectionString);await sql.OpenAsync();await using var query=sql.CreateCommand();
            query.CommandText="SELECT Id,Username,Role,PasswordHash FROM dbo.Users WHERE Username IN ('meeting-creator','meeting-gmail')";
            var users=new List<User>();await using(var reader=await query.ExecuteReaderAsync())
                while(await reader.ReadAsync())users.Add(new(){Id=reader.GetInt32(0),Username=reader.GetString(1),Role=reader.GetString(2),PasswordHash=reader.GetString(3)});
            var hr=users.Single(x=>x.Role=="HR");var intern=users.Single(x=>x.Role=="INTERN");
            var tokens=app.Services.GetRequiredService<AuthTokenService>();
            void Login(User user)=>http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",tokens.Issue(user));
            var start=DateTimeOffset.UtcNow.AddDays(3);
            var request=new CreateMeetingRequest("Họp tiếng Việt",start,start.AddHours(1),"Phòng họp A","Trao đổi tiến độ",[intern.Id],Guid.NewGuid());
            Login(hr);
            using var created=await http.PostAsJsonAsync("/api/meetings",request);
            check(created.StatusCode==HttpStatusCode.Created,"HR creates meeting through real HTTP route and JSON binding");
            var meeting=(await created.Content.ReadFromJsonAsync<MeetingResponse>())!;
            check(meeting.Id>0&&meeting.CreatedByUserId==hr.Id&&meeting.CreatedAt!=default&&mail.Count==1,"HTTP creation persists creator/time and sends email");
            using var duplicate=await http.PostAsJsonAsync("/api/meetings",request);
            var same=(await duplicate.Content.ReadFromJsonAsync<MeetingResponse>())!;
            check(same.Id==meeting.Id&&mail.Count==1,"HTTP double submit returns same meeting without another email");
            var concurrent=request with{ClientRequestId=Guid.NewGuid()};
            var results=await Task.WhenAll(http.PostAsJsonAsync("/api/meetings",concurrent),http.PostAsJsonAsync("/api/meetings",concurrent));
            var ids=new List<int>();foreach(var response in results){using(response){check(response.StatusCode==HttpStatusCode.Created,"concurrent submit succeeds");ids.Add((await response.Content.ReadFromJsonAsync<MeetingResponse>())!.Id);}}
            check(ids.Distinct().Count()==1&&mail.Count==2,"concurrent double submit creates one meeting and one delivery");
            using var invalidTime=await http.PostAsJsonAsync("/api/meetings",request with{EndTime=start,ClientRequestId=Guid.NewGuid()});
            check(invalidTime.StatusCode==HttpStatusCode.BadRequest,"invalid meeting time returns 400");
            using var invalidTitle=await http.PostAsJsonAsync("/api/meetings",request with{Title=" ",ClientRequestId=Guid.NewGuid()});
            check(invalidTitle.StatusCode==HttpStatusCode.BadRequest,"blank meeting title returns 400");
            using var invalidParticipant=await http.PostAsJsonAsync("/api/meetings",request with{ParticipantUserIds=[int.MaxValue],ClientRequestId=Guid.NewGuid()});
            check(invalidParticipant.StatusCode==HttpStatusCode.BadRequest,"nonexistent participant returns 400");
            using var meetings=await http.GetAsync("/api/meetings");
            var persisted=await meetings.Content.ReadFromJsonAsync<List<MeetingResponse>>();
            check(meetings.StatusCode==HttpStatusCode.OK&&persisted!.Any(x=>x.Id==meeting.Id),"meeting list reads persisted data independently of US23");
            Login(intern);
            using var forbidden=await http.PostAsJsonAsync("/api/meetings",request with{ClientRequestId=Guid.NewGuid()});
            check(forbidden.StatusCode==HttpStatusCode.Forbidden,"non-HR direct meeting create returns 403");
            query.CommandText="INSERT dbo.RolePermissions(Role,Permission) VALUES('INTERN','MEETING_READ'),('INTERN','MEETING_CREATE')";
            await query.ExecuteNonQueryAsync();
            using var wrongRole=await http.PostAsJsonAsync("/api/meetings",request with{ClientRequestId=Guid.NewGuid()});
            check(wrongRole.StatusCode==HttpStatusCode.Forbidden,"non-HR remains forbidden even with a manually granted meeting permission");
            Login(hr);query.CommandText="DELETE dbo.RolePermissions WHERE Role='HR' AND Permission='MEETING_CREATE'";await query.ExecuteNonQueryAsync();
            using var revoked=await http.PostAsJsonAsync("/api/meetings",request with{ClientRequestId=Guid.NewGuid()});
            check(revoked.StatusCode==HttpStatusCode.Forbidden,"HR without create permission receives 403 immediately");
            await MeetingMigration.ApplyAsync(sql,default);
            using var stillRevoked=await http.PostAsJsonAsync("/api/meetings",request with{ClientRequestId=Guid.NewGuid()});
            check(stillRevoked.StatusCode==HttpStatusCode.Forbidden,"migration rerun preserves revoked meeting permission");
        }
        finally { await app.StopAsync(); }
    }
}
