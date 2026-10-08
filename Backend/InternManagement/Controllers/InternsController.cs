using InternManagement.Infrastructure;
using InternManagement.DTOs;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/interns")]
public sealed class InternsController : ControllerBase
{
    private readonly IInternService interns;
    private readonly RequestAuthorizationService authorization;
    private readonly AccountService accounts;
    private readonly InternAttendanceService attendance;

    public InternsController(
        IInternService interns,
        RequestAuthorizationService authorization,
        AccountService accounts,
        InternAttendanceService attendance)
    {
        this.interns = interns;
        this.authorization = authorization;
        this.accounts = accounts;
        this.attendance = attendance;
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

    [HttpGet("me/attendance/today")]
    [ProducesResponseType<InternAttendanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTodayAttendance(CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);
        if (!string.Equals(decision.User!.Role, "INTERN", StringComparison.OrdinalIgnoreCase))
            return AuthorizationResponses.Denied(AuthorizationStatus.Forbidden);

        var (intern, error) = await GetCurrentInternAsync(decision.User.Id, cancellationToken);
        if (error is not null) return error;

        var record = await attendance.GetTodayAsync(intern!.Id, cancellationToken);
        return new JsonResult(record) { StatusCode = StatusCodes.Status200OK };
    }

    [HttpPost("me/attendance/check-in")]
    [ProducesResponseType<InternAttendanceResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CheckIn(CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);
        if (!string.Equals(decision.User!.Role, "INTERN", StringComparison.OrdinalIgnoreCase))
            return AuthorizationResponses.Denied(AuthorizationStatus.Forbidden);

        var (intern, error) = await GetCurrentInternAsync(decision.User.Id, cancellationToken);
        if (error is not null) return error;

        try
        {
            var record = await attendance.CheckInAsync(intern!.Id, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, record);
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            return Conflict(new ApiErrorResponse(false, "Bạn đã check-in trong ngày này.", null));
        }
    }

    [HttpPost("me/attendance/check-out")]
    [ProducesResponseType<InternAttendanceResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CheckOut(CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);
        if (!string.Equals(decision.User!.Role, "INTERN", StringComparison.OrdinalIgnoreCase))
            return AuthorizationResponses.Denied(AuthorizationStatus.Forbidden);

        var (intern, error) = await GetCurrentInternAsync(decision.User.Id, cancellationToken);
        if (error is not null) return error;

        var record = await attendance.CheckOutAsync(intern!.Id, cancellationToken);
        return record is null
            ? Conflict(new ApiErrorResponse(false, "Chưa check-in trong ngày hoặc lượt này đã được check-out.", null))
            : Ok(record);
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
        catch (SqlException error) when (error.Number is 2601 or 2627)
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
        catch (SqlException error) when (error.Number is 2601 or 2627)
        {
            return Conflict(new ApiErrorResponse(false, "Email thực tập sinh đã tồn tại.", null));
        }
    }

    private async Task<(InternResponse? Intern, IActionResult? Error)> GetCurrentInternAsync(
        int userId,
        CancellationToken cancellationToken)
    {
        var user = await accounts.GetByIdAsync(userId, cancellationToken);
        var email = user?.Email;
        if (email is null)
            return (null, AuthorizationResponses.Denied(AuthorizationStatus.Unauthenticated));

        var intern = await interns.GetByEmailAsync(email, cancellationToken);
        return intern is null
            ? (null, NotFound(new ApiErrorResponse(false, "Chưa có hồ sơ thực tập sinh gắn với tài khoản này.", null)))
            : (intern, null);
    }
}
