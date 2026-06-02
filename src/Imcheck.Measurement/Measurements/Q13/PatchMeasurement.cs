namespace Imcheck.Measurement.Measurements.Q13;

public sealed record PatchMeasurement(
    int Index,
    double OutputRed,
    double OutputGreen,
    double OutputBlue,
    double NoiseRed,
    double NoiseGreen,
    double NoiseBlue,
    bool IsColor,
    double SampleCenterX,
    double SampleCenterY,
    int SampleX,
    int SampleY,
    int SampleSize,
    int? ReportSampleCenterX = null,
    int? ReportSampleCenterY = null,
    int? ReportSampleWidth = null,
    int? ReportSampleHeight = null)
{
    public double Output => OutputGreen;

    public double Noise => NoiseGreen;

    public int SampleReportCenterX => ReportSampleCenterX ?? (int)Math.Round(SampleCenterX);

    public int SampleReportCenterY => ReportSampleCenterY ?? (int)Math.Round(SampleCenterY);

    public int SampleReportWidth => ReportSampleWidth ?? SampleSize;

    public int SampleReportHeight => ReportSampleHeight ?? SampleSize;
}
