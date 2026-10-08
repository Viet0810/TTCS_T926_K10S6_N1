# Backend và frontend: hồ sơ thực tập sinh

## Cấu trúc backend

`Controllers/` chứa các endpoint; `Services/` xử lý truy cập PostgreSQL qua Npgsql và phân quyền; `DTOs/` khai báo dữ liệu nhận/trả về và các giới hạn kiểm tra. API tìm kiếm sử dụng `Controllers/InternFilterController.cs`, `Services/InternFilterService.cs`, `Services/IInternFilterService.cs` và `DTOs/InternFilterRequest.cs`.

## Quyền và màn hình

| Quyền | ADMIN | HR | MENTOR | INTERN | Chức năng frontend |
| --- | --- | --- | --- | --- | --- |
| `VIEW_INTERNS` | Có | Có | Có | Không | Danh sách/chi tiết hồ sơ |
| `SEARCH_INTERNS` | Có | Có | Có | Không | Tìm kiếm theo từ khóa, trường, chuyên ngành |
| `MANAGE_INTERNS` | Có | Có | Không | Không | Thêm hồ sơ |
| `EDIT_INTERNS` | Có | Có | Không | Không | Chỉnh sửa hồ sơ, bao gồm từ kết quả tìm kiếm |

HR có quyền tìm kiếm và chỉnh sửa riêng. MENTOR giữ quyền tìm kiếm trước đây và chỉ xem hồ sơ. Frontend lấy quyền từ `GET /api/auth/me`; backend kiểm tra lại quyền cho mỗi yêu cầu, dựa vào tài khoản đang có trong PostgreSQL.

## API và dữ liệu

| API | Quyền | Frontend |
| --- | --- | --- |
| `GET /api/interns` | `VIEW_INTERNS` | `API.getInterns()` |
| `GET /api/interns/{id}` | `VIEW_INTERNS` | Chi tiết hồ sơ |
| `GET /api/interns/search?search=...&school=...&major=...` | `SEARCH_INTERNS` | `InternService.getInterns(filters)` |
| `POST /api/interns` | `MANAGE_INTERNS` | `API.createIntern(intern)` |
| `PUT /api/interns/{id}` | `EDIT_INTERNS` | `API.updateIntern(id, intern)` |
| `GET /api/interns/me` | `VIEW_PROFILE` | `API.getMyInternProfile()` |

POST/PUT nhận `fullName`, `email`, `phone`, `school`, `major`. Họ tên/trường/chuyên ngành tối đa 200 ký tự; email tối đa 254 ký tự và phải hợp lệ; số điện thoại có 10–11 chữ số. Hồ sơ trả về có các trường này cùng `id` và `createdAt`.

GET tìm kiếm nhận từ khóa tối đa 254 ký tự; trường và chuyên ngành tối đa 200 ký tự. Trường/chuyên ngành lọc chính xác; từ khóa tìm trong họ tên, email, số điện thoại, trường và chuyên ngành. Mọi giá trị SQL được truyền qua tham số.

PUT trả HTTP 200 sau khi lưu thành công, 400 khi dữ liệu không hợp lệ, 401 khi chưa đăng nhập, 403 khi thiếu quyền, 404 khi hồ sơ không tồn tại và 409 khi email bị trùng. Frontend hiển thị lỗi thật từ API; không có dữ liệu dự phòng.

Nút Chỉnh sửa trong trang tìm kiếm dẫn tới `intern-manage.html?edit={id}`. Trang quản lý kiểm tra `EDIT_INTERNS`, tải hồ sơ thật rồi mở biểu mẫu tương ứng. Ma trận phân quyền và menu bảng điều khiển sử dụng cùng mã quyền với backend.

Backend hỗ trợ nộp và tải CV/đơn xin thực tập qua `/api/interns/me/documents`, quyền `UPLOAD_DOCUMENTS` dành cho INTERN. HR/ADMIN xem và duyệt qua `/api/document-reviews`, quyền `APPROVE_DOCUMENTS`. Chi tiết trong [hướng dẫn tài liệu](intern-documents.md).

SQL và nghiệp vụ tài khoản do `AccountService` xử lý; tài liệu do `InternDocumentService` xử lý. Controller giữ việc kiểm tra quyền và trả response. Middleware chung ghi lỗi theo method/path/endpoint/status/trace ID. Header `X-Request-ID` được cung cấp cho frontend để đối chiếu log; JSON thành công, validation và lỗi nghiệp vụ giữ cấu trúc cũ. Lỗi chưa xử lý trả JSON an toàn, không trả exception. `api.js` dùng `ApiError.status` thay vì suy đoán lỗi từ nội dung thông báo; `Session` chỉ xóa các khóa phiên đăng nhập, không xóa dữ liệu localStorage khác.

## Kiểm tra

Project `Backend/InternManagement.Tests` hiện vẫn dùng SQL Server và chưa được chuyển sang PostgreSQL. Chạy `python tests/check_permissions_ui.py` để kiểm tra các nút theo quyền, liên kết mở hồ sơ và dữ liệu gửi từ biểu mẫu trong Chrome.
