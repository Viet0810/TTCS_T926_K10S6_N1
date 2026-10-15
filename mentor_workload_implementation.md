# Thực hiện chức năng: HR xem số Intern mỗi Mentor đang quản lý

## 1. Mục tiêu
Là HR, tôi muốn xem số lượng Intern mà mỗi Mentor đang quản lý để theo dõi tình trạng phân công nguồn lực.

## 2. Yêu cầu nghiệp vụ
- HR truy cập được danh sách Mentor và số Intern đang quản lý.
- Mỗi Mentor hiển thị:
  - Họ tên
  - Email
  - Số Intern đang quản lý
- Số lượng phải dựa trên dữ liệu assignment đang có trong hệ thống.
- Khi phân công / xóa phân công Mentor, số lượng phải cập nhật theo dữ liệu mới.

## 3. Backend

### 3.1 API cần thêm
Endpoint:
GET /api/intern-assignments/mentor-load

### 3.2 Logic truy vấn
Query SQL mẫu:

SELECT u.Id AS mentorId,
       u.FullName AS mentorName,
       u.Email AS email,
       COUNT(i.Id) AS internCount
FROM dbo.Users u
LEFT JOIN dbo.Interns i ON i.MentorUserId = u.Id
WHERE u.Role = 'MENTOR'
GROUP BY u.Id, u.FullName, u.Email
ORDER BY u.FullName, u.Id;

### 3.3 Phần lưu trữ
- Hàm service: `InternAssignmentService.GetMentorLoadAsync(...)`
- Controller: `InternAssignmentsController.GetMentorLoad(...)`
- DTO: `MentorLoadItem`

### 3.4 Quyền truy cập
- Chỉ HR / người có quyền `ASSIGN_MENTOR` mới được gọi API.
- Nếu không đủ quyền, trả về 403/401 theo cơ chế hiện có.

## 4. Frontend

### 4.1 Trang liên quan
- `Frontend/pages/mentor-assignment-list.html`
- `Frontend/js/mentor-assignment.js`
- `Frontend/js/api.js`

### 4.2 Màn hình hiển thị
Bảng hiển thị 3 cột:
1. Mentor
2. Email
3. Số Intern đang quản lý

### 4.3 Gọi API
Khai báo trong `API`:
`getMentorLoad: () => requestApi("/intern-assignments/mentor-load", { headers: getAuthHeader() })`

### 4.4 Render dữ liệu
- Duyệt mảng dữ liệu trả về từ API.
- Mỗi phần tử có dạng:
  {
    mentorId: 12,
    mentorName: "Nguyễn Văn A",
    email: "mentor.a@ictu.edu.vn",
    internCount: 4
  }
- Tạo `tr` mới cho từng Mentor và in ra số lượng tương ứng.

## 5. Kiểm thử

### 5.1 Kiểm thử API
- Đăng nhập với tài khoản HR.
- Gọi `GET /api/intern-assignments/mentor-load`.
- Kiểm tra status `200 OK`.
- Kiểm tra từng item có `mentorId`, `mentorName`, `email`, `internCount`.

### 5.2 Kiểm thử logic số liệu
- Gán 1 Intern cho Mentor A.
- Gọi API lại.
- `internCount` của Mentor A phải tăng lên 1.
- Nếu xóa assignment hoặc đổi Mentor, số lượng phải giảm / đổi đúng mentor.

### 5.3 Kiểm thử quyền
- Không có quyền HR: 403.
- Chưa đăng nhập: 401.

## 6. Dữ liệu ví dụ
[
  {
    "mentorId": 1,
    "mentorName": "Nguyễn Văn A",
    "email": "mentorA@ictu.edu.vn",
    "internCount": 3
  },
  {
    "mentorId": 2,
    "mentorName": "Trần Thị B",
    "email": "mentorB@ictu.edu.vn",
    "internCount": 1
  }
]

## 7. Checklist hoàn thành
- [ ] Backend API trả về danh sách mentor + số Intern
- [ ] Frontend hiển thị đúng trên trang danh sách phân công
- [ ] Số liệu cập nhật theo assignment
- [ ] Kiểm tra quyền truy cập
- [ ] Kiểm tra trường hợp không có Intern
- [ ] Kiểm tra build/validate không lỗi
