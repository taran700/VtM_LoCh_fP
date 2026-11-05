using System;
using System.IO;
using System.Xml.Linq;

namespace VtM_LoCh_fP
{
    internal static class AppSettings
    {
        private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VtM_LoCh_fP", "settings.xml");
        public static bool DarkTheme { get; set; }
        public static string DisciplineMode { get; set; } = "Готовые";
        public static string PointMode { get; set; } = "Базовые";

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                var root = XDocument.Load(FilePath).Root;
                DarkTheme = (bool?)root.Element("DarkTheme") ?? false;
                DisciplineMode = (string)root.Element("DisciplineMode") ?? "Готовые";
                PointMode = (string)root.Element("PointMode") ?? "Базовые";
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
                    new XElement("DisciplineMode", DisciplineMode),
                    new XElement("PointMode", PointMode))).Save(FilePath);
            }
            catch { }
        }
    }
}
