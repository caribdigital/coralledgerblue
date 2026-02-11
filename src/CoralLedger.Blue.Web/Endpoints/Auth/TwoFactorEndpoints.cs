using System.Security.Claims;
using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Blue.Web.Endpoints.Auth;

public static class TwoFactorEndpoints
{
    public static IEndpointRouteBuilder MapTwoFactorEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth/2fa")
            .WithTags("Two-Factor Authentication")
            .RequireAuthorization();

        group.MapPost("/setup", Setup2FA)
            .WithName("Setup2FA")
            .WithSummary("Generate secret key and QR code URI for 2FA setup")
            .Produces<TwoFactorSetupResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        group.MapPost("/enable", Enable2FA)
            .WithName("Enable2FA")
            .WithSummary("Verify code and enable 2FA for the account")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        group.MapPost("/disable", Disable2FA)
            .WithName("Disable2FA")
            .WithSummary("Disable 2FA for the account (requires current code)")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        group.MapPost("/validate", Validate2FA)
            .WithName("Validate2FA")
            .WithSummary("Validate 2FA code during login")
            .AllowAnonymous()
            .RequireRateLimiting(CoralLedger.Blue.Web.Security.SecurityConfiguration.StrictRateLimiterPolicy)
            .Produces<TwoFactorValidateResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapGet("/status", Get2FAStatus)
            .WithName("Get2FAStatus")
            .WithSummary("Check if 2FA is enabled for the current user")
            .Produces<TwoFactorStatusResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        return app;
    }

    private static async Task<IResult> Setup2FA(
        ClaimsPrincipal user,
        ITotpService totpService,
        MarineDbContext context)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Results.Unauthorized();
        }

        var tenantUser = await context.TenantUsers
            .FirstOrDefaultAsync(u => u.Id == userGuid)
            .ConfigureAwait(false);

        if (tenantUser == null)
        {
            return Results.Unauthorized();
        }

        if (tenantUser.TwoFactorEnabled)
        {
            return Results.Problem(
                detail: "Two-factor authentication is already enabled",
                statusCode: 400,
                title: "2FA Already Enabled");
        }

        var secretKey = totpService.GenerateSecretKey();
        var qrCodeUri = totpService.GenerateQrCodeUri(tenantUser.Email, secretKey);

        // Store the secret as pending (expires in 15 minutes)
        tenantUser.SetPendingTwoFactorSecret(secretKey, expirationMinutes: 15);
        await context.SaveChangesAsync().ConfigureAwait(false);

        return Results.Ok(new TwoFactorSetupResponse(secretKey, qrCodeUri));
    }

    private static async Task<IResult> Enable2FA(
        Enable2FARequest request,
        ClaimsPrincipal user,
        ITotpService totpService,
        MarineDbContext context)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Results.Unauthorized();
        }

        var tenantUser = await context.TenantUsers
            .FirstOrDefaultAsync(u => u.Id == userGuid)
            .ConfigureAwait(false);

        if (tenantUser == null)
        {
            return Results.Unauthorized();
        }

        if (tenantUser.TwoFactorEnabled)
        {
            return Results.Problem(
                detail: "Two-factor authentication is already enabled",
                statusCode: 400,
                title: "2FA Already Enabled");
        }

        // Validate that a pending secret exists and hasn't expired
        if (!tenantUser.IsPendingSecretValid())
        {
            return Results.Problem(
                detail: "No pending 2FA setup found or setup has expired. Please run /setup again.",
                statusCode: 400,
                title: "No Pending Setup");
        }

        // Validate the code with the server-stored pending secret
        if (!totpService.ValidateCode(tenantUser.TwoFactorPendingSecretKey!, request.Code))
        {
            return Results.Problem(
                detail: "Invalid verification code",
                statusCode: 400,
                title: "Invalid Code");
        }

        // Enable 2FA with the verified pending secret
        tenantUser.EnableTwoFactor(tenantUser.TwoFactorPendingSecretKey!);
        await context.SaveChangesAsync().ConfigureAwait(false);

        // Generate recovery codes (not persisted - creates significant usability risk)
        // TODO: CRITICAL - Persist recovery codes as hashed values and add endpoints to consume/regenerate them
        // Without recovery codes, users who lose their authenticator cannot regain access to their account
        // Recovery codes should be stored similar to password hashes, and marked as used when consumed
        // See PR review comment: https://github.com/caribdigital/coralledgerblue/pull/118
        var recoveryCodes = totpService.GenerateRecoveryCodes();

        return Results.Ok(new Enable2FAResponse(recoveryCodes));
    }

    private static async Task<IResult> Disable2FA(
        Disable2FARequest request,
        ClaimsPrincipal user,
        ITotpService totpService,
        MarineDbContext context)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Results.Unauthorized();
        }

        var tenantUser = await context.TenantUsers
            .FirstOrDefaultAsync(u => u.Id == userGuid)
            .ConfigureAwait(false);

        if (tenantUser == null)
        {
            return Results.Unauthorized();
        }

        if (!tenantUser.TwoFactorEnabled || string.IsNullOrEmpty(tenantUser.TwoFactorSecretKey))
        {
            return Results.Problem(
                detail: "Two-factor authentication is not enabled",
                statusCode: 400,
                title: "2FA Not Enabled");
        }

        // Validate the code before disabling
        if (!totpService.ValidateCode(tenantUser.TwoFactorSecretKey, request.Code))
        {
            return Results.Problem(
                detail: "Invalid verification code",
                statusCode: 400,
                title: "Invalid Code");
        }

        tenantUser.DisableTwoFactor();
        await context.SaveChangesAsync().ConfigureAwait(false);

        return Results.Ok(new { Message = "Two-factor authentication has been disabled" });
    }

    private static async Task<IResult> Validate2FA(
        Validate2FARequest request,
        ITotpService totpService,
        MarineDbContext context)
    {
        var tenantUser = await context.TenantUsers
            .FirstOrDefaultAsync(u => u.Id == request.UserId)
            .ConfigureAwait(false);

        // Use generic response for all failure modes to prevent user enumeration
        if (tenantUser == null ||
            !tenantUser.TwoFactorEnabled ||
            string.IsNullOrEmpty(tenantUser.TwoFactorSecretKey) ||
            !totpService.ValidateCode(tenantUser.TwoFactorSecretKey, request.Code))
        {
            return Results.Problem(
                detail: "Invalid two-factor authentication attempt",
                statusCode: 401,
                title: "Unauthorized");
        }

        // Record successful login
        tenantUser.RecordLogin();
        await context.SaveChangesAsync().ConfigureAwait(false);

        return Results.Ok(new TwoFactorValidateResponse(true, "Two-factor authentication successful"));
    }

    private static async Task<IResult> Get2FAStatus(
        ClaimsPrincipal user,
        MarineDbContext context)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId) || !Guid.TryParse(userId, out var userGuid))
        {
            return Results.Unauthorized();
        }

        var tenantUser = await context.TenantUsers
            .FirstOrDefaultAsync(u => u.Id == userGuid)
            .ConfigureAwait(false);

        if (tenantUser == null)
        {
            return Results.Unauthorized();
        }

        return Results.Ok(new TwoFactorStatusResponse(tenantUser.TwoFactorEnabled));
    }
}

// Request/Response records
public record TwoFactorSetupResponse(string SecretKey, string QrCodeUri);
public record Enable2FARequest(string Code);
public record Enable2FAResponse(string[] RecoveryCodes);
public record Disable2FARequest(string Code);
public record Validate2FARequest(Guid UserId, string Code);
public record TwoFactorValidateResponse(bool Success, string Message);
public record TwoFactorStatusResponse(bool TwoFactorEnabled);
