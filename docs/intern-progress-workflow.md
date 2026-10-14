# Luồng Intern cập nhật tiến độ công việc

## 0. Mapping theo task K10S6N1-111 đến K10S6N1-114

| Task | Mục tiêu | Trạng thái hiện tại | File / logic liên quan |
| ----- | -------- | ------------------- | --------------------- |
| K10S6N1-111 | Luồng Intern cập nhật trạng thái task | Hoàn thành | [Backend/InternManagement/Controllers/InternPortalController.cs](../Backend/InternManagement/Controllers/InternPortalController.cs) |
| K10S6N1-112 | Ownership và phân quyền Intern | Hoàn thành | [Backend/InternManagement/Controllers/InternPortalController.cs](../Backend/InternManagement/Controllers/InternPortalController.cs), [Backend/InternManagement.Tests/InternPortalBusinessRulesTests.cs](../Backend/InternManagement.Tests/InternPortalBusinessRulesTests.cs) |
| K10S6N1-113 | Frontend chỉ hiển thị hành động phù hợp | Hoàn thành | [Frontend/js/intern-portal.js](../Frontend/js/intern-portal.js) |
| K10S6N1-114 | Test automation và đối chiếu tài liệu với behavior | Hoàn thành | [Backend/InternManagement.Tests/InternPortalBusinessRulesTests.cs](../Backend/InternManagement.Tests/InternPortalBusinessRulesTests.cs), [docs/intern-progress-workflow.md](./intern-progress-workflow.md) |

## 1. Luồng nghiệp vụ

1. Intern đăng nhập qua `POST /api/auth/login`.
2. Backend xác thực tên đăng nhập / email và mật khẩu bằng `AuthTokenService`.
3. JWT/token được phát hành với thông tin `Id`, `Username`, `Role`, `ExpiresAt` theo cơ chế hiện có.
4. Intern truy cập trang `/Frontend/pages/intern-portal.html`.
5. Script frontend gọi `GET /api/intern-portal/me` để đọc hồ sơ thực tập và `GET /api/intern-portal/tasks` để hiển thị danh sách nhiệm vụ.
6. Mỗi task được lọc theo `Interns.UserId` của tài khoản đang đăng nhập, không cho phép truyền `userId` hoặc `internId` tùy ý từ frontend.
7. Intern chọn một task và cập nhật trạng thái thông qua `PUT /api/intern-portal/tasks/{id}/status`.
8. Backend kiểm tra: token hợp lệ, vai trò là `INTERN`, task tồn tại, tenant/ownership hợp lệ và trạng thái chuyển đổi nằm trong quy tắc hiện có.
9. Nếu hợp lệ, hệ thống lưu trạng thái mới và gửi thông báo cho Mentor tương ứng.
10. Frontend reload dữ liệu và hiển thị message thành công / lỗi rõ ràng.

## 2. Quy tắc trạng thái hiện có

Theo schema hiện tại trong `DatabaseInitializer`, danh mục trạng thái task đang hỗ trợ là:

- `NotStarted`
- `InProgress`
- `PendingReview`
- `Completed`

Bản thân code hiện tại cho phép Intern cập nhật trạng thái theo chuỗi an toàn sau:

- `NotStarted` -> `InProgress`
- `InProgress` -> `PendingReview`

Không cho phép Intern cập nhật task đã `Completed`. Nếu task đang ở trạng thái không hợp lệ cho bước chuyển tiếp, backend trả về `409 Conflict` thay vì cập nhật dữ liệu.

## 3. Ma trận phân quyền

| Vai trò    | Thao tác                                             | Quyền hiện tại                        |
| ---------- | ---------------------------------------------------- | ------------------------------------- |
| INTERN     | Xem hồ sơ cá nhân                                    | Có                                    |
| INTERN     | Xem task của chính mình                              | Có                                    |
| INTERN     | Xem chi tiết task của chính mình                     | Có                                    |
| INTERN     | Cập nhật trạng thái task của chính mình              | Có, giới hạn trong quy tắc trạng thái |
| INTERN     | Xem báo cáo, feedback, lịch mentoring của chính mình | Có                                    |
| MENTOR     | Xem task của thực tập sinh được giao                 | Có                                    |
| MENTOR     | Đánh giá task đang chờ review                        | Có                                    |
| HR / ADMIN | Quản lý hồ sơ chung                                  | Có theo `RolePermissions`             |

## 4. Quy tắc ownership

- Ownership được xác định bằng join giữa `dbo.Interns` và `dbo.InternTasks` theo `InternId`.
- API không dựa vào `internId` do frontend gửi lên; nó lấy `Interns.UserId` từ token và lọc dữ liệu theo tài khoản hiện tại.
- Một task chỉ được cập nhật khi `i.UserId = @user` và `t.Id = @id` đều khớp.
- Mentor review và intern task updates luôn dựa trên cùng mối quan hệ `InternId -> MentorUserId` trong `dbo.MentorInternAssignments`.

## 5. Các trường hợp ngoại lệ

- Tài khoản không có bearer token hoặc token hết hạn: `401 Unauthorized`.
- Tài khoản không phải Intern truy cập API Intern portal: `403 Forbidden`.
- Không tìm thấy task hoặc tài khoản chưa liên kết hồ sơ thực tập: `404 NotFound`.
- Trạng thái không hợp lệ hoặc chuyển đổi không phù hợp: `400 BadRequest` hoặc `409 Conflict`.
- Kết nối backend hỏng: frontend hiển thị message "Không kết nối được Backend".

## 6. Tiêu chí nghiệm thu

- Intern đăng nhập thành công và chỉ thấy bài tập của mình.
- Intern không thể thao tác task của người khác dù gửi thay đổi `id` bất kỳ.
- Task ở trạng thái `Completed` không tiếp tục cập nhật từ Intern.
- Trạng thái `PendingReview` chỉ được gửi khi task đang ở `InProgress`.
- Mentor vẫn nhận được notification sau mỗi cập nhật từ Intern.
- Frontend hiển thị data thật và không dùng mock data trong luồng chính.
