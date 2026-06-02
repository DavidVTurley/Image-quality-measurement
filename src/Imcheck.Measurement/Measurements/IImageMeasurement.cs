namespace Imcheck.Measurement.Measurements;

public interface IImageMeasurementResult
{
    string ImagePath { get; }

    string ImageName { get; }

    string ToCsv();
}

public interface IImageMeasurer<in TOptions, out TResult>
    where TResult : IImageMeasurementResult
{
    TResult Measure(string imagePath, TOptions? options = default);
}
