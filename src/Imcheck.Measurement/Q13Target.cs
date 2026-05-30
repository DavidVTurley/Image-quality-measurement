namespace Imcheck.Measurement;

public sealed record Q13TargetPatch(int Index, double InputRed, double InputGreen, double InputBlue)
{
    public double LuminanceInput => (InputRed + InputGreen + InputBlue) / 3.0;
}

public static class Q13Target
{
    public static IReadOnlyList<Q13TargetPatch> KodakPatches { get; } =
    [
        new(0, 0.9049, 0.9024, 0.8760),
        new(1, 0.6994, 0.6890, 0.6766),
        new(2, 0.5511, 0.5448, 0.5471),
        new(3, 0.4338, 0.4277, 0.4317),
        new(4, 0.3443, 0.3410, 0.3481),
        new(5, 0.2679, 0.2671, 0.2726),
        new(6, 0.2153, 0.2117, 0.2154),
        new(7, 0.1700, 0.1690, 0.1718),
        new(8, 0.1330, 0.1329, 0.1363),
        new(9, 0.1071, 0.1059, 0.1081),
        new(10, 0.0828, 0.0828, 0.0847),
        new(11, 0.0671, 0.0671, 0.0692),
        new(12, 0.0533, 0.0534, 0.0554),
        new(13, 0.0452, 0.0449, 0.0466),
        new(14, 0.0356, 0.0353, 0.0369),
        new(15, 0.0287, 0.0288, 0.0301),
        new(16, 0.0224, 0.0226, 0.0241),
        new(17, 0.0178, 0.0180, 0.0188),
        new(18, 0.0153, 0.0152, 0.0157),
        new(19, 0.0116, 0.0121, 0.0126),
    ];
}
