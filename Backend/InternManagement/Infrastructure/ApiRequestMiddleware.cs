using System.Diagnostics;
using InternManagement.DTOs;

namespace InternManagement.Infrastructure;

public sealed class ApiRequestMiddleware(RequestDelegate next, ILogger<ApiRequestMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var started = Stopwatch.GetTimestamp();
        var failed = false;
        context.Response.Headers["X-Request-ID"] = context.TraceIdentifier;
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // A disconnected browser is not a server failure; no response can be sent.
            logger.LogInformation("Request cancelled. Method {Method}, path {Path}, trace {TraceId}",
                context.Request.Method, context.Request.Path, context.TraceIdentifier);
        }
        catch (Exception exception)
        {
            failed = true;
            var status = exception is BadHttpRequestException badRequest ? badRequest.StatusCode : 500;
            logger.LogError(exception, "API failed. Method {Method}, path {Path}, endpoint {Endpoint}, status {Status}, trace {TraceId}",
                context.Request.Method, context.Request.Path, context.GetEndpoint()?.DisplayName, status, context.TraceIdentifier);
            if (context.Response.HasStarted) throw;
            context.Response.Clear();
            context.Response.Headers["X-Request-ID"] = context.TraceIdentifier;
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(ErrorResponse(context.Request.Path, status), context.RequestAborted);
        }
        finally
        {
            if (!failed && context.Response.StatusCode >= 400)
            {
                logger.LogWarning("API rejected request. Method {Method}, path {Path}, endpoint {Endpoint}, status {Status}, trace {TraceId}, elapsed {ElapsedMs} ms",
                    context.Request.Method, context.Request.Path, context.GetEndpoint()?.DisplayName,
                    context.Response.StatusCode, context.TraceIdentifier, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
        }
    }

    private static object ErrorResponse(PathString path, int status)
    {
        if (status == 413)
            return new { message = "Tệp vượt quá dung lượng cho phép. Vui lòng chọn PDF tối đa 5 MB." };
        if (status < 500)
            return new ApiErrorResponse(false, "Yêu cầu không hợp lệ. Vui lòng kiểm tra dữ liệu gửi lên.", null);
        // Preserve the document error messages already consumed by their screens.
        if (path.StartsWithSegments("/api/interns/me/documents"))
            return new { message = "Không thể xử lý tài liệu lúc này. Vui lòng thử lại sau." };
        if (path.StartsWithSegments("/api/document-reviews"))
            return new { message = "Không thể xử lý yêu cầu tài liệu lúc này. Vui lòng thử lại sau." };
        return new ApiErrorResponse(false, "Máy chủ chưa thể xử lý yêu cầu. Vui lòng thử lại sau.", null);
    }
}
