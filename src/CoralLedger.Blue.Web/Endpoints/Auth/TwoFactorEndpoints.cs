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
            .Produces<TwoFactorValidateResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

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

        // Store the secret temporarily (will be confirmed when enabled)
        tenantUser.EnableTwoFactor(secretKey);
        tenantUser.DisableTwoFactor(); // Clear enabled flag, keep secret for verification

        // Actually just set the secret without enabling
        // We need to add a method or modify the entity to support pending 2FA setup
        // For now, we'll return the secret and let the enable endpoint verify and enable

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

        // Validate the code with the provided secret
        if (!totpService.ValidateCode(request.SecretKey, request.Code))
        {
            return Results.Problem(
                detail: "Invalid verification code",
                statusCode: 400,
                title: "Invalid Code");
        }

        // Enable 2FA with the verified secret
        tenantUser.EnableTwoFactor(request.SecretKey);
        await context.SaveChangesAsync().ConfigureAwait(false);

        // Generate recovery codes
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

        if (tenantUser == null)
        {
            return Results.Problem(
                detail: "User not found",
                statusCode: 401,
                title: "Unauthorized");
        }

        if (!tenantUser.TwoFactorEnabled || string.IsNullOrEmpty(tenantUser.TwoFactorSecretKey))
        {
            return Results.Problem(
                detail: "Two-factor authentication is not enabled for this user",
                statusCode: 400,
                title: "2FA Not Enabled");
        }

        if (!totpService.ValidateCode(tenantUser.TwoFactorSecretKey, request.Code))
        {
            return Results.Problem(
                detail: "Invalid verification code",
                statusCode: 401,
                title: "Invalid Code");
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
public record Enable2FARequest(string SecretKey, string Code);
public record Enable2FAResponse(string[] RecoveryCodes);
public record Disable2FARequest(string Code);
public record Validate2FARequest(Guid UserId, string Code);
public record TwoFactorValidateResponse(bool Success, string Message);
public record TwoFactorStatusResponse(bool TwoFactorEnabled);
