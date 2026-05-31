namespace Imcheck.Measurement.Measurements.Common;

internal static class MeasurementMath
{
    public static int Clamp(int value, int min, int max)
    {
        return Math.Min(Math.Max(value, min), max);
    }

    public static int MakeOdd(int value)
    {
        return value % 2 == 0 ? value + 1 : value;
    }
}
