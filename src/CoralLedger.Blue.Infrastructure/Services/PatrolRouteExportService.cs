using System.Globalization;
using System.Text;
using System.Xml;
using CoralLedger.Blue.Domain.Entities;

namespace CoralLedger.Blue.Infrastructure.Services.PatrolExport;

public class PatrolRouteExportService : IPatrolRouteExportService
{
    public string ExportToGpx(PatrolRoute patrolRoute)
    {
        var sb = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            Encoding = Encoding.UTF8
        };

        using (var writer = XmlWriter.Create(sb, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("gpx", "http://www.topografix.com/GPX/1/1");
            writer.WriteAttributeString("version", "1.1");
            writer.WriteAttributeString("creator", "CoralLedger Blue");

            // Metadata
            writer.WriteStartElement("metadata");
            writer.WriteElementString("name", $"Patrol Route {patrolRoute.Id}");
            writer.WriteElementString("desc", patrolRoute.Notes ?? "Conservation patrol route");
            writer.WriteElementString("time", patrolRoute.StartTime.ToString("yyyy-MM-ddTHH:mm:ssZ"));
            writer.WriteEndElement(); // metadata

            // Track
            writer.WriteStartElement("trk");
            writer.WriteElementString("name", $"Patrol by {patrolRoute.OfficerName ?? "Officer"}");
            writer.WriteElementString("type", "patrol");

            writer.WriteStartElement("trkseg");
            
            // Track points
            var orderedPoints = patrolRoute.Points.OrderBy(p => p.Timestamp).ToList();
            foreach (var point in orderedPoints)
            {
                writer.WriteStartElement("trkpt");
                writer.WriteAttributeString("lat", point.Location.Y.ToString("F6", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("lon", point.Location.X.ToString("F6", CultureInfo.InvariantCulture));
                
                if (point.Altitude.HasValue)
                    writer.WriteElementString("ele", point.Altitude.Value.ToString("F1", CultureInfo.InvariantCulture));
                
                writer.WriteElementString("time", point.Timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                
                if (point.Speed.HasValue || point.Heading.HasValue)
                {
                    writer.WriteStartElement("extensions");
                    if (point.Speed.HasValue)
                        writer.WriteElementString("speed", point.Speed.Value.ToString("F2", CultureInfo.InvariantCulture));
                    if (point.Heading.HasValue)
                        writer.WriteElementString("course", point.Heading.Value.ToString("F1", CultureInfo.InvariantCulture));
                    writer.WriteEndElement(); // extensions
                }
                
                writer.WriteEndElement(); // trkpt
            }
            
            writer.WriteEndElement(); // trkseg
            writer.WriteEndElement(); // trk

            // Waypoints
            foreach (var waypoint in patrolRoute.Waypoints.OrderBy(w => w.Timestamp))
            {
                writer.WriteStartElement("wpt");
                writer.WriteAttributeString("lat", waypoint.Location.Y.ToString("F6", CultureInfo.InvariantCulture));
                writer.WriteAttributeString("lon", waypoint.Location.X.ToString("F6", CultureInfo.InvariantCulture));
                
                writer.WriteElementString("time", waypoint.Timestamp.ToString("yyyy-MM-ddTHH:mm:ssZ"));
                writer.WriteElementString("name", waypoint.Title);
                if (!string.IsNullOrEmpty(waypoint.Notes))
                    writer.WriteElementString("desc", waypoint.Notes);
                if (!string.IsNullOrEmpty(waypoint.WaypointType))
                    writer.WriteElementString("type", waypoint.WaypointType);
                
                writer.WriteEndElement(); // wpt
            }

            writer.WriteEndElement(); // gpx
            writer.WriteEndDocument();
        }

        return sb.ToString();
    }

    public string ExportToKml(PatrolRoute patrolRoute)
    {
        var sb = new StringBuilder();
        var settings = new XmlWriterSettings
        {
            Indent = true,
            IndentChars = "  ",
            NewLineChars = "\n",
            Encoding = Encoding.UTF8
        };

        using (var writer = XmlWriter.Create(sb, settings))
        {
            writer.WriteStartDocument();
            writer.WriteStartElement("kml", "http://www.opengis.net/kml/2.2");

            writer.WriteStartElement("Document");
            writer.WriteElementString("name", $"Patrol Route {patrolRoute.Id}");
            writer.WriteElementString("description", 
                $"Officer: {patrolRoute.OfficerName ?? "Unknown"}\n" +
                $"Start: {patrolRoute.StartTime:yyyy-MM-dd HH:mm}\n" +
                $"Status: {patrolRoute.Status}\n" +
                $"{(patrolRoute.TotalDistanceMeters.HasValue ? $"Distance: {patrolRoute.TotalDistanceMeters.Value / 1000:F2} km\n" : "")}" +
                $"{patrolRoute.Notes ?? ""}");

            // Style for the track line
            writer.WriteStartElement("Style");
            writer.WriteAttributeString("id", "patrolLineStyle");
            writer.WriteStartElement("LineStyle");
            writer.WriteElementString("color", "ff0000ff"); // Red line
            writer.WriteElementString("width", "3");
            writer.WriteEndElement(); // LineStyle
            writer.WriteEndElement(); // Style

            // Style for waypoints
            writer.WriteStartElement("Style");
            writer.WriteAttributeString("id", "waypointStyle");
            writer.WriteStartElement("IconStyle");
            writer.WriteElementString("color", "ff00ff00"); // Green icon
            writer.WriteElementString("scale", "1.2");
            writer.WriteEndElement(); // IconStyle
            writer.WriteEndElement(); // Style

            // Patrol route as LineString
            if (patrolRoute.Points.Count >= 2)
            {
                writer.WriteStartElement("Placemark");
                writer.WriteElementString("name", "Patrol Track");
                writer.WriteElementString("styleUrl", "#patrolLineStyle");
                
                writer.WriteStartElement("LineString");
                writer.WriteElementString("extrude", "1");
                writer.WriteElementString("tessellate", "1");
                writer.WriteElementString("altitudeMode", "clampToGround");
                
                var coordinates = string.Join("\n", 
                    patrolRoute.Points
                        .OrderBy(p => p.Timestamp)
                        .Select(p => $"{p.Location.X.ToString("F6", CultureInfo.InvariantCulture)},{p.Location.Y.ToString("F6", CultureInfo.InvariantCulture)},{(p.Altitude ?? 0).ToString("F1", CultureInfo.InvariantCulture)}"));
                
                writer.WriteElementString("coordinates", coordinates);
                writer.WriteEndElement(); // LineString
                writer.WriteEndElement(); // Placemark
            }

            // Waypoints as Placemarks
            foreach (var waypoint in patrolRoute.Waypoints.OrderBy(w => w.Timestamp))
            {
                writer.WriteStartElement("Placemark");
                writer.WriteElementString("name", waypoint.Title);
                writer.WriteElementString("description", 
                    $"Time: {waypoint.Timestamp:yyyy-MM-dd HH:mm}\n" +
                    $"{(string.IsNullOrEmpty(waypoint.WaypointType) ? "" : $"Type: {waypoint.WaypointType}\n")}" +
                    $"{waypoint.Notes ?? ""}");
                writer.WriteElementString("styleUrl", "#waypointStyle");
                
                writer.WriteStartElement("Point");
                writer.WriteElementString("coordinates", 
                    $"{waypoint.Location.X.ToString("F6", CultureInfo.InvariantCulture)}," +
                    $"{waypoint.Location.Y.ToString("F6", CultureInfo.InvariantCulture)},0");
                writer.WriteEndElement(); // Point
                writer.WriteEndElement(); // Placemark
            }

            writer.WriteEndElement(); // Document
            writer.WriteEndElement(); // kml
            writer.WriteEndDocument();
        }

        return sb.ToString();
    }
}
