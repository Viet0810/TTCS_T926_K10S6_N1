using System.Net.Mail;
using InternManagement.DTOs;
using InternManagement.Infrastructure;
using InternManagement.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;

namespace InternManagement.Controllers;

[ApiController]
[Route("api/mentors")]
public sealed class MentorsController : ControllerBase
{
    private readonly AccountService accounts;
    private readonly RequestAuthorizationService authorization;
    private readonly PasswordResetService passwordReset;
    private readonly ILogger<MentorsController> logger;

    public MentorsController(
        AccountService accounts,
        RequestAuthorizationService authorization,
        PasswordResetService passwordReset,
        ILogger<MentorsController> logger)
    {
        this.accounts = accounts;
        this.authorization = authorization;
        this.passwordReset = passwordReset;
        this.logger = logger;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(CreateMentorRequest request, CancellationToken cancellationToken)
    {
        var decision = authorization.Evaluate(Request);
        if (decision.Status != AuthorizationStatus.Authorized)
            return AuthorizationResponses.Denied(decision.Status);
        if (decision.User!.Role != "HR")
            return AuthorizationResponses.Denied(AuthorizationStatus.Forbidden);

        var temporaryPassword = TemporaryPasswordGenerator.Generate();
        var accountRequest = new CreateUserRequest
        {
            FullName = request.FullName,
            Email = request.Email,
            Password = temporaryPassword,
            Role = "MENTOR"
        };

        try
        {
            var user = await accounts.CreateAsync(accountRequest, "MENTOR", cancellationToken);
            logger.LogInformation(
                "Mentor account created. User {UserId}, RecipientDomain {RecipientDomain}.",
                user.Id,
                new MailAddress(user.Email).Host);

            var emailSent = false;
            try
            {
                emailSent = await passwordReset.SendAccountCreatedAsync(user, temporaryPassword, cancellationToken);
            }
            catch (Exception error)
            {
                logger.LogWarning(
                    "Mentor account setup email failed for user {UserId}, RecipientDomain {RecipientDomain}, type {FailureType}.",
                    user.Id,
                    new MailAddress(user.Email).Host,
                    error.GetType().Name);
            }

            var message = emailSent
                ? "Tạo tài khoản thành công. Email hướng dẫn đã được gửi đến người dùng."
                : "Tài khoản đã được tạo nhưng chưa gửi được email hướng dẫn.";
            return CreatedAtAction(
                nameof(UsersController.GetUserById),
                "Users",
                new { id = user.Id },
                new { success = true, message, data = user, accountCreated = true, emailSent });
        }
        catch (SqlException error) when (error.Number is 2601 or 2627)
        {
            return Conflict(new ApiErrorResponse(false, "Email hoặc tên đăng nhập đã tồn tại.", null));
        }
    }
}
