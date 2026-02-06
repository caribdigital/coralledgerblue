# End-User Authentication System

## Overview

This directory contains the end-user authentication system for CoralLedger Blue, implementing secure user registration, login, and session management using JWT tokens.

## Architecture

### Authentication Schemes

The application supports **two authentication schemes**:

1. **API Key Authentication** (`X-API-Key` header) - For server-to-server integrations
2. **JWT Bearer Authentication** (`Authorization: Bearer <token>` header) - For end-users

Both schemes are supported simultaneously using ASP.NET Core's Policy Scheme authentication.

## Components

### Domain Entities

#### TenantUser
- **Purpose**: Represents an authenticated user within a tenant
- **Key Fields**:
  - `Email`: User email (unique per tenant)
  - `PasswordHash`: BCrypt-hashed password
  - `Role`: User role (Admin, Manager, User, Viewer)
  - `IsActive`: Account status
  - `EmailConfirmed`: Email verification status
  - `FailedLoginAttempts`: Failed login counter
  - `LockedOutUntil`: Account lockout expiration

#### Gamification Entities
All gamification entities (`UserProfile`, `UserPoints`, `UserBadge`, `UserAchievement`) now include:
- `CitizenEmail`: Legacy field for backward compatibility
- `TenantUserId`: Foreign key to `TenantUser` (nullable for anonymous observations)

### Services

#### IPasswordHasher / PasswordHasher
- **Technology**: BCrypt.Net with work factor 12
- **Purpose**: Secure password hashing and verification
- **Security**: Constant-time comparison to prevent timing attacks

#### IJwtTokenService / JwtTokenService
- **Technology**: System.IdentityModel.Tokens.Jwt
- **Token Expiration**: Configurable (default: 60 minutes)
- **Claims**: UserId, Email, Name, Role, TenantId

#### ICurrentUser / CurrentUserService
- **Purpose**: Access authenticated user context throughout the application
- **Properties**: UserId, Email, Name, IsAuthenticated, Roles, TenantId

### API Endpoints

#### POST /api/auth/register
Register a new user account.

**Request:**
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123",
  "fullName": "John Doe",
  "tenantId": "guid-optional"
}
```

**Response:**
```json
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "base64-refresh-token",
  "userId": "guid",
  "email": "user@example.com",
  "fullName": "John Doe",
  "role": "User",
  "tenantId": "guid"
}
```

**Validation:**
- Email format validation
- Password minimum 8 characters
- Duplicate email check per tenant

#### POST /api/auth/login
Authenticate with email and password.

**Request:**
```json
{
  "email": "user@example.com",
  "password": "SecurePassword123",
  "tenantId": "guid-optional"
}
```

**Response:** Same as register

**Security Features:**
- Account lockout after 5 failed attempts (15 minutes)
- Failed login counter reset on successful login
- Generic error messages to prevent user enumeration

### Blazor UI

#### Login.razor (`/login`)
- Email and password form with validation
- DataAnnotations-based validation
- Error handling and loading states
- Link to registration page

#### Register.razor (`/register`)
- Registration form with email, password, and full name
- Password confirmation validation
- Email format validation
- Minimum password length enforcement

#### LoginDisplay.razor
- Shows user name/email when authenticated
- Login/Register links when not authenticated
- Logout functionality

## Configuration

### appsettings.json

```json
{
  "Jwt": {
    "Secret": "YOUR_SECRET_KEY_HERE",
    "Issuer": "CoralLedger.Blue",
    "Audience": "CoralLedger.Blue.Web",
    "ExpirationMinutes": 60
  }
}
```

### Environment Variables

- `JWT__SECRET`: JWT signing secret (required in production)
- `JWT__ISSUER`: Token issuer (optional, defaults to "CoralLedger.Blue")
- `JWT__AUDIENCE`: Token audience (optional, defaults to "CoralLedger.Blue.Web")
- `JWT__EXPIRATIONMINUTES`: Token expiration in minutes (optional, defaults to 60)

**⚠️ Security Warning**: Never commit JWT secrets to source control. Use environment variables or secure secret management (Azure Key Vault, AWS Secrets Manager, etc.).

### Generating a Secure JWT Secret

```bash
# Generate a 256-bit (32-byte) secure random key
openssl rand -base64 32
```

## Security Features

### Password Security
- **Hashing**: BCrypt with work factor 12 (configurable)
- **Minimum Length**: 8 characters (configurable)
- **No Password History**: Not implemented (could be added)

### Account Protection
- **Failed Login Limit**: 5 attempts
- **Lockout Duration**: 15 minutes
- **Lockout Message**: Generic (doesn't reveal unlock time to prevent timing attacks)

### Token Security
- **Algorithm**: HS256 (HMAC-SHA256)
- **Expiration**: 60 minutes (configurable)
- **Clock Skew**: Zero tolerance
- **Validation**: Issuer, Audience, Lifetime, Signature

### API Security
- **Rate Limiting**: Inherited from existing rate limiting middleware
- **CORS**: Inherited from existing CORS configuration
- **HTTPS**: Enforced in production

## Database Schema

### Migrations

1. **20260206112016_AddPasswordAuthenticationToTenantUser**
   - Adds password fields to `TenantUser` table
   - Adds `PasswordHash`, `EmailConfirmed`, `FailedLoginAttempts`, `LockedOutUntil`

2. **20260206112813_LinkGamificationToTenantUser**
   - Adds `TenantUserId` foreign key to gamification tables
   - Maintains `CitizenEmail` for backward compatibility
   - Adds navigation properties

### Indexes

- `(TenantId, Email)` - Unique index on TenantUsers
- `TenantUserId` - Index on all gamification tables for efficient lookups

## Usage Examples

### Protecting Endpoints

```csharp
app.MapGet("/api/protected", [Authorize] (ICurrentUser currentUser) =>
{
    return Results.Ok(new { 
        UserId = currentUser.UserId,
        Email = currentUser.Email,
        IsAuthenticated = currentUser.IsAuthenticated 
    });
});
```

### Role-Based Authorization

```csharp
app.MapDelete("/api/admin/users/{id}", [Authorize(Roles = "Admin")] 
    (Guid id, MarineDbContext context) =>
{
    // Admin-only endpoint
});
```

### Accessing Current User in Services

```csharp
public class MyService
{
    private readonly ICurrentUser _currentUser;
    
    public MyService(ICurrentUser currentUser)
    {
        _currentUser = currentUser;
    }
    
    public async Task DoSomething()
    {
        if (_currentUser.IsAuthenticated)
        {
            var userId = _currentUser.UserId;
            // Use authenticated user's ID
        }
    }
}
```

## Testing

### Unit Tests (TODO)
- [ ] PasswordHasher tests
- [ ] JwtTokenService tests
- [ ] CurrentUserService tests
- [ ] TenantUser domain logic tests

### Integration Tests (TODO)
- [ ] Registration endpoint tests
- [ ] Login endpoint tests
- [ ] Account lockout tests
- [ ] JWT authentication tests

## Known Limitations

1. **No Email Verification**: Email confirmation flow not implemented
2. **No Password Reset**: Forgot password flow not implemented
3. **No Refresh Token Storage**: Refresh tokens generated but not persisted
4. **No OAuth2 External Providers**: Google, Microsoft, GitHub login not implemented
5. **No Blazor Auth State**: Token storage and AuthenticationStateProvider not implemented

## Future Enhancements

### Priority 1
- [ ] Implement token storage in Blazor (HttpOnly cookies or secure storage)
- [ ] Add AuthenticationStateProvider for Blazor
- [ ] Email verification flow
- [ ] Password reset flow

### Priority 2
- [ ] OAuth2 external providers (Google, Microsoft, GitHub)
- [ ] Two-factor authentication (2FA)
- [ ] Refresh token rotation and storage
- [ ] Password complexity requirements

### Priority 3
- [ ] Session management (view active sessions, revoke tokens)
- [ ] Audit log for authentication events
- [ ] Brute force protection (IP-based rate limiting)
- [ ] CAPTCHA for registration and login

## Troubleshooting

### "JWT Secret not configured" error
**Problem**: Application fails to start with InvalidOperationException

**Solution**: Set the JWT secret in configuration or environment variable:
```bash
export JWT__SECRET="your-secure-secret-here"
```

### Tokens expire immediately
**Problem**: Users are logged out immediately after login

**Cause**: Clock skew or system time issues

**Solution**: Ensure server time is synchronized with NTP

### Account locked indefinitely
**Problem**: User cannot login even after 15 minutes

**Cause**: Database not saving `LockedOutUntil` or time zone issues

**Solution**: Check database migration applied correctly and UTC time usage

## References

- [ASP.NET Core Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/)
- [JWT Bearer Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/jwt-bearer)
- [BCrypt.Net Documentation](https://github.com/BcryptNet/bcrypt.net)
- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
