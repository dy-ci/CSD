using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace CSD.Services
{
    public class GpuData
    {
        public string Name { get; set; } = "未知";
        public double VramGb { get; set; }
        public string DriverVersion { get; set; } = "未知";
    }

    public class DriveData
    {
        public string Name { get; set; } = "未知";
        public string MediaType { get; set; } = "未知";
        public double TotalGb { get; set; }
    }

    public class LogicalDriveData
    {
        public string Letter { get; set; } = "";
        public double TotalGb { get; set; }
        public double AvailableGb { get; set; }
    }

    public class CpuData
    {
        public string Model { get; set; } = "未知";
        public int Cores { get; set; }
        public int LogicalProcessors { get; set; }
        public string Frequency { get; set; } = "未知";
        public uint MaxClockSpeedMhz { get; set; }
        public uint L2CacheKb { get; set; }
        public uint L3CacheKb { get; set; }
    }

    public class DisplayData
    {
        public string Resolution { get; set; } = "未知";
        public int RefreshRate { get; set; }
    }

    public class PerformanceInfo
    {
        public string Motherboard { get; set; } = "未知";
        public string BiosVersion { get; set; } = "未知";

        public List<CpuData> Cpus { get; set; } = new();

        public double RamTotalGb { get; set; }
        public double RamAvailableGb { get; set; }
        public uint RamSpeed { get; set; }
        public int RamSlotsUsed { get; set; }
        public string RamType { get; set; } = "未知";

        public List<GpuData> Gpus { get; set; } = new();

        public List<DriveData> Drives { get; set; } = new();
        public List<LogicalDriveData> LogicalDrives { get; set; } = new();

        public List<DisplayData> Displays { get; set; } = new();

        public string OsVersion { get; set; } = "未知";
        public string OsArchitecture { get; set; } = "未知";
        public string BrowserInfo { get; set; } = "未知";
        public int RunningServicesCount { get; set; }
        public int TotalProcessesCount { get; set; }
        public double BackgroundProcessRatio { get; set; }

        public double AppMemoryMb { get; set; }
        public double AppCpuUsage { get; set; }

        public int Score { get; set; }
        public string Rating { get; set; } = "";
        public string RatingDescription { get; set; } = "";
    }

    public static class PerformanceService
    {
        public static PerformanceInfo GetPerformanceInfo()
        {
            var info = new PerformanceInfo();
            var exceptions = new ConcurrentBag<Exception>();

            try
            {
                Parallel.Invoke(new ParallelOptions { MaxDegreeOfParallelism = 4 },
                    () => SafeQuery(() => QueryMotherboard(info), exceptions),
                    () => SafeQuery(() => QueryCpu(info), exceptions),
                    () => SafeQuery(() => QueryRamCapacity(info), exceptions),
                    () => SafeQuery(() => QueryRamDetails(info), exceptions),
                    () => SafeQuery(() => QueryGpu(info), exceptions),
                    () => SafeQuery(() => QueryStorage(info), exceptions),
                    () => SafeQuery(() => QueryDisplay(info), exceptions),
                    () => SafeQuery(() => QuerySoftware(info), exceptions)
                );

                CalculateScore(info);

                if (!exceptions.IsEmpty)
                {
                    Debug.WriteLine($"[PerformanceService] {exceptions.Count} WMI query(s) failed:");
                    foreach (var ex in exceptions)
                        Debug.WriteLine($"  {ex.GetType().Name}: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PerformanceService] Fatal error: {ex.Message}");
            }

            return info;
        }

        private static void SafeQuery(Action action, ConcurrentBag<Exception> exceptions)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exceptions.Add(ex);
            }
        }

        private static void QueryMotherboard(PerformanceInfo info)
        {
            using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard"))
            using (var results = searcher.Get())
            {
                foreach (var obj in results)
                {
                    info.Motherboard = $"{obj["Manufacturer"]} {obj["Product"]}".Trim();
                    break;
                }
            }

            using (var searcher = new ManagementObjectSearcher("SELECT SMBIOSBIOSVersion FROM Win32_BIOS"))
            using (var results = searcher.Get())
            {
                foreach (var obj in results)
                {
                    info.BiosVersion = obj["SMBIOSBIOSVersion"]?.ToString() ?? "未知";
                    break;
                }
            }
        }

        private static void QueryCpu(PerformanceInfo info)
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed, L2CacheSize, L3CacheSize FROM Win32_Processor");
            using var results = searcher.Get();

            foreach (var obj in results)
            {
                var rawFreq = Convert.ToDouble(obj["MaxClockSpeed"] ?? 0);

                info.Cpus.Add(new CpuData
                {
                    Model = obj["Name"]?.ToString() ?? "未知",
                    Cores = Convert.ToInt32(obj["NumberOfCores"] ?? 0),
                    LogicalProcessors = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0),
                    Frequency = (rawFreq / 1000.0).ToString("0.00") + " GHz",
                    MaxClockSpeedMhz = (uint)rawFreq,
                    L2CacheKb = Convert.ToUInt32(obj["L2CacheSize"] ?? 0),
                    L3CacheKb = Convert.ToUInt32(obj["L3CacheSize"] ?? 0)
                });
            }
        }

        private static void QueryRamCapacity(PerformanceInfo info)
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            using var results = searcher.Get();

            foreach (var obj in results)
            {
                info.RamTotalGb = Convert.ToDouble(obj["TotalVisibleMemorySize"] ?? 0) / 1024.0 / 1024.0;
                info.RamAvailableGb = Convert.ToDouble(obj["FreePhysicalMemory"] ?? 0) / 1024.0 / 1024.0;
                break;
            }
        }

        private static void QueryRamDetails(PerformanceInfo info)
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Speed, SMBIOSMemoryType FROM Win32_PhysicalMemory");
            using var results = searcher.Get();

            int maxSlots = 0;
            foreach (var obj in results)
            {
                info.RamSlotsUsed++;
                if (info.RamSpeed == 0)
                    info.RamSpeed = Convert.ToUInt32(obj["Speed"] ?? 0);

                int smbiosType = Convert.ToInt32(obj["SMBIOSMemoryType"] ?? 0);
                maxSlots++;
            }

            if (maxSlots > 0)
            {
                if (maxSlots % 2 == 0 && info.RamSlotsUsed >= 2)
                {
                    int typeCode = 0;
                    using var searcher2 = new ManagementObjectSearcher(
                        "SELECT SMBIOSMemoryType FROM Win32_PhysicalMemory");
                    using var results2 = searcher2.Get();
                    foreach (var obj in results2)
                    {
                        typeCode = Convert.ToInt32(obj["SMBIOSMemoryType"] ?? 0);
                        break;
                    }
                    if (typeCode > 0)
                    {
                        info.RamType = typeCode switch
                        {
                            20 => "DDR",
                            21 => "DDR2",
                            24 => "DDR3",
                            26 => "DDR4",
                            34 or 35 => "DDR5",
                            _ => "未知类型"
                        };
                        return;
                    }
                }

                using var searcher3 = new ManagementObjectSearcher(
                    "SELECT SMBIOSMemoryType FROM Win32_PhysicalMemory");
                using var results3 = searcher3.Get();
                foreach (var obj in results3)
                {
                    int typeCode = Convert.ToInt32(obj["SMBIOSMemoryType"] ?? 0);
                    info.RamType = typeCode switch
                    {
                        20 => "DDR",
                        21 => "DDR2",
                        24 => "DDR3",
                        26 => "DDR4",
                        34 or 35 => "DDR5",
                        _ => "未知类型"
                    };
                    break;
                }
            }
        }

        private static void QueryGpu(PerformanceInfo info)
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, AdapterRAM, DriverVersion FROM Win32_VideoController");
            using var results = searcher.Get();

            foreach (var obj in results)
            {
                long vram = Convert.ToInt64(obj["AdapterRAM"] ?? 0);
                double vramGb = vram / 1024.0 / 1024.0 / 1024.0;
                if (vramGb < 0) vramGb = 0;

                info.Gpus.Add(new GpuData
                {
                    Name = obj["Name"]?.ToString() ?? "未知",
                    VramGb = vramGb,
                    DriverVersion = obj["DriverVersion"]?.ToString() ?? "未知"
                });
            }
        }

        private static void QueryStorage(PerformanceInfo info)
        {
            bool hasPhysicalDrives = false;

            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"\\.\root\microsoft\windows\storage",
                    "SELECT FriendlyName, Size, MediaType FROM MSFT_PhysicalDisk");
                using var results = searcher.Get();

                foreach (var obj in results)
                {
                    int mediaType = Convert.ToInt32(obj["MediaType"] ?? 0);
                    info.Drives.Add(new DriveData
                    {
                        Name = obj["FriendlyName"]?.ToString() ?? "未知",
                        TotalGb = Convert.ToUInt64(obj["Size"] ?? 0) / 1024.0 / 1024.0 / 1024.0,
                        MediaType = mediaType switch
                        {
                            4 => "SSD",
                            3 => "HDD",
                            5 => "NVMe/SCM",
                            _ => "未知"
                        }
                    });
                    hasPhysicalDrives = true;
                }
            }
            catch
            {
                // Fallback to Win32_DiskDrive
            }

            if (!hasPhysicalDrives)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT Model, Size FROM Win32_DiskDrive");
                    using var results = searcher.Get();

                    foreach (var obj in results)
                    {
                        info.Drives.Add(new DriveData
                        {
                            Name = obj["Model"]?.ToString() ?? "未知",
                            TotalGb = Convert.ToUInt64(obj["Size"] ?? 0) / 1024.0 / 1024.0 / 1024.0,
                            MediaType = "未知"
                        });
                    }
                }
                catch { }
            }

            try
            {
                foreach (var d in DriveInfo.GetDrives()
                    .Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                {
                    info.LogicalDrives.Add(new LogicalDriveData
                    {
                        Letter = d.Name,
                        TotalGb = d.TotalSize / 1024.0 / 1024.0 / 1024.0,
                        AvailableGb = d.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0
                    });
                }
            }
            catch { }
        }

        private static void QueryDisplay(PerformanceInfo info)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate FROM Win32_VideoController");
                using var results = searcher.Get();

                foreach (var obj in results)
                {
                    int width = Convert.ToInt32(obj["CurrentHorizontalResolution"] ?? 0);
                    int height = Convert.ToInt32(obj["CurrentVerticalResolution"] ?? 0);
                    int refresh = Convert.ToInt32(obj["CurrentRefreshRate"] ?? 0);
                    if (width > 0 && height > 0)
                    {
                        info.Displays.Add(new DisplayData
                        {
                            Resolution = $"{width} x {height}",
                            RefreshRate = refresh > 0 ? refresh : 60
                        });
                    }
                }
            }
            catch { }

            if (info.Displays.Count == 0)
            {
                try
                {
                    using var searcher = new ManagementObjectSearcher(
                        "SELECT ScreenWidth, ScreenHeight FROM Win32_DesktopMonitor");
                    using var results = searcher.Get();

                    foreach (var obj in results)
                    {
                        int width = Convert.ToInt32(obj["ScreenWidth"] ?? 0);
                        int height = Convert.ToInt32(obj["ScreenHeight"] ?? 0);
                        if (width > 0 && height > 0)
                        {
                            info.Displays.Add(new DisplayData
                            {
                                Resolution = $"{width} x {height}",
                                RefreshRate = 60
                            });
                        }
                    }
                }
                catch { }
            }
        }

        private static void QuerySoftware(PerformanceInfo info)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Caption, Version, OSArchitecture FROM Win32_OperatingSystem");
                using var results = searcher.Get();

                foreach (var obj in results)
                {
                    info.OsVersion = $"{obj["Caption"]} ({obj["Version"]})";
                    info.OsArchitecture = obj["OSArchitecture"]?.ToString() ?? "未知";
                    break;
                }
            }
            catch
            {
                info.OsVersion = RuntimeInformation.OSDescription;
            }

            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(
                    @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice");
                if (key != null)
                {
                    var progId = key.GetValue("ProgId")?.ToString();
                    if (progId != null)
                    {
                        if (progId.Contains("Chrome")) info.BrowserInfo = "Google Chrome";
                        else if (progId.Contains("Firefox")) info.BrowserInfo = "Mozilla Firefox";
                        else if (progId.Contains("MSEdge")) info.BrowserInfo = "Microsoft Edge";
                        else info.BrowserInfo = progId;
                    }
                }
            }
            catch { }

            try
            {
                using var procSearcher = new ManagementObjectSearcher(
                    "SELECT SessionId FROM Win32_Process");
                using var procResults = procSearcher.Get();

                int total = 0, session0 = 0;
                foreach (var obj in procResults)
                {
                    total++;
                    if (Convert.ToInt32(obj["SessionId"] ?? -1) == 0)
                        session0++;
                }
                info.TotalProcessesCount = total;
                info.BackgroundProcessRatio = total > 0 ? (double)session0 / total : 0;
            }
            catch { }

            try
            {
                using var svcSearcher = new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_Service WHERE State='Running'");
                using var svcResults = svcSearcher.Get();

                int count = 0;
                foreach (var _ in svcResults) count++;
                info.RunningServicesCount = count;
            }
            catch { }

            try
            {
                using var currentProcess = Process.GetCurrentProcess();
                info.AppMemoryMb = currentProcess.WorkingSet64 / 1024.0 / 1024.0;
                info.AppCpuUsage = 0;
            }
            catch { }
        }

        private static void CalculateScore(PerformanceInfo info)
        {
            double score = 0;

            int totalLogicalProcessors = info.Cpus.Sum(c => c.LogicalProcessors);
            double avgFreqMhz = info.Cpus.Count > 0
                ? info.Cpus.Average(c => c.MaxClockSpeedMhz)
                : 0;
            score += Math.Min(30, totalLogicalProcessors * 2 + (avgFreqMhz / 1000.0) * 2);

            score += Math.Min(25, info.RamTotalGb * 1.5);

            double maxVram = info.Gpus.Count > 0 ? info.Gpus.Max(g => g.VramGb) : 0;
            score += Math.Min(15, maxVram * 2 + 5);

            bool hasSsd = info.Drives.Any(d => d.MediaType == "SSD" || d.MediaType == "NVMe/SCM");
            score += hasSsd ? 10 : 2;
            double totalAvailable = info.LogicalDrives.Sum(d => d.AvailableGb);
            score += Math.Min(5, totalAvailable > 50 ? 5 : totalAvailable / 10);

            double swScore = 15;
            swScore -= info.BackgroundProcessRatio * 10;
            if (info.RamAvailableGb < 2) swScore -= 5;
            score += Math.Max(0, swScore);

            info.Score = (int)Math.Clamp(Math.Round(score), 0, 100);

            if (info.Score <= 20)
            {
                info.Rating = "性能极差";
                info.RatingDescription = "无法流畅运行基础功能";
            }
            else if (info.Score <= 40)
            {
                info.Rating = "性能较差";
                info.RatingDescription = "仅支持基础功能运行";
            }
            else if (info.Score <= 60)
            {
                info.Rating = "性能中等";
                info.RatingDescription = "可支撑常规功能使用";
            }
            else if (info.Score <= 80)
            {
                info.Rating = "性能良好";
                info.RatingDescription = "可流畅运行绝大多数功能";
            }
            else
            {
                info.Rating = "性能优异";
                info.RatingDescription = "可承载高负载场景运行";
            }
        }
    }
}
