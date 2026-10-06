using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Runtime.InteropServices;

namespace Cam4TrackingMonitor.Diagnostics;

internal static class RuntimeForegroundProbe
{
    [DllImport("user32.dll")]
    private static extern bool SetCursorPos(int x, int y);
    private static readonly string[] Targets = ["ETH_LINK", "RX_TOTAL", "SYNC_TOTAL", "FREED_TOTAL"];

    public static async Task RunAsync(Window window)
    {
        var foregroundProbe = string.Equals(Environment.GetEnvironmentVariable("CAM4_FOREGROUND_PROBE"), "1", StringComparison.Ordinal);
        var tabProbe = string.Equals(Environment.GetEnvironmentVariable("CAM4_TAB_PROBE"), "1", StringComparison.Ordinal);
        if (!foregroundProbe && !tabProbe) return;
        var directory = Path.Combine(AppContext.BaseDirectory, "ForegroundProbe");
        Directory.CreateDirectory(directory);
        var log = new StringBuilder();
        var tabs = FindVisualChildren<TabControl>(window).First();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        if (tabProbe)
        {
            await RunTabProbe(window, tabs, directory, log);
            await File.WriteAllTextAsync(Path.Combine(directory, "tab-runtime.log"), log.ToString(), Encoding.UTF8);
            return;
        }
        Capture(window, tabs, "01_INITIAL_DASHBOARD", directory, log);
        if (int.TryParse(Environment.GetEnvironmentVariable("CAM4_FOREGROUND_CYCLES"), out var cycles) && cycles > 0)
        {
            var checks = 0;
            var mismatches = 0;
            for (var cycle = 1; cycle <= cycles; cycle++)
            {
                tabs.SelectedIndex = 1; await DelayUi(); tabs.SelectedIndex = 0; await DelayUi();
                mismatches += ValidateTargets(window, log, cycle, "AFTER_CONFIGURATION", ref checks);
                tabs.SelectedIndex = 2; await DelayUi(); tabs.SelectedIndex = 0; await DelayUi();
                mismatches += ValidateTargets(window, log, cycle, "AFTER_EVENTS", ref checks);
            }
            Capture(window, tabs, "04_AFTER_100_CYCLES", directory, log);
            log.AppendLine($"SUMMARY cycles={cycles} checks={checks} mismatches={mismatches}");
            await File.WriteAllTextAsync(Path.Combine(directory, "foreground-runtime.log"), log.ToString(), Encoding.UTF8);
            return;
        }
        tabs.SelectedIndex = 1;
        await DelayUi();
        tabs.SelectedIndex = 0;
        await DelayUi();
        Capture(window, tabs, "02_AFTER_CONFIGURATION", directory, log);
        tabs.SelectedIndex = 2;
        await DelayUi();
        tabs.SelectedIndex = 0;
        await DelayUi();
        Capture(window, tabs, "03_AFTER_EVENTS", directory, log);
        await File.WriteAllTextAsync(Path.Combine(directory, "foreground-runtime.log"), log.ToString(), Encoding.UTF8);
    }

    private static async Task RunTabProbe(Window window, TabControl tabs, string directory, StringBuilder log)
    {
        var prefix = Environment.GetEnvironmentVariable("CAM4_TAB_PROBE_PREFIX") ?? "TAB";
        for (var selectedIndex = 0; selectedIndex < tabs.Items.Count; selectedIndex++)
        {
            tabs.SelectedIndex = selectedIndex;
            await DelayUi();
            for (var hoverIndex = 0; hoverIndex < tabs.Items.Count; hoverIndex++)
            {
                var hoverTab = (TabItem)tabs.Items[hoverIndex];
                var point = hoverTab.PointToScreen(new Point(hoverTab.ActualWidth / 2, hoverTab.ActualHeight / 2));
                SetCursorPos((int)point.X, (int)point.Y);
                await DelayUi();
                log.AppendLine($"COMBINATION selected={selectedIndex} hover={hoverIndex}");
                for (var index = 0; index < tabs.Items.Count; index++)
                {
                    var tab = (TabItem)tabs.Items[index];
                    var border = tab.Template.FindName("TabBorder", tab) as Border;
                    log.AppendLine($"  TAB index={index} header={tab.Header} IsSelected={tab.IsSelected} IsMouseOver={tab.IsMouseOver} Background={DescribeBrush(tab.Background)} Foreground={DescribeBrush(tab.Foreground)} BorderBackground={DescribeBrush(border?.Background)} BackgroundSource={DescribeSource(tab, Control.BackgroundProperty)} ForegroundSource={DescribeSource(tab, Control.ForegroundProperty)} BorderBackgroundSource={(border is null ? "NOT_FOUND" : DescribeSource(border, Panel.BackgroundProperty))}");
                }
                SaveScreenshot(window, Path.Combine(directory, $"{prefix}_S{selectedIndex}_H{hoverIndex}.png"));
            }
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("CAM4_TAB_CYCLES"), out var cycles) && cycles > 0)
        {
            var checks = 0;
            var mismatches = 0;
            for (var cycle = 1; cycle <= cycles; cycle++)
            {
                foreach (var (selectedIndex, hoverIndex) in new[] { (0, 1), (1, 2), (2, 0), (0, 2) })
                {
                    tabs.SelectedIndex = selectedIndex;
                    await DelayUi(25);
                    var hoverTab = (TabItem)tabs.Items[hoverIndex];
                    var point = hoverTab.PointToScreen(new Point(hoverTab.ActualWidth / 2, hoverTab.ActualHeight / 2));
                    SetCursorPos((int)point.X, (int)point.Y);
                    await DelayUi(25);
                    mismatches += ValidateTabVisuals(tabs, selectedIndex, hoverIndex, cycle, log, ref checks);
                    if (selectedIndex == 0)
                        mismatches += ValidateTargets(window, log, cycle, $"TAB_STRESS_HOVER_{hoverIndex}", ref checks);
                }
            }
            log.AppendLine($"TAB_STRESS cycles={cycles} checks={checks} mismatches={mismatches}");
        }
    }

    private static int ValidateTabVisuals(TabControl tabs, int selectedIndex, int hoverIndex, int cycle, StringBuilder log, ref int checks)
    {
        var mismatches = 0;
        var normal = ((SolidColorBrush)Application.Current.FindResource("SurfaceRaised")).Color;
        var selected = ((SolidColorBrush)Application.Current.FindResource("Selection")).Color;
        var hover = Color.FromRgb(0x1C, 0x34, 0x45);
        var white = Colors.White;
        for (var index = 0; index < tabs.Items.Count; index++)
        {
            checks++;
            var tab = (TabItem)tabs.Items[index];
            var border = tab.Template.FindName("TabBorder", tab) as Border;
            var expectedBackground = index == selectedIndex ? selected : index == hoverIndex ? hover : normal;
            var actualBackground = (border?.Background as SolidColorBrush)?.Color;
            var headerPresenter = tab.Template.FindName("HeaderPresenter", tab) as ContentPresenter;
            var actualForeground = headerPresenter?.GetValue(TextElement.ForegroundProperty) as SolidColorBrush;
            if (actualBackground != expectedBackground || actualForeground?.Color != white)
            {
                mismatches++;
                log.AppendLine($"TAB_MISMATCH cycle={cycle} selected={selectedIndex} hover={hoverIndex} tab={index} expectedBackground=#{expectedBackground.A:X2}{expectedBackground.R:X2}{expectedBackground.G:X2}{expectedBackground.B:X2} actualBackground={DescribeBrush(border?.Background)} actualHeaderForeground={DescribeBrush(actualForeground)}");
            }
        }
        return mismatches;
    }

    private static int ValidateTargets(Window window, StringBuilder log, int cycle, string stage, ref int checks)
    {
        var mismatches = 0;
        var expected = (SolidColorBrush)Application.Current.FindResource("TextPrimary");
        var blocks = FindVisualChildren<TextBlock>(window).ToList();
        foreach (var target in Targets)
        {
            checks++;
            var block = blocks.FirstOrDefault(item => GetText(item).Contains(target, StringComparison.Ordinal));
            var actual = block?.Foreground as SolidColorBrush;
            if (actual?.Color != expected.Color)
            {
                mismatches++;
                log.AppendLine($"MISMATCH cycle={cycle} stage={stage} target={target} expected={DescribeBrush(expected)} actual={DescribeBrush(actual)}");
            }
        }
        return mismatches;
    }

    private static async Task DelayUi(int milliseconds = 400)
    {
        await Task.Delay(milliseconds);
        await Application.Current.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
    }

    private static void Capture(Window window, TabControl tabs, string stage, string directory, StringBuilder log)
    {
        log.AppendLine($"STAGE={stage} TIME={DateTime.Now:O}");
        foreach (var resourceName in new[] { "TextPrimary", "TextSecondary", "MutedBrush", "Accent", "AccentBrush" })
            log.AppendLine($"RESOURCE {resourceName}={DescribeBrush(Application.Current.TryFindResource(resourceName))}");
        foreach (TabItem tab in tabs.Items)
            log.AppendLine($"TAB header={tab.Header} selected={tab.IsSelected} foreground={DescribeBrush(tab.Foreground)} source={DescribeSource(tab, Control.ForegroundProperty)}");

        var textBlocks = FindVisualChildren<TextBlock>(window).ToList();
        foreach (var target in Targets)
        {
            var block = textBlocks.FirstOrDefault(item => GetText(item).Contains(target, StringComparison.Ordinal));
            if (block is null) { log.AppendLine($"TARGET={target} NOT_FOUND"); continue; }
            log.AppendLine($"TARGET={target} TYPE={block.GetType().FullName} TEXT={GetText(block)} FOREGROUND={DescribeBrush(block.Foreground)} SOURCE={DescribeSource(block, TextElement.ForegroundProperty)} STYLE={DescribeStyle(block.Style)}");
            var runIndex = 0;
            foreach (var run in block.Inlines.OfType<Run>())
                log.AppendLine($"  RUN[{runIndex++}] text={run.Text} foreground={DescribeBrush(run.Foreground)} source={DescribeSource(run, TextElement.ForegroundProperty)} style={DescribeStyle(run.Style)}");
            var ancestor = VisualTreeHelper.GetParent(block);
            var level = 0;
            while (ancestor is not null && level++ < 12)
            {
                var foreground = ancestor switch { Control c => DescribeBrush(c.Foreground), TextBlock t => DescribeBrush(t.Foreground), _ => "N/A" };
                log.AppendLine($"  ANCESTOR[{level}] type={ancestor.GetType().Name} foreground={foreground}");
                ancestor = VisualTreeHelper.GetParent(ancestor);
            }
        }
        SaveScreenshot(window, Path.Combine(directory, stage + ".png"));
        log.AppendLine();
    }

    private static string DescribeSource(DependencyObject item, DependencyProperty property)
    {
        var source = DependencyPropertyHelper.GetValueSource(item, property);
        return $"BaseValueSource={source.BaseValueSource}; IsExpression={source.IsExpression}; IsAnimated={source.IsAnimated}; IsCoerced={source.IsCoerced}; IsCurrent={source.IsCurrent}";
    }

    private static string DescribeStyle(Style? style)
        => style is null ? "NULL" : $"TargetType={style.TargetType.FullName}; BasedOn={style.BasedOn?.TargetType.FullName ?? "NULL"}; Setters={style.Setters.Count}; Triggers={style.Triggers.Count}";
    private static string DescribeBrush(object? value)
        => value is SolidColorBrush brush ? $"#{brush.Color.A:X2}{brush.Color.R:X2}{brush.Color.G:X2}{brush.Color.B:X2}" : value?.ToString() ?? "NULL";
    private static string GetText(TextBlock block) => new TextRange(block.ContentStart, block.ContentEnd).Text.Trim();

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match) yield return match;
            foreach (var nested in FindVisualChildren<T>(child)) yield return nested;
        }
    }

    private static void SaveScreenshot(Window window, string path)
    {
        var width = Math.Max(1, (int)window.ActualWidth);
        var height = Math.Max(1, (int)window.ActualHeight);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }
}
