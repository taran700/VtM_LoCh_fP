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
        public static bool StorytellerExperiencePrices { get; set; } = false;
        public static string NeonatePurchaseMode { get; set; } = "Свободные пункты затем опыт";
        public static bool ExperiencePurchasesEnabled { get; set; } = true;
        public static bool FreebiePurchasesEnabled { get; set; } = true;
        public static bool UpdateNotificationsEnabled { get; set; } = true;
        public static DateTime? UpdateLastAttemptUtc { get; set; }
        public static DateTime? UpdateLastSuccessUtc { get; set; }
        public static DateTime? UpdateRateLimitUntilUtc { get; set; }
        public static string UpdateLastKnownVersion { get; set; } = string.Empty;
        public static string UpdateLastAssetUrl { get; set; } = string.Empty;
        public static string UpdateLastCheckMessage { get; set; } = string.Empty;

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
                StorytellerExperiencePrices = (bool?)root.Element("StorytellerExperiencePrices") ?? false;
                NeonatePurchaseMode = (string)root.Element("NeonatePurchaseMode") ?? "Свободные пункты затем опыт";
                ExperiencePurchasesEnabled = (bool?)root.Element("ExperiencePurchasesEnabled") ?? true;
                FreebiePurchasesEnabled = (bool?)root.Element("FreebiePurchasesEnabled") ?? true;
                UpdateNotificationsEnabled = (bool?)root.Element("UpdateNotificationsEnabled") ?? true;
                UpdateLastAttemptUtc = ParseUtc((string)root.Element("UpdateLastAttemptUtc"));
                UpdateLastSuccessUtc = ParseUtc((string)root.Element("UpdateLastSuccessUtc"));
                UpdateRateLimitUntilUtc = ParseUtc((string)root.Element("UpdateRateLimitUntilUtc"));
                UpdateLastKnownVersion = (string)root.Element("UpdateLastKnownVersion") ?? string.Empty;
                UpdateLastAssetUrl = (string)root.Element("UpdateLastAssetUrl") ?? string.Empty;
                UpdateLastCheckMessage = (string)root.Element("UpdateLastCheckMessage") ?? string.Empty;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Не удалось загрузить настройки приложения: " + ex);
            }
        }

        private static DateTime? ParseUtc(string value)
        {
            DateTime parsed;
            return DateTime.TryParse(value, null, System.Globalization.DateTimeStyles.RoundtripKind, out parsed)
                ? (DateTime?)parsed.ToUniversalTime() : null;
        }

        private static string FormatUtc(DateTime? value)
        {
            return value.HasValue ? value.Value.ToUniversalTime().ToString("o") : string.Empty;
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
                    new XElement("RuleOfSeven", RuleOfSeven),
                    new XElement("StorytellerExperiencePrices", StorytellerExperiencePrices),
                    new XElement("NeonatePurchaseMode", NeonatePurchaseMode),
                    new XElement("ExperiencePurchasesEnabled", ExperiencePurchasesEnabled),
                    new XElement("FreebiePurchasesEnabled", FreebiePurchasesEnabled),
                    new XElement("UpdateNotificationsEnabled", UpdateNotificationsEnabled),
                    new XElement("UpdateLastAttemptUtc", FormatUtc(UpdateLastAttemptUtc)),
                    new XElement("UpdateLastSuccessUtc", FormatUtc(UpdateLastSuccessUtc)),
                    new XElement("UpdateRateLimitUntilUtc", FormatUtc(UpdateRateLimitUntilUtc)),
                    new XElement("UpdateLastKnownVersion", UpdateLastKnownVersion ?? string.Empty),
                    new XElement("UpdateLastAssetUrl", UpdateLastAssetUrl ?? string.Empty),
                    new XElement("UpdateLastCheckMessage", UpdateLastCheckMessage ?? string.Empty))).Save(FilePath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError("Не удалось сохранить настройки приложения: " + ex);
            }
        }
    }
}
