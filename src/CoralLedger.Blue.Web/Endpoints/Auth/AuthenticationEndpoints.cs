using System.Security.Claims;
using CoralLedger.Blue.Application.Common.Interfaces;
using CoralLedger.Blue.Domain.Entities;
using CoralLedger.Blue.Infrastructure.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CoralLedger.Blue.Web.Endpoints.Auth;

public static class AuthenticationEndpoints
{
    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        group.MapPost("/register", Register)
            .WithName("Register")
            .WithSummary("Register a new user account")
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapPost("/login", Login)
            .WithName("Login")
            .WithSummary("Login with email and password")
            .Produces<AuthResponse>(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", async (HttpContext httpContext) =>
            {
                await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Results.Ok();
            })
            .WithName("Logout")
            .WithSummary("Logout and clear authentication cookie")
            .Produces(StatusCodes.Status200OK);

        group.MapPost("/send-verification-email", SendVerificationEmail)
            .WithName("SendVerificationEmail")
            .WithSummary("Send or resend email verification link")
            .RequireRateLimiting(CoralLedger.Blue.Web.Security.SecurityConfiguration.EmailRateLimiterPolicy)
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces<ProblemDetails>(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/verify-email", VerifyEmail)
            .WithName("VerifyEmail")
            .WithSummary("Verify email address with token")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        group.MapPost("/forgot-password", ForgotPassword)
            .WithName("ForgotPassword")
            .WithSummary("Request password reset link")
            .RequireRateLimiting(CoralLedger.Blue.Web.Security.SecurityConfiguration.EmailRateLimiterPolicy)
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status429TooManyRequests);

        group.MapPost("/reset-password", ResetPassword)
            .WithName("ResetPassword")
            .WithSummary("Reset password with token")
            .Produces(StatusCodes.Status200OK)
            .Produces<ProblemDetails>(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        MarineDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        ITenantContext tenantContext,
        IEmailService emailService,
        HttpContext httpContext)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = "Email and password are required"
            });
        }

        // Validate password strength
        var passwordValidation = ValidatePasswordStrength(request.Password);
        if (!passwordValidation.IsValid)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Weak password",
                Detail = passwordValidation.ErrorMessage
            });
        }

        // Determine tenant - use provided TenantId or current tenant from context
        var tenantId = request.TenantId ?? tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Tenant required",
                Detail = "Tenant ID is required for registration"
            });
        }

        // Check if user already exists
        var existingUser = await context.TenantUsers
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant() && u.TenantId == tenantId.Value);

        if (existingUser != null)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "User already exists",
                Detail = "A user with this email already exists"
            });
        }

        // Create new user
        var user = TenantUser.Create(
            tenantId.Value,
            request.Email,
            request.FullName);

        // Hash and set password
        var passwordHash = passwordHasher.HashPassword(request.Password);
        user.SetPassword(passwordHash);

        context.TenantUsers.Add(user);
        await context.SaveChangesAsync();

        // Generate and send verification email
        await SendVerificationEmailToUser(user, context, emailService, httpContext);

        // Generate tokens
        var accessToken = jwtTokenService.GenerateAccessToken(user);
        var refreshToken = jwtTokenService.GenerateRefreshToken();

        // Sign in with cookie authentication for Blazor
        await SignInWithCookie(httpContext, user, accessToken);

        return Results.Ok(new AuthResponse(
            accessToken,
            refreshToken,
            user.Id,
            user.Email,
            user.FullName,
            user.Role,
            user.TenantId));
    }

    private static async Task<IResult> Login(
        LoginRequest request,
        MarineDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        ITenantContext tenantContext,
        HttpContext httpContext)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.Unauthorized();
        }

        // Determine tenant
        var tenantId = request.TenantId ?? tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Tenant required",
                Detail = "Tenant ID is required for login"
            });
        }

        // Find user
        var user = await context.TenantUsers
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant() && u.TenantId == tenantId.Value);

        if (user == null)
        {
            return Results.Unauthorized();
        }

        // Check if account is locked
        if (user.IsLockedOut())
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Account locked",
                detail: "Account is temporarily locked due to too many failed login attempts. Please try again later.");
        }

        // Check if user is active
        if (!user.IsActive)
        {
            return Results.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                title: "Account disabled",
                detail: "This account has been disabled");
        }

        // Verify password
        if (string.IsNullOrEmpty(user.PasswordHash) || !passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            user.RecordFailedLogin();
            await context.SaveChangesAsync();
            return Results.Unauthorized();
        }

        // Record successful login
        user.RecordLogin();
        await context.SaveChangesAsync();

        // Generate tokens
        var accessToken = jwtTokenService.GenerateAccessToken(user);
        var refreshToken = jwtTokenService.GenerateRefreshToken();

        // Sign in with cookie authentication for Blazor
        await SignInWithCookie(httpContext, user, accessToken);

        return Results.Ok(new AuthResponse(
            accessToken,
            refreshToken,
            user.Id,
            user.Email,
            user.FullName,
            user.Role,
            user.TenantId));
    }

    /// <summary>
    /// Validates password meets complexity requirements
    /// </summary>
    private static (bool IsValid, string? ErrorMessage) ValidatePasswordStrength(string password)
    {
        if (password.Length < 8)
        {
            return (false, "Password must be at least 8 characters long");
        }

        if (!password.Any(char.IsUpper))
        {
            return (false, "Password must contain at least one uppercase letter");
        }

        if (!password.Any(char.IsLower))
        {
            return (false, "Password must contain at least one lowercase letter");
        }

        if (!password.Any(char.IsDigit))
        {
            return (false, "Password must contain at least one number");
        }

        return (true, null);
    }

    /// <summary>
    /// Signs in the user with cookie authentication
    /// </summary>
    private static async Task SignInWithCookie(HttpContext httpContext, TenantUser user, string accessToken)
    {
        // Create claims principal from user
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.Role, user.Role),
            new("TenantId", user.TenantId.ToString())
        };

        if (!string.IsNullOrEmpty(user.FullName))
        {
            claims.Add(new Claim(ClaimTypes.Name, user.FullName));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        // Sign in with cookie (HttpOnly, Secure in production)
        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true, // Cookie persists across browser sessions
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1) // Match JWT expiration
            });
    }

    private static async Task<IResult> SendVerificationEmail(
        SendVerificationEmailRequest request,
        MarineDbContext context,
        ITenantContext tenantContext,
        IEmailService emailService,
        HttpContext httpContext)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = "Email is required"
            });
        }

        // Determine tenant
        var tenantId = request.TenantId ?? tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Tenant required",
                Detail = "Tenant ID is required"
            });
        }

        // Find user
        var user = await context.TenantUsers
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant() && u.TenantId == tenantId.Value);

        if (user == null)
        {
            // Return generic message to prevent email enumeration
            return Results.Ok(new { message = "If the email exists, a verification link has been sent." });
        }

        // Check if email is already verified
        if (user.EmailConfirmed)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Email already verified",
                Detail = "This email address is already verified"
            });
        }

        // Send verification email
        await SendVerificationEmailToUser(user, context, emailService, httpContext);

        return Results.Ok(new { message = "Verification email sent successfully" });
    }

    private static async Task<IResult> VerifyEmail(
        VerifyEmailRequest request,
        MarineDbContext context)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = "Token is required"
            });
        }

        // Find token
        var verificationToken = await context.EmailVerificationTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == request.Token);

        if (verificationToken == null)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid token",
                Detail = "The verification token is invalid"
            });
        }

        // Check if token is valid (not used and not expired)
        if (!verificationToken.IsValid())
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid or expired token",
                Detail = verificationToken.IsUsed
                    ? "This verification token has already been used"
                    : "This verification token has expired. Please request a new one."
            });
        }

        // Mark token as used
        verificationToken.MarkAsUsed();

        // Confirm user email
        verificationToken.User.ConfirmEmail();

        await context.SaveChangesAsync();

        return Results.Ok(new { message = "Email verified successfully" });
    }

    /// <summary>
    /// Helper method to create and send verification email for a user
    /// </summary>
    private static async Task SendVerificationEmailToUser(
        TenantUser user,
        MarineDbContext context,
        IEmailService emailService,
        HttpContext httpContext)
    {
        // Invalidate any existing unused tokens for this user
        var existingTokens = await context.EmailVerificationTokens
            .Where(t => t.UserId == user.Id && !t.IsUsed)
            .ToListAsync();

        foreach (var token in existingTokens)
        {
            context.EmailVerificationTokens.Remove(token);
        }

        // Create new verification token
        var verificationToken = EmailVerificationToken.Create(user.Id, expirationHours: 48);
        context.EmailVerificationTokens.Add(verificationToken);
        await context.SaveChangesAsync();

        // Build verification URL
        var scheme = httpContext.Request.Scheme;
        var host = httpContext.Request.Host.Value;
        var verificationUrl = $"{scheme}://{host}/verify-email?token={Uri.EscapeDataString(verificationToken.Token)}";

        // Send email
        var subject = "Verify your email address - CoralLedger Blue";
        var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #0066cc 0%, #004499 100%); color: white; padding: 30px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 8px 8px; }}
        .button {{ display: inline-block; background: #0066cc; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 20px; color: #666; font-size: 12px; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🌊 CoralLedger Blue</h1>
            <p>Marine Intelligence Platform</p>
        </div>
        <div class=""content"">
            <h2>Verify Your Email Address</h2>
            <p>Hello{(string.IsNullOrEmpty(user.FullName) ? "" : $" {user.FullName}")},</p>
            <p>Thank you for registering with CoralLedger Blue! Please verify your email address by clicking the button below:</p>
            <p style=""text-align: center;"">
                <a href=""{verificationUrl}"" class=""button"">Verify Email Address</a>
            </p>
            <p>Or copy and paste this link into your browser:</p>
            <p style=""word-break: break-all; color: #0066cc;"">{verificationUrl}</p>
            <p><strong>This link will expire in 48 hours.</strong></p>
            <p>If you didn't create an account with CoralLedger Blue, please ignore this email.</p>
        </div>
        <div class=""footer"">
            <p>CoralLedger Blue - Protecting Marine Ecosystems</p>
        </div>
    </div>
</body>
</html>";

        var plainTextContent = $@"
CoralLedger Blue - Email Verification

Hello{(string.IsNullOrEmpty(user.FullName) ? "" : $" {user.FullName}")},

Thank you for registering with CoralLedger Blue! Please verify your email address by visiting this link:

{verificationUrl}

This link will expire in 48 hours.

If you didn't create an account with CoralLedger Blue, please ignore this email.

--
CoralLedger Blue
Marine Intelligence Platform
";

        await emailService.SendEmailAsync(user.Email, subject, htmlContent, plainTextContent);
    }

    private static async Task<IResult> ForgotPassword(
        ForgotPasswordRequest request,
        MarineDbContext context,
        ITenantContext tenantContext,
        IEmailService emailService,
        HttpContext httpContext)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = "Email is required"
            });
        }

        // Determine tenant
        var tenantId = request.TenantId ?? tenantContext.TenantId;
        if (!tenantId.HasValue)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Tenant required",
                Detail = "Tenant ID is required"
            });
        }

        // Find user - but don't reveal if email exists (prevent enumeration)
        var user = await context.TenantUsers
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant() && u.TenantId == tenantId.Value);

        // Always return success message to prevent email enumeration
        if (user == null)
        {
            return Results.Ok(new { message = "If the email exists, a password reset link has been sent." });
        }

        // Invalidate any existing unused tokens for this user
        var existingTokens = await context.PasswordResetTokens
            .Where(t => t.UserId == user.Id && !t.IsUsed)
            .ToListAsync();

        foreach (var token in existingTokens)
        {
            context.PasswordResetTokens.Remove(token);
        }

        // Create new password reset token (2 hour expiration)
        var resetToken = PasswordResetToken.Create(user.Id, expirationHours: 2);
        context.PasswordResetTokens.Add(resetToken);
        await context.SaveChangesAsync();

        // Build reset URL
        var scheme = httpContext.Request.Scheme;
        var host = httpContext.Request.Host.Value;
        var resetUrl = $"{scheme}://{host}/reset-password?token={Uri.EscapeDataString(resetToken.Token)}";

        // Send email
        var subject = "Reset your password - CoralLedger Blue";
        var htmlContent = $@"
<!DOCTYPE html>
<html>
<head>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background: linear-gradient(135deg, #0066cc 0%, #004499 100%); color: white; padding: 30px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background: #f9f9f9; padding: 30px; border-radius: 0 0 8px 8px; }}
        .button {{ display: inline-block; background: #0066cc; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
        .footer {{ text-align: center; margin-top: 20px; color: #666; font-size: 12px; }}
        .warning {{ background: #fff3cd; border-left: 4px solid #ffc107; padding: 10px; margin: 20px 0; }}
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""header"">
            <h1>🌊 CoralLedger Blue</h1>
            <p>Marine Intelligence Platform</p>
        </div>
        <div class=""content"">
            <h2>Reset Your Password</h2>
            <p>Hello{(string.IsNullOrEmpty(user.FullName) ? "" : $" {user.FullName}")},</p>
            <p>We received a request to reset your password for your CoralLedger Blue account. Click the button below to reset it:</p>
            <p style=""text-align: center;"">
                <a href=""{resetUrl}"" class=""button"">Reset Password</a>
            </p>
            <p>Or copy and paste this link into your browser:</p>
            <p style=""word-break: break-all; color: #0066cc;"">{resetUrl}</p>
            <div class=""warning"">
                <strong>⚠️ Important:</strong>
                <ul style=""margin: 5px 0;"">
                    <li>This link will expire in 2 hours</li>
                    <li>This link can only be used once</li>
                    <li>If you didn't request this, please ignore this email</li>
                </ul>
            </div>
        </div>
        <div class=""footer"">
            <p>CoralLedger Blue - Protecting Marine Ecosystems</p>
        </div>
    </div>
</body>
</html>";

        var plainTextContent = $@"
CoralLedger Blue - Password Reset

Hello{(string.IsNullOrEmpty(user.FullName) ? "" : $" {user.FullName}")},

We received a request to reset your password for your CoralLedger Blue account. 

To reset your password, visit this link:
{resetUrl}

IMPORTANT:
- This link will expire in 2 hours
- This link can only be used once
- If you didn't request this, please ignore this email

--
CoralLedger Blue
Marine Intelligence Platform
";

        await emailService.SendEmailAsync(user.Email, subject, htmlContent, plainTextContent);

        return Results.Ok(new { message = "If the email exists, a password reset link has been sent." });
    }

    private static async Task<IResult> ResetPassword(
        ResetPasswordRequest request,
        MarineDbContext context,
        IPasswordHasher passwordHasher)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid request",
                Detail = "Token and new password are required"
            });
        }

        // Validate password strength
        var passwordValidation = ValidatePasswordStrength(request.NewPassword);
        if (!passwordValidation.IsValid)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Weak password",
                Detail = passwordValidation.ErrorMessage
            });
        }

        // Find token
        var resetToken = await context.PasswordResetTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == request.Token);

        if (resetToken == null)
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid token",
                Detail = "The password reset token is invalid"
            });
        }

        // Check if token is valid (not used and not expired)
        if (!resetToken.IsValid())
        {
            return Results.BadRequest(new ProblemDetails
            {
                Title = "Invalid or expired token",
                Detail = resetToken.IsUsed
                    ? "This password reset token has already been used"
                    : "This password reset token has expired. Please request a new one."
            });
        }

        // Mark token as used
        resetToken.MarkAsUsed();

        // Update user password
        var passwordHash = passwordHasher.HashPassword(request.NewPassword);
        resetToken.User.SetPassword(passwordHash);

        // Reset any account lockout
        resetToken.User.ResetFailedLoginAttempts();

        await context.SaveChangesAsync();

        return Results.Ok(new { message = "Password reset successfully" });
    }
}
