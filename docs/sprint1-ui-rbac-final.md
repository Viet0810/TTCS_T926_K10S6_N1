Updated permissions and runtime behavior: [Dynamic RBAC, 2026-10-11](dynamic-rbac-2026-10-11.md). Role restrictions in this earlier report describe historical/default behavior.

# Sprint 1 — UI và RBAC, 09/10/2026

## Kết quả

Admin chỉnh quyền Sprint 1 của HR, Mentor và Intern qua checkbox, PUT và GET đọc lại SQL. Quyền thao tác và quyền đọc tách biệt; quyền quản trị thiết yếu được bảo vệ tại backend. Những chức năng ngoài phạm vi cấu hình Sprint 1 được ghi rõ là cố định.

Đã bổ sung màu Admin `#7C3AED`, HR `#9333EA`, Mentor `#2563EB`, Intern `#16A34A`; icon SVG chung, vòng avatar, role selector, accent island, trạng thái và nút duyệt xanh. Thanh lưu RBAC bám đáy vùng hiển thị; phần tìm kiếm có role, icon và số quyền. Dialog chuyển role có **Ở lại / Bỏ thay đổi**. Illustration vector nhỏ dùng cho đăng nhập, hồ sơ rỗng, tài liệu và tìm kiếm không có kết quả. Motion nhẹ và hỗ trợ reduced motion.

Dashboard Admin đếm tài khoản từ `/api/users`; HR đếm Intern và tài liệu theo trạng thái từ API; Intern lấy hồ sơ và tài liệu của chính mình. Chỉ gọi API khi được cấp quyền, lỗi không biến thành số 0. Mentor không có API đọc số Intern được phân công/task tương ứng nên không tạo các KPI đó.

## Figma

Nguồn nền tảng: https://www.figma.com/design/HqJ8BZunjX5lY6MxpyPMkD. Các island, spacing, typography và screen Sprint 1 đã có từ lượt trước. Bộ màu mới lấy theo yêu cầu trực tiếp của người dùng. Lần kiểm tra hiện tại vẫn nhận lỗi **Figma MCP Starter tool call limit**; palette, icon, dashboard và bản RBAC mới chưa được đồng bộ lên Figma. Không coi Figma đã hoàn tất. Ảnh `sprint1-rbac-desktop.png` và `sprint1-rbac-mobile.png` là screenshot của mã giao diện chạy với fixture catalog thật, không phải ảnh xuất Figma hay dữ liệu sản xuất.

## Audit phân quyền

- Kiến trúc dùng SQL trực tiếp với Microsoft.Data.SqlClient; không có EF DbContext/UserRole và không chuyển sang kiến trúc khác.
- Role tại Users; grant tại PermissionRoles/RolePermissions. Catalog và default mapping tại backend. Migration chỉ seed role mới, không ghi đè grant đã chỉnh sau restart.
- GET `/api/permissions`, GET `/api/permissions/{role}/defaults`, PUT `/api/permissions/{role}`.
- PUT kiểm tra role, code, duplicate, phạm vi role, dependency, essential Admin và thực hiện transaction.
- Token DataProtection chứa identity/password version, không nhúng danh sách permission. Backend đọc grant SQL mỗi request, không cache; phiên đang đăng nhập nhận thay đổi ngay, không cần đăng nhập lại.
- Frontend navigation dựa trên `/auth/me`, cập nhật sau Save, tải trang, focus và visibility. Trang duyệt cập nhật khả năng xem/duyệt khi session được tải lại. Khi cửa sổ liên tục mở mà chưa có request mới, giao diện có thể chưa phản ánh thay đổi từ máy khác; backend vẫn chặn request ngay.
- Reset chỉ lấy preview mặc định; DB đổi khi bấm Save. PUT lỗi giữ draft; PUT thành công nhưng GET lỗi báo đã lưu và cho tải lại, không phát toast thành công giả.

## Bằng chứng kiểm thử

**538 kiểm tra backend PASS**, trên database SQL tạm được xóa sau khi chạy. Không chỉnh grant hay tạo tài khoản trên database đang phục vụ người dùng.

- `check_rbac_live.py`, được gọi từ ApiChecks, chạy Chrome thật và frontend thật, không mock fetch: Admin chọn HR, bỏ/bật UPDATE và Save; cùng token HR nhận 403 rồi 200. Bỏ APPROVE vẫn READ 200 và approve 403. Admin thêm/bớt Mentor UPDATE, API 200/403. Admin chỉnh Intern UPLOAD/READ, grant SQL được đọc lại và Intern GET documents 403 khi thu hồi READ. Khôi phục defaults sau kiểm thử.
- Kiểm tra API riêng xử lý tài liệu pending thật: thu hồi APPROVE trả 403, cấp lại duyệt thành công 200.
- Dừng tiến trình test API và chạy lại bằng cùng SQL: cùng token HR vẫn READ 200/APPROVE 403. Login mới cũng đọc grant đã lưu.
- Request trực tiếp không bypass được actor/permission; backend từ chối xóa quyền Admin thiết yếu. Default preview không sửa SQL.
- Kiểm thử browser fixture: save loading/double submit, lỗi PUT, lỗi GET sau PUT, retry, dirty/cancel/reset, dependency, menu, read-only approval và responsive 320/390/768/1440 px. Fixture chỉ dùng cho kiểm tra UI, tách biệt ca browser/API/SQL thật ở trên.
- `check_dashboard_ui.py` kiểm tra số liệu từ response, không gọi API thiếu quyền, không tạo KPI Mentor giả.
- Các bộ frontend: cấu trúc/auth, accounts, role matrix, permissions, documents, reviews, navigation, own profile, change password, internship, Sprint 2, Sprint 2 completion, viewports, dashboard.
- `dotnet build Backend/InternManagement --no-restore`: **0 warnings, 0 errors**.

## Kiểm thử local

Frontend: http://127.0.0.1:5500/ . Backend: http://localhost:5024/ . `/api/database/status` trả `connected: true`; trang permissions trả HTTP 200. Dùng tài khoản hiện có; không tạo tài khoản demo trên DB chính. Nếu tab đang mở giữ script cũ, tải lại bằng Ctrl+F5.

Danh sách file tích lũy: `sprint1-expanded-files.txt`.
