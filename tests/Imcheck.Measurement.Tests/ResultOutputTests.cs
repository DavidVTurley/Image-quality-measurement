using Imcheck.Measurement.Measurements.Q13;
using Imcheck.Measurement.Measurements.Qa62;

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

        Assert.DoesNotContain("InputRed", csv);
        Assert.DoesNotContain("InputGreen", csv);
        Assert.DoesNotContain("InputBlue", csv);
        Assert.Contains("Patch,OutputRed,OutputGreen,OutputBlue,NoiseRed,NoiseGreen,NoiseBlue,SampleTopLeftX,SampleTopLeftY", csv);
        Assert.DoesNotContain("SampleCenterX", csv);
        Assert.DoesNotContain("SampleX", csv);
        Assert.DoesNotContain("SampleSize", csv);
        Assert.Contains("SampleTopLeftX,SampleTopLeftY,SampleTopRightX,SampleTopRightY,SampleBottomRightX,SampleBottomRightY,SampleBottomLeftX,SampleBottomLeftY", csv);
        Assert.Contains("0,101,102,103,1.1,1.2,1.3,10,20,15,20,15,25,10,25", csv);
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
}
