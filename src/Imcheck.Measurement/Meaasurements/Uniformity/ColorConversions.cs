namespace Imcheck.Measurement.Meaasurements.Uniformity;

internal static class ColorConversions
{
    private static readonly RgbColorSpaceDefinition SRgb = new(
        RgbCompanding.SRgb,
        MatrixFromPrimaries((0.64, 0.33), (0.30, 0.60), (0.15, 0.06), (0.3127, 0.3290)),
        WhitePointFromXy(0.3127, 0.3290));

    private static readonly RgbColorSpaceDefinition AdobeRgb1998 = new(
        RgbCompanding.Gamma22,
        MatrixFromPrimaries((0.64, 0.33), (0.21, 0.71), (0.15, 0.06), (0.3127, 0.3290)),
        WhitePointFromXy(0.3127, 0.3290));

    private static readonly RgbColorSpaceDefinition EciRgbV2 = new(
        RgbCompanding.LStar,
        MatrixFromPrimaries((0.67, 0.33), (0.21, 0.71), (0.14, 0.08), (0.3457, 0.3585)),
        WhitePointFromXy(0.3457, 0.3585));

    public static (double L, double A, double B) ToLab(double red, double green, double blue, RgbColorSpace colorSpace, double maxChannelValue)
    {
        var definition = colorSpace switch
        {
            RgbColorSpace.SRgb => SRgb,
            RgbColorSpace.AdobeRgb1998 => AdobeRgb1998,
            RgbColorSpace.EciRgbV2 => EciRgbV2,
            _ => throw new ArgumentOutOfRangeException(nameof(colorSpace), colorSpace, null)
        };

        var r = Decode(Math.Clamp(red / maxChannelValue, 0.0, 1.0), definition.Companding);
        var g = Decode(Math.Clamp(green / maxChannelValue, 0.0, 1.0), definition.Companding);
        var b = Decode(Math.Clamp(blue / maxChannelValue, 0.0, 1.0), definition.Companding);

        var x = definition.RgbToXyz[0, 0] * r + definition.RgbToXyz[0, 1] * g + definition.RgbToXyz[0, 2] * b;
        var y = definition.RgbToXyz[1, 0] * r + definition.RgbToXyz[1, 1] * g + definition.RgbToXyz[1, 2] * b;
        var z = definition.RgbToXyz[2, 0] * r + definition.RgbToXyz[2, 1] * g + definition.RgbToXyz[2, 2] * b;

        var fx = LabF(x / definition.WhitePoint.X);
        var fy = LabF(y / definition.WhitePoint.Y);
        var fz = LabF(z / definition.WhitePoint.Z);

        return (116.0 * fy - 16.0, 500.0 * (fx - fy), 200.0 * (fy - fz));
    }

    private static double Decode(double encoded, RgbCompanding companding)
    {
        return companding switch
        {
            RgbCompanding.SRgb => encoded <= 0.04045
                ? encoded / 12.92
                : Math.Pow((encoded + 0.055) / 1.055, 2.4),
            RgbCompanding.Gamma22 => Math.Pow(encoded, 2.2),
            RgbCompanding.LStar => encoded <= 0.08
                ? encoded / 9.033
                : Math.Pow((encoded + 0.16) / 1.16, 3.0),
            _ => throw new ArgumentOutOfRangeException(nameof(companding), companding, null)
        };
    }

    private static double LabF(double value)
    {
        const double epsilon = 216.0 / 24389.0;
        const double kappa = 24389.0 / 27.0;
        return value > epsilon
            ? Math.Pow(value, 1.0 / 3.0)
            : (kappa * value + 16.0) / 116.0;
    }

    private static (double X, double Y, double Z) WhitePointFromXy(double x, double y)
    {
        return (x / y, 1.0, (1.0 - x - y) / y);
    }

    private static double[,] MatrixFromPrimaries(
        (double X, double Y) red,
        (double X, double Y) green,
        (double X, double Y) blue,
        (double X, double Y) white)
    {
        var redXyz = XyToXyz(red);
        var greenXyz = XyToXyz(green);
        var blueXyz = XyToXyz(blue);
        var whiteXyz = WhitePointFromXy(white.X, white.Y);

        var primaryMatrix = new[,]
        {
            { redXyz.X, greenXyz.X, blueXyz.X },
            { redXyz.Y, greenXyz.Y, blueXyz.Y },
            { redXyz.Z, greenXyz.Z, blueXyz.Z },
        };
        var scales = Multiply(Invert3x3(primaryMatrix), [whiteXyz.X, whiteXyz.Y, whiteXyz.Z]);

        return new[,]
        {
            { primaryMatrix[0, 0] * scales[0], primaryMatrix[0, 1] * scales[1], primaryMatrix[0, 2] * scales[2] },
            { primaryMatrix[1, 0] * scales[0], primaryMatrix[1, 1] * scales[1], primaryMatrix[1, 2] * scales[2] },
            { primaryMatrix[2, 0] * scales[0], primaryMatrix[2, 1] * scales[1], primaryMatrix[2, 2] * scales[2] },
        };
    }

    private static (double X, double Y, double Z) XyToXyz((double X, double Y) xy)
    {
        return (xy.X / xy.Y, 1.0, (1.0 - xy.X - xy.Y) / xy.Y);
    }

    private static double[] Multiply(double[,] matrix, double[] vector)
    {
        return
        [
            matrix[0, 0] * vector[0] + matrix[0, 1] * vector[1] + matrix[0, 2] * vector[2],
            matrix[1, 0] * vector[0] + matrix[1, 1] * vector[1] + matrix[1, 2] * vector[2],
            matrix[2, 0] * vector[0] + matrix[2, 1] * vector[1] + matrix[2, 2] * vector[2],
        ];
    }

    private static double[,] Invert3x3(double[,] matrix)
    {
        var a = matrix[0, 0];
        var b = matrix[0, 1];
        var c = matrix[0, 2];
        var d = matrix[1, 0];
        var e = matrix[1, 1];
        var f = matrix[1, 2];
        var g = matrix[2, 0];
        var h = matrix[2, 1];
        var i = matrix[2, 2];

        var determinant = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
        if (Math.Abs(determinant) < double.Epsilon)
        {
            throw new InvalidOperationException("RGB primary matrix is not invertible.");
        }

        return new[,]
        {
            { (e * i - f * h) / determinant, (c * h - b * i) / determinant, (b * f - c * e) / determinant },
            { (f * g - d * i) / determinant, (a * i - c * g) / determinant, (c * d - a * f) / determinant },
            { (d * h - e * g) / determinant, (b * g - a * h) / determinant, (a * e - b * d) / determinant },
        };
    }

    private sealed record RgbColorSpaceDefinition(
        RgbCompanding Companding,
        double[,] RgbToXyz,
        (double X, double Y, double Z) WhitePoint);

    private enum RgbCompanding
    {
        SRgb,
        Gamma22,
        LStar
    }
}
