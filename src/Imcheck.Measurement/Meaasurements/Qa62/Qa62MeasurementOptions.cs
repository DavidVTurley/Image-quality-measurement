namespace Imcheck.Measurement.Meaasurements.Qa62;

public sealed record Qa62MeasurementOptions
{
    public int? SampleSize { get; init; }

    public double SamplingPixelsPerInch { get; init; } = 301.1;

    public Qa62TargetBounds? TargetBounds { get; init; }
}

public sealed record Qa62TargetBounds(double X, double Y, double Width, double Height);
