using System.Net;
using InternManagement.DTOs;

namespace InternManagement.Services;

public static class EmailTemplates
{
    private static string E(string? text)=>WebUtility.HtmlEncode(text??"");
    public static string? ActionUrl(string? value)=>Uri.TryCreate(value,UriKind.Absolute,out var url)
        &&(url.Scheme==Uri.UriSchemeHttps||(url.IsLoopback&&url.Scheme==Uri.UriSchemeHttp)) ? url.AbsoluteUri : null;
    private static EmailContent Layout(string type,string subject,string title,string body,string text,string accent="#3568E8",string? action=null,string? url=null)
    {
        var link=ActionUrl(url);
        var cta=link is null?"":$"""
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0"><tr><td align="center" style="padding:24px 0">
            <a href="{E(link)}" style="display:block;background:{accent};color:#ffffff;text-decoration:none;padding:16px 20px;border-radius:10px;font-weight:bold;font-size:16px">{E(action)}</a></td></tr></table>
            <p style="font-size:13px;color:#596B85">Nếu nút không hoạt động, mở liên kết sau:</p>
            <p style="font-size:13px;overflow-wrap:anywhere;word-break:break-all"><a href="{E(link)}" style="color:#3568E8">{E(link)}</a></p>
            """;
        var html=$"""
            <!doctype html><html lang="vi"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{E(subject)}</title></head>
            <body style="margin:0;padding:0;background:#F4F7FB;font-family:Arial,Helvetica,sans-serif;color:#10213F">
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="background:#F4F7FB"><tr><td align="center" style="padding:24px 12px">
            <!--[if mso]><table role="presentation" width="600" cellspacing="0" cellpadding="0"><tr><td><![endif]-->
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" style="max-width:600px;background:#ffffff;border:1px solid #E3EAF4;border-radius:16px">
            <tr><td style="padding:24px;background:{accent};color:#ffffff;border-radius:16px 16px 0 0"><div style="font-size:24px;font-weight:bold;letter-spacing:1px">CODEGYM</div><div style="font-size:14px;line-height:1.6;margin-top:6px">Hệ thống quản lý thực tập sinh</div></td></tr>
            <tr><td style="padding:28px 24px;font-size:16px;line-height:1.7;overflow-wrap:anywhere;word-break:break-word"><h1 style="font-size:24px;line-height:1.4;margin:0 0 20px">{E(title)}</h1>{body}{cta}</td></tr>
            <tr><td style="padding:20px 24px;border-top:1px solid #E3EAF4;font-size:12px;line-height:1.7;color:#596B85">Đây là email tự động từ Hệ thống quản lý thực tập sinh.<br>Vui lòng không trả lời email này.</td></tr></table>
            <!--[if mso]></td></tr></table><![endif]-->
            </td></tr></table></body></html>
            """;
        return new(type,subject,text+(link is null?"":$"\n{action}: {link}\n")+"\nĐây là email tự động. Vui lòng không trả lời email này.",html);
    }
    public static EmailContent Account(UserResponse user,string password,string? loginUrl)
        =>Layout("ACCOUNT_CREATED","Tài khoản hệ thống quản lý thực tập sinh","Tài khoản của bạn đã được tạo",
            $"<p>Xin chào {E(user.FullName)},</p><p>Chào mừng bạn đến với hệ thống.</p><table role=\"presentation\" width=\"100%\" style=\"background:#F8FAFE;border:1px solid #E3EAF4;border-radius:12px\"><tr><td style=\"padding:20px;line-height:1.8\">Vai trò: <strong>{E(user.Role=="MENTOR"?"Mentor":user.Role)}</strong><br>Email đăng nhập: <strong>{E(user.Email)}</strong><br>Mật khẩu tạm thời: <strong>{E(password)}</strong></td></tr></table><p style=\"padding:16px;background:#FFF4E5;color:#8A4B08;border-radius:10px\">Vui lòng đổi mật khẩu ngay sau lần đăng nhập đầu tiên.</p>",
            $"Xin chào {user.FullName},\n\nTài khoản của bạn đã được tạo.\nVai trò: {(user.Role=="MENTOR"?"Mentor":user.Role)}\nEmail đăng nhập: {user.Email}\nMật khẩu tạm thời: {password}\nVui lòng đổi mật khẩu ngay sau lần đăng nhập đầu tiên.\n",
            action:"ĐĂNG NHẬP HỆ THỐNG",url:loginUrl);
    public static EmailContent Review(string name,string kind,string status,string? reason)
    {
        if(status is not ("approved" or "rejected"))throw new ArgumentException("Kết quả xét duyệt không hợp lệ.");
        var approved=status=="approved";var document=kind=="cv"?"CV":"Đơn xin thực tập";
        var badge=approved?"ĐÃ DUYỆT":"CHƯA ĐƯỢC DUYỆT";
        var subject=approved?"Hồ sơ thực tập của bạn đã được duyệt":"Cập nhật kết quả hồ sơ thực tập";
        var note=!approved&&!string.IsNullOrWhiteSpace(reason)?reason.Trim():null;
        return Layout(approved?"REVIEW_APPROVED":"REVIEW_REJECTED",subject,approved?"Hồ sơ đã được duyệt":"Kết quả xét duyệt hồ sơ",
            $"<p>Xin chào {E(name)},</p><p>{E(document)} của bạn {(approved?"đã được chấp nhận":"chưa được chấp nhận")}.</p><p><span style=\"display:inline-block;padding:8px 14px;border-radius:8px;background:{(approved?"#E6F7ED":"#FDECEC")};color:{(approved?"#107C41":"#B42318")};font-weight:bold\">{badge}</span></p>"
            +(note is null?"":$"<p><strong>Lý do:</strong></p><div style=\"padding:16px;background:#F8FAFE;border-radius:10px\">{E(note).Replace("\n","<br>")}</div>")
            +"<p>Bạn có thể đăng nhập để xem tài liệu và cập nhật trạng thái hồ sơ trong hệ thống.</p>",
            $"Xin chào {name},\n\n{document} của bạn {(approved?"đã được chấp nhận":"chưa được chấp nhận")}.\nTrạng thái: {badge} ({(approved?"Approved":"Rejected")})\n"+(note is null?"":$"Lý do: {note}\n"),approved?"#168447":"#B42318");
    }
    public static EmailContent Reset(string link,int minutes)
        =>Layout("PASSWORD_RESET","Đặt lại mật khẩu","Đặt lại mật khẩu của bạn",
            $"<p>Xin chào,</p><p>Hệ thống nhận được yêu cầu đặt lại mật khẩu của bạn.</p><p>Liên kết có hiệu lực trong <strong>{minutes} phút</strong> và chỉ dùng được một lần.</p><p>Nếu bạn không gửi yêu cầu này, bạn có thể bỏ qua email. Mật khẩu của bạn sẽ không thay đổi.</p>",
            $"Xin chào,\nHệ thống nhận được yêu cầu đặt lại mật khẩu.\nLiên kết có hiệu lực trong {minutes} phút và chỉ dùng được một lần.\nNếu bạn không gửi yêu cầu này, hãy bỏ qua email.\n",action:"ĐẶT LẠI MẬT KHẨU",url:link);
}
