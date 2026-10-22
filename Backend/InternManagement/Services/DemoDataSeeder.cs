using System.Data;
using System.Text;
using InternManagement.DTOs;
using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public sealed class DemoDataSeeder(
    IConfiguration configuration,
    PasswordHasher passwords,
    IHostEnvironment environment,
    ILogger<DemoDataSeeder> logger)
{
    public const string ProgramCode = "SUMMER-INTERNSHIP-2026";
    public const string ProgramName = "Chương trình thực tập mùa hè 2026";

    private static readonly (string Name, string Email, string Code)[] Mentors =
    [
        ("Demo Mentor Nguyen Van An", "mentor01@demo.intern.local", "MENTOR-DEMO-01"),
        ("Demo Mentor Tran Thi Binh", "mentor02@demo.intern.local", "MENTOR-DEMO-02"),
        ("Demo Mentor Le Minh Chau", "mentor03@demo.intern.local", "MENTOR-DEMO-03"),
        ("Demo Mentor Pham Quang Dung", "mentor04@demo.intern.local", "MENTOR-DEMO-04"),
        ("Demo Mentor Hoang Thu Giang", "mentor05@demo.intern.local", "MENTOR-DEMO-05")
    ];

    private static readonly string[] VietnameseNames =
    [
        "Nguyen Minh Anh", "Tran Hoang Bao", "Le Gia Bao", "Pham Quynh Chi", "Hoang Minh Chau",
        "Phan Duc Duy", "Vu Tien Dat", "Vo Ngoc Diep", "Dang Thanh Giang", "Bui Thu Ha",
        "Do Quang Hai", "Ngo Minh Hieu", "Duong Gia Huy", "Ly Bao Khanh", "Nguyen Tuan Kiet",
        "Tran Hoang Lam", "Le Ngoc Lan", "Pham Gia Linh", "Hoang Duc Long", "Phan Khanh Ly",
        "Vu Tuan Manh", "Vo Minh Nam", "Dang Bao Ngoc", "Bui Ha Nhi", "Do Thanh Phong",
        "Ngo Minh Phuc", "Duong Anh Quan", "Ly Bao Quyen", "Nguyen Duc Son", "Tran Minh Tam",
        "Le Thu Thao", "Pham Anh Thu", "Hoang Minh Trang", "Phan Quoc Trung", "Vu Anh Tuan",
        "Vo Thanh Tung", "Dang Minh Uyen", "Bui Quang Viet", "Do Hai Yen", "Ngo Bao An",
        "Duong Ngoc Anh", "Ly Tuan Anh", "Nguyen Khanh Binh", "Tran Minh Duc", "Le Quang Huy",
        "Pham Thu Huong", "Hoang Gia Khang", "Phan Duc Minh", "Vu Hoai Nam", "Vo Thu Trang"
    ];

    private readonly string connectionString = configuration.GetConnectionString("InternManagement")
        ?? throw new InvalidOperationException("Chưa cấu hình kết nối cơ sở dữ liệu.");

    public async Task<DemoSeedResult> SeedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            var adminId = await UpsertUserAsync(connection, transaction, "Demo System Admin",
                "admin@demo.intern.local", "ADMIN", null, cancellationToken);
            var hrId = await UpsertUserAsync(connection, transaction, "Demo HR Manager",
                "hr@demo.intern.local", "HR", "HR-DEMO-001", cancellationToken);

            var mentorIds = new int[Mentors.Length];
            for (var index = 0; index < Mentors.Length; index++)
            {
                var mentor = Mentors[index];
                mentorIds[index] = await UpsertUserAsync(connection, transaction, mentor.Name,
                    mentor.Email, "MENTOR", mentor.Code, cancellationToken);
            }

            var programId = await UpsertProgramAsync(connection, transaction, cancellationToken);
            for (var number = 1; number <= 50; number++)
            {
                var mentorIndex = (number - 1) / 10;
                var email = $"intern{number:000}@demo.intern.local";
                var fullName = $"Demo Intern {VietnameseNames[number - 1]}";
                await UpsertUserAsync(connection, transaction, fullName, email, "INTERN", null, cancellationToken);
                var internId = await UpsertInternAsync(connection, transaction, number, fullName, email,
                    mentorIds[mentorIndex], programId, cancellationToken);

                await UpsertDocumentsAsync(connection, transaction, internId, hrId, number, cancellationToken);
                await UpsertContractAsync(connection, transaction, internId, hrId, number, cancellationToken);
                await UpsertAttendanceAsync(connection, transaction, internId, number, cancellationToken);
                await UpsertEvaluationAsync(connection, transaction, internId, mentorIds[mentorIndex], number,
                    cancellationToken);
                await UpsertEvaluationSummaryAsync(connection, transaction, internId, hrId, number,
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            var accountsPath = await WriteAccountsFileAsync(cancellationToken);
            var result = await CountSeededDataAsync(connection, accountsPath, cancellationToken);
            logger.LogInformation(
                "Demo seed completed: {Admins} admin, {Hr} HR, {Mentors} mentors, {Interns} interns.",
                result.Admins, result.HumanResources, result.Mentors, result.Interns);
            return result;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private async Task<int> UpsertUserAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string fullName,
        string email,
        string role,
        string? employeeCode,
        CancellationToken cancellationToken)
    {
        await using var safetyCheck = new SqlCommand(
            "SELECT IsDemoAccount FROM dbo.Users WITH (UPDLOCK, HOLDLOCK) WHERE Email = @email",
            connection,
            transaction);
        safetyCheck.Parameters.Add("@email", SqlDbType.NVarChar, 254).Value = email;
        var existingDemoFlag = await safetyCheck.ExecuteScalarAsync(cancellationToken);
        if (existingDemoFlag is not null && !Convert.ToBoolean(existingDemoFlag))
            throw new InvalidOperationException($"Từ chối ghi đè tài khoản không phải demo: {email}");

        await using var command = new SqlCommand("""
            UPDATE dbo.Users
            SET Username = @email, FullName = @fullName, PasswordHash = @passwordHash,
                Role = @role, MustChangePassword = 0, IsDemoAccount = 1,
                EmployeeCode = @employeeCode
            WHERE Email = @email AND IsDemoAccount = 1;
            IF @@ROWCOUNT = 0
                INSERT dbo.Users
                    (Username, FullName, Email, PasswordHash, Role, MustChangePassword, IsDemoAccount, EmployeeCode)
                VALUES
                    (@email, @fullName, @email, @passwordHash, @role, 0, 1, @employeeCode);
            SELECT Id FROM dbo.Users WHERE Email = @email AND IsDemoAccount = 1;
            """, connection, transaction);
        command.Parameters.Add("@email", SqlDbType.NVarChar, 254).Value = email;
        command.Parameters.Add("@fullName", SqlDbType.NVarChar, 200).Value = fullName;
        command.Parameters.Add("@passwordHash", SqlDbType.NVarChar, 512).Value =
            passwords.Hash(DemoAccountService.DefaultPassword(role));
        command.Parameters.Add("@role", SqlDbType.VarChar, 10).Value = role;
        command.Parameters.Add("@employeeCode", SqlDbType.NVarChar, 50).Value =
            (object?)employeeCode ?? DBNull.Value;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<int> UpsertProgramAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("""
            UPDATE dbo.InternshipPrograms
            SET Name = @name, Department = @department, StartDate = @startDate,
                EndDate = @endDate, UpdatedAt = SYSUTCDATETIME()
            WHERE Code = @code;
            IF @@ROWCOUNT = 0
                INSERT dbo.InternshipPrograms (Code, Name, Department, StartDate, EndDate)
                VALUES (@code, @name, @department, @startDate, @endDate);
            SELECT Id FROM dbo.InternshipPrograms WHERE Code = @code;
            """, connection, transaction);
        command.Parameters.Add("@code", SqlDbType.NVarChar, 100).Value = ProgramCode;
        command.Parameters.Add("@name", SqlDbType.NVarChar, 200).Value = ProgramName;
        command.Parameters.Add("@department", SqlDbType.NVarChar, 200).Value = "Engineering";
        command.Parameters.Add("@startDate", SqlDbType.Date).Value = new DateTime(2026, 7, 6);
        command.Parameters.Add("@endDate", SqlDbType.Date).Value = new DateTime(2026, 9, 30);
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task<int> UpsertInternAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int number,
        string fullName,
        string email,
        int mentorId,
        int programId,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("""
            UPDATE dbo.Interns
            SET FullName = @fullName, Phone = @phone, School = @school, Major = @major,
                StudentCode = @studentCode, ClassName = @className, Faculty = @faculty,
                Organization = @organization, Department = @department, Position = @position,
                StartDate = @startDate, EndDate = @endDate, Status = @status,
                InternshipTopic = @topic, Notes = @notes, MentorUserId = @mentorId,
                InternshipProgramId = @programId
            WHERE Email = @email;
            IF @@ROWCOUNT = 0
                INSERT dbo.Interns
                    (FullName, Email, Phone, School, Major, StudentCode, ClassName, Faculty,
                     Organization, Department, Position, StartDate, EndDate, Status,
                     InternshipTopic, Notes, MentorUserId, InternshipProgramId)
                VALUES
                    (@fullName, @email, @phone, @school, @major, @studentCode, @className, @faculty,
                     @organization, @department, @position, @startDate, @endDate, @status,
                     @topic, @notes, @mentorId, @programId);
            SELECT Id FROM dbo.Interns WHERE Email = @email;
            """, connection, transaction);
        command.Parameters.Add("@fullName", SqlDbType.NVarChar, 200).Value = fullName;
        command.Parameters.Add("@email", SqlDbType.NVarChar, 254).Value = email;
        command.Parameters.Add("@phone", SqlDbType.NVarChar, 20).Value = $"0900{number:000000}";
        command.Parameters.Add("@school", SqlDbType.NVarChar, 200).Value = "Demo University";
        command.Parameters.Add("@major", SqlDbType.NVarChar, 200).Value = "Information Technology";
        command.Parameters.Add("@studentCode", SqlDbType.NVarChar, 50).Value = $"INTERN-DEMO-{number:000}";
        command.Parameters.Add("@className", SqlDbType.NVarChar, 100).Value = "DEMO-2026";
        command.Parameters.Add("@faculty", SqlDbType.NVarChar, 200).Value = "Information Technology";
        command.Parameters.Add("@organization", SqlDbType.NVarChar, 200).Value = "Demo Intern Management";
        command.Parameters.Add("@department", SqlDbType.NVarChar, 200).Value = "Engineering";
        command.Parameters.Add("@position", SqlDbType.NVarChar, 200).Value = "Software Engineering Intern";
        command.Parameters.Add("@startDate", SqlDbType.Date).Value = new DateTime(2026, 7, 6);
        command.Parameters.Add("@endDate", SqlDbType.Date).Value = new DateTime(2026, 9, 30);
        command.Parameters.Add("@status", SqlDbType.NVarChar, 50).Value = "Đang thực tập";
        command.Parameters.Add("@topic", SqlDbType.NVarChar, 500).Value = $"Demo project {number:000}";
        command.Parameters.Add("@notes", SqlDbType.NVarChar, 2000).Value = "Dữ liệu phục vụ trình diễn và kiểm thử.";
        command.Parameters.Add("@mentorId", SqlDbType.Int).Value = mentorId;
        command.Parameters.Add("@programId", SqlDbType.Int).Value = programId;
        return Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
    }

    private static async Task UpsertDocumentsAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int internId,
        int reviewerId,
        int number,
        CancellationToken cancellationToken)
    {
        foreach (var kind in new[] { "cv", "application" })
        {
            var content = Encoding.UTF8.GetBytes($"Demo {kind} metadata for intern {number:000}");
            await using var command = new SqlCommand("""
                UPDATE dbo.InternDocuments
                SET FileName = @fileName, Content = @content, ReviewStatus = 'approved',
                    ReviewComment = N'Demo document approved', ReviewedAt = SYSUTCDATETIME(),
                    ReviewedBy = @reviewer
                WHERE InternId = @internId AND Kind = @kind;
                IF @@ROWCOUNT = 0
                    INSERT dbo.InternDocuments
                        (InternId, Kind, FileName, Content, ReviewStatus, ReviewComment, ReviewedAt, ReviewedBy)
                    VALUES
                        (@internId, @kind, @fileName, @content, 'approved',
                         N'Demo document approved', SYSUTCDATETIME(), @reviewer);
                """, connection, transaction);
            command.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
            command.Parameters.Add("@kind", SqlDbType.VarChar, 20).Value = kind;
            command.Parameters.Add("@fileName", SqlDbType.NVarChar, 255).Value = $"demo-{kind}-{number:000}.txt";
            command.Parameters.Add("@content", SqlDbType.VarBinary, -1).Value = content;
            command.Parameters.Add("@reviewer", SqlDbType.Int).Value = reviewerId;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task UpsertContractAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int internId,
        int uploadedBy,
        int number,
        CancellationToken cancellationToken)
    {
        var content = Encoding.ASCII.GetBytes($"%PDF-1.4\n% Demo contract {number:000}\n%%EOF");
        await using var command = new SqlCommand("""
            UPDATE dbo.InternContracts
            SET FileName = @fileName, Content = @content, UploadedBy = @uploadedBy,
                ConfirmedAt = COALESCE(ConfirmedAt, SYSUTCDATETIME())
            WHERE InternId = @internId;
            IF @@ROWCOUNT = 0
                INSERT dbo.InternContracts
                    (InternId, FileName, Content, UploadedBy, ConfirmedAt)
                VALUES
                    (@internId, @fileName, @content, @uploadedBy, SYSUTCDATETIME());
            """, connection, transaction);
        command.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
        command.Parameters.Add("@fileName", SqlDbType.NVarChar, 255).Value = $"demo-contract-{number:000}.pdf";
        command.Parameters.Add("@content", SqlDbType.VarBinary, -1).Value = content;
        command.Parameters.Add("@uploadedBy", SqlDbType.Int).Value = uploadedBy;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task UpsertAttendanceAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int internId,
        int number,
        CancellationToken cancellationToken)
    {
        for (var offset = 0; offset < 4; offset++)
        {
            var date = new DateTime(2026, 7, 7).AddDays(offset);
            var late = (number + offset) % 7 == 0;
            await using var command = new SqlCommand("""
                UPDATE dbo.AttendanceRecords
                SET CheckIn = @checkIn, CheckOut = @checkOut, WorkingHours = @hours,
                    Status = @status, Note = N'Demo attendance',
                    ScheduledStart = '08:00', ScheduledEnd = '17:30'
                WHERE InternId = @internId AND Date = @date;
                IF @@ROWCOUNT = 0
                    INSERT dbo.AttendanceRecords
                        (InternId, Date, CheckIn, CheckOut, WorkingHours, Status, Note,
                         ScheduledStart, ScheduledEnd)
                    VALUES
                        (@internId, @date, @checkIn, @checkOut, @hours, @status,
                         N'Demo attendance', '08:00', '17:30');
                """, connection, transaction);
            command.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
            command.Parameters.Add("@date", SqlDbType.Date).Value = date;
            command.Parameters.Add("@checkIn", SqlDbType.NVarChar, 10).Value = late ? "08:12" : "07:55";
            command.Parameters.Add("@checkOut", SqlDbType.NVarChar, 10).Value = "17:30";
            command.Parameters.Add("@hours", SqlDbType.Decimal).Value = late ? 8.8m : 9.5m;
            command.Parameters.Add("@status", SqlDbType.VarChar, 30).Value = late ? "LATE" : "ON_TIME";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task UpsertEvaluationAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        int internId,
        int mentorId,
        int number,
        CancellationToken cancellationToken)
    {
        var score = 7.5m + number % 6 * 0.4m;
        await using var command = new SqlCommand("""
            UPDATE dbo.FinalEvaluations
            SET Attendance = @score, Attitude = @score, ProfessionalSkills = @skillScore,
                WorkResult = @score, FinalScore = @finalScore, Classification = @classification,
                Status = N'Đã đánh giá', Comment = N'Hoàn thành tốt nội dung thực tập demo.',
                UpdatedAt = SYSDATETIMEOFFSET()
            WHERE InternId = @internId AND CreatedByUserId = @mentorId
              AND EvaluationSource = 'MENTOR' AND Period = @period;
            IF @@ROWCOUNT = 0
                INSERT dbo.FinalEvaluations
                    (InternId, Period, Attendance, Attitude, ProfessionalSkills, WorkResult,
                     FinalScore, Classification, Status, Comment, CreatedByUserId, EvaluationSource)
                VALUES
                    (@internId, @period, @score, @score, @skillScore, @score, @finalScore,
                     @classification, N'Đã đánh giá',
                     N'Hoàn thành tốt nội dung thực tập demo.', @mentorId, 'MENTOR');
            """, connection, transaction);
        command.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
        command.Parameters.Add("@mentorId", SqlDbType.Int).Value = mentorId;
        command.Parameters.Add("@period", SqlDbType.NVarChar, 200).Value = "Summer Internship 2026";
        command.Parameters.Add("@score", SqlDbType.Decimal).Value = score;
        command.Parameters.Add("@skillScore", SqlDbType.Decimal).Value = Math.Min(10m, score + 0.2m);
        command.Parameters.Add("@finalScore", SqlDbType.Decimal).Value = Math.Min(10m, score + 0.1m);
        command.Parameters.Add("@classification", SqlDbType.NVarChar, 50).Value = score >= 8.5m ? "Tốt" : "Khá";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<string> WriteAccountsFileAsync(CancellationToken cancellationToken)
    {
        var repositoryRoot = Path.GetFullPath(Path.Combine(environment.ContentRootPath, "..", ".."));
        var docsDirectory = Path.Combine(repositoryRoot, "docs");
        Directory.CreateDirectory(docsDirectory);
        var path = Path.Combine(docsDirectory, "demo-accounts.md");

        var content = new StringBuilder()
            .AppendLine("# Tài khoản Demo Intern Management")
            .AppendLine()
            .AppendLine($"- Thời gian tạo: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}")
            .AppendLine($"- Chương trình: {ProgramName} (`{ProgramCode}`)")
            .AppendLine("- Chỉ dùng cho môi trường Development/Demo. Không dùng trên Production.")
            .AppendLine()
            .AppendLine("| STT | Vai trò | Họ tên | Email | Mật khẩu demo | Mã nhân sự/MSSV | Mentor phụ trách |")
            .AppendLine("|---:|---|---|---|---|---|---|");

        var row = 1;
        content.AppendLine($"| {row++} | Admin | Demo System Admin | admin@demo.intern.local | Admin@123 | - | - |");
        content.AppendLine($"| {row++} | HR | Demo HR Manager | hr@demo.intern.local | Hr@123456 | HR-DEMO-001 | - |");
        foreach (var mentor in Mentors)
            content.AppendLine($"| {row++} | Mentor | {mentor.Name} | {mentor.Email} | Mentor@123 | {mentor.Code} | - |");
        for (var number = 1; number <= 50; number++)
        {
            var mentor = Mentors[(number - 1) / 10];
            content.AppendLine($"| {row++} | Intern | Demo Intern {VietnameseNames[number - 1]} | intern{number:000}@demo.intern.local | Intern@123 | INTERN-DEMO-{number:000} | {mentor.Name} |");
        }

        await File.WriteAllTextAsync(path, content.ToString(), new UTF8Encoding(false), cancellationToken);
        return Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
    }

    private static async Task UpsertEvaluationSummaryAsync(
        SqlConnection connection, SqlTransaction transaction, int internId, int hrId,
        int number, CancellationToken cancellationToken)
    {
        var required = number <= 10 ? 2 : 1;
        var status = number >= 39 ? "PUBLISHED" : number >= 26 ? "FINALIZED" : "DRAFT";
        var classification = number % 6 >= 3 ? "Tốt" : "Khá";
        await using var command = new SqlCommand("""
            UPDATE dbo.EvaluationSummaries
            SET RequiredEvaluationCount=@required,AggregateComment=@comment,
                FinalClassification=@classification,Status=@status,
                FinalizedByUserId=CASE WHEN @status IN ('FINALIZED','PUBLISHED') THEN @hrId ELSE NULL END,
                FinalizedAt=CASE WHEN @status IN ('FINALIZED','PUBLISHED') THEN DATEADD(day,-1,SYSDATETIMEOFFSET()) ELSE NULL END,
                PublishedByUserId=CASE WHEN @status='PUBLISHED' THEN @hrId ELSE NULL END,
                PublishedAt=CASE WHEN @status='PUBLISHED' THEN SYSDATETIMEOFFSET() ELSE NULL END,
                UpdatedAt=SYSDATETIMEOFFSET()
            WHERE InternId=@internId;
            IF @@ROWCOUNT=0
                INSERT dbo.EvaluationSummaries
                    (InternId,RequiredEvaluationCount,AggregateComment,FinalClassification,Status,
                     FinalizedByUserId,FinalizedAt,PublishedByUserId,PublishedAt)
                VALUES(@internId,@required,@comment,@classification,@status,
                    CASE WHEN @status IN ('FINALIZED','PUBLISHED') THEN @hrId END,
                    CASE WHEN @status IN ('FINALIZED','PUBLISHED') THEN DATEADD(day,-1,SYSDATETIMEOFFSET()) END,
                    CASE WHEN @status='PUBLISHED' THEN @hrId END,
                    CASE WHEN @status='PUBLISHED' THEN SYSDATETIMEOFFSET() END);

            DECLARE @summaryId INT=(SELECT Id FROM dbo.EvaluationSummaries WHERE InternId=@internId);
            IF @status IN ('FINALIZED','PUBLISHED') AND NOT EXISTS(
                SELECT 1 FROM dbo.EvaluationSummaryHistory WHERE SummaryId=@summaryId AND Action='FINALIZED')
                INSERT dbo.EvaluationSummaryHistory(SummaryId,Action,ActorUserId,AggregateComment,FinalClassification)
                VALUES(@summaryId,'FINALIZED',@hrId,@comment,@classification);
            IF @status='PUBLISHED' AND NOT EXISTS(
                SELECT 1 FROM dbo.EvaluationSummaryHistory WHERE SummaryId=@summaryId AND Action='PUBLISHED')
                INSERT dbo.EvaluationSummaryHistory(SummaryId,Action,ActorUserId,AggregateComment,FinalClassification)
                VALUES(@summaryId,'PUBLISHED',@hrId,@comment,@classification);
            """, connection, transaction);
        command.Parameters.Add("@internId", SqlDbType.Int).Value = internId;
        command.Parameters.Add("@hrId", SqlDbType.Int).Value = hrId;
        command.Parameters.Add("@required", SqlDbType.Int).Value = required;
        command.Parameters.Add("@status", SqlDbType.VarChar, 20).Value = status;
        command.Parameters.Add("@comment", SqlDbType.NVarChar, 2000).Value = $"Nhận xét tổng hợp demo cho Intern {number:000}.";
        command.Parameters.Add("@classification", SqlDbType.NVarChar, 50).Value = classification;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<DemoSeedResult> CountSeededDataAsync(
        SqlConnection connection,
        string accountsFile,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("""
            SELECT
              (SELECT COUNT(*) FROM dbo.Users WHERE IsDemoAccount=1 AND Role='ADMIN'),
              (SELECT COUNT(*) FROM dbo.Users WHERE IsDemoAccount=1 AND Role='HR'),
              (SELECT COUNT(*) FROM dbo.Users WHERE IsDemoAccount=1 AND Role='MENTOR'),
              (SELECT COUNT(*) FROM dbo.Users WHERE IsDemoAccount=1 AND Role='INTERN'),
              (SELECT COUNT(*) FROM dbo.InternshipPrograms WHERE Code=@code),
              (SELECT COUNT(*) FROM dbo.Interns i JOIN dbo.Users u ON u.Email=i.Email WHERE u.IsDemoAccount=1 AND i.MentorUserId IS NOT NULL),
              (SELECT COUNT(*) FROM dbo.InternContracts c JOIN dbo.Interns i ON i.Id=c.InternId JOIN dbo.Users u ON u.Email=i.Email WHERE u.IsDemoAccount=1),
              (SELECT COUNT(*) FROM dbo.AttendanceRecords a JOIN dbo.Interns i ON i.Id=a.InternId JOIN dbo.Users u ON u.Email=i.Email WHERE u.IsDemoAccount=1),
              (SELECT COUNT(*) FROM dbo.InternDocuments d JOIN dbo.Interns i ON i.Id=d.InternId JOIN dbo.Users u ON u.Email=i.Email WHERE u.IsDemoAccount=1),
              (SELECT COUNT(*) FROM dbo.FinalEvaluations e JOIN dbo.Interns i ON i.Id=e.InternId JOIN dbo.Users u ON u.Email=i.Email WHERE u.IsDemoAccount=1 AND e.EvaluationSource='MENTOR');
            """, connection);
        command.Parameters.Add("@code", SqlDbType.NVarChar, 100).Value = ProgramCode;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        await reader.ReadAsync(cancellationToken);
        return new DemoSeedResult(
            reader.GetInt32(0), reader.GetInt32(1), reader.GetInt32(2), reader.GetInt32(3),
            reader.GetInt32(4), reader.GetInt32(5), reader.GetInt32(6), reader.GetInt32(7),
            reader.GetInt32(8), reader.GetInt32(9), accountsFile);
    }
}
