Updated permissions and runtime behavior: [Dynamic RBAC, 2026-10-11](dynamic-rbac-2026-10-11.md). Role restrictions in this earlier report describe historical/default behavior.

Latest UI/RBAC validation: [sprint1-ui-rbac-final.md](sprint1-ui-rbac-final.md) (538 backend checks, live browser/API/SQL and actual process restart).

# Audit yêu cầu Sprint 1 mở rộng — 09/10/2026

Đối chiếu với kết quả và source của lượt Sprint 1 trước; giữ ASP.NET Core/SQL Server/auth/runtime permissions hiện có. Không tạo hệ phân quyền thứ hai.

| US | Backend | API | Frontend | Permission | Test | Status |
|---|---|---|---|---|---|---|
| US1 | Validation/trim/whitelist/persist đúng | POST đã kiểm thử | Form đúng; thiếu skeleton/toast chung | Runtime CREATE | Có | PARTIAL |
| US2 | Ownership email và security fields đúng | PUT đã kiểm thử | View/edit/save/cancel đúng | Runtime UPDATE | Thiếu case HR revoke UPDATE rồi update thật thành công | PARTIAL |
| US3 | School/major/name/email đúng; chưa lọc status riêng | Search parameterized | Toolbar island; thiếu Refresh/status | SEARCH độc lập | Có | PARTIAL |
| US4 | Owner-only PDF/metadata/storage đúng | READ/UPLOAD tách biệt | Custom picker đúng; thiếu skeleton chung | Runtime | Có | PARTIAL |
| US5 | Status/reason/rowversion đúng | Review aliases dùng cùng service | Read-only đúng; thiếu skeleton/toast chung | READ/APPROVE | Regrant chỉ kiểm tra authorized 404; cần decision thật | PARTIAL |
| US39 | Crypto temp password/hash/email/resend đúng | ADMIN actor + permission | Form đúng; confirm native | Runtime | SMTP/API đã có | PARTIAL |
| US40 | DB grants có hiệu lực ngay; default tập trung | Save trả DB state | Thiếu fetch GET sau save, tìm chức năng, Cancel, count, modal/toast | Catalog giới hạn Mentor không thể thêm CREATE/UPDATE dù endpoint chỉ yêu cầu permission | Thiếu grant mới ngoài default | PARTIAL |

## Security / duplication

- Endpoint approve/reject là alias hợp lệ; không thêm service hoặc model role mới.
- Các controller kiểm tra runtime permission ở backend. Account/RBAC vẫn là Admin, review là HR, own documents là Intern; đó là actor scope, không thay thế permission check.
- DTO whitelist không bind Role/PasswordHash/owner ID vào profile. Ownership chỉ lấy user authenticated.
- Dependency CREATE/UPDATE → READ bị khai báo ở cả JS và C#: cần đưa vào catalog backend để UI sử dụng chung.
- Navigation và API URL đã tập trung; không có navigation riêng trong từng page. Giữ Sprint 2/3 và các feature tổng hợp ngoài phạm vi ở chế độ chỉ đọc.
- Không thêm secrets. Diagnostic log có kiểm tra redaction; không loại bỏ log lỗi cần thiết dưới tên cleanup debug.

## Figma gate

Đã đọc skill figma-use/figma-generate-design và thử lại file Sprint 1 hiện có trước khi sửa UI. Figma trả `Starter MCP tool call limit`: không thể tạo các frame mới Admin Dashboard/Shared Modal/Shared Table/Shared Form hoặc hoàn tất pixel polish.

Tiếp tục phần backend, test và bổ sung chức năng/interaction vào layout, token, representative screens đã có. Không tuyên bố đã hoàn tất toàn bộ redesign mở rộng hoặc đã tạo các frame bị chặn.

## Kết quả sau bổ sung

| US | Backend | API | Frontend | Permission | Test | Status |
|---|---|---|---|---|---|---|
| US1 | Giữ validation/whitelist | Giữ POST | Skeleton list, toast, form hiện có | CREATE runtime | PASS | OK |
| US2 | Giữ ownership/security | PUT live permission | Giữ view/edit/save/cancel/reload | HR revoke → 403, regrant → 200 | PASS với profile tồn tại | OK |
| US3 | Thêm lọc Status từ trạng thái source | Search parameterized | School/Major/Status/Refresh; skeleton; giữ island riêng | SEARCH runtime | SQL/API/UI PASS | OK |
| US4 | Giữ owner-only PDF/storage | READ/UPLOAD đúng | Custom picker; loading; toast; read-only | Runtime revoke | PASS | OK |
| US5 | Giữ rowversion/status/reason | APPROVE runtime | Skeleton queue, toast, details/download | Revoke → 403; regrant → xử lý pending thật | PASS | OK |
| US39 | Giữ temp password/hash/email/resend | ADMIN + permission | Modal thay confirm native, toast, skeleton | Actor scope giữ nguyên | SMTP/API/UI PASS | OK |
| US40 | Catalog + DB grants; dependency tập trung backend | Save → GET reload state | Search/count/dirty/cancel/custom checkbox/modal/toast; role/matrix islands | Admin cấp Mentor CREATE/UPDATE ngoài default; essentials khóa | Integration + true viewport PASS | OK |

### Behavior đã xác minh

- `PermissionDescriptor.Requires` là nguồn duy nhất cho dependency trong backend và UI. Default mapping vẫn chỉ ở RolePermissionService; không có permission system thứ hai.
- Catalog phân biệt quyền có thể cấp và quyền mặc định. Mentor mặc định vẫn READ/SEARCH; Admin có thể cấp CREATE/UPDATE hồ sơ. Own document vẫn chỉ Intern, review vẫn HR, quản trị account/RBAC vẫn Admin. Không cấp quyền global profile cho Intern.
- Sau PUT thành công, UI fetch GET matrix và session từ backend; chỉ báo save success khi đọc lại được. Lỗi readback được phân biệt với save failure và có nút tải lại, không giả optimistic success.
- Role tabs/select và số quyền dùng cùng draft; số thay đổi là difference giữa draft và persisted state. Hủy khôi phục persisted state. Đổi role khi dirty và reset default dùng modal; reset chỉ preview, không ghi DB.
- Feature permissions ngoài Sprint 1 vẫn chỉ đọc, không thêm CRUD giả cho Sprint 2/3.
- API áp dụng revocation ngay với token hiện có. Menu/action lấy session mới khi load trang; các trang HR/Intern đã mở trước thay đổi cần refresh để cập nhật UI, API vẫn trả 403 ngay. Không có JWT permission claims tĩnh hoặc cache quyền trong process.
- Shared UI dùng `textContent` cho nội dung biến đổi, dialog keyboard/ESC/focus restoration, toast có role status/alert, motion 120–160 ms và giảm motion theo thiết lập hệ điều hành. Không dùng browser alert/confirm ở RBAC/account.

### Verification cuối

- Backend build: PASS, 0 warnings / 0 errors.
- SQL/API integration suite: **536 PASS**, database tạm đã dọn. Có các case HR UPDATE 403/200, APPROVE pending thật 403/200, thêm Mentor UPDATE ngoài default, remove → API 403, Admin protection, restart/login persistence, direct API bypass, profile ownership, file safety và SMTP capture.
- **13 frontend runners PASS**, gồm smoke HTML/JS/CSS, account modal/resend/delete, RBAC search/count/cancel/reset/toast, navigation trên 19 pages × 4 roles × 4 kích thước; hồi quy Sprint 2 không bị phá.
- CDP kiểm tra viewport CSS thực 320/390/768/1440: PASS cho matrix/documents/reviews. Account/API runner dùng cùng CDP để native dialog close event được kiểm tra thật, thay vì virtual-time dump có thể bỏ qua close event.
- `git diff --check`: PASS. Test hồ sơ cá nhân từng fail khi nhiều Chrome chạy đồng thời; chạy lại riêng PASS, không thay logic để che lỗi.
- Backend localhost đã khởi động lại với build mới, `/api/database/status` trả `connected: true`; frontend port 5500 trả HTTP 200 và có các control mới.

### Artifacts và phạm vi còn thiếu

- [Files changed](sprint1-expanded-files.txt): **56 file trong tổng diff Sprint 1**, bao gồm lượt trước và lượt bổ sung này.
- [RBAC desktop](sprint1-rbac-desktop.png), [RBAC mobile](sprint1-rbac-mobile.png) là ảnh code thật với API fixture trong browser tạm; không phải ảnh Figma hoặc dữ liệu người dùng.
- [Figma hiện có](https://www.figma.com/design/HqJ8BZunjX5lY6MxpyPMkD): design system, 7 desktop + 1 mobile của lượt trước.
- **Chưa hoàn tất toàn bộ redesign mở rộng**: Figma Starter MCP vẫn chặn, nên chưa tạo thêm Admin Dashboard/Shared Modal/Shared Table/Shared Form frames và chưa đồng bộ pixel/motion specifications. Không coi các frame cũ là frame mới hoặc báo tổng yêu cầu UI đã hoàn thành.
- SMTP production không được kiểm thử bằng gửi email cho người thật; transport được xác minh bằng SMTP capture và cấu hình hiện có được giữ, không thêm secret.
