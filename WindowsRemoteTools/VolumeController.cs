using System;
using System.Threading;
using System.Threading.Tasks;
using NAudio.CoreAudioApi;

namespace WindowsRemoteTools
{
    /// <summary>
    /// Controls Windows system volume with enforcement capabilities
    /// </summary>
    public class VolumeController : IDisposable
    {
        private readonly ConfigManager _config;
        private MMDeviceEnumerator? _deviceEnumerator;
        private MMDevice? _device;
        private CancellationTokenSource? _monitoringCts;
        private Task? _monitoringTask;

        public bool _monitoring { get; private set; }

        public VolumeController(ConfigManager config)
        {
            _config = config;
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                _deviceEnumerator = new MMDeviceEnumerator();
                _device = _deviceEnumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            }
            catch (Exception ex)
            {
                // Audio device may not be available in Session 0 (service context) — run without it
                Console.WriteLine($"Audio device not available (service context?): {ex.Message}");
            }
        }

        public float GetVolume()
        {
            try
            {
                return _device?.AudioEndpointVolume.MasterVolumeLevelScalar ?? 0f;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GetVolume COM error: {ex.Message}");
                return 0f;
            }
        }

        public int GetVolumePercent()
        {
            return (int)(GetVolume() * 100);
        }

        public void SetVolume(float level)
        {
            if (_device == null) return;

            try
            {
                // Clamp to valid range
                level = Math.Max(0f, Math.Min(1f, level));

                // Apply max volume limit if enforced
                if (_config.EnforceMaxVolume)
                {
                    float maxLevel = _config.MaxVolume / 100f;
                    level = Math.Min(level, maxLevel);
                }

                _device.AudioEndpointVolume.MasterVolumeLevelScalar = level;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting volume: {ex.Message}");
            }
        }

        public void SetVolumePercent(int percent)
        {
            SetVolume(percent / 100f);
        }

        public bool GetMute()
        {
            return _device?.AudioEndpointVolume.Mute ?? false;
        }

        public void SetMute(bool mute)
        {
            if (_device == null) return;

            try
            {
                _device.AudioEndpointVolume.Mute = mute;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error setting mute: {ex.Message}");
            }
        }

        public void ToggleMute()
        {
            SetMute(!GetMute());
        }

        public void EnforceMaxVolumeOnce()
        {
            if (_config.EnforceMaxVolume)
            {
                int current = GetVolumePercent();
                if (current > _config.MaxVolume)
                {
                    SetVolumePercent(_config.MaxVolume);
                    Console.WriteLine($"Volume limited: {current}% -> {_config.MaxVolume}%");
                }
            }
        }

        public void StartMonitoring()
        {
            if (_monitoring) return;

            _monitoring = true;
            _monitoringCts = new CancellationTokenSource();
            _monitoringTask = Task.Run(() => MonitorVolume(_monitoringCts.Token));
            Console.WriteLine("Volume monitoring started");
        }

        public void StopMonitoring()
        {
            if (!_monitoring) return;

            _monitoring = false;
            _monitoringCts?.Cancel();
            try
            {
                _monitoringTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException ex)
            {
                Console.WriteLine($"Volume monitoring stopped with errors: {ex.InnerException?.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"StopMonitoring error: {ex.Message}");
            }
            Console.WriteLine("Volume monitoring stopped");
        }

        private async Task MonitorVolume(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    EnforceMaxVolumeOnce();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Volume monitoring error: {ex.Message}");
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(_config.VolumeCheckInterval), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        public void SetMaxVolumeLimit(int maxPercent)
        {
            maxPercent = Math.Max(0, Math.Min(100, maxPercent));
            _config.MaxVolume = maxPercent;
            _config.Save();
            Console.WriteLine($"Maximum volume set to {maxPercent}%");
            EnforceMaxVolumeOnce();
        }

        public void EnableEnforcement(bool enable)
        {
            _config.EnforceMaxVolume = enable;
            _config.Save();
            Console.WriteLine($"Volume enforcement {(enable ? "enabled" : "disabled")}");
        }

        public void Dispose()
        {
            StopMonitoring();
            _monitoringCts?.Dispose();
            _device?.Dispose();
            _deviceEnumerator?.Dispose();
        }
    }
}
