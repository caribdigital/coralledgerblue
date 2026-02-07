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

        return app;
    }

    private static async Task<IResult> Register(
        RegisterRequest request,
        MarineDbContext context,
        IPasswordHasher passwordHasher,
        IJwtTokenService jwtTokenService,
        ITenantContext tenantContext,
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
            new("TenantId", user.TenantId.ToString()),
            new("AccessToken", accessToken) // Store JWT for API calls
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
}
