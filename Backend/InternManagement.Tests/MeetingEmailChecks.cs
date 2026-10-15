using InternManagement.DTOs;
using InternManagement.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

internal static class MeetingEmailChecks
{
    private sealed class RecordingEmailService : IEmailService
    {
        public readonly List<(string Recipient, EmailContent Content)> Sent = [];
        public int FailedAttempts;
        public void ValidateConfiguration() { }
        public Task SendAsync(string recipient, EmailContent content, CancellationToken ct)
        {
            var address = EmailService.ValidateRecipient(recipient);
            if (address.StartsWith("smtp-fail@", StringComparison.OrdinalIgnoreCase))
            {
                FailedAttempts++;
                throw new PasswordRecoveryUnavailableException("SMTP test unavailable");
            }
            Sent.Add((address, content));
            return Task.CompletedTask;
        }
    }

    public static async Task RunAsync(string connectionString, Action<bool,string> check)
    {
        await using var sql = new SqlConnection(connectionString); await sql.OpenAsync();
        await using var command = sql.CreateCommand();
        command.CommandText = """
            INSERT dbo.Users(Username,FullName,Email,PasswordHash,Role) VALUES
             ('meeting-creator','Người tạo','creator@company.vn','unused','HR'),
             ('meeting-gmail','Người Gmail','participant@gmail.com','unused','INTERN'),
             ('meeting-company','Người Công ty','participant@company.vn','unused','MENTOR'),
             ('meeting-invalid','Email lỗi','not-an-email','unused','INTERN'),
             ('meeting-smtp-fail','SMTP lỗi','smtp-fail@outlook.com','unused','INTERN'),
             ('meeting-outsider','Người ngoài','outsider@yahoo.com','unused','INTERN');
            SELECT Id,Username FROM dbo.Users;
            """;
        var users = new Dictionary<string,int>();
        await using (var reader = await command.ExecuteReaderAsync())
            while (await reader.ReadAsync()) users[reader.GetString(1)] = reader.GetInt32(0);

        var transport = new RecordingEmailService();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
        {
            ["ConnectionStrings:InternManagement"] = connectionString
        }).Build();
        var service = new MeetingService(config, transport, TimeProvider.System, NullLogger<MeetingService>.Instance);
        var participants = new[]
        {
            users["meeting-gmail"], users["meeting-company"], users["meeting-invalid"], users["meeting-smtp-fail"]
        };
        var start = DateTimeOffset.UtcNow.AddDays(2);
        var result = await service.CreateAsync(users["meeting-creator"], new CreateMeetingRequest(
            "Họp đánh giá tuần", start, "https://meet.example/session", "Trao đổi tiến độ tiếng Việt", participants), default);

        check(result.Id > 0 && result.EmailsSent == 2 && result.EmailsFailed == 2,
            "meeting persists and reports recipient delivery outcomes");
        check(transport.Sent.Select(x => x.Recipient).Order().SequenceEqual(new[] { "participant@company.vn", "participant@gmail.com" }),
            "only valid related participants receive email, including Gmail and non-Gmail");
        check(!transport.Sent.Any(x => x.Recipient == "outsider@yahoo.com"), "unrelated user receives no meeting email");
        var expectedTime = start.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm");
        check(transport.Sent.All(x => x.Content.Subject.Contains("Họp đánh giá tuần")
            && x.Content.PlainText.Contains(expectedTime)
            && x.Content.PlainText.Contains("https://meet.example/session")
            && x.Content.PlainText.Contains("Trao đổi tiến độ tiếng Việt")),
            "meeting email contains UTF-8 title, time, location/link and description");
        command.CommandText = $"SELECT COUNT(*) FROM dbo.Meetings WHERE Id={result.Id}; SELECT COUNT(*) FROM dbo.MeetingParticipants WHERE MeetingId={result.Id}; SELECT COUNT(*) FROM dbo.MeetingEmailDeliveries WHERE MeetingId={result.Id} AND Status='SENT';";
        await using (var reader = await command.ExecuteReaderAsync())
        {
            await reader.ReadAsync(); check(reader.GetInt32(0) == 1, "SMTP and invalid email failures do not roll back meeting");
            await reader.NextResultAsync(); await reader.ReadAsync(); check(reader.GetInt32(0) == 4, "participant relation persists exactly supplied users");
            await reader.NextResultAsync(); await reader.ReadAsync(); check(reader.GetInt32(0) == 2, "successful delivery claims persist");
        }
        var retry = await service.NotifyCreatedAsync(result.Id, default);
        check(retry.Sent == 0 && transport.Sent.Count == 2, "duplicate meeting-created trigger sends no duplicate email");
        var requestId=Guid.NewGuid();
        var idempotentRequest=new CreateMeetingRequest("Double submit",start,start.AddHours(1),null,null,[users["meeting-gmail"]],requestId);
        var first=await service.CreateAsync(users["meeting-creator"],idempotentRequest,default);
        var second=await service.CreateAsync(users["meeting-creator"],idempotentRequest,default);
        check(first.Id==second.Id&&transport.Sent.Count==3,"double submit persists one meeting and sends one email");
        var badParticipant = false;
        try
        {
            await service.CreateAsync(users["meeting-creator"], new CreateMeetingRequest(
                "Invalid participant", start, null, null, [int.MaxValue]), default);
        }
        catch (ArgumentException) { badParticipant = true; }
        check(badParticipant, "nonexistent participant is rejected before meeting persistence");
        command.CommandText = "SELECT COUNT(*) FROM dbo.Meetings";
        check(Convert.ToInt32(await command.ExecuteScalarAsync()) == 2, "invalid meeting creates no data");

        var options=await service.GetParticipantsAsync(default);
        var intern=options.Single(x=>x.Id==users["meeting-gmail"]);
        var mentor=options.Single(x=>x.Id==users["meeting-company"]);
        transport.Sent.Clear();
        await service.CreateAsync(users["meeting-creator"],new CreateMeetingRequest(
            "Một Intern",start,start.AddHours(1),null,null,[intern.Id],Guid.NewGuid()),default);
        check(transport.Sent.Count==1&&transport.Sent[0].Recipient==intern.Email,
            "selecting one Intern sends only to that User's database email");

        transport.Sent.Clear();
        await service.CreateAsync(users["meeting-creator"],new CreateMeetingRequest(
            "Mentor và Intern",start,start.AddHours(1),null,null,[mentor.Id,intern.Id],Guid.NewGuid()),default);
        check(transport.Sent.Select(x=>x.Recipient).Order().SequenceEqual(new[]{mentor.Email,intern.Email}.Order()),
            "selecting Mentor plus Intern sends exactly to both database emails, supporting Gmail and non-Gmail");
        var selectedUsers=new HashSet<int>{mentor.Id,intern.Id};
        check(options.Where(x=>!selectedUsers.Contains(x.Id)).All(x=>!transport.Sent.Any(m=>m.Recipient==x.Email)),
            "no unselected User, including the HR creator, receives the meeting email");
    }
}
