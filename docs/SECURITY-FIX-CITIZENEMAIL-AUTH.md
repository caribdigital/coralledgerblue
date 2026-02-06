# Security Fix: CitizenEmail Impersonation Vulnerability

## Summary

Fixed a critical security vulnerability where the citizen observation submission endpoint accepted `CitizenEmail` directly from the request body without any authentication verification, allowing malicious actors to submit observations claiming to be any user.

## Vulnerability Details

### Before the Fix

**Risk Level:** Critical

**Issue:** The `/api/observations` POST endpoint accepted observations without authentication:

```http
POST /api/observations
{
  "citizenEmail": "anyone@example.com",  // No verification!
  "citizenName": "Any Name",
  "speciesId": "...",
  ...
}
```

**Attack Vectors:**
1. **Impersonation:** Malicious actors could submit observations under any email address
2. **Gamification fraud:** Points and badges could be awarded to arbitrary email addresses
3. **Data integrity:** Cannot trust observation attribution
4. **Reputation manipulation:** Users' accuracy rates could be manipulated

### After the Fix

**Protection Level:** Authenticated

All observation submission endpoints now require API key authentication:

- `POST /api/observations` - Create observation
- `POST /api/observations/{id}/photos` - Upload photos
- `POST /api/observations/{id}/classify-species` - AI classification

## Implementation Details

### 1. API Key Authentication

Added `.RequireAuthorization()` to all observation modification endpoints. API keys are validated via the existing `ApiKeyAuthenticationHandler`.

### 2. Tracking Fields

Added new fields to `CitizenObservation` entity:

```csharp
public string? ApiClientId { get; private set; }
public bool IsEmailVerified { get; private set; }
```

- `ApiClientId`: Tracks which API client created the observation
- `IsEmailVerified`: Indicates if the email came from an authenticated source

### 3. Email Attribution

The system now:
1. Extracts email from authenticated API client's contact email (preferred)
2. Falls back to request's `citizenEmail` for backward compatibility
3. Marks observations as verified when created by authenticated clients

### 4. Ownership Validation

Photo upload and species classification endpoints now verify that the observation belongs to the authenticated client:

```csharp
var clientId = user.FindFirst("ClientId")?.Value;
if (observation.ApiClientId != clientId)
{
    return Results.Forbid();
}
```

## Database Migration

Created migration `20260206020847_AddApiClientAuthenticationToObservations`:

```sql
ALTER TABLE citizen_observations 
ADD COLUMN ApiClientId VARCHAR(100),
ADD COLUMN IsEmailVerified BOOLEAN NOT NULL DEFAULT FALSE;

CREATE INDEX IX_citizen_observations_ApiClientId ON citizen_observations(ApiClientId);
CREATE INDEX IX_citizen_observations_IsEmailVerified ON citizen_observations(IsEmailVerified);
```

**Backward Compatibility:** Existing observations will have:
- `ApiClientId = NULL`
- `IsEmailVerified = FALSE`

This allows administrators to identify and review observations that were submitted before authentication was required.

## Testing

Created comprehensive integration tests in `ObservationEndpointsTests.cs`:

1. ✅ `CreateObservation_WithoutApiKey_ReturnsUnauthorized` - Blocks unauthenticated requests
2. ✅ `CreateObservation_WithValidApiKey_ReturnsCreated` - Allows authenticated requests
3. ✅ `CreateObservation_WithInvalidApiKey_ReturnsUnauthorized` - Rejects invalid keys
4. ✅ `GetObservations_WithoutApiKey_ReturnsOk` - Read operations remain open
5. ✅ `CreateObservation_StoresApiClientId` - Tracks client properly

All tests passing.

## Breaking Changes

**Impact:** High - Existing API clients will be affected

### For API Consumers

**Before:**
```bash
curl -X POST https://api.coralledger.blue/api/observations \
  -H "Content-Type: application/json" \
  -d '{"latitude": 25.0, "longitude": -77.5, ...}'
```

**After:**
```bash
curl -X POST https://api.coralledger.blue/api/observations \
  -H "Content-Type: application/json" \
  -H "X-API-Key: clb_your_api_key_here" \
  -d '{"latitude": 25.0, "longitude": -77.5, ...}'
```

### Migration Path for Existing Clients

1. **Register for API Key**
   ```bash
   POST /api/api-keys/clients
   {
     "name": "My Application",
     "organizationName": "My Org",
     "contactEmail": "contact@example.com",
     "rateLimitPerMinute": 60
   }
   ```

2. **Store API Key Securely**
   - The response includes a `plainKey` field
   - This is the only time the key is visible
   - Store it in environment variables or secure key vault

3. **Update API Calls**
   - Add `X-API-Key` header to all POST requests to `/api/observations` and sub-endpoints

## Security Improvements

### Before vs After

| Aspect | Before | After |
|--------|--------|-------|
| Authentication | ❌ None | ✅ API Key Required |
| Email Verification | ❌ Self-reported | ✅ From authenticated client |
| Impersonation Risk | 🔴 Critical | ✅ Mitigated |
| Audit Trail | ❌ No tracking | ✅ ApiClientId tracked |
| Ownership Validation | ❌ None | ✅ Client ownership verified |

### Additional Security Measures

1. **Rate Limiting:** API keys have rate limits (60 req/min default)
2. **Audit Logging:** All API usage is logged via `ApiUsageTrackingMiddleware`
3. **Key Management:** API keys can be revoked if compromised
4. **Scopes:** Keys support scope-based permissions (future enhancement)

## Recommendations

### Short-term (Completed)
- [x] Require API key authentication for observation submissions
- [x] Track API client ID with observations
- [x] Add ownership validation for related operations
- [x] Flag existing observations as unverified

### Long-term (Future Work)
- [ ] Implement end-user authentication (OAuth2/OIDC)
- [ ] Migrate from email-based identification to user IDs
- [ ] Add email verification workflow for citizen scientists
- [ ] Implement observation claiming for verified users
- [ ] Add admin dashboard to review unverified observations

## Compliance Notes

This fix addresses:
- **OWASP A01:2021** - Broken Access Control
- **OWASP A07:2021** - Identification and Authentication Failures

The implementation follows security best practices:
- Defense in depth (multiple layers of validation)
- Audit logging for accountability
- Backward compatibility for smooth migration
- Clear documentation for API consumers

## Deployment Checklist

Before deploying to production:

1. [ ] Run database migration
2. [ ] Notify API consumers of breaking changes (30 day notice recommended)
3. [ ] Create API keys for known legitimate clients
4. [ ] Monitor authentication failures during migration period
5. [ ] Set up alerts for suspicious patterns (multiple failed auth attempts)
6. [ ] Review and address existing unverified observations
7. [ ] Update public API documentation
8. [ ] Update client SDKs and examples

## Contact

For questions about this security fix or to request an API key, contact:
- Security Team: security@coralledger.blue
- API Support: api-support@coralledger.blue

## References

- Issue: #[issue-number] - CitizenEmail impersonation vulnerability
- PR: #[pr-number] - Security fix implementation
- Documentation: `/docs/api-reference.md`
- Migration Guide: `/docs/API-DEVELOPER-PORTAL.md`
