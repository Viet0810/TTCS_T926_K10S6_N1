# Hồ sơ thực tập sinh

Hồ sơ có 23 trường, đồng bộ giữa biểu mẫu tạo/sửa, hộp thoại xem chi tiết, hồ sơ cá nhân, xuất CSV, API và PostgreSQL.

| Nhóm | Thông tin |
| --- | --- |
| Cá nhân | Họ tên, ngày sinh, email, điện thoại, địa chỉ liên hệ |
| Đào tạo | Mã sinh viên, lớp, trường, khoa, chuyên ngành |
| Thực tập | Đơn vị, địa chỉ đơn vị, bộ phận, vị trí, đề tài/nội dung, ngày bắt đầu, ngày kết thúc, trạng thái |
| Hướng dẫn | Người hướng dẫn tại đơn vị, email, điện thoại, giảng viên hướng dẫn, ghi chú |

Năm trường cũ vẫn bắt buộc: `fullName`, `email`, `phone`, `school`, `major`. Các trường mới có thể để trống; API gửi `null` khi chưa bổ sung. Ngày dùng định dạng `yyyy-MM-dd`. Ngày sinh không được ở tương lai, thời gian thực tập phải sau ngày sinh và ngày kết thúc không trước ngày bắt đầu. Email/số điện thoại người hướng dẫn cũng được kiểm tra trên backend.

Trạng thái cho phép: `Chờ tiếp nhận`, `Đang thực tập`, `Đã hoàn thành`, `Đã dừng`; để trống nếu chưa xác định. Ghi chú tối đa 2.000 ký tự. Trạng thái và ghi chú là thông tin bổ sung để quản lý trong hệ thống, không phải yêu cầu bắt buộc từ biểu mẫu tham khảo.

Backend tự bổ sung các cột nullable khi khởi động bằng `DatabaseInitializer`. Script độc lập tương ứng: `Backend/InternManagement/Sql/extend-intern-profile.sql`. Migration có thể chạy lại, giữ dữ liệu cũ và không tự điền thông tin giả. PUT gửi toàn bộ hồ sơ; frontend giữ các trường đã tải khi sửa.

HR được tìm kiếm và chỉnh sửa, mentor được xem/tìm kiếm, thực tập sinh xem hồ sơ và nộp CV/đơn của mình qua quyền `UPLOAD_DOCUMENTS`. Xem [hướng dẫn tài liệu](intern-documents.md).

## Nguồn tham khảo

- [Hướng dẫn thực tập 2025 của Đại học Quy Nhơn](https://fba.qnu.edu.vn/Resources/Docs/SubDomain/fba/Th%E1%BB%B1c%20t%E1%BA%ADp%202025/FBA.%20Quy%20%C4%91%E1%BB%8Bnh%20h%C6%B0%E1%BB%9Bng%20d%E1%BA%ABn%20TTTH.pdf), biểu mẫu trang 30 và 33: mã sinh viên, lớp, ngành/khoa, vị trí, đơn vị, thời gian và liên hệ người hướng dẫn.
- [Biểu mẫu thực tập của Đại học Việt Nhật – ĐHQGHN](https://vju.vnu.edu.vn/?jet_download=db9e6e498559f11faa8b3ca12777efce578e74e5), trang 9 và 12: ngày sinh, chương trình đào tạo, cơ sở thực tập, bộ phận, nội dung và người hướng dẫn.

Kiểm tra tích hợp SQL bằng `dotnet run --project Backend/InternManagement.Tests`; kiểm tra biểu mẫu và xem chi tiết bằng `python tests/check_permissions_ui.py`. Dữ liệu kiểm thử chỉ nằm trong database tạm và môi trường trình duyệt biệt lập.
