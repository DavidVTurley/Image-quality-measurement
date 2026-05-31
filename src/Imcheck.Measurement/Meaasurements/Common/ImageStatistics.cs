using OpenCvSharp;

namespace Imcheck.Measurement.Meaasurements.Common;

internal static class ImageStatistics
{
    public static (double Mean, double StdDev) MeanAndPopulationStdDev(Mat mat)
    {
        Cv2.MeanStdDev(mat, out var mean, out var stdDev);
        return (mean.Val0, stdDev.Val0);
    }
}
