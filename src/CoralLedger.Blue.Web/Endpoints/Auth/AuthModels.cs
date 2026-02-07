namespace CoralLedger.Blue.Web.Endpoints.Auth;

public record RegisterRequest(
    string Email,
    string Password,
    string? FullName,
    Guid? TenantId);

public record LoginRequest(
    string Email,
    string Password,
    Guid? TenantId);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    Guid UserId,
    string Email,
    string? FullName,
    string Role,
    Guid TenantId);

public record RefreshTokenRequest(
    string RefreshToken);

public record SendVerificationEmailRequest(
    string Email,
    Guid? TenantId);

public record VerifyEmailRequest(
    string Token);

public record ForgotPasswordRequest(
    string Email,
    Guid? TenantId);

public record ResetPasswordRequest(
    string Token,
    string NewPassword);
