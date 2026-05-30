using System.Globalization;
using System.Text;

namespace Imcheck.Measurement.Meaasurements.Q13;

public sealed record Q13MeasurementResult(
    string ImagePath,
    double SamplingPixelsPerInch,
    int SampleSize,
    double InverseGammaRed,
    double InverseGammaGreen,
    double InverseGammaBlue,
    IReadOnlyList<PatchMeasurement> Patches)
{
    public double InverseGamma => InverseGammaBlue;

    public int SampleDataSize => SampleSize * SampleSize;

    public bool IsColor => Patches.Any(p => p.IsColor);

    public string ImageName => Path.GetFileName(ImagePath);

    public string ToCsv()
    {
        var builder = new StringBuilder();

        if (IsColor)
        {
            builder.AppendLine("Patch,SampleCenterX,SampleCenterY,SampleX,SampleY,SampleSize,InputRed,InputGreen,InputBlue,OutputRed,OutputGreen,OutputBlue,NoiseRed,NoiseGreen,NoiseBlue");
            foreach (var patch in Patches)
            {
                builder.Append(patch.Index).Append(',')
                    .Append(Format(patch.SampleCenterX)).Append(',')
                    .Append(Format(patch.SampleCenterY)).Append(',')
                    .Append(patch.SampleX).Append(',')
                    .Append(patch.SampleY).Append(',')
                    .Append(patch.SampleSize).Append(',')
                    .Append(Format(patch.InputRed)).Append(',')
                    .Append(Format(patch.InputGreen)).Append(',')
                    .Append(Format(patch.InputBlue)).Append(',')
                    .Append(Format(patch.OutputRed)).Append(',')
                    .Append(Format(patch.OutputGreen)).Append(',')
                    .Append(Format(patch.OutputBlue)).Append(',')
                    .Append(Format(patch.NoiseRed)).Append(',')
                    .Append(Format(patch.NoiseGreen)).Append(',')
                    .Append(Format(patch.NoiseBlue)).AppendLine();
            }

            return builder.ToString();
        }

        builder.AppendLine("Patch,SampleCenterX,SampleCenterY,SampleX,SampleY,SampleSize,InputRed,InputGreen,InputBlue,Output,Noise");
        foreach (var patch in Patches)
        {
            builder.Append(patch.Index).Append(',')
                .Append(Format(patch.SampleCenterX)).Append(',')
                .Append(Format(patch.SampleCenterY)).Append(',')
                .Append(patch.SampleX).Append(',')
                .Append(patch.SampleY).Append(',')
                .Append(patch.SampleSize).Append(',')
                .Append(Format(patch.InputRed)).Append(',')
                .Append(Format(patch.InputGreen)).Append(',')
                .Append(Format(patch.InputBlue)).Append(',')
                .Append(Format(patch.Output)).Append(',')
                .Append(Format(patch.Noise)).AppendLine();
        }

        return builder.ToString();
    }

    public string ToImcheckText(string title = "Q13 test", string operatorName = "Q13test")
    {
        var builder = new StringBuilder();
        builder.AppendLine(title).AppendLine(" ");
        builder.AppendLine(ImageName).AppendLine(operatorName).AppendLine("  ");
        builder.Append("Sampling: ")
            .Append(Format(SamplingPixelsPerInch, "0.0"))
            .Append("  pix/inch, (")
            .Append(Format(SamplingPixelsPerInch / 25.4, "0.0"))
            .AppendLine(" pix/mm) ");
        builder.Append("Step data size, N = ").AppendLine(SampleDataSize.ToString(CultureInfo.InvariantCulture));

        if (IsColor)
        {
            builder.Append("1/gamma r, g, b:  ")
                .Append(Format(InverseGammaRed, "0.00"))
                .Append("   ")
                .Append(Format(InverseGammaGreen, "0.00"))
                .Append("   ")
                .AppendLine(Format(InverseGammaBlue, "0.00"));
        }
        else
        {
            builder.Append("1/gamma:  ").AppendLine(Format(InverseGamma, "0.00"));
        }

        builder.AppendLine("Input, output, RMS noise ");

        foreach (var patch in Patches)
        {
            builder.Append(Format(patch.InputRed)).Append('\t')
                .Append(Format(patch.InputGreen)).Append('\t')
                .Append(Format(patch.InputBlue)).Append('\t');

            if (IsColor)
            {
                builder.Append(Format(patch.OutputRed)).Append('\t')
                    .Append(Format(patch.OutputGreen)).Append('\t')
                    .Append(Format(patch.OutputBlue)).Append('\t')
                    .Append(Format(patch.NoiseRed)).Append('\t')
                    .Append(Format(patch.NoiseGreen)).Append('\t')
                    .Append(Format(patch.NoiseBlue)).AppendLine();
            }
            else
            {
                builder.Append(Format(patch.Output)).Append('\t')
                    .Append(Format(patch.Noise)).AppendLine();
            }
        }

        return builder.ToString();
    }

    public async Task SaveCsvAsync(string path, CancellationToken cancellationToken = default)
    {
        await File.WriteAllTextAsync(path, ToCsv(), cancellationToken).ConfigureAwait(false);
    }

    private static string Format(double value, string format = "0.####")
    {
        return value.ToString(format, CultureInfo.InvariantCulture);
    }
}
