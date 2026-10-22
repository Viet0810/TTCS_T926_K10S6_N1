namespace InternManagement.Services;

// Only deliberately safe recovery messages may be returned as HTTP 503.
public sealed class PasswordRecoveryUnavailableException(string message) : InvalidOperationException(message);
