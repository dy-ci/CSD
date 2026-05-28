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
    public class EditAndRefreshSettingsModule : SettingsModuleBase
    {
        public override string CategoryKey => "editAndRefresh";
        public override string Title => "编辑与刷新";
        public override string Description => "配置作业编辑行为和自动刷新策略。";
        public override string Glyph => "\uE72C";

        private readonly EditSettingsModule _edit = new();
        private readonly RefreshSettingsModule _refresh = new();

        protected override FrameworkElement BuildContent()
        {
            _edit.Initialize(Context);
            _refresh.Initialize(Context);

            _edit.SettingsChanged += () => NotifySettingsChanged();
            _refresh.SettingsChanged += () => NotifySettingsChanged();

            var editView = _edit.CreateView();
            editView.Visibility = Visibility.Visible;
            var refreshView = _refresh.CreateView();
            refreshView.Visibility = Visibility.Visible;

            return SettingsUIHelper.CreateCategoryView(editView, refreshView);
        }
    }
}
