using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PngCompressorDesktop;

internal static class Program
{
    [STAThread]
    [SupportedOSPlatform("windows10.0.17763")]
    private static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.Run(new MainForm());
    }
}

[SupportedOSPlatform("windows10.0.17763")]
internal sealed class MainForm : Form
{
    private readonly Button addButton = new();
    private readonly Button outputButton = new();
    private readonly Button compressButton = new();
    private readonly Button clearButton = new();
    private readonly NumericUpDown scaleInput = new();
    private readonly NumericUpDown maxWidthInput = new();
    private readonly NumericUpDown maxHeightInput = new();
    private readonly ComboBox optimizeMode = new();
    private readonly ComboBox colorCount = new();
    private readonly ComboBox outputFormat = new();
    private readonly CheckBox keepSmaller = new();
    private readonly DataGridView fileGrid = new();
    private readonly ProgressBar progressBar = new();
    private readonly Label summaryLabel = new();
    private readonly Label outputLabel = new();
    private readonly List<string> files = new();

    private string? outputDirectory;
    private bool isRunning;

    public MainForm()
    {
        Text = "图片压缩工具";
        MinimumSize = new Size(980, 620);
        StartPosition = FormStartPosition.CenterScreen;
        Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Regular);

        BuildLayout();
        UpdateUiState();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(18),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var title = new Label
        {
            Text = "图片压缩",
            AutoSize = true,
            Font = new Font(Font.FontFamily, 24F, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 12),
        };
        root.Controls.Add(title, 0, 0);

        var settings = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 8,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
        };
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.Controls.Add(settings, 0, 1);

        ConfigureNumber(scaleInput, 10, 100, 80);
        ConfigureNumber(maxWidthInput, 0, 20000, 0);
        ConfigureNumber(maxHeightInput, 0, 20000, 0);

        optimizeMode.DropDownStyle = ComboBoxStyle.DropDownList;
        optimizeMode.Items.AddRange(["标准优化", "颜色优化", "强力优化"]);
        optimizeMode.SelectedIndex = 0;
        optimizeMode.Width = 118;

        colorCount.DropDownStyle = ComboBoxStyle.DropDownList;
        colorCount.Items.AddRange(["256", "128", "64", "32"]);
        colorCount.SelectedIndex = 0;
        colorCount.Width = 86;

        outputFormat.DropDownStyle = ComboBoxStyle.DropDownList;
        outputFormat.Items.AddRange(["PNG", "WebP"]);
        outputFormat.SelectedIndex = 0;
        outputFormat.Width = 92;

        keepSmaller.Text = "仅保存更小结果";
        keepSmaller.Checked = true;
        keepSmaller.AutoSize = true;

        settings.Controls.Add(MakeLabel("像素比例%"), 0, 0);
        settings.Controls.Add(scaleInput, 1, 0);
        settings.Controls.Add(MakeLabel("最大宽度"), 2, 0);
        settings.Controls.Add(maxWidthInput, 3, 0);
        settings.Controls.Add(MakeLabel("最大高度"), 4, 0);
        settings.Controls.Add(maxHeightInput, 5, 0);
        settings.Controls.Add(MakeLabel("优化方式"), 6, 0);
        settings.Controls.Add(optimizeMode, 7, 0);
        settings.Controls.Add(MakeLabel("颜色数量"), 0, 1);
        settings.Controls.Add(colorCount, 1, 1);
        settings.Controls.Add(MakeLabel("导出格式"), 2, 1);
        settings.Controls.Add(outputFormat, 3, 1);
        settings.Controls.Add(keepSmaller, 4, 1);
        settings.SetColumnSpan(keepSmaller, 2);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 12),
        };
        root.Controls.Add(actions, 0, 2);

        addButton.Text = "添加 PNG";
        addButton.Width = 112;
        addButton.Click += (_, _) => AddFiles();
        actions.Controls.Add(addButton);

        outputButton.Text = "输出文件夹";
        outputButton.Width = 112;
        outputButton.Click += (_, _) => ChooseOutputDirectory();
        actions.Controls.Add(outputButton);

        compressButton.Text = "开始压缩";
        compressButton.Width = 112;
        compressButton.Click += async (_, _) => await CompressAllAsync();
        actions.Controls.Add(compressButton);

        clearButton.Text = "清空";
        clearButton.Width = 86;
        clearButton.Click += (_, _) => ClearFiles();
        actions.Controls.Add(clearButton);

        outputLabel.AutoSize = true;
        outputLabel.Margin = new Padding(12, 8, 0, 0);
        actions.Controls.Add(outputLabel);

        ConfigureGrid();
        root.Controls.Add(fileGrid, 0, 3);

        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Bottom,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 12, 0, 0),
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));
        root.Controls.Add(footer, 0, 4);

        summaryLabel.AutoSize = true;
        summaryLabel.Dock = DockStyle.Fill;
        footer.Controls.Add(summaryLabel, 0, 0);

        progressBar.Dock = DockStyle.Fill;
        progressBar.Minimum = 0;
        progressBar.Maximum = 100;
        footer.Controls.Add(progressBar, 1, 0);

        AllowDrop = true;
        DragEnter += (_, eventArgs) =>
        {
            if (eventArgs.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            {
                eventArgs.Effect = DragDropEffects.Copy;
            }
        };
        DragDrop += (_, eventArgs) =>
        {
            if (eventArgs.Data?.GetData(DataFormats.FileDrop) is string[] paths)
            {
                AddFiles(paths);
            }
        };
    }

    private static Label MakeLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 8, 8, 8),
        };
    }

    private static void ConfigureNumber(NumericUpDown input, int min, int max, int value)
    {
        input.Minimum = min;
        input.Maximum = max;
        input.Value = value;
        input.Width = 76;
    }

    private void ConfigureGrid()
    {
        fileGrid.Dock = DockStyle.Fill;
        fileGrid.AllowUserToAddRows = false;
        fileGrid.AllowUserToDeleteRows = false;
        fileGrid.AllowUserToResizeRows = false;
        fileGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        fileGrid.BackgroundColor = Color.White;
        fileGrid.BorderStyle = BorderStyle.FixedSingle;
        fileGrid.RowHeadersVisible = false;
        fileGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        fileGrid.ReadOnly = true;
        fileGrid.Columns.Add("file", "文件");
        fileGrid.Columns.Add("original", "原始大小");
        fileGrid.Columns.Add("dimensions", "尺寸");
        fileGrid.Columns.Add("output", "输出大小");
        fileGrid.Columns.Add("saved", "压缩率");
        fileGrid.Columns.Add("status", "状态");
        fileGrid.Columns["file"]!.FillWeight = 220;
        fileGrid.Columns["status"]!.FillWeight = 160;
    }

    private void AddFiles()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择 PNG 图片",
            Filter = "PNG 图片 (*.png)|*.png",
            Multiselect = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            AddFiles(dialog.FileNames);
        }
    }

    private void AddFiles(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(IsPngFile))
        {
            if (!files.Contains(path, StringComparer.OrdinalIgnoreCase))
            {
                files.Add(path);
                var info = new FileInfo(path);
                fileGrid.Rows.Add(Path.GetFileName(path), FormatBytes(info.Length), "等待读取", "", "", "等待");
            }
        }

        UpdateUiState();
    }

    private void ChooseOutputDirectory()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择压缩图片保存位置",
            UseDescriptionForTitle = true,
        };

        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            outputDirectory = dialog.SelectedPath;
            UpdateUiState();
        }
    }

    private void ClearFiles()
    {
        files.Clear();
        fileGrid.Rows.Clear();
        progressBar.Value = 0;
        UpdateUiState();
    }

    private async Task CompressAllAsync()
    {
        if (files.Count == 0 || isRunning)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            ChooseOutputDirectory();
            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                return;
            }
        }

        isRunning = true;
        UpdateUiState();

        long originalTotal = 0;
        long outputTotal = 0;
        progressBar.Value = 0;

        try
        {
            for (var index = 0; index < files.Count; index++)
            {
                SetRow(index, status: "处理中");
                var options = BuildOptions();
                var path = files[index];
                var result = await Task.Run(() => CompressOne(path, outputDirectory!, options));
                originalTotal += result.OriginalBytes;
                outputTotal += result.OutputBytes;

                SetRow(
                    index,
                    dimensions: $"{result.InputWidth}x{result.InputHeight} -> {result.OutputWidth}x{result.OutputHeight}",
                    output: FormatBytes(result.OutputBytes),
                    saved: FormatRatio(result.OriginalBytes, result.OutputBytes),
                    status: result.Status);
                progressBar.Value = Math.Min(100, (int)Math.Round((index + 1) * 100.0 / files.Count));
            }

            summaryLabel.Text = $"完成：{files.Count} 个文件，整体{FormatRatio(originalTotal, outputTotal)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, "压缩失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            isRunning = false;
            UpdateUiState();
        }
    }

    private CompressionOptions BuildOptions()
    {
        return new CompressionOptions(
            ScalePercent: (int)scaleInput.Value,
            MaxWidth: (int)maxWidthInput.Value,
            MaxHeight: (int)maxHeightInput.Value,
            Mode: (OptimizationMode)optimizeMode.SelectedIndex,
            ColorCount: int.Parse((string)colorCount.SelectedItem!),
            Format: ((string)outputFormat.SelectedItem!).Equals("WebP", StringComparison.OrdinalIgnoreCase)
                ? ExportFormat.Webp
                : ExportFormat.Png,
            KeepOnlySmaller: keepSmaller.Checked);
    }

    private void SetRow(int index, string? dimensions = null, string? output = null, string? saved = null, string? status = null)
    {
        if (index < 0 || index >= fileGrid.Rows.Count)
        {
            return;
        }

        var row = fileGrid.Rows[index];
        if (dimensions is not null)
        {
            row.Cells["dimensions"].Value = dimensions;
        }
        if (output is not null)
        {
            row.Cells["output"].Value = output;
        }
        if (saved is not null)
        {
            row.Cells["saved"].Value = saved;
        }
        if (status is not null)
        {
            row.Cells["status"].Value = status;
        }
    }

    private void UpdateUiState()
    {
        addButton.Enabled = !isRunning;
        outputButton.Enabled = !isRunning;
        clearButton.Enabled = !isRunning && files.Count > 0;
        compressButton.Enabled = !isRunning && files.Count > 0;
        compressButton.Text = isRunning ? "压缩中" : "开始压缩";
        outputLabel.Text = string.IsNullOrWhiteSpace(outputDirectory) ? "未选择输出文件夹" : outputDirectory;
        summaryLabel.Text = files.Count == 0 ? "拖入 PNG 或点击“添加 PNG”开始" : $"已添加 {files.Count} 个 PNG";
    }

    [SupportedOSPlatform("windows10.0.17763")]
    private static CompressionResult CompressOne(string sourcePath, string outputDirectory, CompressionOptions options)
    {
        using var source = new Bitmap(sourcePath);
        var targetSize = GetTargetSize(source.Width, source.Height, options);
        using var resized = ResizeBitmap(source, targetSize.Width, targetSize.Height);

        if (options.Mode is OptimizationMode.Color or OptimizationMode.Strong)
        {
            ApplyColorOptimization(resized, options.ColorCount);
        }

        if (options.Mode == OptimizationMode.Strong)
        {
            ClearTransparentPixels(resized);
        }

        var originalBytes = new FileInfo(sourcePath).Length;
        var outputName = BuildOutputName(sourcePath, options.Format);
        var outputPath = Path.Combine(outputDirectory, outputName);

        using var memory = new MemoryStream();
        SaveBitmap(resized, memory, options.Format);
        var outputBytes = memory.Length;

        if (options.KeepOnlySmaller && outputBytes >= originalBytes)
        {
            return new CompressionResult(
                originalBytes,
                originalBytes,
                source.Width,
                source.Height,
                targetSize.Width,
                targetSize.Height,
                "跳过：结果未变小");
        }

        File.WriteAllBytes(outputPath, memory.ToArray());
        return new CompressionResult(
            originalBytes,
            outputBytes,
            source.Width,
            source.Height,
            targetSize.Width,
            targetSize.Height,
            "完成");
    }

    private static string BuildOutputName(string sourcePath, ExportFormat format)
    {
        var extension = format == ExportFormat.Webp ? "webp" : "png";
        return $"{Path.GetFileNameWithoutExtension(sourcePath)}.compressed.{extension}";
    }

    [SupportedOSPlatform("windows10.0.17763")]
    private static void SaveBitmap(Bitmap bitmap, Stream output, ExportFormat format)
    {
        if (format == ExportFormat.Webp)
        {
            bitmap.Save(output, ImageFormat.Webp);
            return;
        }

        bitmap.Save(output, ImageFormat.Png);
    }

    private static Size GetTargetSize(int width, int height, CompressionOptions options)
    {
        var scale = options.ScalePercent / 100.0;
        var maxWidthScale = options.MaxWidth > 0 ? options.MaxWidth / (double)width : 1.0;
        var maxHeightScale = options.MaxHeight > 0 ? options.MaxHeight / (double)height : 1.0;
        var finalScale = Math.Min(Math.Min(scale, maxWidthScale), Math.Min(maxHeightScale, 1.0));

        return new Size(
            Math.Max(1, (int)Math.Round(width * finalScale)),
            Math.Max(1, (int)Math.Round(height * finalScale)));
    }

    private static Bitmap ResizeBitmap(Bitmap source, int width, int height)
    {
        var resized = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        resized.SetResolution(source.HorizontalResolution, source.VerticalResolution);

        using var graphics = Graphics.FromImage(resized);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.SmoothingMode = SmoothingMode.HighQuality;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.DrawImage(source, new Rectangle(0, 0, width, height));
        return resized;
    }

    private static void ApplyColorOptimization(Bitmap bitmap, int colorCount)
    {
        var levels = Math.Max(2, (int)Math.Round(Math.Pow(colorCount, 1.0 / 3.0)));
        var step = 255.0 / (levels - 1);
        ProcessPixels(bitmap, color =>
        {
            if (color.A == 0)
            {
                return Color.FromArgb(0, 0, 0, 0);
            }

            return Color.FromArgb(
                color.A,
                QuantizeChannel(color.R, step),
                QuantizeChannel(color.G, step),
                QuantizeChannel(color.B, step));
        });
    }

    private static int QuantizeChannel(byte value, double step)
    {
        return Math.Clamp((int)Math.Round(Math.Round(value / step) * step), 0, 255);
    }

    private static void ClearTransparentPixels(Bitmap bitmap)
    {
        ProcessPixels(bitmap, color => color.A < 3 ? Color.FromArgb(0, 0, 0, 0) : color);
    }

    private static void ProcessPixels(Bitmap bitmap, Func<Color, Color> transform)
    {
        var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
        var data = bitmap.LockBits(rect, ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        var length = Math.Abs(data.Stride) * bitmap.Height;
        var bytes = new byte[length];
        Marshal.Copy(data.Scan0, bytes, 0, length);

        for (var y = 0; y < bitmap.Height; y++)
        {
            var rowOffset = y * Math.Abs(data.Stride);
            for (var x = 0; x < bitmap.Width; x++)
            {
                var offset = rowOffset + x * 4;
                var color = Color.FromArgb(bytes[offset + 3], bytes[offset + 2], bytes[offset + 1], bytes[offset]);
                var next = transform(color);
                bytes[offset] = next.B;
                bytes[offset + 1] = next.G;
                bytes[offset + 2] = next.R;
                bytes[offset + 3] = next.A;
            }
        }

        Marshal.Copy(bytes, 0, data.Scan0, length);
        bitmap.UnlockBits(data);
    }

    private static bool IsPngFile(string path)
    {
        return File.Exists(path) && string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024)
        {
            return $"{bytes} B";
        }

        string[] units = ["KB", "MB", "GB"];
        var value = bytes / 1024.0;
        var unit = 0;

        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }

    private static string FormatRatio(long original, long output)
    {
        if (original <= 0 || output <= 0)
        {
            return "节省 0%";
        }

        var saved = Math.Max(0, 1 - output / (double)original);
        return $"节省 {saved:P1}";
    }
}

internal enum OptimizationMode
{
    Reencode,
    Color,
    Strong,
}

internal enum ExportFormat
{
    Png,
    Webp,
}

internal sealed record CompressionOptions(
    int ScalePercent,
    int MaxWidth,
    int MaxHeight,
    OptimizationMode Mode,
    int ColorCount,
    ExportFormat Format,
    bool KeepOnlySmaller);

internal sealed record CompressionResult(
    long OriginalBytes,
    long OutputBytes,
    int InputWidth,
    int InputHeight,
    int OutputWidth,
    int OutputHeight,
    string Status);
