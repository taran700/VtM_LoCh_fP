using System;
using System.IO;
using System.Xml.Linq;

namespace VtM_LoCh_fP
{
    internal static class AppSettings
    {
        private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VtM_LoCh_fP", "settings.xml");
        public static bool DarkTheme { get; set; }
        public static string PointMode { get; set; } = "Базовые";
        public static string WoundHealingMode { get; set; } = "Работать Гуль!";

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                var root = XDocument.Load(FilePath).Root;
                DarkTheme = (bool?)root.Element("DarkTheme") ?? false;
                PointMode = (string)root.Element("PointMode") ?? "Базовые";
                WoundHealingMode = (string)root.Element("WoundHealingMode") ?? "Работать Гуль!";
            }
            catch { }
        }

        public static void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                new XDocument(new XElement("Settings",
                    new XElement("DarkTheme", DarkTheme),
                    new XElement("PointMode", PointMode),
                    new XElement("WoundHealingMode", WoundHealingMode))).Save(FilePath);
            }
            catch { }
        }
    }
}
