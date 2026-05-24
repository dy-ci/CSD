using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

using CSD.Services;

namespace CSD.Helpers
{
    internal static class PerformanceFormatter
    {
        public static SolidColorBrush GetScoreBrush(int score)
        {
            return score switch
            {
                <= 40 => new SolidColorBrush(ColorHelper.FromArgb(255, 0xFF, 0x45, 0x00)),
                <= 60 => new SolidColorBrush(ColorHelper.FromArgb(255, 0xFF, 0x8C, 0x00)),
                <= 80 => new SolidColorBrush(ColorHelper.FromArgb(255, 0x8B, 0xC3, 0x4A)),
                _ => new SolidColorBrush(ColorHelper.FromArgb(255, 0x32, 0xCD, 0x32))
            };
        }

        public static string FormatCpuText(List<CpuData> cpus)
        {
            if (cpus.Count == 0)
                return "未检测到处理器";

            return string.Join("\n", cpus.Select((c, i) =>
                $"[{i + 1}] {c.Model}\n    {c.Cores} 核心 / {c.LogicalProcessors} 线程 | {c.Frequency} | L2: {c.L2CacheKb / 1024.0:F1} MB | L3: {c.L3CacheKb / 1024.0:F1} MB"));
        }

        public static string FormatGpuText(List<GpuData> gpus)
        {
            if (gpus.Count == 0)
                return "未检测到显卡";

            return string.Join("\n", gpus.Select((g, i) =>
                $"[{i + 1}] {g.Name}\n    显存: {g.VramGb:F1} GB | 驱动: {g.DriverVersion}"));
        }

        public static string FormatRamText(PerformanceInfo info)
        {
            return $"总计: {info.RamTotalGb:F1} GB | 可用: {info.RamAvailableGb:F1} GB\n" +
                   $"类型: {info.RamType} {info.RamSpeed} MHz | 插槽使用: {info.RamSlotsUsed}";
        }

        public static string FormatStorageText(List<DriveData> drives, List<LogicalDriveData> logicalDrives)
        {
            var phys = drives.Select((d, i) =>
                $"[物理磁盘] {d.Name} ({d.MediaType}) - {d.TotalGb:F0} GB");
            var log = logicalDrives.Select(d =>
                $"[逻辑分区] {d.Letter} {d.AvailableGb:F0} GB 可用 / {d.TotalGb:F0} GB 总计");
            return string.Join("\n", phys) + "\n" + string.Join("\n", log);
        }

        public static string FormatDisplayText(List<DisplayData> displays)
        {
            if (displays.Count == 0)
                return "未检测到显示器";

            return string.Join("\n", displays.Select((d, i) =>
                $"[显示器 {i + 1}] {d.Resolution} @ {d.RefreshRate} Hz"));
        }

        public static string FormatOsText(PerformanceInfo info)
        {
            return $"{info.OsVersion} | 架构: {info.OsArchitecture}";
        }

        public static string FormatProcessesText(int totalProcesses, double bgRatio)
        {
            return $"{(bgRatio * 100):F1}% (总进程数 {totalProcesses})";
        }

        public static string FormatAppUsageText(double memoryMb)
        {
            return $"内存占用: {memoryMb:F1} MB";
        }

        public static string GetOptimizeButtonText(int score)
        {
            return score switch
            {
                <= 40 => "一键性能优化 (老设备专属)",
                <= 60 => "一键性能优化 (推荐)",
                _ => "关闭部分特效以提升性能"
            };
        }

        public static (bool PageAnim, bool InterAnim, bool Blur, bool HighRate, bool HighRes) GetOptimizationPresets(int score)
        {
            return score switch
            {
                <= 40 => (false, false, false, false, false),
                <= 60 => (true, true, false, true, false),
                _ => (true, true, true, true, true)
            };
        }
    }
}
