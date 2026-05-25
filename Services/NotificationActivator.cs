using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Web;
using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;

namespace CSD.Services
{
    public static class ToastHelper
    {
        private const string Aumid = "CSD.ClassworksDesktop";
        internal const string ActivatorClsid = "6D4B3F8A-1C2E-4A5D-9B7C-0E8F3A2D5C1B";
        private static bool _initialized;
        private static bool _toastAvailable = true;

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

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHGetPropertyStoreFromParsingName(
            [MarshalAs(UnmanagedType.LPWStr)] string pszPath,
            IntPtr pbc,
            uint flags,
            ref Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IPropertyStore ppv);

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
                var builder = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(message);

                if (!string.IsNullOrEmpty(notificationId))
                {
                    builder.AddButton(new AppNotificationButton("已读")
                        .AddArgument("action", "read")
                        .AddArgument("notificationId", notificationId));
                }

                AppNotificationManager.Default.Show(builder.BuildNotification());
            }
            catch (Exception ex)
            {
                Debug.WriteLine("ToastHelper.ShowToast failed: " + ex.Message);
                _toastAvailable = false;
                App.TrayService?.ShowNotification(title, message);
            }
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

    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    [Guid(ToastHelper.ActivatorClsid)]
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
