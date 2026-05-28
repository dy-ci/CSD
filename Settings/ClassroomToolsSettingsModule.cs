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
    public class ClassroomToolsSettingsModule : SettingsModuleBase
    {
        public override string CategoryKey => "classroomTools";
        public override string Title => "课堂工具";
        public override string Description => "管理轮播展示和随机点名等课堂互动工具。";
        public override string Glyph => "\uE8B2";

        private readonly PlaybackSettingsModule _playback = new();
        private readonly RandomPickerSettingsModule _randomPicker = new();

        protected override FrameworkElement BuildContent()
        {
            _playback.Initialize(Context);
            _randomPicker.Initialize(Context);

            _playback.SettingsChanged += () => NotifySettingsChanged();
            _randomPicker.SettingsChanged += () => NotifySettingsChanged();

            var playbackView = _playback.CreateView();
            playbackView.Visibility = Visibility.Visible;
            var randomPickerView = _randomPicker.CreateView();
            randomPickerView.Visibility = Visibility.Visible;

            return SettingsUIHelper.CreateCategoryView(playbackView, randomPickerView);
        }
    }
}
