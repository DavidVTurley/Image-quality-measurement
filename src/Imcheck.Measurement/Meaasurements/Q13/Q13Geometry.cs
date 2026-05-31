using OpenCvSharp;

namespace Imcheck.Measurement.Meaasurements.Q13;

public sealed record Q13Point(double X, double Y)
{
    public Point2f ToPoint2f() => new((float)X, (float)Y);
}

public sealed record Q13StripGeometry(Q13Point TopLeft, Q13Point TopRight, Q13Point BottomRight)
{
    public Q13Point BottomLeft => new(
        TopLeft.X + BottomRight.X - TopRight.X,
        TopLeft.Y + BottomRight.Y - TopRight.Y);

    public double Width => Distance(TopLeft, TopRight);

    public double Height => Distance(TopRight, BottomRight);

    public Q13Point Center => new(
        (TopLeft.X + TopRight.X + BottomRight.X + BottomLeft.X) / 4.0,
        (TopLeft.Y + TopRight.Y + BottomRight.Y + BottomLeft.Y) / 4.0);

    public double AngleRadians => Math.Atan2(TopRight.Y - TopLeft.Y, TopRight.X - TopLeft.X);

    public Q13Point PointAt(double normalizedX, double normalizedY)
    {
        var left = Interpolate(TopLeft, BottomLeft, normalizedY);
        var right = Interpolate(TopRight, BottomRight, normalizedY);
        return Interpolate(left, right, normalizedX);
    }

    public Q13StripGeometry Translate(double deltaX, double deltaY)
    {
        return new Q13StripGeometry(
            new Q13Point(TopLeft.X + deltaX, TopLeft.Y + deltaY),
            new Q13Point(TopRight.X + deltaX, TopRight.Y + deltaY),
            new Q13Point(BottomRight.X + deltaX, BottomRight.Y + deltaY));
    }

    public Q13StripGeometry Rotate(double angleRadians)
    {
        var center = Center;
        return new Q13StripGeometry(
            RotatePoint(TopLeft, center, angleRadians),
            RotatePoint(TopRight, center, angleRadians),
            RotatePoint(BottomRight, center, angleRadians));
    }

    public Q13StripGeometry Extend(double normalizedLeft, double normalizedRight)
    {
        return FromThreePoints(
            PointAt(-normalizedLeft, 0),
            PointAt(1.0 + normalizedRight, 0),
            PointAt(1.0 + normalizedRight, 1));
    }

    public Q13StripGeometry ResizeFromCorner(Q13StripCorner corner, Q13Point imagePoint)
    {
        var widthAxis = UnitVector(TopLeft, TopRight);
        var heightAxis = new Q13Point(-widthAxis.Y, widthAxis.X);
        var currentHeightDirection = UnitVector(TopRight, BottomRight);
        if (Dot(heightAxis, currentHeightDirection) < 0)
        {
            heightAxis = new Q13Point(-heightAxis.X, -heightAxis.Y);
        }

        return corner switch
        {
            Q13StripCorner.TopLeft => FromOppositeBottomRight(imagePoint, widthAxis, heightAxis),
            Q13StripCorner.TopRight => FromOppositeBottomLeft(imagePoint, widthAxis, heightAxis),
            Q13StripCorner.BottomRight => FromOppositeTopLeft(imagePoint, widthAxis, heightAxis),
            _ => this
        };
    }

    public static Q13StripGeometry FromThreePoints(Q13Point topLeft, Q13Point topRight, Q13Point bottomRight)
    {
        var widthAxis = UnitVector(topLeft, topRight);
        var heightAxis = new Q13Point(-widthAxis.Y, widthAxis.X);
        var requestedHeight = Dot(new Q13Point(bottomRight.X - topRight.X, bottomRight.Y - topRight.Y), heightAxis);
        if (requestedHeight < 0)
        {
            heightAxis = new Q13Point(-heightAxis.X, -heightAxis.Y);
            requestedHeight = Math.Abs(requestedHeight);
        }

        var correctedBottomRight = new Q13Point(
            topRight.X + heightAxis.X * Math.Max(1, requestedHeight),
            topRight.Y + heightAxis.Y * Math.Max(1, requestedHeight));
        return new Q13StripGeometry(topLeft, topRight, correctedBottomRight);
    }

    public static IReadOnlyList<Q13SampleRegion> CreateDefaultSampleRegions(double normalizedSampleSize = 0.11, int patchCount = 20)
    {
        return Enumerable.Range(0, patchCount)
            .Select(index => new Q13SampleRegion(index, (index + 0.5) / patchCount, 0.5, normalizedSampleSize))
            .ToArray();
    }

    internal Point2f[] SourcePoints()
    {
        return [TopLeft.ToPoint2f(), TopRight.ToPoint2f(), BottomRight.ToPoint2f(), BottomLeft.ToPoint2f()];
    }

    private static Q13Point Interpolate(Q13Point first, Q13Point second, double amount)
    {
        return new Q13Point(
            first.X + (second.X - first.X) * amount,
            first.Y + (second.Y - first.Y) * amount);
    }

    private static Q13Point RotatePoint(Q13Point point, Q13Point center, double angleRadians)
    {
        var cos = Math.Cos(angleRadians);
        var sin = Math.Sin(angleRadians);
        var x = point.X - center.X;
        var y = point.Y - center.Y;
        return new Q13Point(
            center.X + x * cos - y * sin,
            center.Y + x * sin + y * cos);
    }

    private static double Distance(Q13Point first, Q13Point second)
    {
        return Math.Sqrt(Math.Pow(first.X - second.X, 2) + Math.Pow(first.Y - second.Y, 2));
    }

    private static Q13Point UnitVector(Q13Point first, Q13Point second)
    {
        var dx = second.X - first.X;
        var dy = second.Y - first.Y;
        var length = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
        return new Q13Point(dx / length, dy / length);
    }

    private static double Dot(Q13Point first, Q13Point second)
    {
        return first.X * second.X + first.Y * second.Y;
    }

    private Q13StripGeometry FromOppositeBottomRight(Q13Point imagePoint, Q13Point widthAxis, Q13Point heightAxis)
    {
        var opposite = BottomRight;
        var vector = new Q13Point(opposite.X - imagePoint.X, opposite.Y - imagePoint.Y);
        var width = Math.Max(1, Dot(vector, widthAxis));
        var height = Math.Max(1, Dot(vector, heightAxis));
        var topLeft = new Q13Point(opposite.X - widthAxis.X * width - heightAxis.X * height, opposite.Y - widthAxis.Y * width - heightAxis.Y * height);
        var topRight = new Q13Point(opposite.X - heightAxis.X * height, opposite.Y - heightAxis.Y * height);
        return new Q13StripGeometry(topLeft, topRight, opposite);
    }

    private Q13StripGeometry FromOppositeBottomLeft(Q13Point imagePoint, Q13Point widthAxis, Q13Point heightAxis)
    {
        var opposite = BottomLeft;
        var vector = new Q13Point(imagePoint.X - opposite.X, imagePoint.Y - opposite.Y);
        var width = Math.Max(1, Dot(vector, widthAxis));
        var height = Math.Max(1, -Dot(vector, heightAxis));
        var topLeft = new Q13Point(opposite.X - heightAxis.X * height, opposite.Y - heightAxis.Y * height);
        var topRight = new Q13Point(topLeft.X + widthAxis.X * width, topLeft.Y + widthAxis.Y * width);
        var bottomRight = new Q13Point(opposite.X + widthAxis.X * width, opposite.Y + widthAxis.Y * width);
        return new Q13StripGeometry(topLeft, topRight, bottomRight);
    }

    private Q13StripGeometry FromOppositeTopLeft(Q13Point imagePoint, Q13Point widthAxis, Q13Point heightAxis)
    {
        var opposite = TopLeft;
        var vector = new Q13Point(imagePoint.X - opposite.X, imagePoint.Y - opposite.Y);
        var width = Math.Max(1, Dot(vector, widthAxis));
        var height = Math.Max(1, Dot(vector, heightAxis));
        var topRight = new Q13Point(opposite.X + widthAxis.X * width, opposite.Y + widthAxis.Y * width);
        var bottomRight = new Q13Point(topRight.X + heightAxis.X * height, topRight.Y + heightAxis.Y * height);
        return new Q13StripGeometry(opposite, topRight, bottomRight);
    }
}

public enum Q13StripCorner
{
    TopLeft,
    TopRight,
    BottomRight
}

public sealed record Q13SampleRegion(int PatchIndex, double CenterX, double CenterY, double Size)
{
    public Q13SampleRegion MoveTo(double centerX, double centerY) => this with { CenterX = centerX, CenterY = centerY };
}

public sealed record Q13DetectionResult(bool Found, Q13StripGeometry? Geometry, double Score)
{
    public static Q13DetectionResult NotFound { get; } = new(false, null, 0);
}
