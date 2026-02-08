# End-User Authentication System

## Overview

This directory contains the end-user authentication system for CoralLedger Blue, implementing secure user registration, login, and session management using JWT tokens and OAuth2 external providers.

## Architecture

### Authentication Schemes

The application supports **three authentication schemes**:

1. **API Key Authentication** (`X-API-Key` header) - For server-to-server integrations
2. **JWT Bearer Authentication** (`Authorization: Bearer <token>` header) - For end-users
3. **OAuth2 External Providers** (Google, Microsoft) - For social sign-in

All schemes are supported simultaneously using ASP.NET Core's Policy Scheme authentication.

## Components

### Domain Entities

#### TenantUser
- **Purpose**: Represents an authenticated user within a tenant
- **Key Fields**:
  - `Email`: User email (unique per tenant)
  - `PasswordHash`: BCrypt-hashed password (nullable for OAuth-only users)
  - `OAuthProvider`: OAuth provider name (Google, Microsoft, null for local auth)
  - `OAuthSubjectId`: OAuth provider's unique user identifier
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
- Sets HttpOnly authentication cookie for Blazor

#### POST /api/auth/logout
Logout and clear authentication cookie.

**Request:** No body required

**Response:**
```json
200 OK
```

**Behavior:**
- Clears the authentication cookie
- Revokes all active refresh tokens for the user
- Terminates the user's session
- Requires no authentication to call

#### POST /api/auth/refresh
Refresh access token using a refresh token.

**Request:**
```json
{
  "refreshToken": "base64-refresh-token"
}
```

**Response:**
```json
{
  "accessToken": "eyJhbGc...",
  "refreshToken": "new-base64-refresh-token",
  "userId": "guid",
  "email": "user@example.com",
  "fullName": "John Doe",
  "role": "User",
  "tenantId": "guid"
}
```

**Security Features:**
- **Token Rotation**: Old refresh token is revoked, new one is issued
- **Reuse Detection**: Attempting to reuse a revoked token revokes all tokens for that user
- **Longer Lifetime**: Refresh tokens expire in 30 days (configurable)
- **Hashed Storage**: Tokens are stored as SHA-256 hashes in the database

## Refresh Token System

### Overview

CoralLedger Blue implements a secure refresh token system that allows users to obtain new access tokens without re-authenticating. This is essential for maintaining long-lived sessions while keeping access tokens short-lived for security.

### How It Works

1. **Login/Register**: User receives both an access token (60 min) and refresh token (30 days)
2. **Store Token**: Client stores refresh token securely (e.g., localStorage or secure cookie)
3. **Access Token Expires**: When access token expires, client calls `/api/auth/refresh`
4. **Token Rotation**: Server validates refresh token and issues new access + refresh tokens
5. **Old Token Revoked**: Previous refresh token is invalidated to prevent reuse

### Security Features

#### Token Rotation (Single-Use Tokens)
Every time a refresh token is used, it is revoked and a new one is issued. This prevents token replay attacks and limits the impact of token theft.

#### Reuse Detection
If an already-revoked token is presented, the system assumes token theft and immediately revokes ALL refresh tokens for that user. This forces the legitimate user to re-authenticate while locking out the attacker.

#### Hashed Storage
Refresh tokens are never stored in plaintext. They are hashed using SHA-256 before storage, similar to how passwords are hashed. This protects against database leaks.

#### Automatic Cleanup
A background job runs daily to delete expired or old revoked tokens, preventing table bloat while maintaining security audit trails for 30 days.

### Database Schema

```sql
CREATE TABLE refresh_tokens (
    Id UUID PRIMARY KEY,
    TenantUserId UUID NOT NULL REFERENCES tenant_users(Id) ON DELETE CASCADE,
    TokenHash VARCHAR(256) NOT NULL UNIQUE,
    ExpiresAt TIMESTAMP NOT NULL,
    CreatedAt TIMESTAMP NOT NULL,
    RevokedAt TIMESTAMP NULL,
    ReplacedByTokenId UUID NULL
);

-- Indexes for performance
CREATE UNIQUE INDEX IX_RefreshTokens_TokenHash ON refresh_tokens(TokenHash);
CREATE INDEX IX_RefreshTokens_TenantUserId ON refresh_tokens(TenantUserId);
CREATE INDEX IX_RefreshTokens_ExpiresAt ON refresh_tokens(ExpiresAt);
CREATE INDEX IX_RefreshTokens_RevokedAt ON refresh_tokens(RevokedAt);
```

### Usage Example (Client-Side)

```javascript
// Store tokens after login
const loginResponse = await fetch('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password, tenantId })
});

const { accessToken, refreshToken } = await loginResponse.json();
localStorage.setItem('accessToken', accessToken);
localStorage.setItem('refreshToken', refreshToken);

// Refresh token when access token expires
async function refreshAccessToken() {
    const refreshToken = localStorage.getItem('refreshToken');
    
    const response = await fetch('/api/auth/refresh', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ refreshToken })
    });
    
    if (response.ok) {
        const { accessToken, refreshToken: newRefreshToken } = await response.json();
        localStorage.setItem('accessToken', accessToken);
        localStorage.setItem('refreshToken', newRefreshToken);
        return accessToken;
    } else {
        // Refresh failed - user needs to login again
        localStorage.removeItem('accessToken');
        localStorage.removeItem('refreshToken');
        window.location.href = '/login';
    }
}

// Use with HTTP interceptor
axios.interceptors.response.use(null, async (error) => {
    if (error.response?.status === 401) {
        const newToken = await refreshAccessToken();
        error.config.headers['Authorization'] = `Bearer ${newToken}`;
        return axios.request(error.config);
    }
    return Promise.reject(error);
});
```

### Background Cleanup

The `ExpiredTokenCleanupJob` runs daily at 3:00 AM UTC and removes:
- Refresh tokens that expired more than 30 days ago
- Revoked tokens older than 30 days

This maintains a 30-day audit window while preventing table bloat.

### Best Practices

1. **Client Storage**: Store refresh tokens in HttpOnly cookies or secure localStorage
2. **Token Lifetime**: Keep access tokens short (minutes) and refresh tokens longer (weeks)
3. **Automatic Refresh**: Implement token refresh before access token expires
4. **Logout**: Always call logout endpoint to revoke refresh tokens
5. **Security Events**: Revoke all tokens on password change or suspicious activity



### Blazor UI

#### Login.razor (`/login`)
- Email and password form with validation
- DataAnnotations-based validation
- Error handling and loading states
- Link to registration page
- Sets authentication cookie on successful login

#### Register.razor (`/register`)
- Registration form with email, password, and full name
- Password confirmation validation
- Email format validation
- Minimum password length enforcement
- Sets authentication cookie on successful registration

#### Logout.razor (`/logout`)
- Automatically calls logout endpoint
- Clears authentication cookie
- Redirects to login page

#### LoginDisplay.razor
- Uses `<AuthorizeView>` component for authentication state
- Shows user name/email when authenticated
- Login/Register links when not authenticated
- Logout link for authenticated users

## Configuration

### appsettings.json

```json
{
  "Jwt": {
    "Secret": "YOUR_SECRET_KEY_HERE",
    "Issuer": "CoralLedger.Blue",
    "Audience": "CoralLedger.Blue.Web",
    "ExpirationMinutes": 60,
    "RefreshTokenExpirationDays": 30
  }
}
```

### Environment Variables

- `JWT__SECRET`: JWT signing secret (required in production)
- `JWT__ISSUER`: Token issuer (optional, defaults to "CoralLedger.Blue")
- `JWT__AUDIENCE`: Token audience (optional, defaults to "CoralLedger.Blue.Web")
- `JWT__EXPIRATIONMINUTES`: Access token expiration in minutes (optional, defaults to 60)
- `JWT__REFRESHTOKENEXPIRATIONDAYS`: Refresh token expiration in days (optional, defaults to 30)

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
- **Access Token Expiration**: 60 minutes (configurable)
- **Refresh Token Expiration**: 30 days (configurable)
- **Clock Skew**: Zero tolerance
- **Validation**: Issuer, Audience, Lifetime, Signature
- **Refresh Token Storage**: SHA-256 hashed in database
- **Token Rotation**: New refresh token issued on each use
- **Reuse Detection**: Automatic revocation of all tokens on detected theft

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

3. **20260208101213_AddRefreshTokens**
   - Creates `refresh_tokens` table for token storage
   - Adds indexes on `TokenHash`, `TenantUserId`, `ExpiresAt`, `RevokedAt`
   - Implements cascade delete on user deletion

### Indexes

- `(TenantId, Email)` - Unique index on TenantUsers
- `TenantUserId` - Index on all gamification tables for efficient lookups
- `TokenHash` - Unique index on RefreshTokens for fast token validation
- `TenantUserId` - Index on RefreshTokens for user token queries
- `ExpiresAt`, `RevokedAt` - Indexes on RefreshTokens for cleanup operations

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

## OAuth2 External Authentication

### Overview

CoralLedger Blue supports OAuth2 authentication with external providers, allowing users to sign in with their existing Google or Microsoft accounts.

### Supported Providers

1. **Google** - Google Sign-In (OAuth 2.0)
2. **Microsoft** - Microsoft Account / Azure AD

### OAuth Endpoints

- `GET /api/auth/signin-google` - Initiate Google OAuth flow
- `GET /api/auth/signin-microsoft` - Initiate Microsoft OAuth flow
- `GET /api/auth/oauth-callback` - OAuth provider callback handler

### Configuration

OAuth providers are registered conditionally based on configuration:

```csharp
// Google OAuth (only if credentials configured)
var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
if (!string.IsNullOrEmpty(googleClientId) && !string.IsNullOrEmpty(googleClientSecret))
{
    builder.Services.AddAuthentication()
        .AddGoogle("Google", options => { ... });
}
```

**Configuration required** (via User Secrets, Environment Variables, or Key Vault):
```json
{
  "Authentication": {
    "Google": {
      "ClientId": "your-google-client-id",
      "ClientSecret": "your-google-client-secret"
    },
    "Microsoft": {
      "ClientId": "your-microsoft-client-id",
      "ClientSecret": "your-microsoft-client-secret"
    }
  }
}
```

For detailed setup instructions, see [OAuth2 Configuration Guide](OAUTH2-CONFIGURATION.md).

### OAuth Flow

1. **User clicks OAuth button** on Login/Register page
2. **Redirect to provider** (Google/Microsoft) for authentication
3. **User authenticates** and grants consent
4. **Provider redirects back** to callback endpoint with authorization code
5. **Callback handler**:
   - Exchanges code for access token
   - Retrieves user profile (email, name)
   - **Finds or creates user** by email
   - Links OAuth provider to user account
   - Marks email as confirmed (verified by provider)
   - Generates JWT and sets authentication cookie
6. **User redirected** to dashboard (logged in)

### Account Linking

**Existing User**: If a user with the same email exists:
- OAuth provider information is added to their account
- User can now sign in with either password or OAuth
- Email automatically marked as verified

**New User**: If no user exists with that email:
- New account created automatically
- Email pre-verified (no verification email sent)
- Assigned to default tenant
- No password set (OAuth-only authentication)

### Hybrid Authentication

Users can have **both password and OAuth** authentication:
- Create account with email/password
- Later link Google or Microsoft account
- Sign in with either method
- Useful for password recovery and flexibility

### Security Features

✅ **Email Verification** - OAuth users have pre-verified emails  
✅ **Account Lockout** - Lockout policy applies to OAuth logins  
✅ **Account Status** - Inactive accounts cannot sign in via OAuth  
✅ **Audit Trail** - OAuth logins recorded in `LastLoginAt`  
✅ **Provider Validation** - Only configured providers accepted  
✅ **Secure Storage** - OAuth subject IDs stored separately from email  

### UI Integration

OAuth buttons appear on Login and Register pages:
- **Google button** - With Google logo and "Continue with Google" text
- **Microsoft button** - With Microsoft logo and "Continue with Microsoft" text
- **Responsive design** - Works on mobile and desktop
- **Localized** - Supports English, Spanish, and Haitian Creole

### Database Schema

OAuth-related fields in `TenantUser`:
```sql
OAuthProvider VARCHAR(50) NULL  -- 'Google', 'Microsoft', or NULL
OAuthSubjectId VARCHAR(255) NULL  -- Provider's unique user ID
```

### Testing

OAuth integration tests available in `OAuthAuthenticationEndpointsTests.cs`:
```bash
dotnet test --filter "FullyQualifiedName~OAuthAuthenticationEndpointsTests"
```

Tests cover:
- OAuth provider initiation
- New user creation via OAuth
- Existing user account linking
- Email verification behavior
- Database schema validation
- Error handling

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
3. **No OAuth2 External Providers**: Google, Microsoft, GitHub login not implemented

~~3. **No Refresh Token Storage**: Refresh tokens generated but not persisted~~ ✅ **Completed**

## Blazor Authentication State

### Implementation (✅ Completed)

The application now includes a complete Blazor authentication state implementation with the following features:

#### Cookie-Based Authentication
- **JwtAuthenticationStateProvider**: Custom authentication state provider that bridges HttpContext authentication to Blazor components
- **HttpOnly Cookies**: Secure cookie storage prevents XSS attacks
- **SameSite=Lax**: CSRF protection enabled
- **Persistent Sessions**: Cookies survive browser restarts
- **1-hour Expiration**: Matches JWT token lifetime

#### Multi-Scheme Authentication
The application supports three authentication schemes simultaneously:
1. **Cookie Authentication** - Default for Blazor pages
2. **JWT Bearer** - For API calls with Authorization header
3. **API Key** - For external integrations with X-API-Key header

#### Protected Pages
The following pages are protected with `[Authorize]` attribute:
- `/profile` - User profile page
- `/settings` - Application settings
- `/admin/alert-rules` - Admin-only page (requires Admin role)

#### Login Flow
1. User submits login credentials via `/login` page
2. Server validates credentials and generates JWT token
3. Server sets HttpOnly cookie with user claims
4. User is redirected to dashboard or return URL
5. `LoginDisplay` component shows user name and logout link

#### Logout Flow
1. User clicks logout link in `LoginDisplay` component
2. Browser navigates to `/logout` page
3. Page calls `/api/auth/logout` endpoint
4. Server clears authentication cookie
5. User is redirected to login page

### Testing
All 19 authentication integration tests pass:
- ✅ User registration with validation
- ✅ User login with credential verification
- ✅ Account lockout after failed attempts
- ✅ JWT token generation and validation
- ✅ Logout clears authentication cookie
- ✅ Refresh token generation and storage
- ✅ Refresh token validation and rotation
- ✅ Token reuse detection and security

## Future Enhancements

### Priority 1
- [ ] Email verification flow
- [ ] Password reset flow
- [ ] Token expiration handling with automatic logout

### Priority 2
- [ ] OAuth2 external providers (Google, Microsoft, GitHub)
- [ ] Two-factor authentication (2FA)
- [ ] Password complexity requirements

~~- [ ] Refresh token rotation and storage~~ ✅ **Completed**

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
