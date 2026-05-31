using System.Globalization;
using System.Text;

namespace Imcheck.Measurement.Measurements.Qa62;

public sealed record Qa62MeasurementResult(
    string ImagePath,
    double SamplingPixelsPerInch,
    IReadOnlyList<Qa62PatchMeasurement> Patches,
    Qa62SfrSummary SfrSummary,
    IReadOnlyList<Qa62SfrCurvePoint> SfrCurve)
{
    public string ImageName => Path.GetFileName(ImagePath);

    public string ToCsv()
    {
        var builder = new StringBuilder();
        builder.AppendLine("Section,Metric,Red,Green,Blue,Luminance");
        AppendValues(builder, "SFR Sampling efficiency", "Horizontal", SfrSummary.HorizontalSamplingEfficiency);
        AppendValues(builder, "SFR Sampling efficiency", "Vertical", SfrSummary.VerticalSamplingEfficiency);
        AppendValues(builder, "SFR10 cy/mm", "Horizontal", SfrSummary.Sfr10HorizontalCyclesPerMillimeter);
        AppendValues(builder, "SFR10 cy/mm", "Vertical", SfrSummary.Sfr10VerticalCyclesPerMillimeter);
        AppendValues(builder, "SFR50 cy/mm", "Horizontal", SfrSummary.Sfr50HorizontalCyclesPerMillimeter);
        AppendValues(builder, "SFR50 cy/mm", "Vertical", SfrSummary.Sfr50VerticalCyclesPerMillimeter);
        AppendValues(builder, "Misregistration pixels", "Horizontal", SfrSummary.HorizontalMisregistrationPixels);
        AppendValues(builder, "Misregistration pixels", "Vertical", SfrSummary.VerticalMisregistrationPixels);

        builder.AppendLine();
        builder.AppendLine("Step,MeanRed,MeanGreen,MeanBlue,NoiseRed,NoiseGreen,NoiseBlue,SampleTopLeftX,SampleTopLeftY,SampleTopRightX,SampleTopRightY,SampleBottomRightX,SampleBottomRightY,SampleBottomLeftX,SampleBottomLeftY");
        foreach (var patch in Patches)
        {
            builder.Append(patch.Step).Append(',')
                .Append(Format(patch.OutputRed)).Append(',')
                .Append(Format(patch.OutputGreen)).Append(',')
                .Append(Format(patch.OutputBlue)).Append(',')
                .Append(Format(patch.NoiseRed)).Append(',')
                .Append(Format(patch.NoiseGreen)).Append(',')
                .Append(Format(patch.NoiseBlue)).Append(',')
                .Append(patch.SampleTopLeftX).Append(',')
                .Append(patch.SampleTopLeftY).Append(',')
                .Append(patch.SampleTopRightX).Append(',')
                .Append(patch.SampleTopRightY).Append(',')
                .Append(patch.SampleBottomRightX).Append(',')
                .Append(patch.SampleBottomRightY).Append(',')
                .Append(patch.SampleBottomLeftX).Append(',')
                .Append(patch.SampleBottomLeftY).AppendLine();
        }

        builder.AppendLine();
        builder.AppendLine("Frequency cy/mm,SFR-H r,SFR-H g,SFR-H b,SFR-H lum,SFR-V r,SFR-V g,SFR-V b,SFR-V lum");
        foreach (var point in SfrCurve)
        {
            builder.Append(Format(point.FrequencyCyclesPerMillimeter)).Append(',')
                .Append(Format(point.HorizontalRed)).Append(',')
                .Append(Format(point.HorizontalGreen)).Append(',')
                .Append(Format(point.HorizontalBlue)).Append(',')
                .Append(Format(point.HorizontalLuminance)).Append(',')
                .Append(Format(point.VerticalRed)).Append(',')
                .Append(Format(point.VerticalGreen)).Append(',')
                .Append(Format(point.VerticalBlue)).Append(',')
                .Append(Format(point.VerticalLuminance)).AppendLine();
        }

        return builder.ToString();
    }

    public string ToImcheckText()
    {
        var builder = new StringBuilder();
        builder.AppendLine("  ");
        builder.AppendLine("Output from Imcheck Remaker");
        builder.AppendLine(" ");
        builder.AppendLine(DateTime.Now.ToString("dd-MMM-yyyy HH:mm:ss", CultureInfo.InvariantCulture));
        builder.Append("Image/data evaluated:\t").AppendLine(ImagePath);
        builder.AppendLine(" ");
        builder.Append("% OECF applied: ").AppendLine("no");
        builder.Append("Sampling(pix/inch) \t").Append(Format(SamplingPixelsPerInch, "0.0")).AppendLine("\t ");
        builder.AppendLine(" \t ");
        builder.AppendLine("SFR Sampling efficiency r,g,b,lum\t ");
        AppendImcheckValues(builder, "Horiz.", SfrSummary.HorizontalSamplingEfficiency, "0.0");
        AppendImcheckValues(builder, "Vert.", SfrSummary.VerticalSamplingEfficiency, "0.0");
        builder.AppendLine("Spatial frequency for 0.1, 0.5 SFR, (cy/mm)\t ");
        AppendImcheckValues(builder, "10 h: ", SfrSummary.Sfr10HorizontalCyclesPerMillimeter, "0.00");
        AppendImcheckValues(builder, "10 v: ", SfrSummary.Sfr10VerticalCyclesPerMillimeter, "0.00");
        AppendImcheckValues(builder, "50 h: ", SfrSummary.Sfr50HorizontalCyclesPerMillimeter, "0.00");
        AppendImcheckValues(builder, "50 v: ", SfrSummary.Sfr50VerticalCyclesPerMillimeter, "0.00");
        builder.AppendLine("Misregistration r,g,b  (pixels)\t ");
        AppendImcheckValues(builder, "  h: ", SfrSummary.HorizontalMisregistrationPixels, "0.00");
        AppendImcheckValues(builder, "  v: ", SfrSummary.VerticalMisregistrationPixels, "0.00");
        builder.AppendLine(" ");
        builder.AppendLine("step\tMean (RGB)");

        foreach (var patch in Patches)
        {
            builder.Append(patch.Step.ToString(CultureInfo.InvariantCulture).PadLeft(4))
                .Append("\t")
                .Append(Format(patch.OutputRed, "0.000").PadLeft(10))
                .Append("\t")
                .Append(Format(patch.OutputGreen, "0.0000").PadLeft(8))
                .Append("\t")
                .Append(Format(patch.OutputBlue, "0.0000").PadLeft(8))
                .AppendLine();
        }

        builder.AppendLine(" ");
        builder.AppendLine("Frequency cy/mm\tSFR-H r\tg\tb\tlum\tSFR-V r\tg\tb\tlum");
        foreach (var point in SfrCurve)
        {
            builder.Append(Format(point.FrequencyCyclesPerMillimeter, "0.000").PadLeft(8))
                .Append("\t")
                .Append(Format(point.HorizontalRed, "0.0000").PadLeft(8)).Append("\t")
                .Append(Format(point.HorizontalGreen, "0.0000").PadLeft(8)).Append("\t")
                .Append(Format(point.HorizontalBlue, "0.0000").PadLeft(8)).Append("\t")
                .Append(Format(point.HorizontalLuminance, "0.0000").PadLeft(8)).Append("\t")
                .Append(Format(point.VerticalRed, "0.0000").PadLeft(8)).Append("\t")
                .Append(Format(point.VerticalGreen, "0.0000").PadLeft(8)).Append("\t")
                .Append(Format(point.VerticalBlue, "0.0000").PadLeft(8)).Append("\t")
                .Append(Format(point.VerticalLuminance, "0.0000").PadLeft(8)).AppendLine(" ");
        }

        return builder.ToString();
    }

    public async Task SaveCsvAsync(string path, CancellationToken cancellationToken = default)
    {
        await File.WriteAllTextAsync(path, ToCsv(), cancellationToken).ConfigureAwait(false);
    }

    private static void AppendValues(StringBuilder builder, string section, string metric, Qa62ChannelValues values)
    {
        builder.Append(section).Append(',')
            .Append(metric).Append(',')
            .Append(Format(values.Red)).Append(',')
            .Append(Format(values.Green)).Append(',')
            .Append(Format(values.Blue)).Append(',')
            .Append(Format(values.Luminance)).AppendLine();
    }

    private static void AppendImcheckValues(StringBuilder builder, string label, Qa62ChannelValues values, string format)
    {
        builder.Append(label).Append("\t")
            .Append(Format(values.Red, format).PadLeft(7)).Append("\t")
            .Append(Format(values.Green, format).PadLeft(7)).Append("\t")
            .Append(Format(values.Blue, format).PadLeft(7)).Append("\t")
            .Append(Format(values.Luminance, format).PadLeft(7)).AppendLine("\t ");
    }

    private static string Format(double value, string format = "0.####")
    {
        return double.IsFinite(value)
            ? value.ToString(format, CultureInfo.InvariantCulture)
            : "";
    }
}
