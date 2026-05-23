using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace WindowsRemoteTools
{
    /// <summary>
    /// Controller for managing and playing audio files
    /// </summary>
    public class AudioController : IDisposable
    {
        private readonly string _audioDirectory;
        private readonly ConfigManager _config;
        private IWavePlayer? _waveOut;
        private AudioFileReader? _audioFileReader;
        private readonly object _playbackLock = new object();
        private bool _isPlaying;
        private bool _isLooping;
        private string? _currentlyPlayingFile;

        public bool IsPlaying => _isPlaying;
        public string? CurrentlyPlayingFile => _currentlyPlayingFile;

        public AudioController(ConfigManager config)
        {
            _config = config;
            _audioDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WindowsRemoteTools",
                "Sounds"
            );

            // Create directory if it doesn't exist
            if (!Directory.Exists(_audioDirectory))
            {
                Directory.CreateDirectory(_audioDirectory);
                Console.WriteLine($"Created audio directory: {_audioDirectory}");
            }
        }

        /// <summary>
        /// Gets the path to the sounds directory
        /// </summary>
        public string AudioDirectory => _audioDirectory;

        /// <summary>
        /// Lists all available audio files
        /// </summary>
        public List<AudioInfo> ListAll()
        {
            var audioFiles = new List<AudioInfo>();

            try
            {
                var extensions = new[] { ".mp3", ".wav", ".ogg", ".flac", ".aac", ".wma", ".m4a" };
                var files = Directory.GetFiles(_audioDirectory)
                    .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                    .OrderBy(f => f);

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        var audioInfo = new AudioInfo
                        {
                            Name = Path.GetFileName(file),
                            Path = file,
                            Size = fileInfo.Length,
                            LastModified = fileInfo.LastWriteTime
                        };

                        // Try to get audio duration
                        try
                        {
                            using (var reader = new AudioFileReader(file))
                            {
                                audioInfo.Duration = reader.TotalTime;
                            }
                        }
                        catch { }

                        audioFiles.Add(audioInfo);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading audio file {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing audio files: {ex.Message}");
            }

            return audioFiles;
        }

        /// <summary>
        /// Plays an audio file
        /// </summary>
        public bool Play(string filename, bool loop = false)
        {
            try
            {
                Stop(); // Stop any currently playing audio

                filename = Path.GetFileName(filename);
                var filePath = Path.Combine(_audioDirectory, filename);

                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Audio file not found: {filename}");
                    return false;
                }

                lock (_playbackLock)
                {
                    _audioFileReader = new AudioFileReader(filePath);
                    _waveOut = new WaveOutEvent();
                    _waveOut.Init(_audioFileReader);

                    _isLooping = loop;
                    _currentlyPlayingFile = filename;

                    if (loop)
                    {
                        _waveOut.PlaybackStopped += (s, e) =>
                        {
                            if (!_isLooping || !_isPlaying) return;

                            // Restart on a thread-pool thread to avoid re-entrancy on
                            // NAudio's audio callback thread and to catch exceptions safely.
                            Task.Run(() =>
                            {
                                try
                                {
                                    lock (_playbackLock)
                                    {
                                        if (_audioFileReader != null && _waveOut != null && _isLooping && _isPlaying)
                                        {
                                            _audioFileReader.Position = 0;
                                            _waveOut.Play();
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine($"Error restarting audio loop: {ex.Message}");
                                    lock (_playbackLock)
                                    {
                                        _isLooping = false;
                                        _isPlaying = false;
                                    }
                                }
                            });
                        };
                    }
                    else
                    {
                        _waveOut.PlaybackStopped += (s, e) =>
                        {
                            lock (_playbackLock)
                            {
                                _isPlaying = false;
                                _currentlyPlayingFile = null;
                            }
                        };
                    }

                    _waveOut.Play();
                    _isPlaying = true;
                    Console.WriteLine($"Playing audio: {filename} (loop: {loop})");
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error playing audio: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stops the currently playing audio
        /// </summary>
        public void Stop()
        {
            IWavePlayer? waveToStop;
            AudioFileReader? readerToDispose;

            lock (_playbackLock)
            {
                _isLooping = false;
                _isPlaying = false;
                _currentlyPlayingFile = null;

                waveToStop = _waveOut;
                _waveOut = null;

                readerToDispose = _audioFileReader;
                _audioFileReader = null;
            }

            // Lock released before Stop/Dispose to prevent deadlock:
            // waveOutClose() can block waiting for PlaybackStopped callbacks,
            // which in turn try to acquire _playbackLock.
            waveToStop?.Stop();
            waveToStop?.Dispose();
            readerToDispose?.Dispose();

            Console.WriteLine("Audio playback stopped");
        }

        /// <summary>
        /// Saves an audio file from byte array
        /// </summary>
        public bool SaveAudio(string filename, byte[] data)
        {
            try
            {
                // Sanitize filename
                filename = Path.GetFileName(filename);
                var filePath = Path.Combine(_audioDirectory, filename);

                File.WriteAllBytes(filePath, data);
                Console.WriteLine($"Audio saved: {filename} ({data.Length} bytes)");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving audio: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Saves an audio file from base64 string
        /// </summary>
        public bool SaveAudioBase64(string filename, string base64Data)
        {
            try
            {
                var data = Convert.FromBase64String(base64Data);
                return SaveAudio(filename, data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error decoding base64 audio: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deletes an audio file
        /// </summary>
        public bool DeleteAudio(string filename)
        {
            try
            {
                filename = Path.GetFileName(filename);
                var filePath = Path.Combine(_audioDirectory, filename);

                if (File.Exists(filePath))
                {
                    // Stop if currently playing
                    if (_currentlyPlayingFile == filename)
                    {
                        Stop();
                    }

                    File.Delete(filePath);
                    Console.WriteLine($"Audio deleted: {filename}");
                    return true;
                }
                else
                {
                    Console.WriteLine($"Audio not found: {filename}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting audio: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets an audio file by filename
        /// </summary>
        public byte[]? GetAudio(string filename)
        {
            try
            {
                filename = Path.GetFileName(filename);
                var filePath = Path.Combine(_audioDirectory, filename);

                if (File.Exists(filePath))
                {
                    return File.ReadAllBytes(filePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading audio: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Gets the full path to an audio file
        /// </summary>
        public string GetAudioPath(string filename)
        {
            filename = Path.GetFileName(filename);
            return Path.Combine(_audioDirectory, filename);
        }

        /// <summary>
        /// Checks if an audio file exists
        /// </summary>
        public bool AudioExists(string filename)
        {
            filename = Path.GetFileName(filename);
            var filePath = Path.Combine(_audioDirectory, filename);
            return File.Exists(filePath);
        }

        /// <summary>
        /// Gets playback information
        /// </summary>
        public PlaybackInfo GetPlaybackInfo()
        {
            lock (_playbackLock)
            {
                return new PlaybackInfo
                {
                    IsPlaying = _isPlaying,
                    IsLooping = _isLooping,
                    CurrentFile = _currentlyPlayingFile,
                    Position = _audioFileReader?.CurrentTime ?? TimeSpan.Zero,
                    Duration = _audioFileReader?.TotalTime ?? TimeSpan.Zero
                };
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }

    /// <summary>
    /// Information about an audio file
    /// </summary>
    public class AudioInfo
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public TimeSpan Duration { get; set; }
    }

    /// <summary>
    /// Information about current playback state
    /// </summary>
    public class PlaybackInfo
    {
        public bool IsPlaying { get; set; }
        public bool IsLooping { get; set; }
        public string? CurrentFile { get; set; }
        public TimeSpan Position { get; set; }
        public TimeSpan Duration { get; set; }
    }
}
