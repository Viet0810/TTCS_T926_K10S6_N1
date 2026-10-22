# Dynamic RBAC — 11/10/2026

## Audit trước thay đổi

Source dùng ASP.NET Core/SqlClient trực tiếp, không có EF DbContext hay bảng UserRole riêng. Role của tài khoản tại Users. PermissionRoles và RolePermissions đã tồn tại và grant đã lưu SQL; token DataProtection không chứa danh sách quyền. RequestAuthorizationService đọc SQL runtime, không dùng JWT/cache quyền.

Điểm cản trở RBAC động: PermissionCatalog giới hạn nhiều permission vào HR/Intern và khóa chức năng ngoài Sprint 1; SaveAsync từ chối thay đổi các grant đó. Controllers và navigation kiểm tra role HR/Intern bên cạnh permission. Hợp đồng, chương trình, phân công dùng quyền gộp cho mọi thao tác. Vì vậy một số grant mới không thể cấu hình hoặc không được sử dụng dù đã cấp.

## Mô hình và migration

Giữ nguyên PermissionRoles(Role) và RolePermissions(Role, Permission). Thêm **Permissions(Code, Module, Action, DisplayName)** vì trước đó chưa có bảng definition tương đương; RolePermissions có FK đến bảng này và PK chống trùng.

32 permission definitions được seed từ code. Metadata catalog và grants trả về từ SQL. `DefaultPermissionsByRole` chỉ là template khởi tạo/reset. Runtime bắt buộc có connection string và đọc RolePermissions, không fallback sang template.

Migration chỉ chuyển quyền gộp đã tồn tại một lần, khi definition mới được giới thiệu:

- MANAGE_CONTRACTS → READ; chuyển thêm CONTRACT_CREATE/CONTRACT_UPDATE cho role đang có quyền cũ.
- MANAGE_PROGRAMS → READ; chuyển thêm PROGRAM_CREATE/PROGRAM_DELETE.
- ASSIGN_MENTOR → READ; chuyển thêm ASSIGNMENT_CREATE/ASSIGNMENT_UPDATE.
- VIEW_PROFILE của Intern → chuyển thêm VIEW_OWN_PROFILE/EDIT_OWN_PROFILE/VIEW_OWN_SCHEDULE, tương ứng chức năng cá nhân trước đây.

Các thao tác mới CONTRACT_DELETE, PROGRAM_UPDATE, ASSIGNMENT_DELETE không tự cấp cho DB đang dùng. Admin bật trên website khi cần. Startup tiếp theo cập nhật definitions nhưng **không ghi đè grants**, kể cả khi đã thu hồi một quyền được chuyển đổi.

## API và enforcement

| Phân hệ | Quyền/route thực tế |
| --- | --- |
| RBAC | GET `/api/permissions` trả roles/catalog/essential; GET `/api/permissions/{role}`; GET `/{role}/defaults`; PUT `/{role}` |
| Hồ sơ chung | VIEW_INTERNS → GET; MANAGE_INTERNS → POST; EDIT_INTERNS → PUT `/api/interns/{id}` |
| Duyệt tài liệu | DOCUMENT_REVIEW_READ → GET/download; APPROVE_DOCUMENTS → quyết định duyệt/từ chối |
| Hợp đồng | MANAGE_CONTRACTS → GET/list/download/options; CONTRACT_CREATE/UPDATE → PUT upload; CONTRACT_DELETE → DELETE `/api/contracts/{internId}` |
| Chương trình | MANAGE_PROGRAMS → GET; PROGRAM_CREATE → POST; PROGRAM_UPDATE → PUT `/{id}`; PROGRAM_DELETE → DELETE `/{id}` |
| Phân công | ASSIGN_MENTOR → GET/list/options; ASSIGNMENT_CREATE/UPDATE → PUT; ASSIGNMENT_DELETE → DELETE `/{internId}` |
| Tài khoản | MANAGE_USERS/CREATE_USER/DELETE_USER → GET/POST/DELETE; VIEW_PROFILE → GET `/api/account/profile` |
| Hồ sơ cá nhân | VIEW_OWN_PROFILE → GET `/api/interns/me`; EDIT_OWN_PROFILE → PUT; VIEW_OWN_SCHEDULE → GET own schedule |
| Dữ liệu cá nhân khác | VIEW_DOCUMENTS/UPLOAD_DOCUMENTS, VIEW_OWN_CONTRACT, OWN_ATTENDANCE bảo vệ API tương ứng |

Permission quyết định truy cập nghiệp vụ, không khóa theo HR/Mentor/Intern nữa. RBAC quản trị vẫn yêu cầu Admin theo yêu cầu. Các kiểm tra role còn lại phục vụ identity, màu giao diện, kiểu tài khoản và tính hợp lệ của **Mentor được chọn**; chúng không chặn role đang có permission nghiệp vụ.

Các API `me` xác định chủ sở hữu từ user ID/email được xác thực; không nhận owner từ client. Cấp quyền không cho phép thay route/query để đọc dữ liệu cá nhân của người khác. Hợp đồng và phân công phân biệt CREATE/UPDATE **trong transaction với row/range lock**, tránh CREATE ghi đè bản ghi đã có hoặc race giữa hai request. Hợp đồng đã xác nhận vẫn không thể thay thế.

Save xác thực Admin, role, code, duplicate, dependency và essential Admin; ghi transaction SQL. Các quyền RBAC chỉ cấp cho Admin; Admin không thể bỏ MANAGE_USERS, CREATE_USER, DELETE_USER, RBAC_READ, MANAGE_PERMISSIONS. Xóa tài khoản Admin vẫn bị bảo vệ tại backend.

## Website

Admin chọn HR/Mentor/Intern, bật/tắt các quyền thực, rồi **Lưu thay đổi**. Checkbox không lấy trạng thái từ template/source. Toàn bộ quyền nghiệp vụ có thể chỉnh; không còn khóa “ngoài Sprint 1”. Quyền đọc được chọn cùng khi bật thao tác cần nó. Reset chỉ preview template backend rồi chờ Save.

Luồng Save: draft → PUT → GET SQL → render → toast thành công. Lỗi PUT giữ draft; lỗi GET sau PUT thông báo đã lưu nhưng cần tải lại. Dirty state, ở lại/bỏ thay đổi, cancel và double-submit guard được giữ.

Menu dựa trên permissions. Các nút hồ sơ, tìm kiếm, duyệt, upload, hợp đồng, chương trình, phân công cập nhật khi session được đọc lại sau Save, tải trang/focus/visibility. Thay đổi từ cửa sổ khác chưa được nhận trước request tiếp theo; API vẫn enforce ngay với token cũ. Không phải đăng nhập lại.

## Kiểm thử

- Database tạm, cleanup sau chạy; không tạo test account trên DB chính.
- Chrome thật: Admin chỉnh HR/Mentor/Intern bằng checkbox/Save qua API/SQL thật. HR UPDATE 403 rồi 200; READ tài liệu 200 và APPROVE 403; Mentor UPDATE được cấp/thu hồi; Intern document READ bị thu hồi trả 403.
- Chrome thật: Admin thêm program READ/CREATE/UPDATE/DELETE cho Mentor; Mentor dùng website tạo/sửa/xóa chương trình, sau thu hồi quyền focus lại thì menu/form biến mất.
- API thật: Mentor tạo/sửa chương trình, tạo/thay/xóa hợp đồng và phân công; CREATE không thay thế UPDATE; thu hồi DELETE trả 403; cấp lại xóa bản ghi thật. Intern được cấp READ chương trình thì GET 200 nhưng POST 403.
- HR CONTRACT_DELETE bật/tắt độc lập dù không có trong defaults. Own-profile READ và UPDATE tách biệt; thu hồi/cấp lại UPDATE trả 403/200. VIEW_PROFILE thực sự enforce trên account profile API.
- Dừng/chạy lại tiến trình API test: grant đã chỉnh giữ nguyên; cùng token HR READ 200/APPROVE 403. Migration chạy lại không reset grant.
- Browser fixture kiểm tra dirty/reset/error/readonly UI, navigation và responsive 320/390/768/1440. Fixture tách biệt các ca browser/API/SQL thật.

Lệnh: `dotnet run --no-restore --project Backend/InternManagement.Tests`. Log validation tại `docs/dynamic-rbac-backend-tests.log` (ignored). Các runner frontend trong `tests/check_*.py`; `check_rbac_live.py` được harness backend gọi với API/database tạm.

## Figma và chạy local

Nguồn nền tảng: https://www.figma.com/design/HqJ8BZunjX5lY6MxpyPMkD. Giữ floating islands, màu Admin purple/HR violet/Mentor blue/Intern green và SVG. Figma trả danh sách screen hiện có, nhưng lần đọc tiếp để cập nhật thiết kế bị chặn **Starter MCP tool call limit**; matrix động mới chưa đồng bộ lên Figma. Screenshot desktop/mobile là giao diện mã chạy với catalog fixture thật, không phải Figma export.

Frontend http://127.0.0.1:5500/ và backend http://localhost:5024/. Ctrl+F5 nếu tab còn script cũ. Đăng nhập tài khoản Admin đang có, chọn **Phân quyền hệ thống**; thay đổi quyền không yêu cầu sửa source/rebuild/restart. Danh sách file tích lũy: `dynamic-rbac-files.txt`.
