using Imcheck.Measurement.Meaasurements.Q13;
using Imcheck.Measurement.Meaasurements.Uniformity;
using Microsoft.Win32;
using System.Globalization;
using System.IO;
using IOPath = System.IO.Path;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Imcheck.App;

public partial class MainWindow : Window
{
    private readonly Q13Measurer _q13Measurer = new();
    private readonly UniformityAnalyzer _uniformityAnalyzer = new();

    private Q13MeasurementResult? _currentQ13Result;
    private UniformityAnalysisResult? _currentUniformityResult;
    private IReadOnlyList<Q13SamplePoint>? _q13SampleCenters;

    public MainWindow()
    {
        InitializeComponent();
        ConfigureQ13Columns();
        ConfigureUniformityColumns();
    }

    private void Q13OpenButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open Q-13 image",
            Filter = "Image files|*.tif;*.tiff;*.jpg;*.jpeg;*.png;*.bmp|All files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            StatusText.Text = "Measuring Q13 image...";
            _currentQ13Result = _q13Measurer.Measure(dialog.FileName, new Q13MeasurementOptions { SampleCenters = _q13SampleCenters });
            Q13PreviewImage.Source = LoadBitmap(dialog.FileName);
            Q13FileNameText.Text = dialog.FileName;
            ShowQ13Result(_currentQ13Result);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Q13 measurement failed.";
            MessageBox.Show(this, ex.Message, "Measurement failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Q13LoadPointsButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open sample point CSV",
            Filter = "CSV or text files|*.csv;*.txt|All files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            _q13SampleCenters = Q13SamplePointCsv.Load(dialog.FileName);
            Q13ClearPointsButton.IsEnabled = true;
            StatusText.Text = $"Loaded {_q13SampleCenters.Count} explicit Q13 sample centers.";

            if (_currentQ13Result is not null)
            {
                _currentQ13Result = _q13Measurer.Measure(_currentQ13Result.ImagePath, new Q13MeasurementOptions { SampleCenters = _q13SampleCenters });
                ShowQ13Result(_currentQ13Result);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "Could not load sample points", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void Q13ClearPointsButton_Click(object sender, RoutedEventArgs e)
    {
        _q13SampleCenters = null;
        Q13ClearPointsButton.IsEnabled = false;
        StatusText.Text = "Using automatic Q13 straight-line sample centers.";

        if (_currentQ13Result is not null)
        {
            _currentQ13Result = _q13Measurer.Measure(_currentQ13Result.ImagePath);
            ShowQ13Result(_currentQ13Result);
        }
    }

    private async void Q13ExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentQ13Result is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export Q13 CSV",
            Filter = "CSV files|*.csv|All files|*.*",
            FileName = IOPath.ChangeExtension(_currentQ13Result.ImageName, ".csv")
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await _currentQ13Result.SaveCsvAsync(dialog.FileName);
            StatusText.Text = $"Q13 CSV exported to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Q13 CSV export failed.";
            MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UniformityOpenButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Open uniformity image",
            Filter = "Image files|*.tif;*.tiff;*.jpg;*.jpeg;*.png;*.bmp|All files|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            StatusText.Text = "Measuring uniformity image...";
            _currentUniformityResult = _uniformityAnalyzer.Analyze(dialog.FileName);
            UniformityPreviewImage.Source = LoadBitmap(dialog.FileName);
            UniformityFileNameText.Text = dialog.FileName;
            ShowUniformityResult(_currentUniformityResult);
        }
        catch (Exception ex)
        {
            StatusText.Text = "Uniformity measurement failed.";
            MessageBox.Show(this, ex.Message, "Measurement failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void UniformityExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentUniformityResult is null)
        {
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "Export uniformity CSV",
            Filter = "CSV files|*.csv|All files|*.*",
            FileName = IOPath.ChangeExtension(IOPath.GetFileName(_currentUniformityResult.ImagePath), ".csv")
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            await File.WriteAllTextAsync(dialog.FileName, _currentUniformityResult.ToCsv());
            StatusText.Text = $"Uniformity CSV exported to {dialog.FileName}";
        }
        catch (Exception ex)
        {
            StatusText.Text = "Uniformity CSV export failed.";
            MessageBox.Show(this, ex.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UniformityPreviewHost_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawUniformityOverlay();
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.UriSource = new Uri(path);
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private void ShowQ13Result(Q13MeasurementResult result)
    {
        Q13ResultsGrid.ItemsSource = result.Patches;
        Q13SamplingText.Text = string.Create(CultureInfo.InvariantCulture, $"{result.SamplingPixelsPerInch:0.0} pix/inch ({result.SamplingPixelsPerInch / 25.4:0.0} pix/mm)");
        Q13SampleSizeText.Text = result.SampleDataSize.ToString(CultureInfo.InvariantCulture);
        Q13GammaText.Text = result.InverseGamma.ToString("0.00", CultureInfo.InvariantCulture);
        Q13PatchCountText.Text = result.Patches.Count.ToString(CultureInfo.InvariantCulture);
        Q13ExportButton.IsEnabled = true;
        StatusText.Text = _q13SampleCenters is null
            ? "Q13 measurement complete using automatic straight-line centers."
            : "Q13 measurement complete using explicit sample centers.";
    }

    private void ShowUniformityResult(UniformityAnalysisResult result)
    {
        UniformityResultsGrid.ItemsSource = result.Samples;
        UniformityImageText.Text = string.Create(CultureInfo.InvariantCulture, $"{result.ImageWidth}x{result.ImageHeight}, {result.BitDepth}-bit");
        UniformitySampleSizeText.Text = string.Create(CultureInfo.InvariantCulture, $"{result.SampleSize}x{result.SampleSize}");
        UniformityDeltaLText.Text = result.MaxDeltaLStar.ToString("0.####", CultureInfo.InvariantCulture);
        UniformityDeltaEText.Text = result.MaxDeltaEab.ToString("0.####", CultureInfo.InvariantCulture);
        UniformityExportButton.IsEnabled = true;
        DrawUniformityOverlay();
        StatusText.Text = "Uniformity measurement complete.";
    }

    private void DrawUniformityOverlay()
    {
        UniformityOverlayCanvas.Children.Clear();
        if (_currentUniformityResult is null || UniformityPreviewImage.Source is not BitmapSource bitmap)
        {
            return;
        }

        var hostWidth = UniformityPreviewHost.ActualWidth;
        var hostHeight = UniformityPreviewHost.ActualHeight;
        if (hostWidth <= 0 || hostHeight <= 0 || bitmap.PixelWidth <= 0 || bitmap.PixelHeight <= 0)
        {
            return;
        }

        var imageAspect = bitmap.PixelWidth / (double)bitmap.PixelHeight;
        var hostAspect = hostWidth / hostHeight;
        double displayedWidth;
        double displayedHeight;
        double offsetX;
        double offsetY;

        if (hostAspect > imageAspect)
        {
            displayedHeight = hostHeight;
            displayedWidth = displayedHeight * imageAspect;
            offsetX = (hostWidth - displayedWidth) / 2.0;
            offsetY = 0;
        }
        else
        {
            displayedWidth = hostWidth;
            displayedHeight = displayedWidth / imageAspect;
            offsetX = 0;
            offsetY = (hostHeight - displayedHeight) / 2.0;
        }

        foreach (var sample in _currentUniformityResult.Samples)
        {
            var rectangle = new Rectangle
            {
                Width = sample.SampleSize / (double)_currentUniformityResult.ImageWidth * displayedWidth,
                Height = sample.SampleSize / (double)_currentUniformityResult.ImageHeight * displayedHeight,
                Stroke = Brushes.Red,
                StrokeThickness = 2,
                Fill = Brushes.Transparent
            };

            Canvas.SetLeft(rectangle, offsetX + sample.SampleX / (double)_currentUniformityResult.ImageWidth * displayedWidth);
            Canvas.SetTop(rectangle, offsetY + sample.SampleY / (double)_currentUniformityResult.ImageHeight * displayedHeight);
            UniformityOverlayCanvas.Children.Add(rectangle);
        }
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
