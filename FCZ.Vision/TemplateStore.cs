using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using OpenCvSharp;

namespace FCZ.Vision
{
    public class TemplateStore
    {
        private readonly string _basePath;
        private readonly Dictionary<string, Mat> _cache = new Dictionary<string, Mat>();

        public TemplateStore(string basePath)
        {
            _basePath = basePath;
            if (!Directory.Exists(_basePath))
            {
                Directory.CreateDirectory(_basePath);
            }
        }

        public Mat? GetTemplate(string name)
        {
            if (_cache.TryGetValue(name, out var cached))
            {
                return cached;
            }

            string filePath = Path.Combine(_basePath, name);
            if (!File.Exists(filePath))
            {
                return null;
            }

            try
            {
                var mat = Cv2.ImRead(filePath, ImreadModes.Color);
                if (mat.Empty())
                {
                    return null;
                }

                _cache[name] = mat;
                return mat;
            }
            catch
            {
                return null;
            }
        }

        public void ClearCache()
        {
            foreach (var mat in _cache.Values)
            {
                mat?.Dispose();
            }
            _cache.Clear();
        }
    }
}

