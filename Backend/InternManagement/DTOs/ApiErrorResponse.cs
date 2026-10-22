namespace InternManagement.DTOs;

public sealed record ApiErrorResponse(bool Success, string Message, object? Data);