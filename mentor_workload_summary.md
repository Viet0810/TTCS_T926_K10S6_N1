# Tổng hợp số Intern đang quản lý của từng Mentor

## Mục tiêu
Là HR, tôi muốn xem số lượng Intern mà mỗi Mentor đang quản lý để theo dõi tình trạng phân công nguồn lực.

## Yêu cầu chức năng
- Hiển thị danh sách tất cả Mentor đang hoạt động.
- Với mỗi Mentor, hiển thị:
  - Họ tên Mentor
  - Email Mentor
  - Số Intern đang quản lý
- Dữ liệu phải được tính theo dữ liệu assignment hiện tại trong hệ thống.
- Khi có thay đổi assignment mới hoặc xóa assignment, số lượng phải cập nhật tức thời.

## API gợi ý
GET /api/intern-assignments/mentor-load

### Ví dụ phản hồi
[
  {
    "mentorId": 12,
    "mentorName": "Nguyễn Văn A",
    "email": "mentor.a@ictu.edu.vn",
    "internCount": 4
  },
  {
    "mentorId": 15,
    "mentorName": "Trần Thị B",
    "email": "mentor.b@ictu.edu.vn",
    "internCount": 2
  }
]

## Luồng thực thi
1. HR truy cập trang danh sách phân công.
2. Frontend gọi API `/api/intern-assignments/mentor-load`.
3. Backend đếm số Intern theo `MentorUserId` trong bảng `dbo.Interns`.
4. Trang hiển thị bảng gồm:
   - Mentor
   - Email
   - Số Intern đang quản lý

## Logic SQL đề xuất
SELECT u.Id AS mentorId,
       u.FullName AS mentorName,
       u.Email AS email,
       COUNT(i.Id) AS internCount
FROM dbo.Users u
LEFT JOIN dbo.Interns i ON i.MentorUserId = u.Id
WHERE u.Role = 'MENTOR'
GROUP BY u.Id, u.FullName, u.Email
ORDER BY u.FullName, u.Id;

## Ghi chú
- Chỉ HR mới có quyền xem danh sách này.
- Nếu Mentor không có Intern nào, vẫn hiển thị với `internCount = 0`.
- Khi chỉnh sửa phân công Mentor, backend phải cập nhật lại kết quả tính toán theo thời gian thực.
