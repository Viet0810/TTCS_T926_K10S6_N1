# Kết nối PostgreSQL local

1. Đảm bảo PostgreSQL đang chạy. Port mặc định là `5432`; bạn có thể kiểm tra tên database, user và port trong pgAdmin hoặc cấu hình PostgreSQL.
2. Sao chép `appsettings.Database.example.json` thành `appsettings.Database.json` trong thư mục này.
3. Sửa `ConnectionStrings:DefaultConnection` với thông tin PostgreSQL local của bạn:

   ```text
   Host=localhost;Port=5432;Database=internmanagement;Username=postgres;Password=MAT_KHAU_POSTGRES_CUA_BAN;
   ```

4. Tạo database `internmanagement` trong PostgreSQL trước khi chạy ứng dụng, hoặc đổi `Database=` sang tên database đã có. User cần quyền kết nối database và tạo bảng.
5. `Jwt:Key` phải là chuỗi bí mật ngẫu nhiên tối thiểu 32 byte.
6. Chạy backend bằng `dotnet run`. Ứng dụng tự tạo bảng `Users`, thêm cột `UserName` nếu cần, và tạo ADMIN ban đầu nếu tài khoản đó chưa có.
7. Mở thư mục `Frontend` tại `http://127.0.0.1:5148` bằng `py -m http.server 5148 --bind 127.0.0.1`. Frontend dùng cổng này cho tất cả trang; backend API mặc định lắng nghe riêng tại `http://127.0.0.1:5024`.

`appsettings.Database.json` bị Git bỏ qua để tránh đưa mật khẩu CSDL và khóa ký JWT lên kho mã nguồn. Không dùng mật khẩu ví dụ trong môi trường thật.

## API hiện có

- `POST /api/auth/login`: đăng nhập bằng email và mật khẩu, trả JWT cùng thông tin user.
- `GET /api/users`: ADMIN xem danh sách tài khoản.
- `POST /api/users`: ADMIN tạo tài khoản HR, MENTOR hoặc INTERN.
- `DELETE /api/users/{id}`: ADMIN xóa tài khoản, không thể tự xóa tài khoản đang đăng nhập.

Backend dùng EF Core provider chính thức của Npgsql cho PostgreSQL. Schema ban đầu được tạo bằng `EnsureCreated`. Nếu cần thay đổi schema sau khi đã triển khai, hãy chuyển sang EF Core migrations trước khi phát hành các thay đổi database.

Tài khoản ADMIN mặc định cho môi trường local là `admin@gmail.com` với mật khẩu `Admin@123`.
