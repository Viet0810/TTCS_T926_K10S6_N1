using Microsoft.Data.SqlClient;

namespace InternManagement.Services;

public sealed class DatabaseInitializer
{
    private readonly PasswordHasher passwords;

    public DatabaseInitializer(PasswordHasher passwords)
    {
        this.passwords = passwords;
    }

    public async Task InitializeAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID(N'dbo.Users', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Users (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
                    Username NVARCHAR(100) NOT NULL CONSTRAINT UQ_Users_Username UNIQUE,
                    FullName NVARCHAR(200) NOT NULL,
                    Email NVARCHAR(254) NOT NULL CONSTRAINT UQ_Users_Email UNIQUE,
                    PasswordHash NVARCHAR(512) NOT NULL,
                    Role VARCHAR(10) NOT NULL CONSTRAINT CK_Users_Role CHECK (Role IN ('ADMIN', 'HR', 'MENTOR', 'INTERN')),
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_Users_CreatedAt DEFAULT SYSUTCDATETIME()
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Role = 'ADMIN')
               AND NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = N'admin.demo')
            BEGIN
                INSERT INTO dbo.Users (Username, FullName, Email, PasswordHash, Role)
                VALUES (N'admin.demo', N'Quản trị viên Demo', N'admin.demo@example.com', @passwordHash, 'ADMIN');
            END;

            IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = N'hr.demo')
            BEGIN
                INSERT INTO dbo.Users (Username, FullName, Email, PasswordHash, Role)
                VALUES (N'hr.demo', N'Chuyên viên HR Demo', N'hr.demo@example.com', @passwordHash, 'HR');
            END;

            IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Username = N'mentor.demo')
            BEGIN
                INSERT INTO dbo.Users (Username, FullName, Email, PasswordHash, Role)
                VALUES (N'mentor.demo', N'Mentor Demo', N'mentor.demo@example.com', @passwordHash, 'MENTOR');
            END;

            IF OBJECT_ID(N'dbo.Interns', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Interns (
                    Id INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Interns PRIMARY KEY,
                    FullName NVARCHAR(200) NOT NULL,
                    Email NVARCHAR(254) NOT NULL CONSTRAINT UQ_Interns_Email UNIQUE,
                    Phone NVARCHAR(20) NOT NULL,
                    School NVARCHAR(200) NOT NULL,
                    Major NVARCHAR(200) NOT NULL,
                    CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_Interns_CreatedAt DEFAULT SYSDATETIMEOFFSET()
                );
            END;

            IF NOT EXISTS (SELECT 1 FROM dbo.Interns)
            BEGIN
                INSERT INTO dbo.Interns (FullName, Email, Phone, School, Major)
                VALUES
                    (N'Nguyễn Hoàng Nam', N'nam.nh210678@sis.hust.edu.vn', N'0982341890', N'Đại học Bách Khoa Hà Nội', N'Công nghệ thông tin'),
                    (N'Trần Mai Phương', N'phuong.tm@vnu.edu.vn', N'0912884123', N'Đại học Công Nghệ - ĐHQGHN', N'Kỹ thuật phần mềm'),
                    (N'Lê Quốc Bảo', N'baolq.ptit@gmail.com', N'0977456321', N'Học Viện Công Nghệ Bưu Chính Viễn Thông', N'An toàn thông tin'),
                    (N'Phạm Thùy Linh', N'linhpt.fpt@fe.edu.vn', N'0945123987', N'Đại Học FPT', N'Kỹ thuật phần mềm'),
                    (N'Vũ Hải Đăng', N'dang.vh215542@sis.hust.edu.vn', N'0904889678', N'Đại học Bách Khoa Hà Nội', N'Khoa học máy tính'),
                    (N'Đặng Thị Ngọc Ánh', N'anh.dtn@neu.edu.vn', N'0963222119', N'Đại học Kinh Tế Quốc Dân', N'Hệ thống thông tin'),
                    (N'Bùi Tuấn Anh', N'anh.bui@hcmut.edu.vn', N'0938114556', N'Đại học Bách Khoa TP.HCM', N'Công nghệ thông tin'),
                    (N'Hoàng Minh Trí', N'tri.hm@vnu.edu.vn', N'0971654321', N'Đại học Công Nghệ - ĐHQGHN', N'Khoa học máy tính'),
                    (N'Ngô Mỹ Duyên', N'duyennm@ptit.edu.vn', N'0988776554', N'Học Viện Công Nghệ Bưu Chính Viễn Thông', N'Thiết kế Đồ họa / UI-UX'),
                    (N'Trịnh Quang Huy', N'huytq@fpt.edu.vn', N'0915908765', N'Đại Học FPT', N'Kỹ thuật phần mềm'),
                    (N'Cao Thảo Vân', N'van.ct218901@sis.hust.edu.vn', N'0944654990', N'Đại học Bách Khoa Hà Nội', N'Công nghệ thông tin'),
                    (N'Lâm Gia Kiệt', N'kietlg@vnu.edu.vn', N'0909333444', N'Đại học Công Nghệ - ĐHQGHN', N'An toàn thông tin');
            END;
            """;
        command.Parameters.AddWithValue("@passwordHash", passwords.Hash("Demo@123456"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
