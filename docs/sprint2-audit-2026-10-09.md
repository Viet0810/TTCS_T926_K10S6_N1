# Sprint 2 audit — 09/10/2026

Phạm vi: US6–US14 và US21–US22. Kiến trúc hiện tại: ASP.NET Core, SQL trực tiếp qua SqlClient, migration idempotent trong DatabaseInitializer; không có DbContext. Frontend HTML thuần, navigation dùng chung từ quyền backend. Không triển khai US15–US20/US23–US24.

## Audit trước thay đổi

| US | Backend | API | Frontend | Menu | Permission | Test | Status |
|---|---|---|---|---|---|---|---|
| US6 | OK | OK | OK | OK | OK | SQL/API/UI | OK |
| US7 | OK | OK | OK | OK | OK | transitions/version/API/UI | OK |
| US8 | SMTP chung | OK | Hiển thị kết quả gửi | qua US7 | HR | SMTP capture + failure | OK; Gmail thực tế manual |
| US9 | Thiếu contract | Thiếu | Thiếu | Thiếu | Thiếu | Thiếu | Cần bổ sung |
| US10 | Thiếu confirmation | Thiếu | Thiếu | Thiếu | Thiếu | Thiếu | Cần bổ sung |
| US11 | Chỉ có ngày | Chỉ tạo/list/xóa ngày | Chỉ form ngày/lịch sử | Có | MANAGE_PROGRAMS | Ngày đã test | Thiếu tên/phòng ban |
| US12 | OK | OK | Form/list riêng | OK | HR ASSIGN_MENTOR | SQL/API/UI | OK |
| US13 | OK | OK | trong chương trình | OK | HR MANAGE_PROGRAMS | SQL/API/UI | OK |
| US14 | OK | Own schedule | OK | INTERN | VIEW_PROFILE + role | ownership/API/UI | OK |
| US21 | Chỉ có bảng/report attendance | Thiếu tự check-in/out | Thiếu | Thiếu | Thiếu | Thiếu | Cần bổ sung |
| US22 | OK | OK | KPI/filter/list/export | OK | HR VIEW_ATTENDANCE_REPORT | SQL/API/UI | OK |

## Các phần bổ sung

### US9/US10 — hợp đồng

`ContractService`, `ContractsController`, `ContractDtos`, pages `contracts.html` (HR) và `intern-contract.html` (INTERN), JS `contracts.js`. Metadata và PDF nằm trong bảng `InternContracts` theo convention lưu VARBINARY của tài liệu hiện có, không lưu path do client chỉ định.

- GET `/api/contracts`; PUT `/api/contracts/{internId}` multipart `file`; GET `/api/contracts/{internId}/file`: HR + MANAGE_CONTRACTS.
- GET `/api/interns/me/contract`, GET `/api/interns/me/contract/file`, PUT `/api/interns/me/contract/confirm` `{version}`: INTERN + VIEW_OWN_CONTRACT.
- Owner của các route `me` lấy từ token → User email → Intern, không nhận owner từ client.
- Tái sử dụng validator PDF hiện có: extension/MIME/header, không rỗng, 5 MB, filename an toàn. Không thêm implementation SMTP.
- Mỗi Intern một hợp đồng; được thay bản chưa xác nhận. ROWVERSION ngăn xác nhận bản đã thay; SQL update có điều kiện ngăn xác nhận lặp. ConfirmedAt UTC persist. Bản đã xác nhận không cho thay nội dung.
- Trạng thái xác nhận suy ra từ ConfirmedAt; không thêm ContractStatus trùng lặp.

### US11/US13 — chương trình theo phòng ban

Bổ sung nullable `Name`, `Department` vào InternshipPrograms, model/request/response và form/list hiện có. Request cũ chỉ có ngày vẫn tương thích; dữ liệu cũ hiển thị chương trình #id/chưa bổ sung. Không tạo bảng phòng ban hoặc module nhóm mới khi source chưa có. Các Intern cùng InternshipProgramId thuộc cùng chương trình; phân công vẫn dùng liên kết này.

Program option và danh sách phân công hiển thị tên/phòng ban nếu có. Lịch cá nhân và báo cáo chuyên cần đọc phòng ban từ chương trình, fallback profile cũ. Search báo cáo hỗ trợ tên/phòng ban chương trình; không đổi field filter đang dùng. StartDate/EndDate vẫn lấy từ chương trình, không tạo bản sao.

### US21 — chấm công cá nhân

`OwnAttendanceService`, `OwnAttendanceController`, page `intern-attendance.html`, JS `intern-attendance.js`.

- GET `/api/interns/me/attendance`; POST `/check-in`, POST `/check-out`: INTERN + OWN_ATTENDANCE.
- Không nhận userId/internId/date/time từ client. Dùng TimeProvider server và UTC+07 để xác định ngày/giờ Việt Nam.
- Dùng lại AttendanceRecords và index hiện có. Transaction khóa owner/range trước đọc/ghi để hai check-in đồng thời chỉ tạo một record.
- Chặn checkout chưa check-in, checkout lặp, check-in khi ngày đã có record; phát hiện dữ liệu nhiều record trong ngày hoặc giờ không hợp lệ thay vì tự đoán record.
- Check-out áp dụng cho ngày Việt Nam hiện tại; WorkingHours tính chênh lệch thực tế, làm tròn theo schema decimal(4,1) hiện có. Không tự trừ nghỉ trưa/đặt giờ đi muộn khi source chưa có chính sách ca làm. Record tự chấm công dùng status ON_TIME hiện có; không thêm enum.
- UI tải lại thời gian đã persist, disabled/loading theo trạng thái. HR report đọc cùng bảng, không dùng mock data.

### Navigation / authorization

Thêm menu chung HR “Quản lý hợp đồng”; INTERN “Hợp đồng thực tập”, “Chấm công”. Permission khai báo thực tại backend và dùng để kiểm tra endpoint; ma trận frontend lấy quyền backend, thêm label tương ứng. ADMIN/MENTOR giữ quyền cũ, không nhận menu HR/INTERN. MustChangePassword vẫn chặn business endpoints mới qua RequestAuthorizationService.

## Migration

`Sprint2CompletionMigration` gọi từ DatabaseInitializer: thêm hai cột nullable vào InternshipPrograms và bảng InternContracts nếu chưa tồn tại. Không sửa bảng AttendanceRecords, không duplicate StartDate/EndDate/MentorUserId/InternshipProgramId, không seed dữ liệu.

## Kiểm thử / giới hạn

- Integration suite trên SQL database tạm: đăng ký/nộp hai tài liệu → duyệt → email failure giữ quyết định → HR upload/thay hợp đồng → own download/xác nhận → chương trình phòng ban → phân công → lịch → check-in/check-out → report. Mỗi API đọc lại dữ liệu đã persist.
- SMTP capture suite độc lập xác nhận email approval/rejection có đúng sender/recipient/reason, không duplicate và không lộ secret. Không gửi Gmail thật trong test tự động.
- Test contract header/extension, version cũ/sai, confirmation đồng thời, confirmed replacement, cross-owner và wrong role. Test checkout trước check-in, duplicate/concurrent check-in/out, timestamp reload, UTC+07 qua ranh giới ngày và số giờ.
- UI mock: upload/download/confirmation/check-in/out, repeated submit, role guards, responsive. Navigation 19 pages × 4 roles × 4 widths (320/390/768/1440).
- Build backend/tests; chạy hồi quy auth, tài liệu, email, assignment, chương trình, attendance, navigation và permissions. Frontend không có bundler.

Manual: Gmail gửi kết quả thực tế; chọn/upload/download PDF qua browser với API thực; xác nhận hợp đồng; check-in/out bằng account Intern và kiểm tra report HR. Không có kết luận gửi Gmail thành công chỉ dựa trên startup configuration.
