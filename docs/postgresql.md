# Cấu hình PostgreSQL

Backend dùng Npgsql và PostgreSQL. Giá trị mặc định trong `appsettings.json` là `localhost:5432`, database `TTCS_T926_K10S6_N1`, user `postgres`. Hãy tạo database và cấp quyền cho user trước khi chạy ứng dụng. Khi khởi động, backend tạo schema `dbo` cùng các bảng còn thiếu; dữ liệu SQL Server cũ không được tự động chuyển đổi.

Đặt connection string đầy đủ qua biến môi trường để không lưu mật khẩu trong Git:

```powershell
$env:ConnectionStrings__InternManagement = "Host=localhost;Port=5432;Database=TTCS_T926_K10S6_N1;Username=postgres;Password=<mat-khau>"
dotnet run --project Backend/InternManagement
```

Nếu PostgreSQL ở máy khác, thay `Host`, `Port`, database và user theo cấu hình đó. Xóa biến môi trường bằng `$env:ConnectionStrings__InternManagement = $null` khi không còn cần.

Sau khi backend khởi động, kiểm tra `GET http://localhost:5024/api/database/status`. Thành công trả `connected: true`, database và role PostgreSQL hiện tại. Endpoint chỉ khả dụng khi kết nối và bước khởi tạo schema thành công.

Để chuyển dữ liệu từ SQL Server, cần xuất/import có kiểm tra kiểu dữ liệu và khóa ngoại riêng. Không trỏ PostgreSQL connection string vào database SQL Server hoặc kỳ vọng backend sao chép dữ liệu tự động.
