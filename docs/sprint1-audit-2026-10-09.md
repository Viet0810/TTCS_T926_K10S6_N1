# Sprint 1 audit — 09/10/2026

> Báo cáo của lượt triển khai đầu. Kết quả mới nhất theo yêu cầu mở rộng nằm trong [sprint1-expanded-audit.md](sprint1-expanded-audit.md).

## Trước khi sửa

Kiến trúc thực tế: ASP.NET Core 9, Microsoft.Data.SqlClient, SQL Server; HTML/CSS/JS thuần. Không có EF/DbContext, bảng Role/UserRole/Permission hay authorization handler/policy: role nằm trong Users, controller gọi RequestAuthorizationService, RolePermissionService chứa mapping cố định. Migration chạy qua DatabaseInitializer. Giữ framework, engine, storage PDF VARBINARY và contract API hiện có.

| US | Backend | API | Frontend | Permission | Test | Status |
|---|---|---|---|---|---|---|
| US1 | INSERT whitelist, trim, unique email | POST /interns | Form island, nhiều trường tùy chọn luôn mở | MANAGE_INTERNS | SQL/API có sẵn | PARTIAL |
| US2 | UPDATE whitelist; đổi email mất liên kết account | PUT /interns/{id} | Detail/edit/save/cancel, reload | EDIT_INTERNS | Roundtrip có sẵn; thiếu ownership sau đổi email | BROKEN |
| US3 | Search tham số school/major | GET /interns/search | Filter thật; tải options bằng API cần quyền khác | SEARCH_INTERNS | SQL/API/UI có sẵn | PARTIAL |
| US4 | Own profile qua email, PDF signature/MIME/5 MB | Own document endpoints | Custom picker/drop/metadata | GET đang check quyền upload; chưa khóa actor khi cấp quyền sai | SQL/API/UI có sẵn | PARTIAL |
| US5 | Pending/approved/rejected, reason, rowversion | List/download/review + alias approve/reject | Queue/filter/detail; thiếu size; phụ thuộc quyền profile | READ đang dùng APPROVE_DOCUMENTS | SQL/API/UI có sẵn | PARTIAL |
| US39 | Hash, crypto RNG 10 chars, MustChangePassword, SMTP, resend rotate | POST /users; resend | Form theo role, khóa double submit | CREATE_USER + ADMIN | SQL/API/SMTP có sẵn | PARTIAL |
| US40 | Mapping static, chưa persist/update/reset | Chỉ GET mapping | Permission cards chỉ đọc | Permission enforcement có nhưng chưa cấu hình | Thiếu revoke/regrant/reset/protection | MISSING |

Baseline: `dotnet run --no-restore --project Backend/InternManagement.Tests` PASS, database test tạm được xóa. Restore cần quyền mạng; Windows Authentication cần chạy ngoài sandbox để tránh SSPI.

## Các phát hiện liên quan

- API approve/reject là alias dùng chung Review, không có service duyệt bị nhân đôi; giữ tương thích.
- Validation frontend/backend cần cả hai; không coi đó là duplication cần xóa. Mapper profile dùng chung.
- Navigation dùng chung, nhưng menu hồ sơ hiển thị với CREATE/UPDATE trong khi page bắt buộc READ; search lấy options bằng READ dù đã có SEARCH.
- Default permissions hard-code tập trung backend nhưng trang chưa chỉnh được. Giữ mã action đã có; không giả CRUD cho quyền tổng hợp Sprint 2.
- Users.Username NVARCHAR(100) trong khi Email cho phép 254: tạo email dài có thể lỗi SQL. Mở giới hạn username phù hợp email bằng migration.
- HR đổi email profile có thể chuyển ownership sang tài khoản khác; cần transaction kiểm tra account trùng và đồng bộ account đang liên kết.
- SMTP secret không có trong appsettings; dùng environment/User Secrets. Giữ transport email hiện tại.
- Frontend API URL tập trung api.js nhưng cố định localhost:5024; bổ sung cấu hình override, giữ mặc định development.
- `/api/database/status` lộ thông tin database/login: chỉ giữ health connected để test startup, không trả thông tin login.
- Sprint 2 dùng một số feature permission tổng hợp; hiển thị cột Chức năng riêng trong matrix, chỉ đọc trong Sprint 1 để không phát sinh UI CRUD giả.

## Design source

[Sprint 1 Figma](https://www.figma.com/design/HqJ8BZunjX5lY6MxpyPMkD). Tạo file mới qua Figma connector; token/component và representative frames trước khi sửa CSS lớn. Font sản phẩm Segoe UI/Arial không có trên Figma server, frame dùng Noto Sans làm fallback có tiếng Việt; code giữ font hiện có.

## Kết quả sau sửa

| US | Backend | API | Frontend | Permission | Test | Status |
|---|---|---|---|---|---|---|
| US1 | Trim trước validation; whitelist; email unique | POST /interns, 400/409 hợp lệ | Form hai cột; nhóm thông tin thực tập tùy chọn thu gọn | CREATE độc lập | SQL/API + UI | OK |
| US2 | Transaction đồng bộ email/name của account Intern; chặn chuyển sang account khác | PUT /interns/{id} | Detail/edit/save/cancel; reload backend | UPDATE + READ | Cùng token giữ profile/document; role/hash không đổi | OK |
| US3 | Search school/major hiện có được giữ nguyên | GET /interns/search | Options lấy bằng SEARCH; không phụ thuộc VIEW_INTERNS | SEARCH độc lập | SQL/API/UI filter | OK |
| US4 | Actor INTERN; ownership từ account; PDF/MIME/signature/5 MB | GET cần READ; PUT cần UPLOAD | Picker/drop/metadata/download; mode chỉ đọc | VIEW_DOCUMENTS / UPLOAD_DOCUMENTS | Ownership, thu hồi upload, UI chỉ đọc | OK |
| US5 | Status/reason/rowversion và service email hiện có | GET/download READ; review APPROVE; giữ alias | Thêm size; mode chỉ đọc; chi tiết không đòi quyền hồ sơ khác | DOCUMENT_REVIEW_READ / APPROVE_DOCUMENTS | Pending/final/concurrency; revoke/regrant; UI chỉ đọc | OK |
| US39 | Giữ crypto password generator/hash/MustChangePassword/resend; Username 254 | Tạo HR/Mentor/Intern và resend hiện có | Giữ form và guard submit hiện có | ADMIN + CREATE_USER | SQL/API/SMTP capture, không lộ password | OK |
| US40 | Persist RolePermissions; seed một lần; live enforcement | GET catalog; GET defaults; PUT role; validation | Tabs/select; matrix/cards; staged save/reset; busy; menu cập nhật | Essential Admin bảo vệ; actor scope | Same-token revoke/regrant; restart/login; UI menu | OK |

### RBAC contract và migration

- `PermissionRoles` và `RolePermissions` được thêm bởi DatabaseInitializer khi khởi động; không đổi engine hoặc storage tài liệu. Các bảng nghiệp vụ hiện có được giữ.
- Defaults chỉ seed khi role mới được tạo. Khởi động lại không ghi đè quyền đã thu hồi, kể cả role có ít quyền hơn mặc định.
- `GET /api/permissions` trả roles + catalog + essentialAdminPermissions. `GET /api/permissions/{role}/defaults` chỉ đọc. `PUT /api/permissions/{role}` nhận `{ "permissions": [...] }`, validate toàn bộ và ghi trong transaction.
- Backend giới hạn role/actor đúng phạm vi các API hiện có. Khi cấp CREATE/UPDATE/APPROVE, quyền READ cần thiết phải có; UI tự chọn/bỏ theo dependency và backend vẫn validate.
- Quyền FEATURE tổng hợp ngoài Sprint 1 được hiển thị cố định; không tạo action CRUD giả. API legacy `/auth/roles/permissions` được giữ và dùng quyền RBAC_READ.
- `window.INTERN_API_BASE_URL` có thể được khai báo trước api.js cho môi trường triển khai; mặc định development được giữ. SMTP vẫn lấy cấu hình environment/User Secrets hiện có, không thêm secret vào repo.

### Verification

- `dotnet build Backend/InternManagement.Tests --no-restore`: PASS, 0 warnings / 0 errors.
- `dotnet run --no-restore --project Backend/InternManagement.Tests`: **524 PASS**; SQL thực trong database tạm riêng; database đã được dọn; SMTP capture dùng loopback và không gửi cho người thật.
- 12 frontend runners hiện có: PASS (`check_frontend`, `check_api_ui`, `check_permissions_ui`, `check_role_cards_ui`, `check_navigation_ui`, `check_documents_ui`, `check_document_reviews_ui`, `check_own_profile_ui`, `check_change_password_ui`, `check_internship_ui`, `check_sprint2_ui`, `check_sprint2_completion_ui`).
- `python tests/check_sprint1_viewports.py`: **12 PASS** tại viewport CSS thực 320/390/768/1440 cho matrix, documents và reviews. Dùng CDP vì window-size của Chrome có minimum; cần package websocket-client hiện có trong môi trường development.
- `git diff --check`: PASS. Frontend thuần HTML/CSS/JS không có pipeline bundle riêng; checker kiểm tra cấu trúc HTML/assets, cú pháp toàn bộ JS và CSS.
- Ảnh giao diện code dùng fixture giả lập: [desktop](sprint1-rbac-desktop.png), [mobile](sprint1-rbac-mobile.png).

### Figma và giới hạn còn lại

- Design system: hai variable collections Primitives/Light, semantic aliases cho màu, radius/padding/spacing, text/effect styles; components Island, Field, Button, Upload.
- 7 desktop frames bắt buộc và 1 mobile RBAC đã tạo và lưu. IDs nằm trong [sprint1-design.json](sprint1-design.json). Frame đã được xem qua screenshot; RBAC đã chỉnh đúng phạm vi role, form chuyển hai cột.
- Code áp dụng surface trắng, nền #F4F7FB, radius 16, padding 24, shadow nhẹ; matrix mới dùng token/layout và shared navigation của sản phẩm.
- **Figma Starter MCP hết lượt** ở bước tinh chỉnh cuối. File và frames vẫn truy cập được; việc polish thêm nhãn tiếng Việt, chiều cao field trong frame hai cột và đồng bộ chi tiết pixel giữa Figma/code chưa hoàn tất. Không tuyên bố pixel-perfect.
- Không còn blocker build/test/chức năng đã xác minh. SMTP triển khai thực vẫn cần cấu hình hiện có; kiểm thử giao nhận dùng SMTP capture.

### Files changed

38 file có thay đổi nội dung hoặc được thêm:

```text
Backend/InternManagement.Tests/ApiChecks.cs
Backend/InternManagement.Tests/Sprint1ProfileChecks.cs
Backend/InternManagement.Tests/Sprint1RbacChecks.cs
Backend/InternManagement/Controllers/AuthController.cs
Backend/InternManagement/Controllers/DocumentReviewsController.cs
Backend/InternManagement/Controllers/InternDocumentsController.cs
Backend/InternManagement/Controllers/InternsController.cs
Backend/InternManagement/Controllers/PermissionsController.cs
Backend/InternManagement/DTOs/CreateInternRequest.cs
Backend/InternManagement/Infrastructure/Sprint1PermissionMigration.cs
Backend/InternManagement/Program.cs
Backend/InternManagement/Services/AccountService.cs
Backend/InternManagement/Services/DatabaseInitializer.cs
Backend/InternManagement/Services/InternDocumentService.cs
Backend/InternManagement/Services/InternService.cs
Backend/InternManagement/Services/PermissionCatalog.cs
Backend/InternManagement/Services/RolePermissionService.cs
Frontend/css/base.css
Frontend/css/intern.css
Frontend/css/permissions.css
Frontend/css/style.css
Frontend/js/api.js
Frontend/js/document-reviews.js
Frontend/js/hr-search-filter.js
Frontend/js/intern-documents.js
Frontend/js/navigation.js
Frontend/js/permissions.js
Frontend/pages/intern-manage.html
Frontend/pages/permissions.html
tests/check_document_reviews_ui.py
tests/check_documents_ui.py
tests/check_permissions_ui.py
tests/check_role_cards_ui.py
tests/check_sprint1_viewports.py
docs/sprint1-audit-2026-10-09.md
docs/sprint1-design.json
docs/sprint1-rbac-desktop.png
docs/sprint1-rbac-mobile.png
```
