namespace Imcheck.Measurement.Meaasurements.Q13;

public sealed record Q13MeasurementOptions
{
    public int PatchCount { get; init; } = 20;

    public int SampleSize { get; init; } = 39;

    public IReadOnlyList<Q13SamplePoint>? SampleCenters { get; init; }

    public Q13StripGeometry? StripGeometry { get; init; }

    public IReadOnlyList<Q13SampleRegion>? SampleRegions { get; init; }
}
