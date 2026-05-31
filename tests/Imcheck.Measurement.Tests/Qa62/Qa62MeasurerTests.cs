using System.Globalization;
using System.Text.RegularExpressions;
using Imcheck.Measurement.Meaasurements.Qa62;

namespace Imcheck.Measurement.Tests;

public sealed class Qa62MeasurerTests
{
    private static readonly string ExampleImagesDirectory =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "imcheck4v2(2024)_dist",
            "Example_images");

    [Fact]
    public void Qa62ReferenceJpegMeasuresTwentyGrayPatches()
    {
        var imagePath = ExampleImage("QA-62.jpg");
        var reference = LoadReferenceMeans(ExampleImage("QA-62.xls"));

        var result = new Qa62Measurer().Measure(imagePath);

        Assert.Equal(20, result.Patches.Count);
        Assert.Equal(301.1, result.SamplingPixelsPerInch, precision: 1);
        Assert.All(result.Patches.Zip(reference), pair =>
        {
            Assert.InRange(pair.First.OutputRed, pair.Second.Red - 2.0, pair.Second.Red + 2.0);
            Assert.InRange(pair.First.OutputGreen, pair.Second.Green - 2.0, pair.Second.Green + 2.0);
            Assert.InRange(pair.First.OutputBlue, pair.Second.Blue - 2.0, pair.Second.Blue + 2.0);
        });
    }

    [Fact]
    public void Qa62ReferenceJpegProducesSfrSummaryAndCurve()
    {
        var imagePath = ExampleImage("QA-62.jpg");
        var reference = LoadReferenceSummary(ExampleImage("QA-62.xls"));

        var result = new Qa62Measurer().Measure(imagePath);

        Assert.Equal(60, result.SfrCurve.Count);
        Assert.Equal(1.0, result.SfrCurve[0].HorizontalLuminance, precision: 3);
        Assert.Equal(1.0, result.SfrCurve[0].VerticalLuminance, precision: 3);
        Assert.InRange(result.SfrSummary.Sfr10HorizontalCyclesPerMillimeter.Luminance, reference.Sfr10HorizontalLuminance - 0.1, reference.Sfr10HorizontalLuminance + 0.1);
        Assert.InRange(result.SfrSummary.Sfr10VerticalCyclesPerMillimeter.Luminance, reference.Sfr10VerticalLuminance - 0.01, reference.Sfr10VerticalLuminance + 0.01);
        Assert.InRange(result.SfrSummary.Sfr50HorizontalCyclesPerMillimeter.Luminance, reference.Sfr50HorizontalLuminance - 0.1, reference.Sfr50HorizontalLuminance + 0.1);
        Assert.InRange(result.SfrSummary.Sfr50VerticalCyclesPerMillimeter.Luminance, reference.Sfr50VerticalLuminance - 0.1, reference.Sfr50VerticalLuminance + 0.1);
        Assert.Contains("SFR Sampling efficiency r,g,b,lum", result.ToImcheckText());
        Assert.Contains("Frequency cy/mm\tSFR-H r\tg\tb\tlum\tSFR-V r\tg\tb\tlum", result.ToImcheckText());
    }

    private static IReadOnlyList<(double Red, double Green, double Blue)> LoadReferenceMeans(string path)
    {
        var values = new List<(double Red, double Green, double Blue)>();
        var inMeanSection = false;
        var rowRegex = new Regex(@"^\s*(\d+)\s+([0-9.]+)\s+([0-9.]+)\s+([0-9.]+)");

        foreach (var line in File.ReadLines(path))
        {
            if (line.StartsWith("step", StringComparison.OrdinalIgnoreCase))
            {
                inMeanSection = true;
                continue;
            }

            if (inMeanSection && line.StartsWith("Frequency", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (!inMeanSection)
            {
                continue;
            }

            var match = rowRegex.Match(line);
            if (!match.Success)
            {
                continue;
            }

            values.Add((
                Parse(match.Groups[2].Value),
                Parse(match.Groups[3].Value),
                Parse(match.Groups[4].Value)));
        }

        Assert.Equal(20, values.Count);
        return values;
    }

    private static ReferenceSummary LoadReferenceSummary(string path)
    {
        var lines = File.ReadAllLines(path);
        return new ReferenceSummary(
            LastValue(lines, "10 h:"),
            LastValue(lines, "10 v:"),
            LastValue(lines, "50 h:"),
            LastValue(lines, "50 v:"));
    }

    private static double LastValue(IReadOnlyList<string> lines, string prefix)
    {
        var regex = new Regex(@"[-+]?\d+(?:\.\d+)?");
        var line = lines.First(line => line.StartsWith(prefix, StringComparison.Ordinal));
        return Parse(regex.Matches(line).Last().Value);
    }

    private static double Parse(string value)
    {
        return double.Parse(value, CultureInfo.InvariantCulture);
    }

    private static string ExampleImage(string fileName)
    {
        var path = Path.Combine(ExampleImagesDirectory, fileName);
        Assert.True(File.Exists(path), $"Expected reference file was not found: {path}");
        return path;
    }

    private sealed record ReferenceSummary(
        double Sfr10HorizontalLuminance,
        double Sfr10VerticalLuminance,
        double Sfr50HorizontalLuminance,
        double Sfr50VerticalLuminance);
}
