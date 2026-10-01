using InternManagement.DTOs;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;

namespace InternManagement.Infrastructure;

internal static class AuthorizationResponses
{
    public static IActionResult Denied(AuthorizationStatus status) => new ObjectResult(new ApiErrorResponse(false,
        status == AuthorizationStatus.Unauthenticated ? "Vui lòng đăng nhập để tiếp tục."
            : "Bạn không có quyền thực hiện chức năng này.", null))
    {
        StatusCode = status == AuthorizationStatus.Unauthenticated ? 401 : 403
    };
}
