# Email tài khoản và đổi mật khẩu

SMTP dùng chung nằm trong `PasswordResetService`. Cấu hình qua biến môi trường hoặc secret provider của ASP.NET Core, không ghi mật khẩu vào source:

| Biến môi trường | Giá trị |
| --- | --- |
| `Smtp__Host` | Host của nhà cung cấp; Gmail: `smtp.gmail.com` |
| `Smtp__Port` | Gmail: `587` |
| `Smtp__EnableSsl` | Gmail: `true` |
| `Smtp__FromAddress` | Email gửi |
| `Smtp__Username` | Tài khoản SMTP |
| `Smtp__Password` | App Password/SMTP secret, chỉ cấu hình trên máy |
| `Frontend__LoginUrl` | URL trang đăng nhập của môi trường đang chạy |
| `PasswordReset__ResetPageUrl` | URL trang reset password |

Khởi động lại backend sau khi cấu hình. Email tài khoản không phụ thuộc URL reset; nếu chưa cấu hình URL đăng nhập, email vẫn gửi thông tin đăng nhập nhưng không thêm link.

- Admin tạo HR/Mentor: tự sinh mật khẩu tạm 10 ký tự bằng `RandomNumberGenerator`, lưu hash và `MustChangePassword=true`, gửi mật khẩu tạm qua email. API không nhận mật khẩu cho hai role này từ form và không trả mật khẩu.
- Admin gửi lại: `POST /api/users/{id}/resend-login-email`, tạo mật khẩu tạm mới; mật khẩu và phiên cũ mất hiệu lực. Nếu SMTP lỗi, tài khoản vẫn tồn tại và phản hồi `emailSent=false`; sửa cấu hình rồi gửi lại.
- Mọi role đổi mật khẩu của chính mình: `PUT /api/account/change-password`. Sau khi đổi, cờ bắt đổi tắt, các reset token cũ bị vô hiệu hóa và API cấp token phiên mới.
- Phiên cần đổi mật khẩu chỉ được gọi `/api/auth/me` và API đổi mật khẩu; các API nghiệp vụ bị chặn phía server.
- Intern tự đăng ký có cờ mặc định false. Migration không tự đánh dấu tài khoản cũ vì không xác định được mật khẩu hiện tại là tạm hay do người dùng tự đặt; Admin có thể gửi lại email để cấp mật khẩu tạm mới.

Kiểm thử SMTP dùng listener local trong database test riêng. Kiểm thử gửi tới hộp thư thật cần cấu hình SMTP hợp lệ.

Email kết quả xét duyệt dùng `PasswordResetService.SendReviewResultAsync` và cùng transport SMTP. Chỉ gửi sau transition pending → approved/rejected lưu thành công, đến email chủ hồ sơ trong database; rejected dùng lý do thực tế. GET và quyết định lặp không gửi email. Nếu SMTP lỗi, kết quả duyệt được giữ và API trả `emailSent=false`; frontend thông báo rõ. Hiện chưa có hàng đợi tự thử lại email xét duyệt.

## User Secrets trong Development

Project đã có `UserSecretsId`; `WebApplication.CreateBuilder(args)` tự nạp User Secrets trong Development. Service đọc trực tiếp `IConfiguration.GetSection("Smtp")`, không có class binding EmailSettings/SmtpSettings.

Chạy trong thư mục `Backend/InternManagement`, thay các placeholder bằng giá trị trên máy:

```powershell
dotnet user-secrets init
dotnet user-secrets set "Smtp:Host" "smtp.gmail.com"
dotnet user-secrets set "Smtp:Port" "587"
dotnet user-secrets set "Smtp:EnableSsl" "true"
dotnet user-secrets set "Smtp:FromAddress" "<email-gmail-gửi>"
dotnet user-secrets set "Smtp:Username" "<email-gmail-gửi>"
dotnet user-secrets set "Smtp:Password" "<Gmail-App-Password>"
dotnet run --launch-profile http
```

Secrets nằm trong profile người dùng bên ngoài repository; không dùng `dotnet user-secrets list` để đưa các giá trị vào log/chat. Biến môi trường `Smtp__...` có thể ghi đè User Secrets.

Log Development in Host và trạng thái có/không của Username/Password/FromAddress. Log lỗi email gồm exception type, message đã che mật khẩu/token, status SMTP và stage `Configuration`, `MessageConstruction` hoặc `SMTP.SendMailAsync`. `SmtpClient` không cung cấp callback tách từng bước TCP/TLS/AUTH; không tự suy đoán stage con.
