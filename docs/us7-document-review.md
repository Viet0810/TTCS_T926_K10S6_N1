# US7 — Xét duyệt từng CV và đơn xin thực tập

Phạm vi đã xác nhận: duyệt từng tài liệu, không có quyết định tuyển dụng chung cho toàn bộ hồ sơ. Không triển khai email US8, hợp đồng US9/10 hoặc chương trình thực tập.

## Phân tích source trước khi sửa

1. Module sẵn có: DocumentReviewsController, InternDocumentService, DocumentReviewResponse/ReviewDocumentRequest, trang document-reviews.html và JS tương ứng. US6 đã có POST auth/register, LoginRequest/LoginResponse, RegisterInternRequest, tài khoản INTERN, hồ sơ và PDF. Authentication Bearer/Data Protection và role HR đã có.
2. Tái sử dụng Users, Interns, InternDocuments, InternResponse và component InternProfile. SQL trong service theo architecture hiện tại; không thêm repository/entity/table.
3. InternDocuments.ReviewStatus lưu pending/approved/rejected, ReviewComment, ReviewedAt, ReviewedBy và ROWVERSION. Hồ sơ đăng ký chưa nộp tài liệu không xuất hiện trong hàng đợi. Tài liệu mới nộp pending. Interns.Status là trạng thái thực tập, không phải xét duyệt.
4. File sửa: Controllers/DocumentReviewsController.cs, Services/InternDocumentService.cs, DTOs/DocumentDtos.cs, Frontend/pages/document-reviews.html, Frontend/js/document-reviews.js, Frontend/js/dashboard.js, Backend/InternManagement.Tests/DocumentChecks.cs, tests/check_document_reviews_ui.py và docs/intern-documents.md.
5. File mới: Frontend/css/document-reviews.css và tài liệu này. Không sửa US6, schema hay cấu hình DI.
6. API contract bên dưới; route hiện có giữ tương thích nhưng áp dụng cùng business rules mới. Các thuộc tính response danh sách được bổ sung, không đổi tên thuộc tính cũ.
7. Authorization: tất cả route /api/document-reviews cần phiên hợp lệ, quyền APPROVE_DOCUMENTS và role HR. ADMIN/MENTOR/INTERN trả 403, không đăng nhập trả 401. Backend kiểm tra role từ tài khoản được AuthTokenService xác thực lại trong SQL. Không sửa ma trận quyền của module khác.
8. Frontend: HR mở menu xét duyệt; xem danh sách và lọc trạng thái/tìm tên/mã/email; tải PDF để đọc; mở chi tiết đầy đủ qua GET interns/{id} và component hồ sơ hiện có. Duyệt/Từ chối chỉ hiện khi pending và chỉ bật sau khi tải chi tiết thành công. Đang gửi khóa control, chặn đóng dialog và gửi lặp. Thành công cập nhật bảng tại chỗ rồi tải lại dữ liệu; lỗi tải lại sau lưu được phân biệt với lỗi lưu.

## Contract

| Endpoint | Request | Response |
| --- | --- | --- |
| GET /api/document-reviews | Bearer HR | 200 DocumentReviewResponse[]: internId,fullName,studentCode,email,kind,fileName,size,uploadedAt,status,comment,reviewedAt,reviewer,version,phone,school,major,createdAt |
| GET /api/interns/{id} | Route hồ sơ sẵn có | 200 InternResponse đầy đủ; giữ phân quyền VIEW_INTERNS của module hồ sơ |
| GET /api/document-reviews/{id}/{kind} | Bearer HR; kind cv/application | 200 PDF attachment |
| PUT /api/document-reviews/{id}/{kind}/approve | Bearer HR; JSON version,comment (tùy chọn) | 200 message; server đặt approved |
| PUT /api/document-reviews/{id}/{kind}/reject | Bearer HR; JSON version,comment (bắt buộc) | 200 message; server đặt rejected |
| PUT /api/document-reviews/{id}/{kind} | Route cũ: status,comment,version | Cùng kiểm tra HR/pending/version; không thể dùng để thay kết quả đã xử lý |

version là base64 của ROWVERSION 8 byte. Nhận xét tối đa 2000 ký tự. Lỗi: 400 sai loại/trạng thái/nhận xét/phiên bản, 401 chưa đăng nhập, 403 không phải HR, 404 tài liệu/hồ sơ không tồn tại, 409 đã xử lý hoặc phiên bản thay đổi. Lỗi SQL/server qua middleware hiện có, trả message an toàn, log trace ID.

## Workflow và dependency

Chỉ pending → approved hoặc pending → rejected. UPDATE kiểm tra pending và ROWVERSION cùng lúc; hai yêu cầu đồng thời chỉ một yêu cầu thắng. Không cho đổi approved ↔ rejected kể cả gửi phiên bản mới nhất. Không gọi SMTP hay PasswordResetService.

Nộp lại từ US6 đã có nghiệp vụ thay tài liệu, reset pending và xóa kết quả duyệt cũ; giữ nguyên hành vi đó. INTERN không được gọi API xét duyệt. US8 sau này có thể đọc ReviewStatus/ReviewedAt/ReviewedBy/ReviewComment; module hiện tại không có lịch sử quyết định hay hàng đợi email và US7 không bổ sung chúng.

Thay đổi file dùng chung chỉ ở dashboard.js (ẩn menu xét duyệt với người không phải HR), DocumentDtos.cs (bổ sung thông tin ứng viên và request riêng) và service tài liệu (siết trạng thái, bổ sung SELECT danh sách). Luồng upload không đổi.

## Kiểm tra

- Build backend/test đạt 0 warning, 0 error.
- SQL/API trên database tạm: danh sách/chi tiết, duyệt/từ chối, lưu kết quả, request sai, không tồn tại, quyền HR-only, cấm đổi trạng thái cuối, hai quyết định đồng thời và rollback khi lỗi SQL. Bộ hồi quy US6 vẫn đạt; database tạm được xóa.
- Browser mock: danh sách/chi tiết/PDF, loading, chặn gửi lặp, ẩn thao tác trên trạng thái cuối, lỗi API/mạng và tải lại sau lưu; 390/768/1440 px.

Lệnh: `dotnet build Backend/InternManagement.Tests --no-restore -p:OutputPath=bin/Sprint2/net9.0/ -p:UseAppHost=false`, `dotnet Backend/InternManagement.Tests/bin/Sprint2/net9.0/InternManagement.Tests.dll`, `python tests/check_document_reviews_ui.py`.
