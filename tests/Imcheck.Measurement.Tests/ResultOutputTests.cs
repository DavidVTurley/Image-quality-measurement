using Imcheck.Measurement.Measurements.Q13;
using Imcheck.Measurement.Measurements.Qa62;
using Imcheck.Measurement.Measurements.Uniformity;
using System.Text;

namespace Imcheck.Measurement.Tests;

public sealed class ResultOutputTests
{
    [Fact]
    public void Q13CsvKeepsGeometryInFileAndWritesColorChannelsAsRgb()
    {
        var result = new Q13MeasurementResult(
            "q13.tif",
            300,
            5,
            1,
            1,
            1,
            [
                new PatchMeasurement(
                    0,
                    101,
                    102,
                    103,
                    1.1,
                    1.2,
                    1.3,
                    IsColor: true,
                    12.5,
                    24.5,
                    10,
                    20,
                    5)
            ]);

        var csv = result.ToCsv();

        Assert.StartsWith("Image,q13.tif", csv);
        Assert.Contains("Sampling,300.0,11.8", csv);
        Assert.Contains("Sample N,25", csv);
        Assert.Contains("1/gamma r,g,b,1.00,1.00,1.00", csv);
        Assert.Contains("Patch count,1", csv);
        Assert.DoesNotContain("InputRed", csv);
        Assert.DoesNotContain("InputGreen", csv);
        Assert.DoesNotContain("InputBlue", csv);
        Assert.Contains("Patch,OutputRed,OutputGreen,OutputBlue,NoiseRed,NoiseGreen,NoiseBlue,SampleCenterX,SampleCenterY,SampleWidth,SampleHeight", csv);
        Assert.DoesNotContain("SampleTopLeftX", csv);
        Assert.DoesNotContain("SampleX", csv);
        Assert.DoesNotContain("SampleSize", csv);
        Assert.Contains("0,101,102,103,1.1,1.2,1.3,12,24,5,5", csv);
    }

    [Fact]
    public void Q13ResultCsvCanLoadSampleCentersFromExportedGeometry()
    {
        var path = Path.Combine(Path.GetTempPath(), $"q13-result-{Guid.NewGuid():N}.csv");
        var builder = new StringBuilder();
        builder.AppendLine("Image,q13.tif");
        builder.AppendLine("Sampling,300.0,11.8");
        builder.AppendLine("Sample N,25");
        builder.AppendLine("1/gamma,1.00");
        builder.AppendLine("Patch count,20");
        builder.AppendLine();
        builder.AppendLine("Patch,Output,Noise,SampleCenterX,SampleCenterY,SampleWidth,SampleHeight");
        for (var patchIndex = 0; patchIndex < 20; patchIndex++)
        {
            builder.Append(patchIndex)
                .Append(",100,1,")
                .Append(patchIndex * 10 + 2).Append(",22,5,5")
                .AppendLine();
        }

        File.WriteAllText(path, builder.ToString());
        try
        {
            var points = Q13ResultSampleCsv.LoadSampleCenters(path);

            Assert.Equal(20, points.Count);
            Assert.Equal(0, points[0].PatchIndex);
            Assert.Equal(2, points[0].X);
            Assert.Equal(22, points[0].Y);
            Assert.Equal(192, points[19].X);
            Assert.Equal(22, points[19].Y);
            var imported = Q13ResultSampleCsv.Load(path);
            Assert.Equal(5, imported.SampleSize);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Qa62CsvKeepsGeometryInFileAndWritesColorChannelsAsRgb()
    {
        var zero = new Qa62ChannelValues(0, 0, 0, 0);
        var result = new Qa62MeasurementResult(
            "qa62.tif",
            300,
            [
                new Qa62PatchMeasurement(
                    1,
                    101,
                    102,
                    103,
                    1.1,
                    1.2,
                    1.3,
                    12.5,
                    24.5,
                    10,
                    20,
                    5)
            ],
            new Qa62SfrSummary(zero, zero, zero, zero, zero, zero, zero, zero),
            []);

        var csv = result.ToCsv();

        Assert.Contains("Step,MeanRed,MeanGreen,MeanBlue,NoiseRed,NoiseGreen,NoiseBlue,SampleTopLeftX,SampleTopLeftY", csv);
        Assert.DoesNotContain("SampleCenterX", csv);
        Assert.DoesNotContain("SampleX", csv);
        Assert.DoesNotContain("SampleSize", csv);
        Assert.Contains("SampleTopLeftX,SampleTopLeftY,SampleTopRightX,SampleTopRightY,SampleBottomRightX,SampleBottomRightY,SampleBottomLeftX,SampleBottomLeftY", csv);
        Assert.Contains("1,101,102,103,1.1,1.2,1.3,10,20,15,20,15,25,10,25", csv);
    }

    [Fact]
    public void UniformityResultCsvCanLoadSamplesFromExportedGeometry()
    {
        var path = Path.Combine(Path.GetTempPath(), $"uniformity-result-{Guid.NewGuid():N}.csv");
        var csv = """
Image,1200x800,8-bit
Sample size,35x35
Max delta L*,0,Not specified,Not specified
Max delta Eab,0,2,Pass

Name,MeanRed,MeanGreen,MeanBlue,LStar,AStar,BStar,SampleCenterX,SampleCenterY,SampleWidth,SampleHeight
Center,100,100,100,50,0,0,28,38,35,35
""";

        File.WriteAllText(path, csv);
        try
        {
            var samples = UniformityResultSampleCsv.LoadSamples(path);

            Assert.Single(samples);
            Assert.Equal("Center", samples[0].Name);
            Assert.Equal(28, samples[0].CenterX);
            Assert.Equal(38, samples[0].CenterY);
            Assert.Equal(35, samples[0].SampleSize);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void UniformityCsvStartsWithMeasurementSummary()
    {
        var result = new UniformityAnalysisResult(
            "uniformity.tif",
            1200,
            800,
            8,
            RgbColorSpace.SRgb,
            UniformityQualityLevel.Full,
            UniformityImagePlaneSize.UpToA3,
            35,
            [
                new WhiteSheetSampleMeasurement(
                    "Center",
                    27.5,
                    37.5,
                    10,
                    20,
                    35,
                    100,
                    101,
                    102,
                    50,
                    0.1,
                    0.2)
            ],
            1.2345,
            2.3456,
            3.0,
            4.0);

        var csv = result.ToCsv();

        Assert.StartsWith("Image,1200x800,8-bit", csv);
        Assert.Contains("Sample size,35x35", csv);
        Assert.Contains("Max delta L*,1.2345,3,Pass", csv);
        Assert.Contains("Max delta Eab,2.3456,4,Pass", csv);
        Assert.Contains("Name,MeanRed,MeanGreen,MeanBlue,LStar,AStar,BStar,SampleCenterX,SampleCenterY,SampleWidth,SampleHeight", csv);
        Assert.DoesNotContain("SampleTopLeftX", csv);
        Assert.Contains("Center,100,101,102,50,0.1,0.2,28,38,35,35", csv);
    }
}
