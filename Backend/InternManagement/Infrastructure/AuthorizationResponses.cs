using InternManagement.DTOs;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Infrastructure;

internal static class AuthorizationResponses
{
    public static IActionResult Denied(AuthorizationStatus status) => status == AuthorizationStatus.PasswordChangeRequired
        ? new ObjectResult(new { success = false, code = "PASSWORD_CHANGE_REQUIRED", message = "Vui lòng đổi mật khẩu tạm trước khi sử dụng hệ thống." }) { StatusCode = 403 }
        : new ObjectResult(new ApiErrorResponse(false,
        status == AuthorizationStatus.Unauthenticated ? "Vui lòng đăng nhập để tiếp tục."
            : "Bạn không có quyền thực hiện chức năng này.", null))
    {
        StatusCode = status == AuthorizationStatus.Unauthenticated ? 401 : 403
    };
}
