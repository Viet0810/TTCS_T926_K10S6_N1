# Audit frontend — 09/10/2026

Frontend HTML thuần, không có bundler/package.json. Trước thay đổi: 18 HTML (3 trang công khai, 15 trang sau đăng nhập), 10 CSS, 23 JS, một asset logo CodeGym WebP. Sau thay đổi: thêm một HTML và một JS cho hồ sơ thực tập; không thêm thư viện.

Tất cả endpoint dưới đây có prefix `/api`. Quyền lấy từ backend; bảng là bản đồ chức năng, không cấp thêm quyền. Mọi trang sau đăng nhập dùng `api.js`, `navigation.js`, `base.css`, `style.css`; CSS/JS ghi trong bảng là phần bổ sung.

| Role | Page / chức năng | API | HTML | CSS bổ sung | JS bổ sung |
|---|---|---|---|---|---|
| Công khai | Đăng nhập / Quên mật khẩu | POST auth/login, auth/forgot-password | index.html | auth.css | login.js, auth-ui.js, validation.js |
| Công khai | Đăng ký Intern | POST auth/register | register.html | auth.css, register.css | register.js, auth-ui.js, validation.js |
| Công khai | Đặt lại mật khẩu | POST auth/reset-password | reset-password.html | auth.css | reset-password.js, auth-ui.js, validation.js |
| Tất cả | Dashboard | GET auth/me | pages/dashboard.html | — | dashboard.js |
| Tất cả | Hồ sơ tài khoản | GET auth/me | pages/profile.html | — | profile.js |
| Tất cả | Đổi mật khẩu | PUT account/change-password | pages/change-password.html | auth.css | change-password.js, auth-ui.js, validation.js |
| ADMIN | Danh sách / xóa / gửi lại email tài khoản | GET users, DELETE users/{id}, POST users/{id}/resend-login-email | pages/user-manage.html | — | user-manage.js |
| ADMIN | Tạo tài khoản | POST users | pages/user-create.html | — | user-manage.js, validation.js |
| ADMIN | Ma trận quyền | GET auth/roles/permissions | pages/permissions.html | permissions.css | permissions.js |
| ADMIN, HR, MENTOR | Danh sách / chi tiết thực tập sinh; tạo/sửa theo quyền | GET/POST interns, PUT interns/{id} | pages/intern-manage.html | intern.css | intern.js, intern-profile.js |
| ADMIN, HR, MENTOR | Tìm kiếm, lọc, xuất CSV | GET interns, interns/search | pages/hr-search-filter.html | hr-search-filter.css | hr-search-filter.js, hr-search-filter-service.js, intern-profile.js |
| HR | Phân công Mentor | GET intern-assignments/interns, mentors, programs; PUT intern-assignments/{internId} | pages/mentor-assignment.html | — | mentor-assignment.js |
| HR | Danh sách phân công | GET intern-assignments, intern-assignments/programs | pages/mentor-assignment-list.html | — | mentor-assignment.js |
| HR | Xem / xét duyệt tài liệu | GET document-reviews, interns/{id}, document-reviews/{id}/{kind}; PUT document-reviews/{id}/{kind}/approve hoặc reject | pages/document-reviews.html | document-reviews.css | document-reviews.js, intern-profile.js |
| HR | Thiết lập / lịch sử chương trình | GET/POST program-schedule, DELETE program-schedule/{id} | pages/program-setting.html | program-date-setting.css | program-date-setting.js |
| HR | Báo cáo / lọc / xuất chuyên cần | GET interns, attendance/report | pages/attendance-report.html | attendance-report.css | attendance-report.js, attendance-report-service.js |
| INTERN | Lịch cá nhân | GET interns/me/schedule | pages/intern-schedule.html | — | intern-schedule.js |
| INTERN | Nộp / tải CV và đơn | GET interns/me, interns/me/documents; PUT/GET interns/me/documents/{kind} | pages/intern-upload-cv.html | — | intern-documents.js |
| INTERN | Hồ sơ thực tập cá nhân (mới) | GET interns/me | pages/internship-profile.html | — | internship-profile.js, intern-profile.js |

## Kết quả cấu trúc

- Upload và hồ sơ thực tập là hai tác vụ khác nhau. Chuyển phần hồ sơ chỉ đọc sang `internship-profile.html`, giữ API xác định chủ sở hữu từ token và kiểm tra role INTERN + VIEW_PROFILE. Trang upload vẫn kiểm tra liên kết hồ sơ trước khi bật form.
- CV và đơn ở chung vì cùng workflow nộp tài liệu. Không tách.
- Danh sách thực tập sinh và form tạo/sửa là cùng module CRUD. Form hiện chỉ mở khi chọn thêm/sửa hoặc qua `?edit=...`; giữ nguyên để bảo toàn deep link và luồng sửa từ trang tìm kiếm.
- Bộ lọc, KPI, bảng và dialog chi tiết chuyên cần cùng một báo cáo. Không tách.
- Form ngày chương trình và lịch sử cùng workflow quản lý chương trình. Không tách.
- Tài khoản và US12 đã tách form/list từ trước; giữ nguyên.
- Các HTML dài nhất là intern-manage (nhiều trường hồ sơ), program-setting và attendance-report; độ dài không tự nó là lý do tách nghiệp vụ.

## Shared layout / CSS / JS

- `navigation.js` là nguồn menu chung, lọc bằng session permissions backend, role và pathname. Thêm đúng một mục Hồ sơ thực tập cho INTERN có VIEW_PROFILE. ADMIN/HR/MENTOR không nhận mục này.
- Topbar full width sticky, sidebar dưới header, desktop 240px; mobile menu wrap hiện có, không tạo drawer mới.
- Chuẩn hóa logo CodeGym/tên hệ thống tại báo cáo chuyên cần; loại link Dashboard lặp trong profile HTML và script chương trình thừa trên dashboard.
- `base.css` giữ token, typography, accessibility; `style.css` giữ layout, button, control, table, empty state và badge dùng chung. CSS trang vẫn giữ phần riêng, không rewrite cascade toàn bộ.
- Button chương trình dùng thêm class primary/secondary chung và loại rule màu/hover lặp. Badge tài liệu upload/xét duyệt dùng palette và kích thước chung, không đổi status backend.
- Form auth giữ CSS/error helper riêng. Không gộp regex hoặc đổi validation nghiệp vụ hồ sơ/tài liệu.
- API base URL/auth header/error pipeline đã tập trung trong `api.js`; service tìm kiếm/chuyên cần là adapter nghiệp vụ. Không thay API contract.
- Đăng xuất dùng một handler delegated trong `navigation.js`; loại bỏ các handler Session.logout trùng trong JS trang.
- Kiểm tra role tại từng page vẫn cần trước khi gọi business API. Không xóa các kiểm tra này chỉ vì navigation đã lọc menu.

## UI đã hoàn thiện

- File picker là label có input file thật, hỗ trợ focus bàn phím, click/chọn lại và drag/drop một file. Validation PDF, tên file, MIME, tệp rỗng, 5 MB và FormData giữ nguyên; backend không thay đổi.
- Filename dài ellipsis + title, metadata dung lượng/ngày/status/nhận xét tách dòng; hai card cân layout, actions cùng style, mobile một cột.
- Control nội dung dùng height/radius/focus/disabled chung với selector nhẹ để CSS đặc thù vẫn ưu tiên. Search tài khoản/hồ sơ có icon; ô tìm nhanh chuyên cần bỏ inline style nhỏ.
- Empty state tài khoản không còn tbody trống. Loading bổ sung tại tài khoản, hồ sơ, tài liệu xét duyệt, chương trình; giữ loading sẵn có của các trang khác.
- Thông báo tài khoản chuyển từ alert sang live region inline. Confirm xóa/đổi mật khẩu tạm giữ nguyên: chưa có component xác nhận dùng chung; không bỏ bước xác nhận nghiệp vụ.
- Table giữ dữ liệu/cột/actions thật, cuộn trong container; badge review và button UI mới không đổi handler/API.
- Trạng thái thực tập sinh dùng renderer badge chung trong `InternProfile`, tái sử dụng ở danh sách, tìm kiếm và chi tiết. Email trong bảng danh sách/tìm kiếm không wrap giữa tên miền.

## Kiểm thử

Headless Chrome với API mock: kiểm tra assets/HTML/JS, auth/reset, 4 role/password change, tài khoản, quyền, search/edit, chương trình/chuyên cần, xét duyệt, upload/download/validation và US12/14. Navigation kiểm tra 16 trang × 4 role × 4 viewport (320/390/768/1440). Trang hồ sơ mới có test dữ liệu cá nhân, sai role, hồ sơ rỗng, lỗi và retry. Không ghi database thật trong các test frontend.

Manual còn lại: thao tác toàn luồng trên browser với API thật, chọn file bằng bàn phím/file dialog, kéo thả từ hệ điều hành, tải file thực và đăng xuất. Backend/API/schema/migration không thay đổi trong task này.
