namespace Imcheck.Measurement.Measurements.Qa62;

public sealed record Qa62SfrSummary(
    Qa62ChannelValues HorizontalSamplingEfficiency,
    Qa62ChannelValues VerticalSamplingEfficiency,
    Qa62ChannelValues Sfr10HorizontalCyclesPerMillimeter,
    Qa62ChannelValues Sfr10VerticalCyclesPerMillimeter,
    Qa62ChannelValues Sfr50HorizontalCyclesPerMillimeter,
    Qa62ChannelValues Sfr50VerticalCyclesPerMillimeter,
    Qa62ChannelValues HorizontalMisregistrationPixels,
    Qa62ChannelValues VerticalMisregistrationPixels);

public sealed record Qa62ChannelValues(double Red, double Green, double Blue, double Luminance);
