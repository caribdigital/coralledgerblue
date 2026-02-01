# CoralLedger Blue API - Developer Portal

Welcome to the CoralLedger Blue API! This API provides programmatic access to marine protected area data, vessel tracking, coral bleaching alerts, and citizen science observations for The Bahamas.

## Getting Started

### 1. Request API Access

To use the CoralLedger Blue API, you'll need an API key. API keys are currently issued on request. Please contact us at `api@coralledger.blue` with:

- Your organization name
- Contact email
- Intended use case
- Expected API usage (requests per day)

### 2. Obtain Your API Key

Once approved, you'll receive:
- A **Client ID** (e.g., `coral_abc123...`)
- An **API Key** (e.g., `clb_your_secret_key...`)

**Important**: Store your API key securely. It will only be shown once.

### 3. Make Your First Request

Include your API key in the `X-API-Key` header:

```bash
curl -H "X-API-Key: clb_your_secret_key..." \
  https://api.coralledger.blue/api/mpas
```

## Authentication

CoralLedger Blue uses API key authentication. Include your API key in every request using the `X-API-Key` header:

```
X-API-Key: clb_your_secret_key_here
```

### Security Best Practices

- **Never** commit API keys to version control
- Store API keys in environment variables or secure vaults
- Rotate API keys periodically
- Use different API keys for development and production
- Never share API keys publicly

## Rate Limiting

API requests are rate-limited to ensure fair usage:

- **Default**: 60 requests per minute
- **Custom**: Contact us for higher limits

When you exceed the rate limit, you'll receive a `429 Too Many Requests` response with a `Retry-After` header indicating when you can retry.

Example response:
```json
{
  "error": "Too Many Requests",
  "message": "Rate limit exceeded. Please try again later.",
  "retryAfterSeconds": 30
}
```

## API Endpoints

### Marine Protected Areas (MPAs)

#### List All MPAs
```
GET /api/mpas
```

Returns a summary of all Marine Protected Areas in The Bahamas.

**Response:**
```json
[
  {
    "id": "guid",
    "name": "Exuma Cays Land and Sea Park",
    "islandGroup": "Exumas",
    "protectionLevel": "NoTake",
    "areaSquareKm": 456.0,
    "status": "Active"
  }
]
```

#### Get MPA by ID
```
GET /api/mpas/{id}
```

Returns detailed information about a specific MPA.

#### Get MPAs as GeoJSON
```
GET /api/mpas/geojson?resolution=medium
```

Returns all MPAs as a GeoJSON FeatureCollection for mapping.

**Query Parameters:**
- `resolution`: `full`, `detail`, `medium`, or `low` (default: `medium`)

### Vessel Tracking

#### Search Vessels
```
GET /api/vessels/search?query={name}&flag={country}
```

Search for vessels by name, MMSI, IMO, or flag state.

#### Get Fishing Events
```
GET /api/vessels/fishing-events/bahamas?startDate={date}&endDate={date}
```

Get fishing events detected in Bahamian waters.

### Coral Bleaching

#### Get Bleaching Alerts for Bahamas
```
GET /api/bleaching/bahamas
```

Returns current coral bleaching alert levels for The Bahamas.

**Response:**
```json
{
  "region": "Bahamas",
  "alertLevel": "Watch",
  "seaSurfaceTemperature": 29.5,
  "degreeHeatingWeeks": 2.1,
  "updatedAt": "2024-01-15T10:00:00Z"
}
```

#### Get Bleaching Data for MPA
```
GET /api/bleaching/mpa/{mpaId}
```

Returns bleaching data specific to a Marine Protected Area.

### Background Jobs

#### Trigger Manual Data Sync
```
POST /api/jobs/sync/bleaching
POST /api/jobs/sync/vessels
```

Manually trigger data synchronization jobs (requires elevated permissions).

## API Scopes

API keys have assigned scopes that determine what operations they can perform:

- `read` - Read-only access to all endpoints (default)
- `write` - Create and update data (citizen observations, etc.)
- `admin` - Full access including administrative operations

## Response Codes

| Code | Description |
|------|-------------|
| 200 | Success |
| 400 | Bad Request - Invalid parameters |
| 401 | Unauthorized - Missing or invalid API key |
| 403 | Forbidden - Insufficient permissions |
| 404 | Not Found - Resource doesn't exist |
| 429 | Too Many Requests - Rate limit exceeded |
| 500 | Internal Server Error |

## Error Handling

Error responses follow a consistent format:

```json
{
  "error": "Error Type",
  "message": "Human-readable error description",
  "details": {
    "additionalInfo": "value"
  }
}
```

## Code Examples

### cURL

```bash
# Get all MPAs
curl -H "X-API-Key: clb_your_key" \
  https://api.coralledger.blue/api/mpas

# Get MPAs as GeoJSON
curl -H "X-API-Key: clb_your_key" \
  https://api.coralledger.blue/api/mpas/geojson?resolution=medium
```

### Python

```python
import requests

API_KEY = "clb_your_key"
BASE_URL = "https://api.coralledger.blue"

headers = {
    "X-API-Key": API_KEY
}

# Get all MPAs
response = requests.get(f"{BASE_URL}/api/mpas", headers=headers)
mpas = response.json()

for mpa in mpas:
    print(f"{mpa['name']}: {mpa['areaSquareKm']} km²")
```

### JavaScript (Node.js)

```javascript
const axios = require('axios');

const API_KEY = 'clb_your_key';
const BASE_URL = 'https://api.coralledger.blue';

const headers = {
  'X-API-Key': API_KEY
};

// Get all MPAs
async function getMPAs() {
  const response = await axios.get(`${BASE_URL}/api/mpas`, { headers });
  return response.data;
}

getMPAs().then(mpas => {
  mpas.forEach(mpa => {
    console.log(`${mpa.name}: ${mpa.areaSquareKm} km²`);
  });
});
```

### C#

```csharp
using System.Net.Http;
using System.Net.Http.Headers;

var apiKey = "clb_your_key";
var baseUrl = "https://api.coralledger.blue";

using var client = new HttpClient();
client.DefaultRequestHeaders.Add("X-API-Key", apiKey);

// Get all MPAs
var response = await client.GetAsync($"{baseUrl}/api/mpas");
var mpas = await response.Content.ReadFromJsonAsync<List<MpaSummary>>();

foreach (var mpa in mpas)
{
    Console.WriteLine($"{mpa.Name}: {mpa.AreaSquareKm} km²");
}
```

## Usage Analytics

Monitor your API usage through the API Key Management dashboard:

- Total requests
- Success/error rates
- Average response times
- Requests by endpoint
- Rate limit usage

Access your analytics at: `/api/api-keys/clients/{clientId}/usage`

## Support

### Documentation
- Full API Reference: https://api.coralledger.blue/scalar/v1
- GitHub Repository: https://github.com/caribdigital/coralledgerblue
- Issue Tracker: https://github.com/caribdigital/coralledgerblue/issues

### Contact
- Email: api@coralledger.blue
- Discussions: https://github.com/caribdigital/coralledgerblue/discussions

## Changelog

### Version 1.0.0 (Current)
- Initial release
- API key authentication
- MPA data endpoints
- Vessel tracking endpoints
- Coral bleaching endpoints
- Background job triggers

## Terms of Use

By using the CoralLedger Blue API, you agree to:

1. **Attribution**: Credit CoralLedger Blue in any public-facing applications
2. **Fair Use**: Respect rate limits and don't abuse the service
3. **No Resale**: Don't resell or redistribute the raw data
4. **Conservation**: Use the data for marine conservation and research purposes

For commercial use or higher rate limits, please contact us.

## License

The CoralLedger Blue API and associated data are provided under the MIT License. See [LICENSE](https://github.com/caribdigital/coralledgerblue/blob/main/LICENSE) for details.

---

**Built with ❤️ for the Bahamian Blue Economy**

Questions? Contact us at api@coralledger.blue
