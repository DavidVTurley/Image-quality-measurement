using Imcheck.Measurement.Measurements.Q13;

namespace Imcheck.Measurement.Tests;

public sealed class Q13MeasurerTests
{
    private static readonly string ExampleImagesDirectory =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "imcheck4v2(2024)_dist",
            "Example_images");

    [Fact]
    public void KodakReferenceTiffMatchesImcheckOutput()
    {
        var path = ExampleImage("kodak_q13_eciRGBv2_300dpi.tif");
        var expected = new[] { 244, 223, 203, 185, 169, 153, 139, 126, 113, 102, 91, 82, 73, 64, 56, 49, 43, 36, 31, 25 };

        var result = new Q13Measurer().Measure(path);

        Assert.Equal(20, result.Patches.Count);
        Assert.Equal(1521, result.SampleDataSize);
        Assert.Equal(2.47, result.InverseGamma, precision: 2);
        Assert.All(result.Patches.Zip(expected), pair =>
        {
            Assert.Equal(pair.Second, pair.First.Output);
            Assert.Equal(0, pair.First.Noise);
        });
        Assert.Contains("244\t0", result.ToImcheckText());
        Assert.Contains("Patch,Output,Noise,SampleCenterX,SampleCenterY,SampleWidth,SampleHeight", result.ToCsv());
    }

    [Fact]
    public void LightnessRampReferenceTiffMatchesImcheckOutput()
    {
        var path = ExampleImage("grayscale_eciRGBv2_L5-95_300dpi.tif");
        var expected = new[] { 13, 26, 38, 51, 64, 77, 89, 102, 115, 128, 140, 153, 166, 179, 191, 204, 217, 230, 242, 255 };

        var result = new Q13Measurer().Measure(path);

        Assert.Equal(20, result.Patches.Count);
        Assert.All(result.Patches.Zip(expected), pair =>
        {
            Assert.Equal(pair.Second, pair.First.Output);
            Assert.Equal(0, pair.First.Noise);
        });
    }

    [Fact]
    public void RgbJpegProducesTwentyColorRows()
    {
        var path = ExampleImage("Q-13-1.jpg");

        var result = new Q13Measurer().Measure(path, new Q13MeasurementOptions { SampleSize = 9 });

        Assert.Equal(20, result.Patches.Count);
        Assert.True(result.IsColor);
        Assert.All(result.Patches, patch =>
        {
            Assert.InRange(patch.OutputRed, 0, 255);
            Assert.InRange(patch.OutputGreen, 0, 255);
            Assert.InRange(patch.OutputBlue, 0, 255);
            Assert.True(patch.NoiseRed >= 0);
            Assert.True(patch.NoiseGreen >= 0);
            Assert.True(patch.NoiseBlue >= 0);
        });
    }

    [Fact]
    public void ExplicitSampleCentersCanReproduceStraightLineMeasurement()
    {
        var path = ExampleImage("kodak_q13_eciRGBv2_300dpi.tif");
        var centers = Q13Measurer.CreateStraightLineSampleCenters(width: 2398, height: 354);

        var result = new Q13Measurer().Measure(path, new Q13MeasurementOptions { SampleCenters = centers });

        Assert.Equal(20, result.Patches.Count);
        Assert.Equal(244, result.Patches[0].Output);
        Assert.Equal(25, result.Patches[19].Output);
        Assert.Equal(centers[0].X, result.Patches[0].SampleCenterX);
        Assert.Equal(centers[0].Y, result.Patches[0].SampleCenterY);
    }

    [Fact]
    public void ResultCsvLoadsExplicitPatchCoordinates()
    {
        var path = Path.GetTempFileName();
        try
        {
            var lines = new[]
            {
                "Image,q13.tif",
                "Sampling,300.0,11.8",
                "Sample N,25",
                "1/gamma,1.00",
                "Patch count,20",
                "",
                "Patch,Output,Noise,SampleCenterX,SampleCenterY,SampleWidth,SampleHeight"
            }
                .Concat(Enumerable.Range(0, 20).Select(i => FormattableString.Invariant($"{i},100,1,{i + 1},102,5,5")));
            File.WriteAllLines(path, lines);

            var points = Q13ResultSampleCsv.LoadSampleCenters(path);

            Assert.Equal(20, points.Count);
            Assert.Equal(0, points[0].PatchIndex);
            Assert.Equal(1, points[0].X);
            Assert.Equal(102, points[0].Y);
            Assert.Equal(19, points[19].PatchIndex);
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static string ExampleImage(string fileName)
    {
        var path = Path.Combine(ExampleImagesDirectory, fileName);
        Assert.True(File.Exists(path), $"Expected reference image was not found: {path}");
        return path;
    }
}
