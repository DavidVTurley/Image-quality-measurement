using Imcheck.Measurement.Measurements.Q13;
using Imcheck.Measurement.Measurements.Uniformity;
using Imcheck.Measurement;
using Microsoft.Win32;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using IOPath = System.IO.Path;

namespace Imcheck.App;

public partial class MainWindow : Window
{
    private readonly Q13Measurer _q13Measurer = new();
    private readonly Q13GrayscaleDetector _q13Detector = new();
    private readonly UniformityAnalyzer _uniformityAnalyzer = new();
    private readonly MunsellLinearGrayscaleTargetGenerator _munsellGenerator = new();
    private readonly Q13GrayscaleTargetGenerator _q13Generator = new();

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
        InitializeGeneratorTab();
        RefreshMainTabButtons();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ApplyDarkTitleBar();
    }

    private void ApplyDarkTitleBar()
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var useDarkMode = 1;
        _ = DwmSetWindowAttribute(handle, DwmWindowAttribute.UseImmersiveDarkMode, ref useDarkMode, Marshal.SizeOf<int>());

        var captionColor = ColorRef(15, 15, 15);
        _ = DwmSetWindowAttribute(handle, DwmWindowAttribute.CaptionColor, ref captionColor, Marshal.SizeOf<int>());

        var textColor = ColorRef(232, 228, 220);
        _ = DwmSetWindowAttribute(handle, DwmWindowAttribute.TextColor, ref textColor, Marshal.SizeOf<int>());

        var borderColor = ColorRef(42, 42, 42);
        _ = DwmSetWindowAttribute(handle, DwmWindowAttribute.BorderColor, ref borderColor, Marshal.SizeOf<int>());
    }

    private void Q13NavButton_Click(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 0;
    }

    private void UniformityNavButton_Click(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 1;
    }

    private void GeneratorNavButton_Click(object sender, RoutedEventArgs e)
    {
        MainTabs.SelectedIndex = 2;
    }

    private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source == MainTabs)
        {
            RefreshMainTabButtons();
        }
    }

    private void RefreshMainTabButtons()
    {
        MarkMainTabButton(Q13NavButton, MainTabs.SelectedIndex == 0);
        MarkMainTabButton(UniformityNavButton, MainTabs.SelectedIndex == 1);
        MarkMainTabButton(GeneratorNavButton, MainTabs.SelectedIndex == 2);
    }

    private static void MarkMainTabButton(Button button, bool active)
    {
        button.Background = active ? NavBrushFromRgb(36, 36, 36) : NavBrushFromRgb(20, 20, 20);
        button.Foreground = active ? NavBrushFromRgb(200, 169, 110) : NavBrushFromRgb(232, 228, 220);
        button.BorderBrush = active ? NavBrushFromRgb(200, 169, 110) : NavBrushFromRgb(42, 42, 42);
    }

    private static SolidColorBrush NavBrushFromRgb(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private static int ColorRef(byte red, byte green, byte blue)
    {
        return red | (green << 8) | (blue << 16);
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
        // A fresh stream bypasses WPF's URI cache; IgnoreImageCache is only safe for URI-backed images.
        bitmap.StreamSource = memory;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    private static SaveFileDialog CreateCsvReportSaveDialog(string title, string imagePath)
    {
        var dialog = new SaveFileDialog
        {
            Title = title,
            Filter = "CSV files|*.csv|All files|*.*",
            FileName = IOPath.ChangeExtension(IOPath.GetFileName(imagePath), ".csv")
        };

        var directory = IOPath.GetDirectoryName(imagePath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            dialog.InitialDirectory = directory;
        }

        return dialog;
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

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, DwmWindowAttribute attribute, ref int pvAttribute, int cbAttribute);

    private enum DwmWindowAttribute
    {
        UseImmersiveDarkMode = 20,
        BorderColor = 34,
        CaptionColor = 35,
        TextColor = 36
    }

    private void ConfigureQ13Columns()
    {
        Q13ResultsGrid.Columns.Clear();
        AddColumn(Q13ResultsGrid, "Patch", "Index", "0.##", 64);
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
