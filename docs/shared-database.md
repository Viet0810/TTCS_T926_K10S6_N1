# Database development dùng chung

Trạng thái: đã audit; chưa tạo/kết nối cloud DB và chưa chuyển schema sang EF Core. Hiện dùng SQL Server + Microsoft.Data.SqlClient, không có DbContext/EF migrations. Database local được giữ nguyên.

## 1. Cấu hình connection

Trưởng nhóm tạo Azure SQL Database `InternManagement_Dev`, ưu tiên Southeast Asia nếu gói chọn hỗ trợ. Xem [free offer và giới hạn](https://learn.microsoft.com/en-us/azure/azure-sql/database/free-offer?view=azuresql); không coi gói free là compute không giới hạn. Cho phép IP public của từng thành viên trong firewall, không mở toàn bộ Internet.

Copy ADO.NET connection string trong Azure Portal về máy riêng; thay placeholder bằng thông tin thật tại máy. Dùng `Encrypt=True;TrustServerCertificate=False`, database đúng `InternManagement_Dev`. Trưởng nhóm chia thông tin qua kênh riêng; không gửi credential vào chat, Git hoặc frontend. App dùng user chỉ có quyền CRUD cần thiết; tài khoản apply schema do Migration Owner giữ riêng.

Từ thư mục repo, PowerShell:

```powershell
cd Backend/InternManagement
dotnet user-secrets set "ConnectionStrings:InternManagement" "<CLOUD_CONNECTION_STRING>"
dotnet restore
```

Project đã có UserSecretsId; không cần init lại. Biến môi trường `ConnectionStrings__InternManagement` có thể override User Secrets; không commit giá trị thật. Quay về cấu hình local hiện có bằng `dotnet user-secrets remove "ConnectionStrings:InternManagement"` và bỏ override môi trường nếu có.

## 2. Chạy backend

Sau khi quy trình schema bên dưới được hoàn tất và Owner đã chuẩn bị database:

```powershell
dotnet run --launch-profile http
```

Mỗi máy chạy backend/frontend local; frontend chỉ gọi API local, backend kết nối chung database. Cấu hình shared DB không chuyển dữ liệu local lên cloud tự động.

## 3. Quy tắc migration

Hiện startup chạy `DatabaseInitializer` và có thể sửa schema. **Chưa dùng quy trình startup này cho 10 máy/tài khoản CRUD.** Cần hoàn tất EF Core SQL Server baseline tương thích schema hiện tại, giữ dữ liệu và tách apply schema khỏi startup trước khi rollout team. Không chạy `dotnet ef database update` lúc này vì project chưa có EF migrations.

Sau khi có EF migrations: thay đổi entity → tạo migration → review SQL → commit → Owner apply một lần → cả nhóm pull/chạy app. Không xóa migration cũ, drop/reset database hoặc tự tạo migration trùng thay đổi. Default permission seed không được khôi phục các quyền Admin đã thu hồi.

## 4. Secrets và an toàn dữ liệu

Không commit password/connection string cloud, secret files hoặc thông tin đăng nhập. Không xóa/reset Shared DB; không chạy bộ test hiện tại trực tiếp trên cloud vì runner tạo database tạm bằng quyền quản trị local. Không sửa hoặc xóa database SQL Express đang dùng.

## 5. Migration Owner

Trưởng nhóm chỉ định đúng một người apply schema tại một thời điểm; người merge thay đổi database có thể đảm nhiệm. Thành viên khác không có quyền DDL và không apply migration đồng thời.

## 6. Kiểm tra kết nối và dữ liệu chung

Sau khi tự cấu hình secret, báo đã cấu hình, không gửi giá trị. Kiểm tra `http://localhost:5024/api/database/status` trả `connected: true`; endpoint này chỉ xác nhận kết nối, không chứng minh database là cloud. Cần kiểm tra target/schema mà không log credential, rồi kiểm thử hai backend đọc cùng Intern/Program, quyền RBAC, attendance và dữ liệu sau restart.

CV/application lưu trong `dbo.InternDocuments.Content`; hợp đồng trong `dbo.InternContracts.Content`, dạng binary trong SQL, nên dùng chung theo database (chiếm dung lượng DB). Không triển khai object storage trong task này. Duyệt hồ sơ/hợp đồng có rowversion; phân công/chấm công/quyền có transaction/locking. Chưa xác minh task/report của Sprint 3 hoặc concurrency trên cloud.
