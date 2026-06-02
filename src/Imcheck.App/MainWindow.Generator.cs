using Imcheck.Measurement;
using Microsoft.Win32;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using IOPath = System.IO.Path;

namespace Imcheck.App;

public partial class MainWindow
{
    private bool _generatorInitializing;

    private void InitializeGeneratorTab()
    {
        _generatorInitializing = true;
        GeneratorTargetComboBox.SelectedIndex = 0;
        GeneratorNoiseModelComboBox.SelectedIndex = 0;
        GeneratorDpiTextBox.Text = MunsellLinearGrayscaleTargetGenerator.DefaultDpi.ToString(CultureInfo.InvariantCulture);
        GeneratorShowLabelsCheckBox.IsChecked = true;
        GeneratorShowScaleCheckBox.IsChecked = true;
        GeneratorShowTitleCheckBox.IsChecked = true;
        GeneratorNoiseEnabledCheckBox.IsChecked = false;
        GeneratorNoiseAmountTextBox.Text = "0";
        GeneratorNoiseCoverageTextBox.Text = "75";
        GeneratorNoiseGradientTextBox.Text = "0";
        GeneratorNoiseBlurTextBox.Text = "0";
        GeneratorNoisePatchBiasTextBox.Text = "0";
        GeneratorNoiseSeedTextBox.Text = "1234";
        GeneratorOutputPathTextBox.Text = DefaultGeneratorOutputPath();
        _generatorInitializing = false;

        RefreshGeneratorTargetDetails();
        RefreshGeneratorDpiButtons();
        RefreshGeneratorPreview();
    }

    private void GeneratorTargetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_generatorInitializing)
        {
            return;
        }

        GeneratorOutputPathTextBox.Text = DefaultGeneratorOutputPath();
        RefreshGeneratorTargetDetails();
        RefreshGeneratorPreview();
    }

    private void GeneratorDpiButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string rawDpi)
        {
            GeneratorDpiTextBox.Text = rawDpi;
        }
    }

    private void GeneratorOption_Changed(object sender, RoutedEventArgs e)
    {
        if (_generatorInitializing)
        {
            return;
        }

        RefreshGeneratorDpiButtons();
        RefreshGeneratorOutputName();
        RefreshGeneratorPreview();
    }

    private void GeneratorBrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save generated grayscale target PNG",
            Filter = "PNG files|*.png|All files|*.*",
            FileName = IOPath.GetFileName(GeneratorOutputPathTextBox.Text)
        };

        var currentDirectory = IOPath.GetDirectoryName(GeneratorOutputPathTextBox.Text);
        if (!string.IsNullOrWhiteSpace(currentDirectory))
        {
            dialog.InitialDirectory = currentDirectory;
        }

        if (dialog.ShowDialog(this) == true)
        {
            GeneratorOutputPathTextBox.Text = dialog.FileName;
        }
    }

    private void GeneratorGenerateButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dpi = ParsePositiveInt(GeneratorDpiTextBox.Text, "DPI");
            var outputPath = GeneratorOutputPathTextBox.Text;
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                outputPath = IOPath.Combine(Environment.CurrentDirectory, DefaultGeneratorFileName(dpi));
                GeneratorOutputPathTextBox.Text = outputPath;
            }

            StatusText.Text = $"Generating {CurrentGeneratorDisplayName()}...";
            var (width, height) = GenerateSelectedTarget(outputPath, dpi);
            GeneratorExportStatusText.Foreground = BrushFromRgb(109, 187, 138);
            GeneratorExportStatusText.Text = string.Create(CultureInfo.InvariantCulture, $"Generated {width}x{height} PNG.");
            StatusText.Text = $"Generated {CurrentGeneratorDisplayName()}: {outputPath}";
        }
        catch (Exception ex)
        {
            GeneratorExportStatusText.Foreground = BrushFromRgb(224, 112, 112);
            GeneratorExportStatusText.Text = ex.Message;
            StatusText.Text = "Target generation failed.";
            MessageBox.Show(this, ex.Message, "Generation failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private (int Width, int Height) GenerateSelectedTarget(string outputPath, int dpi)
    {
        return CurrentGeneratorTarget() == GeneratorTarget.Q13
            ? GenerateQ13(outputPath, dpi)
            : GenerateMunsell(outputPath, dpi);
    }

    private (int Width, int Height) GenerateMunsell(string outputPath, int dpi)
    {
        var result = _munsellGenerator.Generate(outputPath, new MunsellLinearGrayscaleTargetGeneratorOptions
        {
            Dpi = dpi,
            ShowLabels = GeneratorShowLabelsCheckBox.IsChecked == true,
            ShowMillimeterScale = GeneratorShowScaleCheckBox.IsChecked == true,
            ShowTitle = GeneratorShowTitleCheckBox.IsChecked == true,
            Noise = CreateNoiseOptions()
        });

        return (result.Width, result.Height);
    }

    private (int Width, int Height) GenerateQ13(string outputPath, int dpi)
    {
        var result = _q13Generator.Generate(outputPath, new Q13GrayscaleTargetGeneratorOptions
        {
            Dpi = dpi,
            ShowLabels = GeneratorShowLabelsCheckBox.IsChecked == true,
            ShowMillimeterScale = GeneratorShowScaleCheckBox.IsChecked == true,
            ShowTitle = GeneratorShowTitleCheckBox.IsChecked == true,
            Noise = CreateNoiseOptions()
        });

        return (result.Width, result.Height);
    }

    private GrayscaleNoiseOptions? CreateNoiseOptions()
    {
        if (GeneratorNoiseEnabledCheckBox.IsChecked != true)
        {
            return null;
        }

        return new GrayscaleNoiseOptions
        {
            Enabled = true,
            Model = CurrentNoiseModel(),
            Amount = ParseDouble(GeneratorNoiseAmountTextBox.Text, "Noise amount"),
            Coverage = Math.Clamp(ParseDouble(GeneratorNoiseCoverageTextBox.Text, "Noise coverage") / 100.0, 0, 1),
            VerticalGradient = ParseDouble(GeneratorNoiseGradientTextBox.Text, "Vertical gradient"),
            BlurRadius = Math.Clamp(ParsePositiveOrZeroInt(GeneratorNoiseBlurTextBox.Text, "Blur"), 0, 12),
            PatchBias = ParseDouble(GeneratorNoisePatchBiasTextBox.Text, "Patch bias"),
            Seed = ParsePositiveInt(GeneratorNoiseSeedTextBox.Text, "Seed")
        };
    }

    private void RefreshGeneratorPreview()
    {
        if (_generatorInitializing)
        {
            return;
        }

        try
        {
            var dpi = ParsePositiveInt(GeneratorDpiTextBox.Text, "DPI");
            var (width, height) = CurrentTargetPixelSize(dpi);
            GeneratorPixelSizeText.Text = string.Create(CultureInfo.InvariantCulture, $"{width}x{height} px");

            var previewPath = IOPath.Combine(IOPath.GetTempPath(), $"imcheck-generator-preview-{Guid.NewGuid():N}.png");
            try
            {
                GenerateSelectedTarget(previewPath, 120);
                GeneratorPreviewImage.Source = LoadBitmap(previewPath);
            }
            finally
            {
                if (File.Exists(previewPath))
                {
                    File.Delete(previewPath);
                }
            }

            GeneratorExportStatusText.Foreground = BrushFromRgb(109, 187, 138);
            if (string.IsNullOrWhiteSpace(GeneratorExportStatusText.Text))
            {
                GeneratorExportStatusText.Text = "Ready.";
            }
        }
        catch (Exception ex)
        {
            GeneratorPreviewImage.Source = null;
            GeneratorPixelSizeText.Text = "-";
            GeneratorExportStatusText.Foreground = BrushFromRgb(224, 112, 112);
            GeneratorExportStatusText.Text = ex.Message;
        }
    }

    private void RefreshGeneratorTargetDetails()
    {
        if (CurrentGeneratorTarget() == GeneratorTarget.Q13)
        {
            GeneratorTitleText.Text = "Kodak Q13 Grayscale Generator";
            GeneratorSubtitleText.Text = "Density-spaced Q13 grayscale with shared optional test noise.";
            GeneratorPatchCountText.Text = Q13GrayscaleTargetGenerator.Patches.Count.ToString(CultureInfo.InvariantCulture);
            GeneratorLStarRangeText.Text = "~95.6 to ~10.0";
            GeneratorCardSizeText.Text = "203 x 30 mm";
            GeneratorValuesText.Text = "Density model";
            GeneratorPatchGrid.ItemsSource = Q13GrayscaleTargetGenerator.Patches.Select(GeneratorPatchRow.FromQ13Patch).ToList();
            return;
        }

        GeneratorTitleText.Text = "Munsell Linear Grayscale Generator";
        GeneratorSubtitleText.Text = "Practical full-card recreation with shared optional test noise.";
        GeneratorPatchCountText.Text = MunsellLinearGrayscaleTargetGenerator.Patches.Count.ToString(CultureInfo.InvariantCulture);
        GeneratorLStarRangeText.Text = "95 to 5 + gloss black";
        GeneratorCardSizeText.Text = "255 x 32 mm";
        GeneratorValuesText.Text = "Theoretical L*";
        GeneratorPatchGrid.ItemsSource = MunsellLinearGrayscaleTargetGenerator.Patches.Select(GeneratorPatchRow.FromMunsellPatch).ToList();
    }

    private void RefreshGeneratorOutputName()
    {
        if (!int.TryParse(GeneratorDpiTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dpi) || dpi <= 0)
        {
            return;
        }

        var currentPath = GeneratorOutputPathTextBox.Text;
        if (string.IsNullOrWhiteSpace(currentPath))
        {
            return;
        }

        var directory = IOPath.GetDirectoryName(currentPath);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Environment.CurrentDirectory;
        }

        GeneratorOutputPathTextBox.Text = IOPath.Combine(directory, DefaultGeneratorFileName(dpi));
    }

    private void RefreshGeneratorDpiButtons()
    {
        var dpi = GeneratorDpiTextBox.Text.Trim();
        MarkDpiButton(GeneratorDpi150Button, dpi == "150");
        MarkDpiButton(GeneratorDpi300Button, dpi == "300");
        MarkDpiButton(GeneratorDpi600Button, dpi == "600");
    }

    private (int Width, int Height) CurrentTargetPixelSize(int dpi)
    {
        var widthMm = CurrentGeneratorTarget() == GeneratorTarget.Q13
            ? Q13GrayscaleTargetGenerator.TargetWidthMillimeters
            : MunsellLinearGrayscaleTargetGenerator.TargetWidthMillimeters;
        var heightMm = CurrentGeneratorTarget() == GeneratorTarget.Q13
            ? Q13GrayscaleTargetGenerator.TargetHeightMillimeters
            : MunsellLinearGrayscaleTargetGenerator.TargetHeightMillimeters;

        return ((int)Math.Round(widthMm / 25.4 * dpi), (int)Math.Round(heightMm / 25.4 * dpi));
    }

    private string DefaultGeneratorOutputPath()
    {
        var dpi = int.TryParse(GeneratorDpiTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedDpi) && parsedDpi > 0
            ? parsedDpi
            : MunsellLinearGrayscaleTargetGenerator.DefaultDpi;

        return IOPath.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), DefaultGeneratorFileName(dpi));
    }

    private string DefaultGeneratorFileName(int dpi)
    {
        return CurrentGeneratorTarget() == GeneratorTarget.Q13
            ? $"Kodak_Q13_Grayscale_{dpi}dpi.png"
            : $"Munsell_Linear_Grayscale_{dpi}dpi.png";
    }

    private string CurrentGeneratorDisplayName()
    {
        return CurrentGeneratorTarget() == GeneratorTarget.Q13
            ? "Kodak Q13 Grayscale target"
            : "Munsell Linear Grayscale target";
    }

    private GeneratorTarget CurrentGeneratorTarget()
    {
        return (GeneratorTargetComboBox.SelectedItem as ComboBoxItem)?.Tag as string == "q13"
            ? GeneratorTarget.Q13
            : GeneratorTarget.Munsell;
    }

    private GrayscaleNoiseModel CurrentNoiseModel()
    {
        return ((GeneratorNoiseModelComboBox.SelectedItem as ComboBoxItem)?.Tag as string) switch
        {
            "uniform" => GrayscaleNoiseModel.Uniform,
            "patch" => GrayscaleNoiseModel.Patch,
            _ => GrayscaleNoiseModel.Gaussian
        };
    }

    private static int ParsePositiveInt(string raw, string label)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value <= 0)
        {
            throw new InvalidOperationException($"{label} must be a positive integer.");
        }

        return value;
    }

    private static int ParsePositiveOrZeroInt(string raw, string label)
    {
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            throw new InvalidOperationException($"{label} must be zero or a positive integer.");
        }

        return value;
    }

    private static double ParseDouble(string raw, string label)
    {
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            throw new InvalidOperationException($"{label} must be a number.");
        }

        return value;
    }

    private static void MarkDpiButton(Button button, bool active)
    {
        button.BorderBrush = active ? BrushFromRgb(200, 169, 110) : BrushFromRgb(68, 68, 68);
        button.Foreground = active ? BrushFromRgb(200, 169, 110) : BrushFromRgb(102, 102, 102);
        button.Background = Brushes.Transparent;
    }

    private static SolidColorBrush BrushFromRgb(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private enum GeneratorTarget
    {
        Munsell,
        Q13
    }

    private sealed record GeneratorPatchRow(string Label, double LStar, byte EncodedRgb, string Hex, Brush SwatchBrush)
    {
        public static GeneratorPatchRow FromMunsellPatch(MunsellLinearGrayscalePatch patch)
        {
            return new GeneratorPatchRow(
                patch.Label,
                patch.LStar,
                patch.EncodedRgb,
                patch.Hex,
                BrushFromRgb(patch.EncodedRgb, patch.EncodedRgb, patch.EncodedRgb));
        }

        public static GeneratorPatchRow FromQ13Patch(Q13GrayscalePatch patch)
        {
            return new GeneratorPatchRow(
                patch.Label,
                patch.LStar,
                patch.EncodedRgb,
                patch.Hex,
                BrushFromRgb(patch.EncodedRgb, patch.EncodedRgb, patch.EncodedRgb));
        }
    }
}
