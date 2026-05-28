using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;


using CSD.Views;
using CSD.Models;
using CSD.Services;
using CSD.Helpers;
using CSD.Settings;


namespace CSD.Settings
{
    public class DisplayAndNotificationSettingsModule : SettingsModuleBase
    {
        public override string CategoryKey => "displayAndNotification";
        public override string Title => "显示与通知";
        public override string Description => "调整首页卡片的显示外观和通知音效。";
        public override string Glyph => "\uE7F8";

        private readonly DisplaySettingsModule _display = new();
        private readonly NotificationSettingsModule _notification = new();

        protected override FrameworkElement BuildContent()
        {
            _display.Initialize(Context);
            _notification.Initialize(Context);

            _display.SettingsChanged += () => NotifySettingsChanged();
            _notification.SettingsChanged += () => NotifySettingsChanged();

            var displayView = _display.CreateView();
            displayView.Visibility = Visibility.Visible;
            var notificationView = _notification.CreateView();
            notificationView.Visibility = Visibility.Visible;

            return SettingsUIHelper.CreateCategoryView(displayView, notificationView);
        }
    }
}
