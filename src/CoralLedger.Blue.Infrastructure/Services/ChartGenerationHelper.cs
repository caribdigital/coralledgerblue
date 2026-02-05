using CoralLedger.Blue.Application.Features.Reports.DTOs;
using ScottPlot;
using SkiaSharp;

namespace CoralLedger.Blue.Infrastructure.Services;

/// <summary>
/// Helper class for generating charts and maps for PDF reports
/// </summary>
public static class ChartGenerationHelper
{
    /// <summary>
    /// Generate a line chart showing bleaching trend (DHW over time)
    /// </summary>
    public static byte[] GenerateBleachingTrendChart(List<BleachingAlertItem> alerts, int width = 800, int height = 400)
    {
        if (alerts == null || alerts.Count == 0)
        {
            return Array.Empty<byte>();
        }

        var plot = new Plot();
        
        // Sort alerts by date
        var sortedAlerts = alerts.OrderBy(a => a.Date).ToList();
        
        // Prepare data
        var dates = sortedAlerts.Select(a => a.Date.ToOADate()).ToArray();
        var dhwValues = sortedAlerts.Select(a => a.DegreeHeatingWeeks).ToArray();
        
        // Add line plot
        var signal = plot.Add.Scatter(dates, dhwValues);
        signal.LineWidth = 2;
        signal.Color = ScottPlot.Color.FromHex("#1976D2"); // Blue
        signal.MarkerSize = 8;
        signal.MarkerShape = MarkerShape.FilledCircle;
        
        // Configure axes
        plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.DateTimeAutomatic();
        plot.Axes.Bottom.Label.Text = "Date";
        plot.Axes.Left.Label.Text = "Degree Heating Weeks (°C-weeks)";
        
        // Title
        plot.Title("Coral Bleaching Trend - DHW Over Time");
        
        // Style
        plot.FigureBackground.Color = ScottPlot.Color.FromHex("#FFFFFF");
        plot.DataBackground.Color = ScottPlot.Color.FromHex("#FAFAFA");
        
        // Add horizontal line at DHW = 4 (critical threshold)
        var criticalLine = plot.Add.HorizontalLine(4);
        criticalLine.Color = ScottPlot.Color.FromHex("#D32F2F"); // Red
        criticalLine.LineWidth = 2;
        criticalLine.LinePattern = LinePattern.Dashed;
        
        // Render to PNG
        return plot.GetImage(width, height).GetImageBytes();
    }

    /// <summary>
    /// Generate a bar chart showing vessel activity by event type
    /// </summary>
    public static byte[] GenerateVesselActivityChart(Dictionary<string, int> eventsByType, int width = 800, int height = 400)
    {
        if (eventsByType == null || eventsByType.Count == 0)
        {
            return Array.Empty<byte>();
        }

        var plot = new Plot();
        
        // Prepare data
        var eventTypes = eventsByType.Keys.ToArray();
        var counts = eventsByType.Values.Select(v => (double)v).ToArray();
        var positions = Enumerable.Range(0, eventTypes.Length).Select(i => (double)i).ToArray();
        
        // Add bar plot
        var barPlot = plot.Add.Bars(positions, counts);
        
        // Color bars
        for (int i = 0; i < barPlot.Bars.Count; i++)
        {
            barPlot.Bars[i].FillColor = ScottPlot.Color.FromHex("#1976D2");
        }
        
        // Configure axes
        plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(
            positions, eventTypes);
        plot.Axes.Bottom.Label.Text = "Event Type";
        plot.Axes.Left.Label.Text = "Number of Events";
        plot.Axes.Bottom.TickLabelStyle.Rotation = 45;
        plot.Axes.Bottom.TickLabelStyle.Alignment = Alignment.MiddleRight;
        
        // Title
        plot.Title("Vessel Activity by Event Type");
        
        // Style
        plot.FigureBackground.Color = ScottPlot.Color.FromHex("#FFFFFF");
        plot.DataBackground.Color = ScottPlot.Color.FromHex("#FAFAFA");
        
        // Render to PNG
        return plot.GetImage(width, height).GetImageBytes();
    }

    /// <summary>
    /// Generate a pie chart showing observations by status
    /// </summary>
    public static byte[] GenerateObservationsStatusChart(int approved, int pending, int rejected, int width = 600, int height = 400)
    {
        if (approved == 0 && pending == 0 && rejected == 0)
        {
            return Array.Empty<byte>();
        }

        var plot = new Plot();
        
        // Prepare data
        var values = new List<double>();
        var labels = new List<string>();
        var colors = new List<ScottPlot.Color>();
        
        if (approved > 0)
        {
            values.Add(approved);
            labels.Add($"Approved ({approved})");
            colors.Add(ScottPlot.Color.FromHex("#388E3C")); // Green
        }
        
        if (pending > 0)
        {
            values.Add(pending);
            labels.Add($"Pending ({pending})");
            colors.Add(ScottPlot.Color.FromHex("#F57C00")); // Orange
        }
        
        if (rejected > 0)
        {
            values.Add(rejected);
            labels.Add($"Rejected ({rejected})");
            colors.Add(ScottPlot.Color.FromHex("#D32F2F")); // Red
        }
        
        // Add pie chart
        var pie = plot.Add.Pie(values.ToArray());
        pie.ExplodeFraction = 0.05;
        
        // Set colors and labels
        for (int i = 0; i < pie.Slices.Count; i++)
        {
            pie.Slices[i].FillColor = colors[i];
            pie.Slices[i].Label = labels[i];
            pie.Slices[i].LabelStyle.FontSize = 12;
            pie.Slices[i].LabelStyle.Bold = true;
        }
        
        // Title
        plot.Title("Observations by Status");
        
        // Style
        plot.FigureBackground.Color = ScottPlot.Color.FromHex("#FFFFFF");
        plot.DataBackground.Color = ScottPlot.Color.FromHex("#FAFAFA");
        plot.HideGrid();
        
        // Render to PNG
        return plot.GetImage(width, height).GetImageBytes();
    }

    /// <summary>
    /// Generate a simple map showing MPA boundary
    /// </summary>
    public static byte[] GenerateMpaMap(double centerLat, double centerLon, string mpaName, int width = 800, int height = 400)
    {
        using var surface = SKSurface.Create(new SKImageInfo(width, height));
        var canvas = surface.Canvas;
        
        // Clear background
        canvas.Clear(SKColors.White);
        
        // Draw ocean background
        var oceanPaint = new SKPaint
        {
            Color = SKColor.Parse("#B3E5FC"),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawRect(0, 0, width, height, oceanPaint);
        
        // Draw grid lines
        var gridPaint = new SKPaint
        {
            Color = SKColor.Parse("#E0E0E0"),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            PathEffect = SKPathEffect.CreateDash(new float[] { 5, 5 }, 0)
        };
        
        // Draw vertical grid lines
        for (int x = 0; x <= width; x += width / 8)
        {
            canvas.DrawLine(x, 0, x, height, gridPaint);
        }
        
        // Draw horizontal grid lines
        for (int y = 0; y <= height; y += height / 6)
        {
            canvas.DrawLine(0, y, width, y, gridPaint);
        }
        
        // Draw MPA boundary (simplified circle)
        var mpaBoundaryPaint = new SKPaint
        {
            Color = SKColor.Parse("#1976D2"),
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3
        };
        
        var mpaFillPaint = new SKPaint
        {
            Color = SKColor.Parse("#1976D2").WithAlpha(50),
            Style = SKPaintStyle.Fill
        };
        
        float centerX = width / 2f;
        float centerY = height / 2f;
        float radius = Math.Min(width, height) / 4f;
        
        canvas.DrawCircle(centerX, centerY, radius, mpaFillPaint);
        canvas.DrawCircle(centerX, centerY, radius, mpaBoundaryPaint);
        
        // Draw center point
        var centerPointPaint = new SKPaint
        {
            Color = SKColor.Parse("#D32F2F"),
            Style = SKPaintStyle.Fill
        };
        canvas.DrawCircle(centerX, centerY, 6, centerPointPaint);
        
        // Draw coordinate labels
        var textPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 14,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial")
        };
        
        var coordText = $"{centerLat:F4}°, {centerLon:F4}°";
        var textWidth = textPaint.MeasureText(coordText);
        canvas.DrawText(coordText, (width - textWidth) / 2, centerY + radius + 25, textPaint);
        
        // Draw title
        var titlePaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 16,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };
        
        var titleText = $"MPA Location: {mpaName}";
        var titleWidth = titlePaint.MeasureText(titleText);
        canvas.DrawText(titleText, (width - titleWidth) / 2, 25, titlePaint);
        
        // Draw scale/compass rose indicator
        var compassSize = 40f;
        var compassX = width - compassSize - 20;
        var compassY = height - compassSize - 20;
        
        // Draw compass circle
        var compassPaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2
        };
        canvas.DrawCircle(compassX, compassY, compassSize / 2, compassPaint);
        
        // Draw N arrow
        var arrowPath = new SKPath();
        arrowPath.MoveTo(compassX, compassY - compassSize / 2 - 5);
        arrowPath.LineTo(compassX - 5, compassY - compassSize / 4);
        arrowPath.LineTo(compassX + 5, compassY - compassSize / 4);
        arrowPath.Close();
        
        var arrowPaint = new SKPaint
        {
            Color = SKColors.Black,
            Style = SKPaintStyle.Fill
        };
        canvas.DrawPath(arrowPath, arrowPaint);
        
        // Draw N label
        var nPaint = new SKPaint
        {
            Color = SKColors.Black,
            TextSize = 14,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Arial", SKFontStyle.Bold)
        };
        var nWidth = nPaint.MeasureText("N");
        canvas.DrawText("N", compassX - nWidth / 2, compassY - compassSize / 2 - 10, nPaint);
        
        // Convert to byte array
        using var image = surface.Snapshot();
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}
