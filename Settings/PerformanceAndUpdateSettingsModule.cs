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
    public class PerformanceAndUpdateSettingsModule : SettingsModuleBase
    {
        public override string CategoryKey => "performanceAndUpdate";
        public override string Title => "性能与更新";
        public override string Description => "检测设备性能、优化视觉效果，并配置更新渠道。";
        public override string Glyph => "\uE773";

        private readonly PerformanceSettingsModule _performance = new();
        private readonly UpdateSettingsModule _update = new();

        protected override FrameworkElement BuildContent()
        {
            _performance.Initialize(Context);
            _update.Initialize(Context);

            _performance.SettingsChanged += () => NotifySettingsChanged();
            _update.SettingsChanged += () => NotifySettingsChanged();

            var performanceView = _performance.CreateView();
            performanceView.Visibility = Visibility.Visible;
            var updateView = _update.CreateView();
            updateView.Visibility = Visibility.Visible;

            return SettingsUIHelper.CreateCategoryView(performanceView, updateView);
        }
    }
}
