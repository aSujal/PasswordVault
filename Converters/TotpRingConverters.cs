using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace PasswordVault.Converters;

// Renders the countdown ring behind a live TOTP code: an SVG-style arc path that sweeps away
// as the current 30s (or whatever period) window elapses.
public class TotpRingPathConverter : IValueConverter
{
    private const double Radius = 6.5;
    private const double Center = 8;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not double fraction) return null;

        fraction = Math.Clamp(fraction, 0.0, 1.0);
        if (fraction <= 0.0) return null;

        double sweepAngle = fraction * 360.0;
        const double startAngle = -90; // 12 o'clock

        var (startX, startY) = PointOnCircle(startAngle);

        if (fraction >= 0.999)
        {
            // A single arc command can't sweep a full circle - split into two half-circles.
            var (midX, midY) = PointOnCircle(startAngle + 180);
            return FormattableString.Invariant(
                $"M {startX:F2},{startY:F2} A {Radius},{Radius} 0 1 1 {midX:F2},{midY:F2} A {Radius},{Radius} 0 1 1 {startX:F2},{startY:F2}");
        }

        var (endX, endY) = PointOnCircle(startAngle + sweepAngle);
        int largeArc = sweepAngle > 180 ? 1 : 0;
        return FormattableString.Invariant(
            $"M {startX:F2},{startY:F2} A {Radius},{Radius} 0 {largeArc} 1 {endX:F2},{endY:F2}");
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static (double x, double y) PointOnCircle(double angleDegrees)
    {
        double rad = angleDegrees * Math.PI / 180.0;
        return (Center + Radius * Math.Cos(rad), Center + Radius * Math.Sin(rad));
    }
}

// Turns the ring/code red once the current TOTP code is about to expire.
public class TotpUrgentBrushConverter : IValueConverter
{
    private const int UrgentThresholdSeconds = 5;
    private static readonly IBrush Normal = new SolidColorBrush(Colors.MediumSeaGreen);
    private static readonly IBrush Urgent = new SolidColorBrush(Colors.IndianRed);

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is int secondsRemaining && secondsRemaining <= UrgentThresholdSeconds ? Urgent : Normal;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
