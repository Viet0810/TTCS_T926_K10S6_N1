using System.Data;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using InternManagement.DTOs;
using InternManagement.Models;
using InternManagement.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

// All SQL writes are confined to a uniquely named temporary database.
if (args.Contains("--validation-only"))
{
    await Sprint2ValidationChecks.RunAsync();
    return;
}
var settingsPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../InternManagement/appsettings.json"));
var settings = new ConfigurationBuilder().AddJsonFile(settingsPath)
    .AddJsonFile(Path.Combine(Path.GetDirectoryName(settingsPath)!, "appsettings.Development.json"), optional:true)
    .AddEnvironmentVariables().Build();
var source = settings.GetConnectionString("InternManagement") ?? throw new Exception("Missing SQL configuration.");
if (Environment.GetEnvironmentVariable("INTERN_TEST_SQL_SERVER") is { Length: > 0 } testServer)
    source = new SqlConnectionStringBuilder(source) { DataSource = testServer }.ConnectionString;
var databaseName = "InternManagement_AuthTests_" + Guid.NewGuid().ToString("N");
if (!Regex.IsMatch(databaseName, "^InternManagement_AuthTests_[a-f0-9]{32}$")) throw new Exception("Unsafe database name.");
var masterSettings = new SqlConnectionStringBuilder(source) { InitialCatalog = "master", ConnectTimeout = 5 };
var testSettings = new SqlConnectionStringBuilder(source) { InitialCatalog = databaseName, ConnectTimeout = 5 };
await using var master = new SqlConnection(masterSettings.ConnectionString);
await master.OpenAsync();
await using (var create = master.CreateCommand())
{
    create.CommandText = $"CREATE DATABASE [{databaseName}]";
    await create.ExecuteNonQueryAsync();
}

try
{
    await new DatabaseInitializer().InitializeAsync(testSettings.ConnectionString);
    await using var database = new SqlConnection(testSettings.ConnectionString);
    await database.OpenAsync();
    async Task<object?> Sql(string query, params (string Name, object Value)[] parameters)
    {
        await using var command = database.CreateCommand();
        command.CommandText = query;
        foreach (var parameter in parameters) command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        return await command.ExecuteScalarAsync();
    }
    void Check(bool condition, string name)
    {
        if (!condition) throw new Exception("FAIL: " + name);
        Console.WriteLine("PASS: " + name);
    }

    Check((int)(await Sql("SELECT COUNT(*) FROM dbo.Users"))! == 0
        && (int)(await Sql("SELECT COUNT(*) FROM dbo.Interns"))! == 0, "database initialization creates no accounts or sample rows");
    var hasher = new PasswordHasher();
    var oldHash = hasher.Hash("Original-password-9");
    var userId = (int)(await Sql("""
        INSERT INTO dbo.Users (Username, FullName, Email, PasswordHash, Role)
        OUTPUT INSERTED.Id VALUES (N'auth-test', N'Auth integration test', N'auth-test@example.invalid', @hash, 'INTERN');
        """, ("@hash", oldHash)))!;

    using var smtp = new TcpListener(IPAddress.Loopback, 0);
    smtp.Start();
    var smtpPort = ((IPEndPoint)smtp.LocalEndpoint).Port;
    var mailTask = CaptureMail(smtp);
    var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?>
    {
        ["ConnectionStrings:InternManagement"] = testSettings.ConnectionString,
        ["Smtp:Host"] = "127.0.0.1", ["Smtp:Port"] = smtpPort.ToString(), ["Smtp:EnableSsl"] = "false",
        ["Smtp:FromAddress"] = "no-reply@example.invalid",
        ["PasswordReset:ResetPageUrl"] = "http://localhost:5500/Frontend/reset-password.html"
    }).Build();
    var recovery = new PasswordResetService(configuration, hasher, NullLogger<PasswordResetService>.Instance);
    var tokens = new AuthTokenService(new EphemeralDataProtectionProvider(), configuration);
    var originalUser = new User { Id=userId, Username="auth-test", Email="auth-test@example.invalid", FullName="Auth test", Role="INTERN", PasswordHash=oldHash };
    var session = tokens.Issue(originalUser);
    Check(tokens.TryValidate(session, out _), "a session validates against its database account");

    var resetPageUrl = configuration["PasswordReset:ResetPageUrl"];
    foreach (var invalidUrl in new[] { "", "not-a-url", "http://example.invalid/reset-password.html" })
    {
        configuration["PasswordReset:ResetPageUrl"] = invalidUrl;
        var resetUrlRejected = false;
        try { await recovery.RequestAsync(originalUser.Email, CancellationToken.None); }
        catch (PasswordRecoveryUnavailableException error)
        {
            resetUrlRejected = error.Message.Contains("PasswordReset:ResetPageUrl") && !error.Message.Contains("SMTP");
        }
        Check(resetUrlRejected, "missing/invalid reset URL is distinguished from configured SMTP");
    }
    configuration["PasswordReset:ResetPageUrl"] = resetPageUrl;
    configuration["Smtp:Host"] = "";
    var smtpConfigurationRejected = false;
    try { await recovery.RequestAsync(originalUser.Email, CancellationToken.None); }
    catch (PasswordRecoveryUnavailableException error) { smtpConfigurationRejected = error.Message.Contains("SMTP"); }
    Check(smtpConfigurationRejected, "missing SMTP is distinguished from valid reset URL");
    configuration["Smtp:Host"] = "127.0.0.1";
    Check((int)(await Sql("SELECT COUNT(*) FROM dbo.PasswordResetTokens"))! == 0, "configuration failures create no reset tokens");

    await recovery.RequestAsync("missing@example.invalid", CancellationToken.None);
    Check((int)(await Sql("SELECT COUNT(*) FROM dbo.PasswordResetTokens"))! == 0, "unknown email creates no reset token");
    await recovery.RequestAsync(originalUser.Email, CancellationToken.None);
    var mail = await mailTask.WaitAsync(TimeSpan.FromSeconds(10));
    var match = Regex.Match(mail, "#token=([A-F0-9]{64})");
    Check(match.Success, "SMTP receives a real email with a reset link");
    Check(mail.Contains("RCPT TO:<auth-test@example.invalid>", StringComparison.OrdinalIgnoreCase)
        && mail.Contains("MAIL FROM:<no-reply@example.invalid>", StringComparison.OrdinalIgnoreCase), "forgot password uses account recipient and shared system sender");
    Check(mail.Contains(resetPageUrl + "#token="), "reset link uses configured frontend page and token fragment");
    var secret = match.Groups[1].Value;
    var hash = SHA256.HashData(Convert.FromHexString(secret));
    var storedHash = (byte[])(await Sql("SELECT TokenHash FROM dbo.PasswordResetTokens WHERE UserId=@id", ("@id",userId)))!;
    Check(CryptographicOperations.FixedTimeEquals(hash,storedHash), "SQL stores the token hash rather than its plaintext");
    await recovery.RequestAsync(originalUser.Email, CancellationToken.None);
    Check((int)(await Sql("SELECT COUNT(*) FROM dbo.PasswordResetTokens"))! == 1, "repeated requests within one minute are throttled");

    Check(!await recovery.ResetAsync(Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), "New-password-9", CancellationToken.None), "unknown reset token is rejected");
    await Sql("UPDATE dbo.PasswordResetTokens SET ExpiresAt=DATEADD(MINUTE,-1,SYSUTCDATETIME())");
    Check(!await recovery.ResetAsync(secret, "New-password-9", CancellationToken.None), "expired reset token is rejected");
    Check((string)(await Sql("SELECT PasswordHash FROM dbo.Users WHERE Id=@id",("@id",userId)))! == oldHash, "failed resets leave the password unchanged");
    await Sql("UPDATE dbo.PasswordResetTokens SET ExpiresAt=DATEADD(MINUTE,30,SYSUTCDATETIME())");
    var sibling = RandomNumberGenerator.GetBytes(32);
    await Sql("INSERT INTO dbo.PasswordResetTokens(UserId,TokenHash,ExpiresAt) VALUES(@id,@hash,DATEADD(MINUTE,30,SYSUTCDATETIME()))", ("@id",userId),("@hash",SHA256.HashData(sibling)));
    var attempts = await Task.WhenAll(
        recovery.ResetAsync(secret,"New-password-A",CancellationToken.None),
        recovery.ResetAsync(secret,"New-password-B",CancellationToken.None));
    Check(attempts.Count(result => result) == 1, "concurrent reuse changes the password only once");
    var newHash = (string)(await Sql("SELECT PasswordHash FROM dbo.Users WHERE Id=@id",("@id",userId)))!;
    Check(!hasher.Verify("Original-password-9",newHash) && (hasher.Verify("New-password-A",newHash)||hasher.Verify("New-password-B",newHash)), "new password is persisted with the existing password hasher");
    Check(!tokens.TryValidate(session,out _), "password reset invalidates existing sessions");
    Check(!await recovery.ResetAsync(secret,"Another-password-9",CancellationToken.None), "used links are rejected");
    Check(!await recovery.ResetAsync(Convert.ToHexString(sibling),"Another-password-9",CancellationToken.None), "a successful reset invalidates other outstanding links");
    var resetUser = new User { Id=userId, Username=originalUser.Username, Email=originalUser.Email, FullName=originalUser.FullName, Role=originalUser.Role, PasswordHash=newHash };
    Check(tokens.TryValidate(tokens.Issue(resetUser),out _), "new sessions validate after password reset");

    // The frontend expects both persistence and response to include the same fields.
    var interns = new InternService(configuration);
    var created = await interns.CreateAsync(new CreateInternRequest {FullName="Integration test",Email="intern@example.invalid",Phone="0900000000",School="Integration school",Major="Integration major"},CancellationToken.None);
    var updated = await interns.UpdateAsync(created.Id,new CreateInternRequest {FullName="Updated name",Email=created.Email,Phone=created.Phone,School=created.School,Major="Updated major"},CancellationToken.None);
    Check(updated?.FullName == "Updated name" && (await interns.GetByIdAsync(created.Id,CancellationToken.None))?.Major == "Updated major", "intern edits persist to SQL Server");
    Check(await interns.UpdateAsync(int.MaxValue,new CreateInternRequest {FullName="Missing",Email="missing@example.invalid",Phone="0900000000",School="School",Major="Major"},CancellationToken.None) is null, "updating a nonexistent profile returns no success");

    await AccountEmailChecks.RunAsync(configuration, Check, CaptureMail);
    await ReviewEmailChecks.RunAsync(configuration, Check, CaptureMail);
    configuration["Smtp:Host"] = "";
    var missingSmtpRejected = false;
    try { await recovery.RequestAsync(originalUser.Email,CancellationToken.None); }
    catch (InvalidOperationException) { missingSmtpRejected = true; }
    Check(missingSmtpRejected, "missing SMTP configuration does not pretend an email was sent");
    await ProfileChecks.RunAsync(configuration, Check);
    await ApiChecks.RunAsync(testSettings.ConnectionString, Check);
}
finally
{
    SqlConnection.ClearAllPools();
    await using var drop = master.CreateCommand();
    drop.CommandText = $"ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}];";
    await drop.ExecuteNonQueryAsync();
    Console.WriteLine("Temporary integration database removed.");
}

static async Task<string> CaptureMail(TcpListener listener)
{
    using var client = await listener.AcceptTcpClientAsync();
    await using var stream = client.GetStream();
    using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen:true);
    using var writer = new StreamWriter(stream, Encoding.ASCII,leaveOpen:true) { NewLine="\r\n", AutoFlush=true };
    await writer.WriteLineAsync("220 localhost integration SMTP");
    var content = new StringBuilder();
    var envelope = new StringBuilder();
    while (await reader.ReadLineAsync() is { } line)
    {
        if (line.StartsWith("DATA",StringComparison.OrdinalIgnoreCase))
        {
            await writer.WriteLineAsync("354 Send message");
            while (await reader.ReadLineAsync() is { } part && part != ".") content.Append(part).Append("\r\n");
            await writer.WriteLineAsync("250 Accepted");
        }
        else if (line.StartsWith("MAIL FROM:",StringComparison.OrdinalIgnoreCase) || line.StartsWith("RCPT TO:",StringComparison.OrdinalIgnoreCase))
        {
            envelope.AppendLine(line);
            await writer.WriteLineAsync("250 localhost");
        }
        else if (line.StartsWith("QUIT",StringComparison.OrdinalIgnoreCase)) { await writer.WriteLineAsync("221 Closing"); break; }
        else await writer.WriteLineAsync("250 localhost");
    }
    var raw = content.ToString();
    var split = raw.IndexOf("\r\n\r\n",StringComparison.Ordinal);
    var headers = raw[..split];
    var body = raw[(split+4)..];
    if (headers.Contains("Content-Transfer-Encoding: base64",StringComparison.OrdinalIgnoreCase))
        body = Encoding.UTF8.GetString(Convert.FromBase64String(body));
    if (headers.Contains("Content-Transfer-Encoding: quoted-printable",StringComparison.OrdinalIgnoreCase))
        body = Regex.Replace(body.Replace("=\r\n",""), "=([0-9A-F]{2})", m => ((char)Convert.ToByte(m.Groups[1].Value,16)).ToString());
    return envelope + "\r\n" + headers + "\r\n\r\n" + body;
}
