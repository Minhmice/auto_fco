using System;
using System.Drawing;
using System.IO;

namespace FCZ.Core.Configuration
{
    public static class AppConfig
    {
        private static readonly string AppDataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "FCZAutoDebugger");

        public static string TemplatesPath { get; } = Path.Combine(AppDataRoot, "Templates");
        public static string ScenariosPath { get; } = Path.Combine(AppDataRoot, "Scenarios");
        public static string LogsPath { get; } = Path.Combine(AppDataRoot, "logs");

        public static Size DefaultWindowSize { get; } = new Size(1600, 900);

        static AppConfig()
        {
            EnsureDirectoriesExist();
        }

        public static void EnsureDirectoriesExist()
        {
            Directory.CreateDirectory(TemplatesPath);
            Directory.CreateDirectory(ScenariosPath);
            Directory.CreateDirectory(LogsPath);
        }
    }
}

