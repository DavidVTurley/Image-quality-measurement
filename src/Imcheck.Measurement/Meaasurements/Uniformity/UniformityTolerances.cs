namespace Imcheck.Measurement.Meaasurements.Uniformity;

public static class UniformityTolerances
{
    public static double? IlluminationDeltaL(UniformityQualityLevel qualityLevel, UniformityImagePlaneSize imagePlaneSize)
    {
        if (qualityLevel == UniformityQualityLevel.ExtraLight)
        {
            return imagePlaneSize == UniformityImagePlaneSize.UpToA3 ? 5.0 : null;
        }

        return imagePlaneSize switch
        {
            UniformityImagePlaneSize.UpToA3 => 3.0,
            UniformityImagePlaneSize.UpToA2 => 4.0,
            UniformityImagePlaneSize.UpToA1 => 5.0,
            UniformityImagePlaneSize.UpToA0 => 6.0,
            _ => throw new ArgumentOutOfRangeException(nameof(imagePlaneSize), imagePlaneSize, null)
        };
    }

    public static double WhiteBalanceDeltaEab(UniformityQualityLevel qualityLevel)
    {
        return qualityLevel == UniformityQualityLevel.ExtraLight ? 5.0 : 3.0;
    }
}
