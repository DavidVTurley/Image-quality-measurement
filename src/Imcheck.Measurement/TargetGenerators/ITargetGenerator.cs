namespace Imcheck.Measurement;

public interface ITargetGenerator<in TOptions, out TResult>
{
    TResult Generate(string outputPath, TOptions? options = default);
}
