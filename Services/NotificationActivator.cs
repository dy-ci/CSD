using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Web;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace CSD.Services
{
    [Obsolete("请使用 NotificationService 替代。ToastHelper 基于旧版 Windows.UI.Notifications，"
        + "新代码应统一走 NotificationService (AppNotification API)。")]
    public static class ToastHelper
    {
        private const string Aumid = "CSD.ClassworksDesktop";
        internal const string ActivatorClsid = "6D4B3F8A-1C2E-4A5D-9B7C-0E8F3A2D5C1B";
        private static bool _initialized;
        private static bool _toastAvailable = true;

        [Obsolete("使用 NotificationService.Instance.Initialize() 替代。")]
        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;
            try
            {
                RegisterActivator();
                CreateStartMenuShortcut();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ToastHelper.Initialize failed: " + ex.Message);
            }
        }

        /// <summary>
        /// 确保 unpackaged 模式下 AUMID 注册完整（Start Menu 快捷方式和注册表项）。
        /// 由 NotificationService.Initialize() 调用，替代旧的 ToastHelper.Initialize()。
        /// </summary>
        public static void EnsureAumidRegistration()
        {
            try
            {
                RegisterActivator();
                CreateStartMenuShortcut();
                Debug.WriteLine("ToastHelper.EnsureAumidRegistration completed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ToastHelper.EnsureAumidRegistration failed: " + ex.Message);
            }
        }

        private static void RegisterActivator()
        {
            var clsidKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                $"Software\\Classes\\CLSID\\{{{ActivatorClsid}}}");
            clsidKey.SetValue("AppID", $"{{{ActivatorClsid}}}");

            var appIdKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                $"Software\\Classes\\AppID\\{{{ActivatorClsid}}}");
            appIdKey.SetValue("AppUserModelID", Aumid);

            var aumidKey = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(
                $"Software\\Classes\\AppUserModelId\\{Aumid}");
            aumidKey.SetValue("DisplayName", "CSD - Classworks Desktop");
            aumidKey.SetValue("IconUri", Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico"));
            aumidKey.SetValue("IconBackgroundColor", "transparent");
        }

        private static void CreateStartMenuShortcut()
        {
            var startMenuPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "Windows", "Start Menu", "Programs", "CSD");
            Directory.CreateDirectory(startMenuPath);
            var shortcutPath = Path.Combine(startMenuPath, "CSD.lnk");

            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;
            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic shortcut = shell.CreateShortcut(shortcutPath);
            shortcut.TargetPath = Process.GetCurrentProcess().MainModule!.FileName;
            shortcut.Description = "CSD - Classworks Desktop";
            shortcut.Save();

            SetShortcutAppUserModelId(shortcutPath, Aumid);
        }

        private static void SetShortcutAppUserModelId(string shortcutPath, string aumid)
        {
            var propertyStore = GetPropertyStoreFromFile(shortcutPath);
            if (propertyStore == null) return;

            var key = new PropertyKey(
                new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"), 5);
            var value = new PropVariant(aumid);
            try
            {
                propertyStore.SetValue(ref key, ref value);
                propertyStore.Commit();
            }
            finally
            {
                Marshal.FreeCoTaskMem(value.pointerValue);
                Marshal.ReleaseComObject(propertyStore);
            }
        }

        private static IPropertyStore? GetPropertyStoreFromFile(string path)
        {
            var guid = typeof(IPropertyStore).GUID;
            var hr = SHGetPropertyStoreFromParsingName(
                path, IntPtr.Zero, 0, ref guid, out var store);
            return hr == 0 ? store : null;
        }

        public static void ShowToast(string title, string message, string? notificationId)
        {
            if (!_toastAvailable)
            {
                Debug.WriteLine("ToastHelper: toast not available, using tray fallback");
                App.TrayService?.ShowNotification(title, message);
                return;
            }

            try
            {
                var xml = $@"
<toast>
  <visual>
    <binding template='ToastGeneric'>
      <text>{EscapeXml(title)}</text>
      <text>{EscapeXml(message)}</text>
    </binding>
  </visual>";

                if (!string.IsNullOrEmpty(notificationId))
                {
                    xml += $@"
  <actions>
    <action content='已读' arguments='action=read&notificationId={notificationId}' activationType='foreground'/>
  </actions>";
                }

                xml += "\n</toast>";

                var doc = new XmlDocument();
                doc.LoadXml(xml);
                var toast = new ToastNotification(doc);
                ToastNotificationManager.CreateToastNotifier(Aumid).Show(toast);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ToastHelper.ShowToast failed: " + ex.Message);
                _toastAvailable = false;
                App.TrayService?.ShowNotification(title, message);
            }
        }

        private static string EscapeXml(string text)
        {
            return text.Replace("&", "&amp;").Replace("<", "&lt;")
                .Replace(">", "&gt;").Replace("'", "&apos;")
                .Replace("\"", "&quot;");
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetPropertyStoreFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            uint flags,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppv);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IPropertyStore
    {
        void GetCount(out uint cProps);
        void GetAt(uint iProp, out PropertyKey pkey);
        void GetValue(ref PropertyKey pkey, out PropVariant pv);
        void SetValue(ref PropertyKey pkey, ref PropVariant pv);
        void Commit();
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct PropertyKey
    {
        public Guid fmtid;
        public uint pid;
        public PropertyKey(Guid guid, uint id) { fmtid = guid; pid = id; }
    }

    [StructLayout(LayoutKind.Explicit)]
    internal struct PropVariant
    {
        [FieldOffset(0)] private ushort vt;
        [FieldOffset(8)] public IntPtr pointerValue;

        public PropVariant(string value)
        {
            vt = (ushort)VarEnum.VT_LPWSTR;
            pointerValue = Marshal.StringToCoTaskMemUni(value);
        }
    }

    [ComImport]
    [Guid("53E31837-6600-4A81-9395-75CFFE746F94")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface INotificationActivationCallback
    {
        void Activate(
            [MarshalAs(UnmanagedType.LPWStr)] string appUserModelId,
            [MarshalAs(UnmanagedType.LPWStr)] string invokedArgs,
            IntPtr data,
            uint count);
    }

    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
#pragma warning disable CS0618 // ToastHelper 保留旧 COM 激活器兼容性
    [Guid(ToastHelper.ActivatorClsid)]
#pragma warning restore CS0618
    public class NotificationActivator : INotificationActivationCallback
    {
        public void Activate(string appUserModelId, string invokedArgs, IntPtr data, uint count)
        {
            if (string.IsNullOrEmpty(invokedArgs))
                return;

            try
            {
                var args = HttpUtility.ParseQueryString(invokedArgs);
                var action = args["action"];
                var notificationId = args["notificationId"];

                if (action == "read" && !string.IsNullOrEmpty(notificationId))
                {
                    _ = SocketIoService.Instance.SendEventAsync("notification-read", new
                    {
                        eventId = $"read-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}",
                        notificationId = notificationId,
                        deviceInfo = new { deviceName = "桌面端", deviceType = "desktop" }
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("NotificationActivator failed: " + ex.Message);
            }
        }
    }
}
