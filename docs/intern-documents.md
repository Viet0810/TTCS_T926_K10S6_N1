# Nộp CV và đơn xin thực tập

Đăng nhập bằng tài khoản INTERN, mở **Nộp CV và đơn xin thực tập** trên bảng điều khiển. Chọn PDF không rỗng, tối đa 5 MB và nhấn nộp. Sau khi lưu có thể tải bản đã nộp. Nộp lại thay thế bản cũ cùng loại; mỗi hồ sơ có tối đa một CV và một đơn.

Email tài khoản phải trùng email hồ sơ thực tập sinh do HR tạo. Nếu chưa có hồ sơ tương ứng, trang báo lỗi và không cho nộp. Quyền mới `UPLOAD_DOCUMENTS` chỉ cấp cho INTERN. Backend lấy chủ sở hữu từ phiên đăng nhập và SQL, không nhận ID hồ sơ từ trình duyệt.

| API | Chức năng |
| --- | --- |
| GET `/api/interns/me/documents` | Danh sách tên tệp, kích thước và thời điểm nộp |
| PUT `/api/interns/me/documents/cv` | Multipart form, trường `file`: nộp/thay CV |
| PUT `/api/interns/me/documents/application` | Nộp/thay đơn xin thực tập |
| GET `/api/interns/me/documents/{kind}` | Tải bản đã lưu với Bearer token |

Backend tự tạo bảng `interndocuments` khi khởi động. Nội dung lưu dạng `BYTEA` trong PostgreSQL cùng tên tệp và thời điểm UTC; dữ liệu được sao lưu cùng database. Không có thư mục tài liệu công khai. Upsert trong transaction giữ một tài liệu cho mỗi hồ sơ và loại tài liệu.

Truy cập SQL/validation/lưu tài liệu nằm trong `Services/InternDocumentService.cs`; hai controller tài liệu giữ request/response và kiểm tra quyền. `Infrastructure/ApiRequestMiddleware.cs` xử lý lỗi/log dùng chung và cấp header `X-Request-ID`; không còn các exception filter riêng bị lặp ở controller.

HR và ADMIN mở **Xem và duyệt tài liệu**, tìm theo tên/mã/email hoặc lọc trạng thái. Nhấn **Tải để xem** để đọc PDF, sau đó **Duyệt / Nhận xét** để chấp nhận hoặc yêu cầu bổ sung. Yêu cầu bổ sung phải có lý do. Nhận xét và trạng thái hiển thị trên trang nộp tài liệu của thực tập sinh; nộp lại đưa tài liệu về chờ duyệt.

API HR dùng quyền `APPROVE_DOCUMENTS`: GET `/api/document-reviews` lấy danh sách; GET `/api/document-reviews/{internId}/{kind}` tải PDF; PUT cùng đường dẫn gửi `status` (`approved`/`rejected`), `comment`, `version`. Phiên bản dạng số tăng dần ngăn quyết định cũ ghi đè bản mới (HTTP 409); HR cần tải lại nếu tài liệu thay đổi. PostgreSQL lưu trạng thái, nhận xét, tài khoản, thời điểm duyệt và trạng thái email. PUT trả `{ message, notification: { status, message, attemptedAt } }`; `status` là `sent` hoặc `failed`. GET trả thêm `notificationStatus`, `notificationMessage`, `notificationAttemptedAt` trên mỗi tài liệu để trang HR hiển thị kết quả gửi. Khi tải lên bản mới, trạng thái duyệt và thông báo được đặt lại. Email dùng cấu hình `Smtp` hiện có. Đây là kết quả duyệt hiện tại, chưa có lịch sử các quyết định trước đó.

Duyệt/từ chối trùng trạng thái hiện tại trả 409 và không cập nhật phiên bản. Tài liệu không còn tồn tại trả 404; loại tài liệu, trạng thái, nhận xét và phiên bản không hợp lệ trả 400. Từ chối vẫn yêu cầu lý do. HR và ADMIN giữ quyền duyệt hiện có; INTERN/MENTOR không được gọi trực tiếp API duyệt. Lỗi xử lý danh sách/tải file/lưu quyết định trả 500 với `message` an toàn, ghi exception cùng method/path/trace ID vào log máy chủ.

Trang HR cập nhật trạng thái bằng JavaScript. Nếu quyết định đã lưu nhưng tải lại danh sách thất bại, thông báo nêu rõ đã lưu; thao tác duyệt trên bản thiếu phiên bản mới bị khóa đến khi tải lại. File được tải xuống để HR mở bằng trình đọc PDF, không có trình xem PDF nhúng. Kiểm tra trình duyệt HR: `python tests/check_document_reviews_ui.py`.

Kiểm tra phần mở rộng, kích thước và chữ ký đầu tệp PDF theo [hướng dẫn tải tệp của Microsoft](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-9.0). Hệ thống chưa tích hợp quét mã độc. Tệp trả về dưới dạng tải xuống.

Validation phía trình duyệt phân biệt chưa chọn tệp, tệp rỗng, sai extension/MIME, tên quá dài và vượt 5 MB. Backend kiểm tra lại extension, tên, dung lượng, MIME và chữ ký `%PDF-`. MIME `application/pdf`, `application/octet-stream` hoặc MIME không được cung cấp được chấp nhận nếu các kiểm tra PDF còn lại đạt. DOC/DOCX chưa được hỗ trợ.

Upload thành công trả HTTP 200 với `{"message":"Đã lưu tài liệu vào hệ thống."}`. Lỗi validation trả HTTP 400 với `message`; chưa đăng nhập/không có quyền/chưa gắn hồ sơ trả 401/403/404 theo contract hiện có. Request vượt giới hạn HTTP có thể trả 413. Lỗi xử lý tài liệu trả HTTP 500 với `{"message":"Không thể xử lý tài liệu lúc này. Vui lòng thử lại sau."}`; exception và trace ID được ghi vào log máy chủ, không trả chi tiết SQL cho trình duyệt.

Tên client được bỏ đường dẫn và chỉ lưu làm tên hiển thị; khóa lưu trữ là `(InternId, Kind)`, không có đường dẫn file vật lý hay yêu cầu tạo tên GUID. Trùng tên không ảnh hưởng tài liệu của người khác hoặc loại tài liệu còn lại. Nếu lưu thất bại, transaction giữ nguyên bản cũ.

Chạy kiểm thử SQL/API: `dotnet run --project Backend/InternManagement.Tests`. Chỉ ghi database tạm, không thêm tài liệu thử vào database thật. Test lỗi lưu dùng trigger tạm trong database test và gỡ sau khi kiểm tra. Kiểm tra trình duyệt: `python tests/check_documents_ui.py` (mock API, không ghi SQL).

Nếu Backend đang chạy và khóa output mặc định, có thể build riêng để kiểm tra mà không dừng server:

```powershell
dotnet build Backend/InternManagement.Tests/InternManagement.Tests.csproj --no-restore -p:OutputPath=bin/DocumentChecks/net9.0/ -p:UseAppHost=false
dotnet Backend/InternManagement.Tests/bin/DocumentChecks/net9.0/InternManagement.Tests.dll
```

Kết quả kiểm tra ngày 02/10/2026: build Backend và project test thành công (0 warning, 0 error); test trình duyệt và SQL/API đạt. Đã kiểm tra CV, đơn, thiếu/rỗng/sai định dạng/MIME/quá dung lượng, trùng tên, tên chứa đường dẫn, phân quyền trực tiếp, lỗi lưu và rollback. Database test đã được xóa. Bản Backend đang chạy cần khởi động lại với code mới để áp dụng thay đổi.
