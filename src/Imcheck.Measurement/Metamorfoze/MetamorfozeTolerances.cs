namespace Imcheck.Measurement.Metamorfoze;

public static class MetamorfozeTolerances
{
    public static double? IlluminationDeltaL(MetamorfozeQualityLevel qualityLevel, MetamorfozeImagePlaneSize imagePlaneSize)
    {
        if (qualityLevel == MetamorfozeQualityLevel.ExtraLight)
        {
            return imagePlaneSize == MetamorfozeImagePlaneSize.UpToA3 ? 5.0 : null;
        }

        return imagePlaneSize switch
        {
            MetamorfozeImagePlaneSize.UpToA3 => 3.0,
            MetamorfozeImagePlaneSize.UpToA2 => 4.0,
            MetamorfozeImagePlaneSize.UpToA1 => 5.0,
            MetamorfozeImagePlaneSize.UpToA0 => 6.0,
            _ => throw new ArgumentOutOfRangeException(nameof(imagePlaneSize), imagePlaneSize, null)
        };
    }

    public static double WhiteBalanceDeltaEab(MetamorfozeQualityLevel qualityLevel)
    {
        return qualityLevel == MetamorfozeQualityLevel.ExtraLight ? 5.0 : 3.0;
    }
}
