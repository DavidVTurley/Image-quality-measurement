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
    double? ReportSampleTopLeftX = null,
    double? ReportSampleTopLeftY = null,
    double? ReportSampleTopRightX = null,
    double? ReportSampleTopRightY = null,
    double? ReportSampleBottomRightX = null,
    double? ReportSampleBottomRightY = null,
    double? ReportSampleBottomLeftX = null,
    double? ReportSampleBottomLeftY = null)
{
    public double Output => OutputGreen;

    public double Noise => NoiseGreen;

    public double SampleTopLeftX => ReportSampleTopLeftX ?? SampleX;

    public double SampleTopLeftY => ReportSampleTopLeftY ?? SampleY;

    public double SampleTopRightX => ReportSampleTopRightX ?? SampleX + SampleSize;

    public double SampleTopRightY => ReportSampleTopRightY ?? SampleY;

    public double SampleBottomRightX => ReportSampleBottomRightX ?? SampleX + SampleSize;

    public double SampleBottomRightY => ReportSampleBottomRightY ?? SampleY + SampleSize;

    public double SampleBottomLeftX => ReportSampleBottomLeftX ?? SampleX;

    public double SampleBottomLeftY => ReportSampleBottomLeftY ?? SampleY + SampleSize;
}
