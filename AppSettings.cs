using System;
using System.IO;
using System.Xml.Linq;

namespace VtM_LoCh_fP
{
    internal static class AppSettings
    {
        private static readonly string FilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VtM_LoCh_fP", "settings.xml");
        public static bool DarkTheme { get; set; }
        public static bool GenerationPointIncrease { get; set; } = true;
        public static string WoundHealingMode { get; set; } = "Работать Гуль!";
        public static bool RuleOfSeven { get; set; } = true;

        public static void Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return;
                var root = XDocument.Load(FilePath).Root;
                DarkTheme = (bool?)root.Element("DarkTheme") ?? false;
                GenerationPointIncrease = (bool?)root.Element("GenerationPointIncrease") ?? ((string)root.Element("PointMode") == "Усиленные");
                WoundHealingMode = (string)root.Element("WoundHealingMode") ?? "Работать Гуль!";
                RuleOfSeven = (bool?)root.Element("RuleOfSeven") ?? true;
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
                    new XElement("GenerationPointIncrease", GenerationPointIncrease),
                    new XElement("WoundHealingMode", WoundHealingMode),
                    new XElement("RuleOfSeven", RuleOfSeven))).Save(FilePath);
            }
            catch { }
        }
    }
}
