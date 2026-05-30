using OpenCvSharp;

namespace Imcheck.Measurement.Tests;

public sealed class Qa62TargetGeneratorTests
{
    private static readonly (int X, int Y, Vec3b Bgr)[] ExpectedPatchSamples =
    [
        (333, 375, new Vec3b(223, 224, 222)),
        (556, 375, new Vec3b(203, 205, 205)),
        (779, 375, new Vec3b(195, 197, 197)),
        (1002, 375, new Vec3b(177, 181, 181)),
        (1224, 375, new Vec3b(158, 163, 163)),
        (1447, 375, new Vec3b(143, 147, 148)),
        (1447, 591, new Vec3b(133, 137, 138)),
        (1447, 807, new Vec3b(128, 131, 132)),
        (1447, 1024, new Vec3b(119, 123, 124)),
        (1447, 1240, new Vec3b(113, 116, 117)),
        (1447, 1387, new Vec3b(101, 105, 105)),
        (1224, 1387, new Vec3b(92, 95, 96)),
        (1002, 1387, new Vec3b(87, 90, 90)),
        (779, 1387, new Vec3b(83, 86, 86)),
        (556, 1387, new Vec3b(79, 81, 81)),
        (333, 1387, new Vec3b(77, 79, 79)),
        (333, 1240, new Vec3b(71, 73, 73)),
        (333, 1024, new Vec3b(65, 66, 66)),
        (333, 807, new Vec3b(56, 56, 56)),
        (333, 591, new Vec3b(54, 54, 54)),
    ];

    [Fact]
    public void GenerateCreatesExpectedSixHundredDpiPng()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        try
        {
            var result = new Qa62TargetGenerator().Generate(path);

            Assert.Equal(path, result.OutputPath);
            Assert.Equal(1800, result.Width);
            Assert.Equal(2250, result.Height);
            Assert.Equal(600, result.Dpi);

            using var image = Cv2.ImRead(path, ImreadModes.Color);
            Assert.False(image.Empty());
            Assert.Equal(1800, image.Width);
            Assert.Equal(2250, image.Height);
            Assert.Equal(MatType.CV_8UC3, image.Type());

            foreach (var (x, y, expected) in ExpectedPatchSamples)
            {
                Assert.Equal(expected, image.At<Vec3b>(y, x));
            }
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
