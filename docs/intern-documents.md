# Nộp CV và đơn xin thực tập

Đăng nhập bằng tài khoản INTERN, mở **Nộp CV và đơn xin thực tập** trên bảng điều khiển. Chọn PDF không rỗng, tối đa 5 MB và nhấn nộp. Sau khi lưu có thể tải bản đã nộp. Nộp lại thay thế bản cũ cùng loại; mỗi hồ sơ có tối đa một CV và một đơn.

Email tài khoản phải trùng email hồ sơ thực tập sinh. Đăng ký qua `Frontend/register.html` tạo tài khoản INTERN và hồ sơ cơ bản trong cùng transaction; sau đó đăng nhập để nộp tài liệu. Các tài khoản được cấp theo luồng cũ vẫn cần hồ sơ tương ứng; nếu chưa có, trang báo lỗi và không cho nộp. Quyền `UPLOAD_DOCUMENTS` chỉ cấp cho INTERN. Backend lấy chủ sở hữu từ phiên đăng nhập và SQL, không nhận ID hồ sơ từ trình duyệt.

| API | Chức năng |
| --- | --- |
| GET `/api/interns/me/documents` | Danh sách tên tệp, kích thước và thời điểm nộp |
| PUT `/api/interns/me/documents/cv` | Multipart form, trường `file`: nộp/thay CV |
| PUT `/api/interns/me/documents/application` | Nộp/thay đơn xin thực tập |
| GET `/api/interns/me/documents/{kind}` | Tải bản đã lưu với Bearer token |

Backend tự tạo bảng `InternDocuments` khi khởi động. Nội dung lưu trong `VARBINARY(MAX)` của SQL Server cùng loại, tên và thời điểm UTC; dữ liệu được sao lưu cùng database. Không có thư mục tài liệu công khai. Thay thế dùng transaction và khóa để tránh tạo bản trùng khi gửi đồng thời.

Truy cập SQL/validation/lưu tài liệu nằm trong `Services/InternDocumentService.cs`; hai controller tài liệu giữ request/response và kiểm tra quyền. `Infrastructure/ApiRequestMiddleware.cs` xử lý lỗi/log dùng chung và cấp header `X-Request-ID`; không còn các exception filter riêng bị lặp ở controller.

Chỉ HR mở **Xem và duyệt tài liệu**, tìm theo tên/mã/email hoặc lọc trạng thái. Nhấn **Tải để xem** để đọc PDF, sau đó **Xem chi tiết** để đọc đầy đủ hồ sơ và duyệt/từ chối tài liệu đang pending. Từ chối phải có lý do. Nhận xét và trạng thái hiển thị trên trang nộp tài liệu của thực tập sinh; nộp lại đưa phiên bản tài liệu mới về chờ duyệt theo nghiệp vụ US6 hiện có.

API HR dùng quyền `APPROVE_DOCUMENTS` và kiểm tra role HR: GET `/api/document-reviews` lấy danh sách; GET `/api/document-reviews/{internId}/{kind}` tải PDF; PUT thêm `/approve` hoặc `/reject` gửi `comment`, `version` để server quyết định trạng thái. PUT cũ vẫn nhận `status`, `comment`, `version` và áp dụng cùng quy tắc. ROWVERSION cùng điều kiện pending ngăn xét duyệt lại/ghi đè (HTTP 409). SQL lưu trạng thái, nhận xét, tài khoản và thời điểm duyệt. Chưa có lịch sử các quyết định trước đó. Contract mới xem [US7](us7-document-review.md).

Chỉ pending được duyệt/từ chối; mọi tài liệu đã approved/rejected trả 409 kể cả đổi sang kết quả khác. Tài liệu không còn tồn tại trả 404; loại tài liệu, trạng thái, nhận xét và phiên bản không hợp lệ trả 400. Từ chối yêu cầu lý do. ADMIN/INTERN/MENTOR không được gọi API xét duyệt. Lỗi xử lý danh sách/tải file/lưu quyết định trả 500 với `message` an toàn, ghi exception cùng method/path/trace ID vào log máy chủ.

Trang HR cập nhật trạng thái bằng JavaScript. Nếu quyết định đã lưu nhưng tải lại danh sách thất bại, thông báo nêu rõ đã lưu; bản đã xử lý vẫn xem được chi tiết nhưng không hiện nút xét duyệt. File được tải xuống để HR mở bằng trình đọc PDF, không có trình xem PDF nhúng. Kiểm tra trình duyệt HR: `python tests/check_document_reviews_ui.py`.

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
