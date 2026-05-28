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
    public class StartupAndShutdownSettingsModule : SettingsModuleBase
    {
        public override string CategoryKey => "startupAndShutdown";
        public override string Title => "启动与关闭";
        public override string Description => "配置开机自启动和关闭主窗口时的行为。";
        public override string Glyph => "\uE7E8";

        private readonly AutoStartSettingsModule _autoStart = new();
        private readonly CloseBehaviorSettingsModule _closeBehavior = new();

        protected override FrameworkElement BuildContent()
        {
            _autoStart.Initialize(Context);
            _closeBehavior.Initialize(Context);

            _autoStart.SettingsChanged += () => NotifySettingsChanged();
            _closeBehavior.SettingsChanged += () => NotifySettingsChanged();

            var autoStartView = _autoStart.CreateView();
            autoStartView.Visibility = Visibility.Visible;
            var closeBehaviorView = _closeBehavior.CreateView();
            closeBehaviorView.Visibility = Visibility.Visible;

            return SettingsUIHelper.CreateCategoryView(autoStartView, closeBehaviorView);
        }
    }
}
