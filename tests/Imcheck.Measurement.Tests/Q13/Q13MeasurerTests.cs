using Imcheck.Measurement.Measurements.Q13;
using Imcheck.Measurement;

namespace Imcheck.Measurement.Tests;

public sealed class Q13MeasurerTests
{
    [Fact]
    public void GeneratedQ13TiffMatchesGeneratedPatchValues()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.tif");
        try
        {
            var generated = GenerateQ13(path);
            var centers = Q13Centers(generated.Width, generated.Height);

            var result = new Q13Measurer().Measure(path, new Q13MeasurementOptions { SampleCenters = centers, SampleSize = 9 });

            Assert.Equal(20, result.Patches.Count);
            Assert.Equal(81, result.SampleDataSize);
            Assert.All(result.Patches.Zip(Q13GrayscaleTargetGenerator.Patches), pair =>
            {
                Assert.Equal(pair.Second.EncodedRgb, pair.First.Output);
                Assert.Equal(0, pair.First.Noise);
            });
            Assert.True(double.IsFinite(result.InverseGamma));
            Assert.Contains($"{Q13GrayscaleTargetGenerator.Patches[0].EncodedRgb}\t0", result.ToImcheckText());
            Assert.Contains("Patch,Output,Noise,SampleCenterX,SampleCenterY,SampleWidth,SampleHeight", result.ToCsv());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void GeneratedQ13PngProducesTwentyColorRows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        try
        {
            var generated = GenerateQ13(path);
            var centers = Q13Centers(generated.Width, generated.Height);

            var result = new Q13Measurer().Measure(path, new Q13MeasurementOptions { SampleCenters = centers, SampleSize = 9 });

            Assert.Equal(20, result.Patches.Count);
            Assert.True(result.IsColor);
            Assert.All(result.Patches.Zip(Q13GrayscaleTargetGenerator.Patches), pair =>
            {
                Assert.Equal(pair.Second.EncodedRgb, pair.First.OutputRed);
                Assert.Equal(pair.Second.EncodedRgb, pair.First.OutputGreen);
                Assert.Equal(pair.Second.EncodedRgb, pair.First.OutputBlue);
                Assert.Equal(0, pair.First.NoiseRed);
                Assert.Equal(0, pair.First.NoiseGreen);
                Assert.Equal(0, pair.First.NoiseBlue);
            });
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void ExplicitSampleCentersCanReproduceStraightLineMeasurement()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        try
        {
            var generated = GenerateQ13(path);
            var centers = Q13Centers(generated.Width, generated.Height);

            var result = new Q13Measurer().Measure(path, new Q13MeasurementOptions { SampleCenters = centers, SampleSize = 9 });

            Assert.Equal(20, result.Patches.Count);
            Assert.Equal(Q13GrayscaleTargetGenerator.Patches[0].EncodedRgb, result.Patches[0].Output);
            Assert.Equal(Q13GrayscaleTargetGenerator.Patches[19].EncodedRgb, result.Patches[19].Output);
            Assert.Equal(centers[0].X, result.Patches[0].SampleCenterX);
            Assert.Equal(centers[0].Y, result.Patches[0].SampleCenterY);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void OutlierRejectionRemovesSingleExtremeSamplePixel()
    {
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.png");
        try
        {
            var generated = GenerateQ13(path);
            var centers = Q13Centers(generated.Width, generated.Height);
            using (var image = OpenCvSharp.Cv2.ImRead(path, OpenCvSharp.ImreadModes.Unchanged))
            {
                var center = centers[0];
                image.Set((int)Math.Round(center.Y), (int)Math.Round(center.X), new OpenCvSharp.Vec3b(0, 0, 0));
                OpenCvSharp.Cv2.ImWrite(path, image);
            }

            var regular = new Q13Measurer().Measure(path, new Q13MeasurementOptions { SampleCenters = centers, SampleSize = 9 });
            var rejected = new Q13Measurer().Measure(path, new Q13MeasurementOptions
            {
                SampleCenters = centers,
                SampleSize = 9,
                UseOutlierRejection = true,
                OutlierSigmaThreshold = 2.0
            });

            Assert.True(regular.Patches[0].Noise > 0);
            Assert.Equal(Q13GrayscaleTargetGenerator.Patches[0].EncodedRgb, rejected.Patches[0].Output);
            Assert.Equal(0, rejected.Patches[0].Noise);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
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

    private static Q13GrayscaleTargetGeneratorResult GenerateQ13(string path)
    {
        return new Q13GrayscaleTargetGenerator().Generate(
            path,
            new Q13GrayscaleTargetGeneratorOptions
            {
                ShowLabels = false,
                ShowMillimeterScale = false,
                ShowTitle = false
            });
    }

    private static IReadOnlyList<Q13SamplePoint> Q13Centers(int width, int height)
    {
        return Enumerable.Range(0, Q13GrayscaleTargetGenerator.Patches.Count)
            .Select(index =>
            {
                var layout = Q13GrayscaleTargetGenerator.GetPatchLayout(index, width, height);
                return new Q13SamplePoint(index, layout.CenterX, layout.CenterY);
            })
            .ToArray();
    }
}
