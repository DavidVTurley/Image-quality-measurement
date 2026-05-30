namespace Imcheck.Measurement;

public sealed record Q13MeasurementOptions
{
    public int PatchCount { get; init; } = 20;

    public int SampleSize { get; init; } = 39;

    public IReadOnlyList<Q13SamplePoint>? SampleCenters { get; init; }
}
