# Đăng nhập và đặt lại mật khẩu

Mở `Frontend/index.html` bằng Live Server hoặc máy chủ web. Trang đăng nhập sử dụng tài khoản trong bảng `dbo.Users`; mật khẩu phải có định dạng băm do `PasswordHasher` của backend tạo.

Backend sử dụng connection string `ConnectionStrings:InternManagement` hiện có. Khi khởi động, backend bổ sung bảng `dbo.PasswordResetTokens` nếu chưa tồn tại. Không tự tạo tài khoản hoặc hồ sơ thực tập sinh.

Backend dùng PostgreSQL. Cấu hình connection string qua biến môi trường; xem [hướng dẫn PostgreSQL](postgresql.md).

## Cấu hình email thật

Cấu hình `Smtp:Host`, `Smtp:Port`, `Smtp:EnableSsl`, `Smtp:FromAddress`, `Smtp:Username`, `Smtp:Password` qua biến môi trường hoặc .NET User Secrets. Không đưa mật khẩu SMTP vào Git.

Ví dụ tên biến môi trường trong PowerShell:

```powershell
$env:Smtp__Host = "smtp.ten-mien-cua-ban.vn"
$env:Smtp__Port = "587"
$env:Smtp__EnableSsl = "true"
$env:Smtp__FromAddress = "dia-chi-gui-cua-ban"
$env:Smtp__Username = "tai-khoan-smtp-cua-ban"
# Đặt Smtp__Password trong môi trường riêng của bạn.
```

`PasswordReset:ResetPageUrl` phải trỏ đúng URL của `Frontend/reset-password.html` đang phục vụ. Ví dụ, nếu Live Server mở dự án tại `http://localhost:5500/TTCS_T926_K10S6_N1/Frontend/index.html`, URL cần đặt là `http://localhost:5500/TTCS_T926_K10S6_N1/Frontend/reset-password.html`. Khi triển khai, sử dụng HTTPS. Backend không tự suy ra URL từ yêu cầu của người dùng.

Có thể đặt URL bằng biến môi trường `PasswordReset__ResetPageUrl`. Giá trị mặc định để trống để tránh gửi nhầm đường dẫn.

SMTP sử dụng STARTTLS (`EnableSsl: true`), thường qua cổng 587. Khi chưa cấu hình email, biểu mẫu hiển thị lỗi và không báo đã gửi.

## Luồng hoạt động

1. `POST /api/auth/forgot-password` nhận `{ "email": "..." }`.
2. Nếu email đã đăng ký, backend gửi liên kết đặt lại qua SMTP. Thông báo trả về giống nhau cho email tồn tại và không tồn tại.
3. `Frontend/reset-password.html` lấy mã từ phần `#token=...` của liên kết và xóa mã khỏi thanh địa chỉ.
4. `POST /api/auth/reset-password` nhận `{ "token": "...", "password": "..." }` và cập nhật `dbo.Users.PasswordHash`.

Mật khẩu mới có từ 8 đến 200 ký tự. Mã đặt lại được tạo ngẫu nhiên; database chỉ lưu mã băm SHA-256, có hiệu lực 30 phút mặc định và dùng một lần. Một lần đặt lại thành công vô hiệu hóa các mã khác của tài khoản và các phiên đăng nhập cũ. Các endpoint khôi phục giới hạn 10 yêu cầu mỗi địa chỉ IP trong 15 phút; backend không gửi liên tiếp cho cùng tài khoản trong vòng một phút.

## Những phần đã bỏ khỏi luồng sử dụng

Đã bỏ việc tự đăng nhập, dữ liệu dự phòng trong localStorage, số liệu/trường thông tin tự điền và tài khoản tự sinh. Trang tìm kiếm lấy trường/chuyên ngành và số lượng từ API thật. Các trường đang lưu được trong hồ sơ thực tập sinh là họ tên, email, số điện thoại, trường và chuyên ngành; thao tác chỉnh sửa dùng `PUT /api/interns/{id}`.

Trang tài liệu hỗ trợ nộp và tải CV/đơn xin thực tập, lưu trực tiếp trong SQL. Xem [hướng dẫn tài liệu](intern-documents.md). Chưa có chức năng duyệt tài liệu.

Các bản ghi đã tồn tại trong database được giữ nguyên. Nếu cần dọn các bản ghi đã tạo từ phiên bản cũ, hãy xác minh từng bản ghi và sao lưu database trước khi xóa.

## Chạy kiểm thử tích hợp

```powershell
dotnet run --project Backend/InternManagement.Tests
```

Kiểm tra giao diện và thao tác biểu mẫu bằng Chrome:

```powershell
python tests/check_frontend.py
```

Bộ kiểm thử backend hiện vẫn chứa mã khởi tạo SQL Server cũ; chưa chuyển sang PostgreSQL. Các script kiểm tra giao diện vẫn dùng mock API.
