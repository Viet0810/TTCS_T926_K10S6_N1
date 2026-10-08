# Email kết quả duyệt hồ sơ

Khi HR duyệt hoặc yêu cầu bổ sung CV/đơn xin thực tập, backend lưu quyết định trước, sau đó gửi email tới địa chỉ email trên hồ sơ thực tập sinh. Cùng lúc đó, kết quả cũng được lưu vào chuông thông báo riêng của tài khoản thực tập sinh.

## Cấu hình SMTP

Email cần một máy chủ SMTP hợp lệ. Tệp cục bộ `Backend/InternManagement/appsettings.Smtp.json` đã được tạo và backend tự nạp tệp này khi khởi động. Điền thông tin SMTP vào tệp đó hoặc dùng biến môi trường để ghi đè. Tệp SMTP cục bộ được loại khỏi Git để tránh đưa mật khẩu lên kho mã nguồn. Có thể xem cấu trúc tại `Backend/InternManagement/appsettings.Smtp.example.json`.

Với Gmail/Google Workspace, dùng `smtp.gmail.com`, cổng `587`, bật TLS (`EnableSsl: true`), và dùng đầy đủ địa chỉ Gmail làm cả `FromAddress` lẫn `Username`. Ứng dụng SMTP này cần App Password của Google (yêu cầu bật xác minh 2 bước), không dùng mật khẩu đăng nhập Google. Nếu tài khoản Workspace bị quản trị viên giới hạn SMTP, cần nhờ quản trị viên cho phép.

Các tên biến môi trường tương ứng:

| Biến môi trường | Ý nghĩa |
| --- | --- |
| `Smtp__Host` | Tên máy chủ SMTP của nhà cung cấp email |
| `Smtp__Port` | Cổng SMTP, thường do nhà cung cấp chỉ định |
| `Smtp__EnableSsl` | Bật TLS/SSL theo yêu cầu của nhà cung cấp |
| `Smtp__FromAddress` | Địa chỉ email gửi |
| `Smtp__Username` | Tài khoản SMTP nếu nhà cung cấp yêu cầu xác thực |
| `Smtp__Password` | Mật khẩu ứng dụng hoặc thông tin xác thực SMTP |

Sau khi cấu hình, khởi động lại backend. Màn hình HR hiển thị trạng thái gửi email sau khi lưu quyết định. Nếu SMTP chưa được cấu hình hoặc gửi thất bại, quyết định duyệt vẫn được lưu và thực tập sinh vẫn nhận thông báo trong hệ thống.
