using System;
using System.IO;
using Windows.Media.Core;
using Windows.Media.Playback;

namespace CSD.Services
{
    public static class SoundService
    {
        private static MediaPlayer? _mediaPlayer;

        public static void PlaySound(string fileName, bool loop = false)
        {
            try
            {
                if (_mediaPlayer == null)
                {
                    _mediaPlayer = new MediaPlayer();
                }

                var filePath = Path.Combine(AppContext.BaseDirectory, "Assets", "sounds", fileName);
                if (File.Exists(filePath))
                {
                    _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri("file:///" + filePath.Replace('\\', '/')));
                    _mediaPlayer.IsLoopingEnabled = loop;
                    _mediaPlayer.Play();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to play sound {fileName}: {ex.Message}");
            }
        }

        public static void PlayAbsolutePathSound(string absolutePath, bool loop = false)
        {
            try
            {
                if (_mediaPlayer == null)
                    _mediaPlayer = new MediaPlayer();
                if (File.Exists(absolutePath))
                {
                    _mediaPlayer.Source = MediaSource.CreateFromUri(new Uri("file:///" + absolutePath.Replace('\\', '/')));
                    _mediaPlayer.IsLoopingEnabled = loop;
                    _mediaPlayer.Play();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to play sound {absolutePath}: {ex.Message}");
            }
        }

        public static void StopSound()
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.Pause();
                _mediaPlayer.Source = null;
            }
        }
    }
}