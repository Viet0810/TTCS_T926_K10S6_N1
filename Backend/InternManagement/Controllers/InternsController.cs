using InternManagement.Infrastructure;
using InternManagement.DTOs;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using NpgsqlTypes;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/interns")]
public sealed class InternsController : ControllerBase
{
    private readonly IInternService interns;
    private readonly RequestAuthorizationService authorization;
    private readonly AccountService accounts;
    private readonly NotificationService notifications;
    private readonly ILogger<InternsController> logger;

    public InternsController(IInternService interns, RequestAuthorizationService authorization, AccountService accounts,
        NotificationService notifications, ILogger<InternsController> logger)
    {
        this.interns = interns;
        this.authorization = authorization;
        this.accounts = accounts;
        this.notifications = notifications;
        this.logger = logger;
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<InternResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.ViewInterns);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);

        return Ok(await interns.GetAllAsync(cancellationToken));
    }

    [HttpGet("me")]
    [ProducesResponseType<InternResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.ViewProfile);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);

        var user = await accounts.GetByIdAsync(decision.User!.Id, cancellationToken);
        var email = user?.Email;
        if (email is null) return AuthorizationResponses.Denied(AuthorizationStatus.Unauthenticated);
        var intern = await interns.GetByEmailAsync(email, cancellationToken);
        return intern is null
            ? NotFound(new ApiErrorResponse(false, "Chưa có hồ sơ thực tập sinh gắn với tài khoản này.", null))
            : Ok(intern);
    }

    [HttpPost("me")]
    [ProducesResponseType<InternResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CreateMyProfile(CreateInternRequest request, CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.UploadDocuments);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);

        var user = await accounts.GetByIdAsync(decision.User!.Id, cancellationToken);
        if (user is null) return AuthorizationResponses.Denied(AuthorizationStatus.Unauthenticated);
        try
        {
            var intern = await interns.CreateAsync(request with { Email = user.Email }, cancellationToken);
            return CreatedAtAction(nameof(GetMyProfile), intern);
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Conflict(new ApiErrorResponse(false, "Email này đã có hồ sơ thực tập sinh. Vui lòng tải lại hồ sơ.", null));
        }
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType<InternResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.ViewInterns);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);

        var intern = await interns.GetByIdAsync(id, cancellationToken);
        return intern is null ? NotFound() : Ok(intern);
    }

    [HttpPost]
    [ProducesResponseType<InternResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateInternRequest request, CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.ManageInterns);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);

        try
        {
            var intern = await interns.CreateAsync(request, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = intern.Id }, intern);
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Conflict(new ApiErrorResponse(false, "Email thực tập sinh đã tồn tại.", null));
        }
    }

    [HttpPut("{id:int}")]
    [ProducesResponseType<InternResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(int id, CreateInternRequest request, CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.EditInterns);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);
        try
        {
            var intern = await interns.UpdateAsync(id, request, cancellationToken);
            return intern is null
                ? NotFound(new ApiErrorResponse(false, "Không tìm thấy hồ sơ thực tập sinh.", null))
                : Ok(intern);
        }
        catch (PostgresException error) when (error.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return Conflict(new ApiErrorResponse(false, "Email thực tập sinh đã tồn tại.", null));
        }
    }

    [HttpPut("{id:int}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(int id, UpdateInternStatusRequest request, CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request, PermissionNames.EditInterns);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);

        var status = request.Status == "Chưa xác định" ? null : request.Status;
        var result = await interns.UpdateStatusAsync(id, status, cancellationToken);
        if (result is null) return NotFound(new ApiErrorResponse(false, "Không tìm thấy hồ sơ thực tập sinh.", null));

        var notificationCreated = false;
        if (result.Changed)
        {
            var label = status ?? "Chưa xác định";
            try
            {
                notificationCreated = await notifications.NotifyInternAsync(result.Intern.Email,
                    "Trạng thái thực tập được cập nhật", $"HR đã cập nhật trạng thái thực tập của bạn thành: {label}.",
                    "intern-upload-cv.html",
                    CancellationToken.None);
            }
            catch (Exception error)
            {
                logger.LogWarning(error, "Could not create intern notification after status update.");
            }
        }

        return Ok(new { intern = result.Intern, changed = result.Changed, notificationCreated });
    }

}
