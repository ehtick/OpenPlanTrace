namespace OpenPlanTrace;

internal static class StructuralGeometry
{
    private const double Epsilon = 1e-9;

    public static double NormalizeAngle(double angle)
    {
        while (angle < 0)
        {
            angle += Math.PI;
        }

        while (angle >= Math.PI)
        {
            angle -= Math.PI;
        }

        return angle;
    }

    public static double AngleDifference(PlanLineSegment first, PlanLineSegment second)
    {
        var difference = Math.Abs(NormalizeAngle(first.AngleRadians) - NormalizeAngle(second.AngleRadians));
        return Math.Min(difference, Math.PI - difference);
    }

    public static bool AreParallel(
        PlanLineSegment first,
        PlanLineSegment second,
        double angleToleranceRadians) =>
        AngleDifference(first, second) <= angleToleranceRadians;

    public static PlanVector UnitDirection(PlanLineSegment line)
    {
        var vector = line.Vector;
        var length = line.Length;
        if (length <= Epsilon)
        {
            return new PlanVector(1, 0);
        }

        var x = vector.X / length;
        var y = vector.Y / length;
        if (x < -Epsilon || (Math.Abs(x) <= Epsilon && y < 0))
        {
            x = -x;
            y = -y;
        }

        return new PlanVector(x, y);
    }

    public static PlanVector UnitNormal(PlanLineSegment line)
    {
        var direction = UnitDirection(line);
        return new PlanVector(-direction.Y, direction.X);
    }

    public static double Dot(PlanPoint point, PlanVector vector) =>
        (point.X * vector.X) + (point.Y * vector.Y);

    public static double Dot(PlanVector first, PlanVector second) =>
        (first.X * second.X) + (first.Y * second.Y);

    public static double AxisCoordinate(PlanLineSegment line)
    {
        var normal = UnitNormal(line);
        return Dot(line.Midpoint, normal);
    }

    public static double PerpendicularDistance(PlanLineSegment first, PlanLineSegment second)
    {
        var normal = UnitNormal(first);
        var firstAxis = Dot(first.Midpoint, normal);
        var secondAxis = Dot(second.Midpoint, normal);
        return Math.Abs(firstAxis - secondAxis);
    }

    public static (double Start, double End) ProjectionRange(
        PlanLineSegment line,
        PlanVector direction)
    {
        var start = Dot(line.Start, direction);
        var end = Dot(line.End, direction);
        return start <= end ? (start, end) : (end, start);
    }

    public static double OverlapLength(PlanLineSegment first, PlanLineSegment second)
    {
        var direction = UnitDirection(first);
        var firstRange = ProjectionRange(first, direction);
        var secondRange = ProjectionRange(second, direction);
        return Math.Max(0, Math.Min(firstRange.End, secondRange.End) - Math.Max(firstRange.Start, secondRange.Start));
    }

    public static double OverlapRatio(PlanLineSegment first, PlanLineSegment second)
    {
        var denominator = Math.Max(Epsilon, Math.Min(first.Length, second.Length));
        return Math.Clamp(OverlapLength(first, second) / denominator, 0, 1);
    }

    public static double ProjectedGap(PlanLineSegment first, PlanLineSegment second)
    {
        var direction = UnitDirection(first);
        var firstRange = ProjectionRange(first, direction);
        var secondRange = ProjectionRange(second, direction);
        if (firstRange.End < secondRange.Start)
        {
            return secondRange.Start - firstRange.End;
        }

        if (secondRange.End < firstRange.Start)
        {
            return firstRange.Start - secondRange.End;
        }

        return 0;
    }

    public static double EndpointDistance(PlanLineSegment first, PlanLineSegment second) =>
        new[]
        {
            first.Start.DistanceTo(second.Start),
            first.Start.DistanceTo(second.End),
            first.End.DistanceTo(second.Start),
            first.End.DistanceTo(second.End)
        }.Min();

    public static bool IsPointNearInterior(
        PlanPoint point,
        PlanLineSegment line,
        double tolerance)
    {
        var parameter = line.ProjectParameter(point);
        return parameter > 0.01
            && parameter < 0.99
            && line.DistanceToPoint(point) <= tolerance;
    }

    public static int OrientationBucket(
        PlanLineSegment line,
        double angleToleranceRadians)
    {
        var bucketSize = Math.Max(angleToleranceRadians, Math.PI / 180.0);
        return (int)Math.Round(NormalizeAngle(line.AngleRadians) / bucketSize);
    }

    public static PlanLineSegment Canonicalize(PlanLineSegment line)
    {
        if (line.Start.X < line.End.X)
        {
            return line;
        }

        if (line.Start.X > line.End.X)
        {
            return line.Reverse();
        }

        return line.Start.Y <= line.End.Y ? line : line.Reverse();
    }

    public static double DistanceBetweenBounds(PlanRect first, PlanRect second)
    {
        var dx = Math.Max(0, Math.Max(first.X - second.Right, second.X - first.Right));
        var dy = Math.Max(0, Math.Max(first.Y - second.Bottom, second.Y - first.Bottom));
        return Math.Sqrt((dx * dx) + (dy * dy));
    }
}
