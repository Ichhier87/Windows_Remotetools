using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace WindowsRemoteTools
{
    /// <summary>
    /// Controller for managing and displaying pictures
    /// </summary>
    public class PictureController : IDisposable
    {
        private readonly string _pictureDirectory;
        private readonly ConfigManager _config;

        public PictureController(ConfigManager config)
        {
            _config = config;
            _pictureDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "WindowsRemoteTools",
                "Pictures"
            );

            // Create directory if it doesn't exist
            if (!Directory.Exists(_pictureDirectory))
            {
                Directory.CreateDirectory(_pictureDirectory);
                Console.WriteLine($"Created picture directory: {_pictureDirectory}");
            }
        }

        /// <summary>
        /// Gets the path to the pictures directory
        /// </summary>
        public string PictureDirectory => _pictureDirectory;

        /// <summary>
        /// Lists all available pictures
        /// </summary>
        public List<PictureInfo> ListAll()
        {
            var pictures = new List<PictureInfo>();

            try
            {
                var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
                var files = Directory.GetFiles(_pictureDirectory)
                    .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
                    .OrderBy(f => f);

                foreach (var file in files)
                {
                    try
                    {
                        var fileInfo = new FileInfo(file);
                        var pictureInfo = new PictureInfo
                        {
                            Name = Path.GetFileName(file),
                            Path = file,
                            Size = fileInfo.Length,
                            LastModified = fileInfo.LastWriteTime
                        };

                        // Try to get image dimensions
                        try
                        {
                            using (var img = Image.FromFile(file))
                            {
                                pictureInfo.Width = img.Width;
                                pictureInfo.Height = img.Height;
                            }
                        }
                        catch { }

                        pictures.Add(pictureInfo);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error reading picture {file}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error listing pictures: {ex.Message}");
            }

            return pictures;
        }

        /// <summary>
        /// Saves a picture from base64 data
        /// </summary>
        public bool SavePicture(string filename, byte[] data)
        {
            try
            {
                // Sanitize filename
                filename = Path.GetFileName(filename);
                var filePath = Path.Combine(_pictureDirectory, filename);

                File.WriteAllBytes(filePath, data);
                Console.WriteLine($"Picture saved: {filename} ({data.Length} bytes)");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving picture: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Saves a picture from base64 string
        /// </summary>
        public bool SavePictureBase64(string filename, string base64Data)
        {
            try
            {
                var data = Convert.FromBase64String(base64Data);
                return SavePicture(filename, data);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error decoding base64 picture: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deletes a picture
        /// </summary>
        public bool DeletePicture(string filename)
        {
            try
            {
                filename = Path.GetFileName(filename);
                var filePath = Path.Combine(_pictureDirectory, filename);

                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Console.WriteLine($"Picture deleted: {filename}");
                    return true;
                }
                else
                {
                    Console.WriteLine($"Picture not found: {filename}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error deleting picture: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets a picture by filename
        /// </summary>
        public byte[]? GetPicture(string filename)
        {
            try
            {
                filename = Path.GetFileName(filename);
                var filePath = Path.Combine(_pictureDirectory, filename);

                if (File.Exists(filePath))
                {
                    return File.ReadAllBytes(filePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading picture: {ex.Message}");
            }
            return null;
        }

        /// <summary>
        /// Gets a picture as base64 string
        /// </summary>
        public string? GetPictureBase64(string filename)
        {
            var data = GetPicture(filename);
            return data != null ? Convert.ToBase64String(data) : null;
        }

        /// <summary>
        /// Generates a thumbnail for a picture
        /// </summary>
        public byte[]? GenerateThumbnail(string filename, int maxWidth = 200, int maxHeight = 200)
        {
            try
            {
                filename = Path.GetFileName(filename);
                var filePath = Path.Combine(_pictureDirectory, filename);

                if (!File.Exists(filePath))
                    return null;

                using (var originalImage = Image.FromFile(filePath))
                {
                    // Calculate thumbnail size maintaining aspect ratio
                    var ratioX = (double)maxWidth / originalImage.Width;
                    var ratioY = (double)maxHeight / originalImage.Height;
                    var ratio = Math.Min(ratioX, ratioY);

                    var newWidth = (int)(originalImage.Width * ratio);
                    var newHeight = (int)(originalImage.Height * ratio);

                    using (var thumbnail = new Bitmap(newWidth, newHeight))
                    {
                        using (var graphics = Graphics.FromImage(thumbnail))
                        {
                            graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                            graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
                            graphics.DrawImage(originalImage, 0, 0, newWidth, newHeight);
                        }

                        using (var ms = new MemoryStream())
                        {
                            thumbnail.Save(ms, ImageFormat.Jpeg);
                            return ms.ToArray();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error generating thumbnail: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the full path to a picture file
        /// </summary>
        public string GetPicturePath(string filename)
        {
            filename = Path.GetFileName(filename);
            return Path.Combine(_pictureDirectory, filename);
        }

        /// <summary>
        /// Checks if a picture exists
        /// </summary>
        public bool PictureExists(string filename)
        {
            filename = Path.GetFileName(filename);
            var filePath = Path.Combine(_pictureDirectory, filename);
            return File.Exists(filePath);
        }

        public void Dispose()
        {
            // Cleanup if needed
        }
    }

    /// <summary>
    /// Information about a picture file
    /// </summary>
    public class PictureInfo
    {
        public string Name { get; set; } = "";
        public string Path { get; set; } = "";
        public long Size { get; set; }
        public DateTime LastModified { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
    }
}
