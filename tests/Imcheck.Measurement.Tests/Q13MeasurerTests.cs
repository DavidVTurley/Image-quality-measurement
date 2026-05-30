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
        Assert.Contains("Patch,InputRed,InputGreen,InputBlue,Output,Noise", result.ToCsv());
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

    private static string ExampleImage(string fileName)
    {
        var path = Path.Combine(ExampleImagesDirectory, fileName);
        Assert.True(File.Exists(path), $"Expected reference image was not found: {path}");
        return path;
    }
}
