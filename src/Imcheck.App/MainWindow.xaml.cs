using Imcheck.Measurement.Meaasurements.Q13;
using Imcheck.Measurement.Meaasurements.Uniformity;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace Imcheck.App;

public partial class MainWindow : Window
{
    private readonly Q13Measurer _q13Measurer = new();
    private readonly Q13GrayscaleDetector _q13Detector = new();
    private readonly UniformityAnalyzer _uniformityAnalyzer = new();

    private Q13MeasurementResult? _currentQ13Result;
    private UniformityAnalysisResult? _currentUniformityResult;
    private IReadOnlyList<Q13SamplePoint>? _q13SampleCenters;
    private string? _pendingQ13ImagePath;
    private Q13StripGeometry? _pendingQ13Geometry;
    private List<Q13SampleRegion> _pendingQ13Regions = [];
    private Q13StripGeometry? _acceptedQ13Geometry;
    private List<Q13SampleRegion>? _acceptedQ13Regions;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureQ13Columns();
        ConfigureUniformityColumns();
    }

    private static BitmapImage LoadBitmap(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        memory.Position = 0;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        bitmap.StreamSource = memory;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static (double DisplayedWidth, double DisplayedHeight, double OffsetX, double OffsetY)? ImageDisplayTransform(
        double hostWidth,
        double hostHeight,
        int imageWidth,
        int imageHeight)
    {
        if (hostWidth <= 0 || hostHeight <= 0 || imageWidth <= 0 || imageHeight <= 0)
        {
            return null;
        }

        var imageAspect = imageWidth / (double)imageHeight;
        var hostAspect = hostWidth / hostHeight;

        if (hostAspect > imageAspect)
        {
            var displayedHeight = hostHeight;
            var displayedWidth = displayedHeight * imageAspect;
            return (displayedWidth, displayedHeight, (hostWidth - displayedWidth) / 2.0, 0);
        }

        var fittedWidth = hostWidth;
        var fittedHeight = fittedWidth / imageAspect;
        return (fittedWidth, fittedHeight, 0, (hostHeight - fittedHeight) / 2.0);
    }

    private void ConfigureQ13Columns()
    {
        Q13ResultsGrid.Columns.Clear();
        AddColumn(Q13ResultsGrid, "Patch", "Index", "0.##", 64);
        AddColumn(Q13ResultsGrid, "Center X", "SampleCenterX", "0.##");
        AddColumn(Q13ResultsGrid, "Center Y", "SampleCenterY", "0.##");
        AddColumn(Q13ResultsGrid, "Input R", "InputRed", "0.0000");
        AddColumn(Q13ResultsGrid, "Input G", "InputGreen", "0.0000");
        AddColumn(Q13ResultsGrid, "Input B", "InputBlue", "0.0000");
        AddColumn(Q13ResultsGrid, "Output R", "OutputRed", "0.####");
        AddColumn(Q13ResultsGrid, "Output G", "OutputGreen", "0.####");
        AddColumn(Q13ResultsGrid, "Output B", "OutputBlue", "0.####");
        AddColumn(Q13ResultsGrid, "Noise R", "NoiseRed", "0.####");
        AddColumn(Q13ResultsGrid, "Noise G", "NoiseGreen", "0.####");
        AddColumn(Q13ResultsGrid, "Noise B", "NoiseBlue", "0.####");
    }

    private void ConfigureUniformityColumns()
    {
        UniformityResultsGrid.Columns.Clear();
        AddColumn(UniformityResultsGrid, "Area", "Name", null, 100);
        AddColumn(UniformityResultsGrid, "Center X", "SampleCenterX", "0.##");
        AddColumn(UniformityResultsGrid, "Center Y", "SampleCenterY", "0.##");
        AddColumn(UniformityResultsGrid, "Size", "SampleSize", "0.##", 72);
        AddColumn(UniformityResultsGrid, "Mean R", "MeanRed", "0.####");
        AddColumn(UniformityResultsGrid, "Mean G", "MeanGreen", "0.####");
        AddColumn(UniformityResultsGrid, "Mean B", "MeanBlue", "0.####");
        AddColumn(UniformityResultsGrid, "L*", "LStar", "0.####");
        AddColumn(UniformityResultsGrid, "a*", "AStar", "0.####");
        AddColumn(UniformityResultsGrid, "b*", "BStar", "0.####");
    }

    private static void AddColumn(DataGrid grid, string header, string path, string? stringFormat, double? width = null)
    {
        var binding = new Binding(path);
        if (stringFormat is not null)
        {
            binding.StringFormat = stringFormat;
        }

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = header,
            Binding = binding,
            Width = width ?? new DataGridLength(1, DataGridLengthUnitType.Star)
        });
    }
}
