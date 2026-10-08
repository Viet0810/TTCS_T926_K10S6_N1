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

    public InternsController(IInternService interns, RequestAuthorizationService authorization, AccountService accounts)
    {
        this.interns = interns;
        this.authorization = authorization;
        this.accounts = accounts;
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

}
