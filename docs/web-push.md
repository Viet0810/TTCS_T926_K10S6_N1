# Bật thông báo trạng thái hồ sơ trên thiết bị

Web Push gửi thông báo của hệ thống tới trình duyệt/thiết bị sau khi HR duyệt CV hoặc cập nhật trạng thái. Thông báo trong chuông của ứng dụng và email vẫn được tạo như trước; Web Push là kênh bổ sung. Người dùng phải tự bật trên từng trình duyệt/thiết bị và cho phép quyền thông báo.

## 1. Kiểm tra cấu hình máy chủ

Tệp `Backend/InternManagement/appsettings.Push.json` đã được tạo cục bộ bằng script tạo khóa. Tệp này bị Git bỏ qua vì chứa khóa riêng. Giữ nguyên `PrivateKey`; không gửi tệp này qua email/chat hoặc đưa lên Git. Nếu cần tạo lại cặp khóa, sao lưu/xóa tệp cấu hình cục bộ trước, rồi chạy lại script ở bước 2.

Mở `Backend/InternManagement/appsettings.Push.json` và sửa `WebPush:Subject` thành địa chỉ liên hệ hợp lệ dạng `mailto:dia-chi-email-cua-ban`. Không cần sửa `PublicKey` hoặc `PrivateKey` sau khi đã tạo.

SMTP và Web Push là hai cấu hình riêng. SMTP hiện nằm ở `Backend/InternManagement/appsettings.Smtp.json`; nếu cần cập nhật email, chỉ sửa tệp cục bộ này. Không điền thông tin thật vào tệp `.example.json`.

## 2. Tạo lại khóa Web Push khi cần

Mở PowerShell tại thư mục gốc project và chạy:

```powershell
.BackendInternManagementToolsGenerate-VapidKeys.ps1 -Subject "mailto:dia-chi-email-cua-ban"
```

Script tạo `Backend/InternManagement/appsettings.Push.json` và không in khóa riêng ra màn hình. Chỉ chạy lại khi đã sao lưu/xóa tệp hiện tại; cùng một cặp khóa phải được giữ ổn định để những thiết bị đã đăng ký tiếp tục hoạt động.

## 3. Khởi động lại backend

Trong terminal đang chạy backend, nhấn `Ctrl+C`. Sau đó mở PowerShell mới ở thư mục gốc project:

```powershell
dotnet build Backend/InternManagement/InternManagement.csproj -p:UseAppHost=false
dotnet run --project Backend/InternManagement
```

Khi backend khởi động, ứng dụng tự tạo bảng `dbo.pushsubscriptions` trong PostgreSQL nếu bảng chưa có. Không cần chạy SQL thủ công.

## 4. Mở frontend bằng localhost

Mở terminal thứ hai ở thư mục gốc project và chạy:

```powershell
python -m http.server 5500 --directory Frontend --bind 127.0.0.1
```

Mở `http://127.0.0.1:5500/` trên trình duyệt. Không mở HTML bằng `file://`. `localhost`/loopback phù hợp để phát triển; khi triển khai thật, frontend phải chạy bằng HTTPS vì service worker và Push API yêu cầu ngữ cảnh an toàn.

## 5. Bật quyền trên từng tài khoản và thiết bị

1. Đăng nhập bằng tài khoản thực tập sinh trên trình duyệt/thiết bị nhận thông báo.
2. Bấm biểu tượng chuông, rồi chọn **Bật thông báo trên thiết bị**.
3. Khi trình duyệt hỏi quyền, chọn **Cho phép**. Nếu trước đây đã chặn, mở cài đặt quyền thông báo của trang `127.0.0.1` để đổi lại.
4. Lặp lại thao tác cho mỗi thiết bị/trình duyệt. Mỗi đăng ký được gắn với tài khoản đang đăng nhập, nên thiết bị của HR không nhận thông báo riêng của thực tập sinh.
5. Để ngừng nhận trên thiết bị đang dùng, đăng nhập lại đúng tài khoản và chọn **Tắt thông báo trên thiết bị này**.

## 6. Kiểm tra luồng HR → thực tập sinh

1. Để thực tập sinh đăng nhập và bật Web Push như bước 5.
2. Đăng nhập tài khoản HR trên một cửa sổ/trình duyệt khác.
3. Mở **Duyệt hồ sơ**, chọn CV của thực tập sinh, đổi trạng thái sang **Đã duyệt** hoặc **Cần bổ sung**, rồi lưu.
4. Trình duyệt/thiết bị của thực tập sinh sẽ nhận thông báo; bấm thông báo sẽ mở trang hệ thống. Chuông trong ứng dụng cũng giữ thông báo chi tiết, và backend tiếp tục gửi email theo cấu hình SMTP.

## Nếu thiết bị vẫn không hiện thông báo

- Kiểm tra quyền thông báo của trang trong cài đặt trình duyệt và quyền thông báo của trình duyệt trong cài đặt Windows/Android.
- Mở trang bằng đúng địa chỉ và cổng đã đăng ký; subscription thuộc riêng từng origin (scheme, host, port).
- Bấm chuông và kiểm tra nút đã đổi thành **Tắt thông báo trên thiết bị này**. Nếu chưa, xem cửa sổ backend có báo thiếu cấu hình Web Push hay lỗi gửi không.
- Nếu đã đổi cặp khóa VAPID, tắt rồi bật lại thông báo trên từng trình duyệt để đăng ký bằng khóa mới.
- Máy chủ triển khai thật cần HTTPS. Trình duyệt có thể giới hạn thông báo nền khi thiết bị bật chế độ tiết kiệm pin/tắt quyền thông báo.

Tham khảo: [MDN Push API](https://developer.mozilla.org/en-US/docs/Web/API/Push_API), [MDN Service Worker API](https://developer.mozilla.org/en-US/docs/Web/API/Service_Worker_API), [Lib.Net.Http.WebPush](https://github.com/tpeczek/Lib.Net.Http.WebPush).
