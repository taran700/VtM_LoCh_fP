using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;

namespace VtM_LoCh_fP
{
    internal sealed class BioFactDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool CanBeShared { get; set; }
        public bool SabbatOnly { get; set; }
        public bool CaitiffRestricted { get; set; }
        public List<TrackLevelDescription> Levels { get; set; }
        public override string ToString() => Name;
    }

    internal static class BioFactService
    {
        private static List<BioFactDefinition> _items;

        public static List<BioFactDefinition> GetAll()
        {
            if (_items == null) Load();
            return new List<BioFactDefinition>(_items);
        }

        public static BioFactDefinition Find(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            if (_items == null) Load();
            return _items.Find(x => string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static void Load()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "data.json");
            if (!File.Exists(path)) throw new FileNotFoundException("Не найден файл базы данных Data\\data.json.", path);
            var serializer = new JavaScriptSerializer();
            var database = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            object raw;
            var result = new List<BioFactDefinition>();
            if (database.TryGetValue("backgrounds", out raw) && raw is IList entries)
            {
                foreach (var obj in entries)
                {
                    var entry = obj as Dictionary<string, object>;
                    if (entry == null) continue;
                    var item = new BioFactDefinition
                    {
                        Id = Read(entry, "id"), Name = Read(entry, "name"),
                        Description = Clean(Read(entry, "description")),
                        CanBeShared = ReadBool(entry, "can_be_shared"),
                        SabbatOnly = ReadBool(entry, "sabbat_only"),
                        CaitiffRestricted = ReadBool(entry, "caitiff_restricted"),
                        Levels = new List<TrackLevelDescription>()
                    };
                    var levels = entry.ContainsKey("levels") ? entry["levels"] as IList : null;
                    if (levels != null)
                    {
                        foreach (var levelObj in levels)
                        {
                            var level = levelObj as Dictionary<string, object>;
                            if (level == null) continue;
                            int number; int.TryParse(Read(level, "level"), out number);
                            item.Levels.Add(new TrackLevelDescription { Level = number, Rank = Read(level, "rank"), Description = Clean(Read(level, "description")) });
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(item.Name)) result.Add(item);
                }
            }
            _items = result;
        }

        private static string Read(Dictionary<string, object> source, string key)
        {
            object value;
            return source.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : string.Empty;
        }
        private static bool ReadBool(Dictionary<string, object> source, string key)
        {
            object value;
            return source.TryGetValue(key, out value) && value != null && Convert.ToBoolean(value);
        }
        private static string Clean(string value) => Regex.Replace(value ?? string.Empty, @"\s*\[cite:\s*[^\]]+\]", string.Empty, RegexOptions.IgnoreCase).Trim();
    }
}
