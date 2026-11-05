using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;

namespace VtM_LoCh_fP
{
    internal sealed class TrackDescription
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<TrackLevelDescription> Levels { get; set; }
    }

    internal sealed class TrackLevelDescription
    {
        public int Level { get; set; }
        public string Rank { get; set; }
        public string Description { get; set; }
    }

    internal static class TrackDescriptionService
    {
        private static Dictionary<string, TrackDescription> _items;

        public static TrackDescription Find(string name)
        {
            if (_items == null) Load();
            TrackDescription result;
            return !string.IsNullOrWhiteSpace(name) && _items.TryGetValue(name.Trim(), out result) ? result : null;
        }

        private static void Load()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "data.json");
            if (!File.Exists(path)) throw new FileNotFoundException("Не найден файл базы данных Data\\data.json.", path);
            var serializer = new JavaScriptSerializer();
            var database = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            var result = new Dictionary<string, TrackDescription>(StringComparer.OrdinalIgnoreCase);
            LoadSection(database, "attributes", result);
            LoadSection(database, "abilities", result);
            _items = result;
        }

        private static void LoadSection(Dictionary<string, object> database, string section, Dictionary<string, TrackDescription> target)
        {
            object raw;
            if (!database.TryGetValue(section, out raw)) return;
            var entries = raw as IList;
            if (entries == null) return;
            foreach (var entryObject in entries)
            {
                var entry = entryObject as Dictionary<string, object>;
                if (entry == null) continue;
                string name = Read(entry, "name");
                if (string.IsNullOrWhiteSpace(name)) continue;
                var info = new TrackDescription { Name = name, Description = Clean(Read(entry, "description")), Levels = new List<TrackLevelDescription>() };
                var levels = entry.ContainsKey("levels") ? entry["levels"] as IList : null;
                if (levels != null)
                {
                    foreach (var levelObject in levels)
                    {
                        var level = levelObject as Dictionary<string, object>;
                        if (level == null) continue;
                        int number;
                        int.TryParse(Read(level, "level"), out number);
                        info.Levels.Add(new TrackLevelDescription { Level = number, Rank = Read(level, "rank"), Description = Clean(Read(level, "description")) });
                    }
                }
                target[name] = info;
            }
        }

        private static string Clean(string value)
        {
            return Regex.Replace(value ?? string.Empty, @"\s*\[cite:\s*[^\]]+\]", string.Empty, RegexOptions.IgnoreCase).Trim();
        }

        private static string Read(Dictionary<string, object> source, string key)
        {
            object value;
            return source.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : string.Empty;
        }
    }
}
