using NetTopologySuite.Geometries;

namespace CoralLedger.Domain.Validation;

/// <summary>
/// Validates geographic coordinates for the CoralLedger platform.
/// Implements Thorne Rule 10: Coordinate validation gates.
/// </summary>
public static class CoordinateValidator
{
    /// <summary>
    /// Valid latitude range
    /// </summary>
    public const double MinLatitude = -90.0;
    public const double MaxLatitude = 90.0;

    /// <summary>
    /// Valid longitude range
    /// </summary>
    public const double MinLongitude = -180.0;
    public const double MaxLongitude = 180.0;

    /// <summary>
    /// Bahamas bounding box (approximate)
    /// </summary>
    public static class BahamasBounds
    {
        public const double MinLatitude = 20.91;  // Southern tip (Great Inagua)
        public const double MaxLatitude = 27.26;  // Northern tip (Walker's Cay)
        public const double MinLongitude = -80.47; // Western edge
        public const double MaxLongitude = -72.71; // Eastern edge (Inagua)
    }

    /// <summary>
    /// Maximum coordinate precision (decimal places)
    /// Beyond this precision is generally meaningless for marine applications
    /// </summary>
    public const int MaxPrecisionDecimalPlaces = 6; // ~11cm precision

    /// <summary>
    /// Validates that latitude is within valid range (-90 to 90)
    /// </summary>
    public static bool IsValidLatitude(double latitude)
    {
        return latitude >= MinLatitude && latitude <= MaxLatitude && !double.IsNaN(latitude) && !double.IsInfinity(latitude);
    }

    /// <summary>
    /// Validates that longitude is within valid range (-180 to 180)
    /// </summary>
    public static bool IsValidLongitude(double longitude)
    {
        return longitude >= MinLongitude && longitude <= MaxLongitude && !double.IsNaN(longitude) && !double.IsInfinity(longitude);
    }

    /// <summary>
    /// Validates that both latitude and longitude are within valid ranges
    /// </summary>
    public static bool IsValidCoordinate(double latitude, double longitude)
    {
        return IsValidLatitude(latitude) && IsValidLongitude(longitude);
    }

    /// <summary>
    /// Validates a Point geometry
    /// </summary>
    public static bool IsValidPoint(Point? point)
    {
        if (point == null || point.IsEmpty)
        {
            return false;
        }

        return IsValidCoordinate(point.Y, point.X);
    }

    /// <summary>
    /// Validates that coordinates are within The Bahamas bounding box
    /// </summary>
    public static bool IsWithinBahamas(double latitude, double longitude)
    {
        return latitude >= BahamasBounds.MinLatitude &&
               latitude <= BahamasBounds.MaxLatitude &&
               longitude >= BahamasBounds.MinLongitude &&
               longitude <= BahamasBounds.MaxLongitude;
    }

    /// <summary>
    /// Validates that a Point is within The Bahamas bounding box
    /// </summary>
    public static bool IsWithinBahamas(Point? point)
    {
        if (point == null || point.IsEmpty)
        {
            return false;
        }

        return IsWithinBahamas(point.Y, point.X);
    }

    /// <summary>
    /// Validates coordinate precision doesn't exceed maximum useful precision
    /// </summary>
    public static bool HasValidPrecision(double value)
    {
        var decimalPlaces = GetDecimalPlaces(value);
        return decimalPlaces <= MaxPrecisionDecimalPlaces;
    }

    /// <summary>
    /// Truncates coordinate to maximum useful precision
    /// </summary>
    public static double TruncatePrecision(double value)
    {
        var factor = Math.Pow(10, MaxPrecisionDecimalPlaces);
        return Math.Truncate(value * factor) / factor;
    }

    /// <summary>
    /// Full validation of a coordinate with detailed result
    /// </summary>
    public static CoordinateValidationResult Validate(double latitude, double longitude, bool requireBahamas = false)
    {
        var errors = new List<string>();

        if (double.IsNaN(latitude))
        {
            errors.Add("Latitude is NaN");
        }
        else if (double.IsInfinity(latitude))
        {
            errors.Add("Latitude is infinite");
        }
        else if (!IsValidLatitude(latitude))
        {
            errors.Add($"Latitude {latitude} is outside valid range [{MinLatitude}, {MaxLatitude}]");
        }

        if (double.IsNaN(longitude))
        {
            errors.Add("Longitude is NaN");
        }
        else if (double.IsInfinity(longitude))
        {
            errors.Add("Longitude is infinite");
        }
        else if (!IsValidLongitude(longitude))
        {
            errors.Add($"Longitude {longitude} is outside valid range [{MinLongitude}, {MaxLongitude}]");
        }

        if (errors.Count == 0 && requireBahamas && !IsWithinBahamas(latitude, longitude))
        {
            errors.Add($"Coordinate ({latitude}, {longitude}) is outside The Bahamas region");
        }

        return new CoordinateValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Full validation of a Point geometry
    /// </summary>
    public static CoordinateValidationResult Validate(Point? point, bool requireBahamas = false)
    {
        if (point == null)
        {
            return new CoordinateValidationResult(false, new[] { "Point is null" });
        }

        if (point.IsEmpty)
        {
            return new CoordinateValidationResult(false, new[] { "Point is empty" });
        }

        return Validate(point.Y, point.X, requireBahamas);
    }

    /// <summary>
    /// Normalizes longitude to the range [-180, 180]
    /// </summary>
    public static double NormalizeLongitude(double longitude)
    {
        while (longitude < -180) longitude += 360;
        while (longitude > 180) longitude -= 360;
        return longitude;
    }

    private static int GetDecimalPlaces(double value)
    {
        var text = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var decimalIndex = text.IndexOf('.');
        if (decimalIndex < 0) return 0;
        return text.Length - decimalIndex - 1;
    }
}

/// <summary>
/// Result of coordinate validation
/// </summary>
public record CoordinateValidationResult(bool IsValid, IEnumerable<string> Errors)
{
    public static CoordinateValidationResult Success => new(true, Array.Empty<string>());

    public string ErrorMessage => string.Join("; ", Errors);
}
