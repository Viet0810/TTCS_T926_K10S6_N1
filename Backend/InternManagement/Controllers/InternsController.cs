using System.Net.Mail;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using InternManagement.DTOs;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/interns")]
public sealed class InternsController : ControllerBase
{
    private readonly string connectionString;
    private readonly AuthTokenService tokens;
    private readonly PasswordHasher passwords;

    public InternsController(IConfiguration configuration, AuthTokenService tokens, PasswordHasher passwords)
    {
        connectionString = configuration.GetConnectionString("InternManagement")
            ?? throw new InvalidOperationException("ConnectionStrings:InternManagement is missing.");
        this.tokens = tokens;
        this.passwords = passwords;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var denied = Authorize(out var failure);
        if (denied) return failure!;
        var interns = new List<InternResponse>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectSql + " ORDER BY Id DESC";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) interns.Add(ReadIntern(reader));
        return Ok(interns);
    }

    [HttpGet("mentors")]
    public async Task<IActionResult> GetMentors(CancellationToken cancellationToken)
    {
        var denied = Authorize(out var failure);
        if (denied) return failure!;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id,FullName AS Name,Email FROM dbo.Users WHERE Role='MENTOR' ORDER BY FullName";
        var mentors = new List<object>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) mentors.Add(new { id = reader.GetInt32(0), name = reader.GetString(1), email = reader.GetString(2) });
        return Ok(mentors);
    }

    [HttpPost("import-excel")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> ImportExcel([FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        var denied = Authorize(out var failure);
        if (denied) return failure!;
        var extension = file is null ? "" : Path.GetExtension(file.FileName);
        if (file is null || file.Length == 0 || (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase)))
            return BadRequest(new { message = "Chọn tệp Excel .xlsx có dữ liệu." });

        IReadOnlyList<IReadOnlyDictionary<int, string>> rows;
        try
        {
            await using var stream = file.OpenReadStream();
            rows = string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase)
                ? InternCsvReader.Read(stream)
                : InternExcelReader.Read(stream);
        }
        catch (InvalidDataException ex) { return BadRequest(new { message = ex.Message }); }
        catch (Exception ex) when (ex is System.Xml.XmlException or IOException or InvalidOperationException)
        { return BadRequest(new { message = "Tệp Excel không hợp lệ hoặc bị lỗi. Hãy lưu lại dưới định dạng .xlsx rồi thử lại." }); }

        var nonEmptyRows = rows.Where(row => row.Values.Any(value => !string.IsNullOrWhiteSpace(value))).ToList();
        if (nonEmptyRows.Count < 2) return BadRequest(new { message = "Tệp cần có dòng tiêu đề và ít nhất một dòng thực tập sinh." });
        if (nonEmptyRows.Count > 1001) return BadRequest(new { message = "Mỗi lần chỉ nhập tối đa 1.000 thực tập sinh." });

        var headers = nonEmptyRows[0].ToDictionary(cell => cell.Key, cell => NormalizeHeader(cell.Value));
        var fieldColumns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
        {
            var field = HeaderField(header.Value);
            if (field is not null && !fieldColumns.ContainsKey(field)) fieldColumns[field] = header.Key;
        }
        var required = new[] { "Name", "Mssv", "Email", "School", "Major" };
        var missing = required.Where(name => !fieldColumns.ContainsKey(name)).ToArray();
        if (missing.Length > 0)
            return BadRequest(new { message = $"Thiếu cột bắt buộc: {string.Join(", ", missing)}. Tải tệp mẫu để xem tên cột hợp lệ." });

        var created = new List<object>();
        var rejected = new List<object>();
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        for (var index = 1; index < nonEmptyRows.Count; index++)
        {
            var rowNumber = index + 1;
            var row = nonEmptyRows[index];
            string Read(string key) => fieldColumns.TryGetValue(key, out var col) && row.TryGetValue(col, out var value) ? value.Trim() : "";
            var name = Read("Name"); var mssv = Read("Mssv"); var email = Read("Email");
            var school = Read("School"); var major = Read("Major");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(mssv) || string.IsNullOrWhiteSpace(school) || string.IsNullOrWhiteSpace(major)
                || !MailAddress.TryCreate(email, out var parsedEmail) || !string.Equals(parsedEmail.Address, email, StringComparison.OrdinalIgnoreCase))
            {
                rejected.Add(new { row = rowNumber, email, mssv, reason = "Thiếu họ tên, MSSV, trường, chuyên ngành hoặc email không hợp lệ." });
                continue;
            }

            var position = Read("Position"); if (string.IsNullOrWhiteSpace(position)) position = "Thực tập sinh";
            var status = Read("Status"); if (string.IsNullOrWhiteSpace(status)) status = "Đang thực tập";
            var phone = Read("Phone"); var department = Read("Department");
            if (name.Length > 200 || mssv.Length > 50 || email.Length > 100 || phone.Length > 30 || school.Length > 200
                || major.Length > 150 || position.Length > 150 || status.Length > 50 || department.Length > 150)
            {
                rejected.Add(new { row = rowNumber, email, mssv, reason = "Một hoặc nhiều trường vượt quá độ dài cho phép." });
                continue;
            }
            object startDate; object endDate;
            try { startDate = ExcelDate(Read("StartDate")); endDate = ExcelDate(Read("EndDate")); }
            catch (InvalidDataException ex) { rejected.Add(new { row = rowNumber, email, mssv, reason = ex.Message }); continue; }
            if (startDate is DateTime start && endDate is DateTime end && end < start)
            {
                rejected.Add(new { row = rowNumber, email, mssv, reason = "Ngày kết thúc phải sau ngày bắt đầu." });
                continue;
            }
            var password = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                int? mentorId = null;
                var mentorEmail = Read("MentorEmail");
                string? mentorName = null;
                if (!string.IsNullOrWhiteSpace(mentorEmail))
                {
                    await using var mentorLookup = connection.CreateCommand(); mentorLookup.Transaction = transaction;
                    mentorLookup.CommandText = "SELECT Id,FullName FROM dbo.Users WHERE Email=@email AND Role='MENTOR'";
                    mentorLookup.Parameters.AddWithValue("@email", mentorEmail);
                    await using var mentorReader = await mentorLookup.ExecuteReaderAsync(cancellationToken);
                    if (!await mentorReader.ReadAsync(cancellationToken)) throw new InvalidDataException("MentorEmail không khớp tài khoản Mentor nào.");
                    mentorId = mentorReader.GetInt32(0); mentorName = mentorReader.GetString(1);
                    await mentorReader.CloseAsync();
                }

                await using var createUser = connection.CreateCommand(); createUser.Transaction = transaction;
                createUser.CommandText = "INSERT dbo.Users(Username,FullName,Email,PasswordHash,Role) OUTPUT inserted.Id VALUES(@email,@name,@email,@hash,'INTERN')";
                createUser.Parameters.AddWithValue("@email", email); createUser.Parameters.AddWithValue("@name", name); createUser.Parameters.AddWithValue("@hash", passwords.Hash(password));
                var userId = Convert.ToInt32(await createUser.ExecuteScalarAsync(cancellationToken));
                await using var createProfile = connection.CreateCommand(); createProfile.Transaction = transaction;
                createProfile.CommandText = "INSERT dbo.Interns(FullName,Mssv,Email,Phone,School,Major,InternshipPosition,Status,Mentor,Progress,Gpa,Department,StartDate,EndDate,UserId) OUTPUT inserted.Id VALUES(@name,@mssv,@email,@phone,@school,@major,@position,@status,@mentor,0,NULL,@department,@start,@end,@user)";
                createProfile.Parameters.AddWithValue("@name", name); createProfile.Parameters.AddWithValue("@mssv", mssv); createProfile.Parameters.AddWithValue("@email", email);
                createProfile.Parameters.AddWithValue("@phone", DbValue(phone)); createProfile.Parameters.AddWithValue("@school", school); createProfile.Parameters.AddWithValue("@major", major);
                createProfile.Parameters.AddWithValue("@position", position); createProfile.Parameters.AddWithValue("@status", status); createProfile.Parameters.AddWithValue("@mentor", (object?)mentorName ?? DBNull.Value);
                createProfile.Parameters.AddWithValue("@department", DbValue(department)); createProfile.Parameters.AddWithValue("@start", startDate); createProfile.Parameters.AddWithValue("@end", endDate); createProfile.Parameters.AddWithValue("@user", userId);
                var internId = Convert.ToInt32(await createProfile.ExecuteScalarAsync(cancellationToken));
                if (mentorId.HasValue)
                {
                    await using var assign = connection.CreateCommand(); assign.Transaction = transaction; assign.CommandText = "INSERT dbo.MentorInternAssignments(InternId,MentorUserId) VALUES(@intern,@mentor)";
                    assign.Parameters.AddWithValue("@intern", internId); assign.Parameters.AddWithValue("@mentor", mentorId.Value); await assign.ExecuteNonQueryAsync(cancellationToken);
                }
                await transaction.CommitAsync(cancellationToken);
                created.Add(new { row = rowNumber, name, mssv, email, password, mentor = mentorName });
            }
            catch (InvalidDataException ex)
            {
                await transaction.RollbackAsync(cancellationToken);
                rejected.Add(new { row = rowNumber, email, mssv, reason = ex.Message });
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                await transaction.RollbackAsync(cancellationToken);
                rejected.Add(new { row = rowNumber, email, mssv, reason = "Email hoặc MSSV đã tồn tại trong hệ thống." });
            }
            catch (SqlException)
            {
                await transaction.RollbackAsync(cancellationToken);
                rejected.Add(new { row = rowNumber, email, mssv, reason = "Cơ sở dữ liệu không chấp nhận thông tin ở dòng này." });
            }
        }
        return Ok(new { createdCount = created.Count, rejectedCount = rejected.Count, created, rejected, message = $"Đã tạo {created.Count} tài khoản; {rejected.Count} dòng cần kiểm tra." });
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateInternRequest request, CancellationToken cancellationToken)
    {
        var denied = Authorize(out var failure); if (denied) return failure!;
        var errors = Validate(new UpdateInternRequest(request.Name, request.Mssv, request.Email, request.Phone,
            request.School, request.Major, request.Role, request.Status, request.Mentor, request.Progress, request.Gpa));
        if (request.StartDate.HasValue && request.EndDate.HasValue && request.EndDate < request.StartDate)
            errors["endDate"] = "Ngày kết thúc phải sau ngày bắt đầu.";
        if (errors.Count > 0) return BadRequest(new { message = "Thông tin hồ sơ không hợp lệ.", errors });
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(cancellationToken);
        int? mentorId = null;
        if (!string.IsNullOrWhiteSpace(request.Mentor))
        {
            await using var lookupMentor = connection.CreateCommand(); lookupMentor.CommandText = "SELECT TOP 1 Id FROM dbo.Users WHERE Role='MENTOR' AND FullName=@name ORDER BY Id";
            lookupMentor.Parameters.AddWithValue("@name", request.Mentor.Trim());
            var mentorResult = await lookupMentor.ExecuteScalarAsync(cancellationToken);
            if (mentorResult is null) return BadRequest(new { message = "Chọn một tài khoản Mentor hợp lệ." });
            mentorId = Convert.ToInt32(mentorResult);
        }
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT dbo.Interns(FullName,Mssv,Email,Phone,School,Major,InternshipPosition,Status,Mentor,Progress,Gpa,Department,StartDate,EndDate,Batch,UserId)
            OUTPUT inserted.Id VALUES(@name,@mssv,@email,@phone,@school,@major,@position,@status,@mentor,@progress,@gpa,@department,@start,@end,@batch,
              (SELECT Id FROM dbo.Users WHERE Email=@email AND Role='INTERN'));
            """;
        command.Parameters.AddWithValue("@name", request.Name!.Trim()); command.Parameters.AddWithValue("@mssv", request.Mssv!.Trim());
        command.Parameters.AddWithValue("@email", request.Email!.Trim()); command.Parameters.AddWithValue("@phone", DbValue(request.Phone));
        command.Parameters.AddWithValue("@school", request.School!.Trim()); command.Parameters.AddWithValue("@major", request.Major!.Trim());
        command.Parameters.AddWithValue("@position", request.Role!.Trim()); command.Parameters.AddWithValue("@status", request.Status!.Trim());
        command.Parameters.AddWithValue("@mentor", DbValue(request.Mentor)); command.Parameters.AddWithValue("@progress", request.Progress);
        command.Parameters.AddWithValue("@gpa", DbValue(request.Gpa)); command.Parameters.AddWithValue("@department", DbValue(request.Department));
        command.Parameters.AddWithValue("@start", (object?)request.StartDate?.Date ?? DBNull.Value); command.Parameters.AddWithValue("@end", (object?)request.EndDate?.Date ?? DBNull.Value); command.Parameters.AddWithValue("@batch", DBNull.Value);
        try
        {
            var id = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken));
            if (mentorId.HasValue)
            {
                await using var assignment = connection.CreateCommand();
                assignment.CommandText = "INSERT dbo.MentorInternAssignments(InternId,MentorUserId) VALUES(@intern,@mentor)";
                assignment.Parameters.AddWithValue("@intern", id); assignment.Parameters.AddWithValue("@mentor", mentorId.Value);
                await assignment.ExecuteNonQueryAsync(cancellationToken);
            }
            return CreatedAtAction(nameof(GetById), new { id }, new { id, message = "Đã tạo hồ sơ thực tập sinh." });
        }
        catch (SqlException error) when (error.Number is 2601 or 2627)
        { return Conflict(new { message = "Mã số sinh viên này đã tồn tại." }); }
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var denied = Authorize(out var failure);
        if (denied) return failure!;
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = SelectSql + " WHERE Id = @id";
        command.Parameters.AddWithValue("@id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? Ok(ReadIntern(reader))
            : NotFound(new { success = false, message = "Không tìm thấy hồ sơ thực tập sinh.", data = (object?)null });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateInternRequest request, CancellationToken cancellationToken)
    {
        var denied = Authorize(out var failure);
        if (denied) return failure!;
        var errors = Validate(request);
        if (errors.Count > 0)
            return BadRequest(new { success = false, message = "Thông tin hồ sơ không hợp lệ.", errors, data = (object?)null });

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        int? mentorUserId = null;
        if (!string.IsNullOrWhiteSpace(request.Mentor))
        {
            await using var mentorLookup = connection.CreateCommand();
            mentorLookup.CommandText = "SELECT TOP 1 Id FROM dbo.Users WHERE Role='MENTOR' AND FullName=@name ORDER BY Id";
            mentorLookup.Parameters.AddWithValue("@name", request.Mentor.Trim());
            var mentorResult = await mentorLookup.ExecuteScalarAsync(cancellationToken);
            if (mentorResult is null) return BadRequest(new { message = "Chọn một tài khoản Mentor hợp lệ." });
            mentorUserId = Convert.ToInt32(mentorResult);
        }
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Interns SET FullName=@name, Mssv=@mssv, Email=@email, Phone=@phone,
                School=@school, Major=@major, InternshipPosition=@position, Status=@status,
                Mentor=@mentor, Progress=@progress, Gpa=@gpa
            WHERE Id=@id;
            """;
        command.Parameters.AddWithValue("@id", id);
        command.Parameters.AddWithValue("@name", request.Name!.Trim());
        command.Parameters.AddWithValue("@mssv", request.Mssv!.Trim());
        command.Parameters.AddWithValue("@email", request.Email!.Trim());
        command.Parameters.AddWithValue("@phone", DbValue(request.Phone));
        command.Parameters.AddWithValue("@school", request.School!.Trim());
        command.Parameters.AddWithValue("@major", request.Major!.Trim());
        command.Parameters.AddWithValue("@position", request.Role!.Trim());
        command.Parameters.AddWithValue("@status", request.Status!.Trim());
        command.Parameters.AddWithValue("@mentor", DbValue(request.Mentor));
        command.Parameters.AddWithValue("@progress", request.Progress);
        command.Parameters.AddWithValue("@gpa", DbValue(request.Gpa));
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            return NotFound(new { success = false, message = "Không tìm thấy hồ sơ thực tập sinh.", data = (object?)null });

        await using (var assignment = connection.CreateCommand())
        {
            assignment.CommandText = mentorUserId.HasValue
                ? "IF EXISTS(SELECT 1 FROM dbo.MentorInternAssignments WHERE InternId=@id) UPDATE dbo.MentorInternAssignments SET MentorUserId=@mentor,AssignedAt=SYSUTCDATETIME() WHERE InternId=@id ELSE INSERT dbo.MentorInternAssignments(InternId,MentorUserId) VALUES(@id,@mentor)"
                : "DELETE FROM dbo.MentorInternAssignments WHERE InternId=@id";
            assignment.Parameters.AddWithValue("@id", id);
            if (mentorUserId.HasValue) assignment.Parameters.AddWithValue("@mentor", mentorUserId.Value);
            await assignment.ExecuteNonQueryAsync(cancellationToken);
        }

        var data = new InternResponse(id, request.Name!.Trim(), request.Mssv!.Trim(), request.Email!.Trim(),
            request.Phone?.Trim() ?? "", request.School!.Trim(), request.Major!.Trim(), request.Role!.Trim(),
            request.Status!.Trim(), request.Mentor?.Trim() ?? "", request.Progress, request.Gpa?.Trim() ?? "");
        return Ok(new { success = true, message = "Cập nhật hồ sơ thực tập sinh thành công.", data });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var denied = Authorize(out var failure); if (denied) return failure!;
        await using var connection = new SqlConnection(connectionString); await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand(); command.CommandText = "DELETE FROM dbo.Interns WHERE Id=@id"; command.Parameters.AddWithValue("@id", id);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 0 ? NotFound(new { message = "Không tìm thấy hồ sơ." }) : NoContent();
    }

    private bool Authorize(out IActionResult? failure)
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            || !tokens.TryValidate(header[7..].Trim(), out var user))
        {
            failure = Unauthorized(new { success = false, message = "Bạn cần đăng nhập để thực hiện thao tác này." });
            return true;
        }
        if (!RolePermissions.HasPermission(user!.Role, "interns.manage"))
        {
            failure = StatusCode(403, new { success = false, message = "Tài khoản không có quyền quản lý hồ sơ thực tập sinh." });
            return true;
        }
        failure = null;
        return false;
    }

    private static Dictionary<string, string> Validate(UpdateInternRequest r)
    {
        var errors = new Dictionary<string, string>();
        Required(r.Name, "name", "Họ tên"); Required(r.Mssv, "mssv", "MSSV");
        Required(r.School, "school", "Trường"); Required(r.Major, "major", "Chuyên ngành");
        Required(r.Role, "role", "Vị trí"); Required(r.Status, "status", "Trạng thái");
        if (string.IsNullOrWhiteSpace(r.Email) || !MailAddress.TryCreate(r.Email.Trim(), out var address)
            || !string.Equals(address.Address, r.Email.Trim(), StringComparison.OrdinalIgnoreCase))
            errors["email"] = "Email không đúng định dạng.";
        if (r.Phone?.Length > 30 || (r.Phone is not null && r.Phone.Any(c => !char.IsDigit(c) && c is not '+' and not '-' and not ' ' and not '(' and not ')')))
            errors["phone"] = "Số điện thoại không hợp lệ.";
        if (r.Progress is < 0 or > 100) errors["progress"] = "Tiến độ phải từ 0 đến 100.";
        return errors;
        void Required(string? value, string key, string label)
        { if (string.IsNullOrWhiteSpace(value)) errors[key] = $"{label} không được để trống."; }
    }

    private static object DbValue(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    private static object ExcelDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DBNull.Value;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var serial) && serial is >= 15000 and <= 100000)
        {
            try { return DateTime.FromOADate(serial).Date; }
            catch (ArgumentException) { }
        }
        if (DateTime.TryParse(value, CultureInfo.GetCultureInfo("vi-VN"), DateTimeStyles.None, out var date)
            || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out date)) return date.Date;
        throw new InvalidDataException("Ngày bắt đầu hoặc kết thúc không đúng định dạng.");
    }

    private static string NormalizeHeader(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(character))
                builder.Append(char.ToLowerInvariant(character));
        return builder.ToString();
    }

    private static string? HeaderField(string normalized) => normalized switch
    {
        "hoten" or "fullname" or "name" or "internname" => "Name",
        "mssv" or "masinhvien" or "studentid" => "Mssv",
        "email" or "emailtaikhoan" => "Email",
        "sodienthoai" or "phone" or "phonenumber" => "Phone",
        "truong" or "school" or "university" => "School",
        "chuyennganh" or "major" => "Major",
        "vitri" or "position" or "internshipposition" => "Position",
        "trangthai" or "status" => "Status",
        "phongban" or "department" => "Department",
        "ngaybatdau" or "startdate" => "StartDate",
        "ngayketthuc" or "enddate" => "EndDate",
        "mentoremail" or "emailmentor" => "MentorEmail",
        _ => null
    };

    private const string SelectSql = "SELECT Id, FullName, Mssv, Email, Phone, School, Major, InternshipPosition, Status, Mentor, Progress, Gpa FROM dbo.Interns";
    private static InternResponse ReadIntern(SqlDataReader r) => new(
        r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.IsDBNull(4) ? "" : r.GetString(4),
        r.GetString(5), r.GetString(6), r.GetString(7), r.GetString(8), r.IsDBNull(9) ? "" : r.GetString(9),
        r.GetInt32(10), r.IsDBNull(11) ? "" : r.GetString(11));
}
