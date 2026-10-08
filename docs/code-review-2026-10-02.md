# Rà soát và làm mịn code — 02/10/2026

Đã rà soát Controllers, Services, DTOs, Models, Program.cs, HTML/CSS/JavaScript và các test hiện có. Thay đổi theo từng nhóm có build và kiểm tra hồi quy. Không xóa chức năng/file, không thêm package, không thay schema/dữ liệu ứng dụng, không đổi endpoint/HTTP method/field JSON.

## Các nhóm đã hoàn thiện

1. Middleware xử lý lỗi và log dùng chung. Giữ response validation/nghiệp vụ hiện có; lỗi chưa xử lý trả JSON an toàn. Log ghi method, path không có query, endpoint/controller/action, HTTP status, trace ID và exception. Console Backend có timestamp UTC. Header `X-Request-ID` được expose qua CORS để FE đối chiếu log. Không thêm log request body, Authorization, password/hash/token hay connection string.
2. Tách SQL/nghiệp vụ tài khoản sang `AccountService`; tài liệu sang `InternDocumentService`. Controller kiểm tra quyền, nhận request và chọn response. Danh sách tài liệu dùng DTO rõ kiểu thay cho object ẩn danh; field JSON giữ nguyên. Transaction thay file và kiểm tra ROWVERSION giữ nguyên. Gom response từ chối quyền bị lặp; không đổi ma trận quyền.
3. FE dùng một luồng `requestApi` cho JSON/blob. `ApiError` lưu status, endpoint, method, timestamp, category và trace ID. Console chỉ ghi metadata cùng thông báo chung, không in query/body/header/server exception. Lỗi JSON hỏng và HTTP 204 được xử lý rõ. Các trang chuyển từ kiểm tra chuỗi thông báo sang status khi quyết định chuyển trang.
4. Gom thao tác phiên đăng nhập trong `Session`; chỉ xóa token/role/user, giữ dữ liệu localStorage khác. Quản lý tài khoản dùng permission Backend và chặn request tạo/xóa lặp; mất mạng/HTTP 500 không tự đăng xuất. Gom tải blob, tách hàm validation/DOM/trạng thái xử lý tại các màn hình hồ sơ/tài liệu. Cấu hình trường hồ sơ được trình bày nhiều dòng để dễ bảo trì.
5. Hash mật khẩu lưu sai định dạng/iteration không hợp lệ/key rỗng bị từ chối thay vì gây exception. Thuật toán và hash hợp lệ không đổi. Luồng email chỉ trả các exception nghiệp vụ có thông báo chủ động an toàn, không trả tùy ý `InvalidOperationException.Message`.

## Danh sách file của đợt này

Đường dẫn tương đối từ root project. “Mới” là file bổ sung trong đợt rà soát này, không tính các file đã có từ yêu cầu trước.

| Nhóm | File | Thay đổi |
| --- | --- | --- |
| Cấu hình mới | `.editorconfig` | Quy ước UTF-8, thụt dòng và newline |
| BE | `Backend/InternManagement/Program.cs` | DI service, middleware, timestamp log, CORS trace header, lỗi health check an toàn |
| BE mới | `Backend/InternManagement/Infrastructure/ApiRequestMiddleware.cs` | Xử lý/log lỗi chung và trace ID |
| BE mới | `Backend/InternManagement/Infrastructure/AuthorizationResponses.cs` | Gom response từ chối quyền giữ contract |
| BE | `Backend/InternManagement/Controllers/AuthController.cs` | Dùng AccountService, log lỗi recovery, exception nghiệp vụ an toàn |
| BE | `Backend/InternManagement/Controllers/UsersController.cs` | Chuyển SQL sang AccountService |
| BE | `Backend/InternManagement/Controllers/InternsController.cs` | Lấy tài khoản qua service, response quyền dùng chung |
| BE | `Backend/InternManagement/Controllers/InternFilterController.cs` | Response quyền dùng chung |
| BE | `Backend/InternManagement/Controllers/InternDocumentsController.cs` | Chuyển SQL/validation file sang service, bỏ filter bị lặp |
| BE | `Backend/InternManagement/Controllers/DocumentReviewsController.cs` | Service tài liệu, đánh giá quyền một lần, tách đọc version, bỏ filter bị lặp |
| BE mới | `Backend/InternManagement/Services/AccountService.cs` | Xác thực và truy cập SQL tài khoản |
| BE mới | `Backend/InternManagement/Services/InternDocumentService.cs` | Chủ hồ sơ, danh sách, validation, lưu/tải, duyệt |
| BE mới | `Backend/InternManagement/DTOs/DocumentDtos.cs` | DTO tài liệu/request duyệt giữ field hiện tại |
| BE | `Backend/InternManagement/Services/InternRecordMapper.cs` | Gom trim/null/tham số chuỗi bị lặp |
| BE | `Backend/InternManagement/Services/PasswordHasher.cs` | Từ chối hash lưu hỏng an toàn |
| BE | `Backend/InternManagement/Services/PasswordResetService.cs` | Exception nghiệp vụ chỉ chứa thông báo an toàn |
| BE mới | `Backend/InternManagement/Services/PasswordRecoveryUnavailableException.cs` | Phân biệt lỗi recovery dự kiến với lỗi server |
| FE | `Frontend/js/api.js` | ApiError, request JSON/blob, metadata debug, Session, API duyệt, tải blob dùng chung |
| FE | `Frontend/js/auth-ui.js` | Dùng Session.clear |
| FE | `Frontend/js/dashboard.js` | Session dùng chung, kiểm tra 401 bằng status |
| FE | `Frontend/js/profile.js` | Session dùng chung, kiểm tra 401 bằng status |
| FE | `Frontend/js/permissions.js` | Kiểm tra 401/403 bằng status |
| FE | `Frontend/js/user-manage.js` | Permission, giữ phiên khi lỗi server, chặn gửi lặp, phân biệt lưu thành công/tải lại lỗi |
| FE | `Frontend/js/intern.js` | Tách đọc form/validation/trạng thái lưu; Session/permission dùng chung |
| FE | `Frontend/js/hr-search-filter.js` | Session/permission dùng chung |
| FE | `Frontend/js/hr-search-filter-service.js` | Tải CSV bằng hàm blob dùng chung |
| FE | `Frontend/js/intern-profile.js` | Trình bày cấu hình trường/nhóm rõ ràng |
| FE | `Frontend/js/intern-documents.js` | Hàm gắn sự kiện theo loại tài liệu, Session, tải blob dùng chung |
| FE | `Frontend/js/document-reviews.js` | Tách mở form/validation/busy/download/init, dùng API chung |
| Test BE | `Backend/InternManagement.Tests/ApiChecks.cs` | Chạy test tài khoản và kiểm tra log |
| Test BE mới | `Backend/InternManagement.Tests/AccountChecks.cs` | CRUD, response/error contract, trace/CORS, hash hỏng |
| Test FE mới | `tests/check_api_ui.py` | HTTP/network/JSON/204, debug metadata, session, quản lý tài khoản |
| Tài liệu | `docs/backend-frontend-contract.md` | Đồng bộ kiến trúc và luồng duyệt hiện có |
| Tài liệu | `docs/intern-documents.md` | Trỏ tới service/middleware thay controller SQL/filter |
| Tài liệu mới | `docs/code-review-2026-10-02.md` | Báo cáo này |

## Contract và phân quyền

Login giữ `token`, `user`; phiên hiện tại giữ `user`, `permissions`. Danh sách vẫn trả array; tạo tài khoản giữ `success/message/data` và HTTP 201; xóa giữ HTTP 204; tài liệu giữ `message` và các trường metadata hiện có. Không ép mọi endpoint sang một envelope mới. Response validation chuẩn ASP.NET Core giữ `errors`. Lỗi chưa xử lý dùng `success/message/data` hiện có, riêng tài liệu giữ thông báo/shape `message` cũ. Bổ sung header `X-Request-ID`, không thêm field bắt buộc vào JSON.

Luồng quyền: đăng nhập → phát token → mỗi request xác minh token/tài khoản SQL hiện tại → `RolePermissionService` trả/kiểm tra permission → controller cho phép hoặc trả 401/403. FE đọc permission qua `/api/auth/me` và dùng `Session.hasPermission`; quyết định cuối vẫn ở Backend. Ma trận ADMIN/HR/MENTOR/INTERN không đổi.

## Các rủi ro được giữ nguyên

- `Users.Username` giới hạn 100 ký tự trong SQL nhưng tạo tài khoản lấy email có giới hạn 254 làm username. Email dài có thể gây lỗi lưu. Không đổi giới hạn nghiệp vụ hoặc schema trong đợt này.
- `VIEW_DOCUMENTS` ở menu một số vai trò dẫn tới trang nộp chỉ cho phép `UPLOAD_DOCUMENTS`. Chưa đổi điều hướng/phạm vi xem của Mentor vì cần chốt nghiệp vụ.
- API `/api/database/status` khi thành công vẫn trả tên database/login SQL theo contract cũ. Đã bỏ exception khỏi response lỗi; chưa thay quyền/field của response thành công.
- CORS cho mọi origin, địa chỉ Backend localhost cố định, token localStorage, thời hạn phiên, SQL initialization và các giới hạn upload được giữ nguyên. Không thay cơ chế bảo mật/triển khai trong refactor.
- PDF được kiểm tra extension/MIME/chữ ký và dung lượng; chưa quét mã độc hoặc phân tích cấu trúc PDF đầy đủ. Duyệt lưu kết quả hiện tại, chưa lưu lịch sử quyết định.
- HTML/CSS không được viết lại hay gom selector khi chưa chứng minh tác động cascade. Giao diện hiện tại giữ nguyên; chỉ logic JS thay đổi.

## Kiểm tra

Build mỗi nhóm bằng:

```powershell
dotnet build Backend/InternManagement.Tests/InternManagement.Tests.csproj --no-restore -p:OutputPath=bin/DocumentChecks/net9.0/ -p:UseAppHost=false
```

Backend và project test build thành công, 0 warning/0 error. Output riêng tránh khóa bởi Backend đang chạy. Kiểm thử SQL/API chạy assembly test trên database có tên duy nhất; trigger mô phỏng lỗi chỉ trong database test, được gỡ và database được xóa.

Sau kiểm thử, đã dừng đúng tiến trình dự án, chạy `dotnet build --no-restore` vào output mặc định (0 warning/0 error) và khởi động lại Backend tại `http://localhost:5024`. `/api/database/status` trả HTTP 200, `connected=true` và `X-Request-ID`; gọi `/api/interns` không có token trả 401 và có log endpoint/status/trace. `backend.stderr.log` không có lỗi khởi động tại thời điểm kiểm tra.

Đã đạt: đăng nhập/khôi phục mật khẩu, phiên/token, quyền bốn vai trò, CRUD tài khoản/201/204/409/404, 23 trường hồ sơ và tìm kiếm, upload CV/đơn/PDF validation/trùng tên/lỗi lưu, duyệt/từ chối/version/rollback, CORS khi lỗi và correlation header. Kiểm tra log xác nhận timestamp/endpoint/trace và không chứa mật khẩu/hash/Authorization của fixture.

Các test trình duyệt dùng mock API, không ghi SQL:

```powershell
python tests/check_frontend.py
python tests/check_permissions_ui.py
python tests/check_documents_ui.py
python tests/check_document_reviews_ui.py
python tests/check_api_ui.py
```

Test API UI kiểm tra status 400/401/403/404/409/500, mất mạng, JSON hỏng, HTTP 204, tải blob, metadata không chứa credential/query/body, giữ storage khác, quản lý tài khoản không tự logout khi server lỗi và chặn gửi lặp.

SMTP được kiểm tra qua server SMTP tạm ở loopback, không gửi thư đến người dùng thật. Không dùng test để chứng minh cấu hình SMTP production hay khả năng quét mã độc.

## Theo dõi khi lỗi

Mở Console trình duyệt tìm `[api.js] API request failed`: endpoint, method, status, category, timestamp và trace ID. Giao diện hiển thị thông báo dễ hiểu, không dùng stack trace.

Tìm cùng trace ID trong `Backend/InternManagement/backend.stdout.log` hoặc console ASP.NET Core. Log lỗi gồm action/controller qua endpoint và exception/stack phía Backend. Log `backend.stderr.log` hỗ trợ lỗi tiến trình/khởi động. Không đưa token/password/connection string vào câu lệnh tìm kiếm hoặc ảnh chụp log chia sẻ.
