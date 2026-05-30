namespace Imcheck.Measurement;

public sealed record Qa62SfrCurvePoint(
    double FrequencyCyclesPerMillimeter,
    double HorizontalRed,
    double HorizontalGreen,
    double HorizontalBlue,
    double HorizontalLuminance,
    double VerticalRed,
    double VerticalGreen,
    double VerticalBlue,
    double VerticalLuminance);
