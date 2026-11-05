using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;

namespace VtM_LoCh_fP
{
    public sealed class HumanityPathSinEntry
    {
        public int Level { get; set; }
        public string Sin { get; set; }
        public string Rationale { get; set; }
    }

    internal sealed class HumanityPathDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Description { get; set; }
        public string BearingName { get; set; }
        public string BearingEffect { get; set; }
        public string Edicts { get; set; }
        public string FirstVirtue { get; set; } = "Совесть";
        public string SecondVirtue { get; set; } = "Самоконтроль";
        public List<TrackLevelDescription> Levels { get; set; } = new List<TrackLevelDescription>();
        public List<HumanityPathSinEntry> Hierarchy { get; set; } = new List<HumanityPathSinEntry>();
        public override string ToString() { return Name; }
    }

    internal static class HumanityPathService
    {
        private static List<HumanityPathDefinition> _items;
        public static List<HumanityPathDefinition> GetAll()
        {
            if (_items == null) Load();
            return new List<HumanityPathDefinition>(_items);
        }
        public static HumanityPathDefinition Find(string id)
        {
            if (_items == null) Load();
            return _items.Find(x => string.Equals(x.Id, id, StringComparison.OrdinalIgnoreCase));
        }
        public static HumanityPathDefinition FindByName(string name)
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
            var db = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            object raw;
            var items = new List<HumanityPathDefinition>();
            if (db.TryGetValue("humanity_paths", out raw) && raw is IList list)
            {
                foreach (var value in list)
                {
                    var entry = value as Dictionary<string, object>;
                    if (entry == null) continue;
                    var item = new HumanityPathDefinition
                    {
                        Id = Read(entry, "id"), Name = Read(entry, "name"), Type = Read(entry, "type"),
                        Description = Clean(Read(entry, "description")), Levels = new List<TrackLevelDescription>(),
                        Hierarchy = new List<HumanityPathSinEntry>()
                    };
                    object virtuesRaw;
                    if (entry.TryGetValue("virtues", out virtuesRaw) && virtuesRaw is IList virtues && virtues.Count >= 2)
                    {
                        item.FirstVirtue = VirtueName(Convert.ToString(virtues[0]));
                        item.SecondVirtue = VirtueName(Convert.ToString(virtues[1]));
                    }
                    object bearing;
                    if (entry.TryGetValue("bearing", out bearing) && bearing is Dictionary<string, object> b)
                    {
                        item.BearingName = Read(b, "name");
                        item.BearingEffect = Clean(Read(b, "effect"));
                    }
                    object ethics;
                    if (entry.TryGetValue("ethics", out ethics) && ethics is IList ethicsList)
                        item.Edicts = string.Join(Environment.NewLine, ethicsList.Cast<object>().Select(x => Clean(Convert.ToString(x))).Where(x => !string.IsNullOrWhiteSpace(x)));
                    object levels;
                    if (entry.TryGetValue("levels", out levels) && levels is IList levelList)
                    {
                        foreach (var levelValue in levelList)
                        {
                            var level = levelValue as Dictionary<string, object>;
                            if (level == null) continue;
                            int number;
                            int.TryParse(Read(level, "level"), out number);
                            item.Levels.Add(new TrackLevelDescription { Level = number, Rank = Read(level, "rank"), Description = Clean(Read(level, "description")) });
                        }
                    }
                    object hierarchy;
                    if (entry.TryGetValue("hierarchy", out hierarchy) && hierarchy is IList hierarchyList)
                    {
                        foreach (var rowValue in hierarchyList)
                        {
                            var row = rowValue as Dictionary<string, object>;
                            if (row == null) continue;
                            int number;
                            int.TryParse(Read(row, "level"), out number);
                            var sin = new HumanityPathSinEntry { Level = number, Sin = Clean(Read(row, "sin")), Rationale = Clean(Read(row, "rationale")) };
                            item.Hierarchy.Add(sin);
                            if (item.Levels.All(x => x.Level != number))
                                item.Levels.Add(new TrackLevelDescription { Level = number, Rank = "Грех", Description = sin.Sin + (string.IsNullOrWhiteSpace(sin.Rationale) ? string.Empty : Environment.NewLine + sin.Rationale) });
                        }
                    }
                    item.Levels = item.Levels.OrderBy(x => x.Level).ToList();
                    if (!string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Name))
                    {
                        items.Add(item);
                        object variants;
                        if (entry.TryGetValue("variant_paths", out variants) && variants is IList variantList)
                        {
                            foreach (var variantValue in variantList)
                            {
                                var variant = variantValue as Dictionary<string, object>;
                                if (variant == null) continue;
                                var variantId = Read(variant, "id");
                                var variantName = Read(variant, "name");
                                if (string.IsNullOrWhiteSpace(variantId) || string.IsNullOrWhiteSpace(variantName)) continue;
                                items.Add(new HumanityPathDefinition
                                {
                                    Id = variantId, Name = variantName, Type = "path",
                                    Description = Clean(Read(variant, "description")),
                                    FirstVirtue = item.FirstVirtue, SecondVirtue = item.SecondVirtue, BearingName = item.BearingName, BearingEffect = item.BearingEffect,
                                    Edicts = item.Edicts, Levels = new List<TrackLevelDescription>(item.Levels),
                                    Hierarchy = item.Hierarchy.Select(x => new HumanityPathSinEntry { Level = x.Level, Sin = x.Sin, Rationale = x.Rationale }).ToList()
                                });
                            }
                        }
                    }
                }
            }
            _items = items;
        }
        private static string VirtueName(string value)
        {
            switch ((value ?? "").ToLowerInvariant())
            { case "conviction": return "Решимость"; case "instinct": return "Инстинкты"; case "self_control": return "Самоконтроль"; case "conscience": return "Совесть"; default: return value; }
        }
        private static string Read(Dictionary<string, object> source, string key)
        {
            object value;
            return source.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : string.Empty;
        }
        private static string Clean(string value) { return Regex.Replace(value ?? string.Empty, @"\s*\[cite:\s*[^\]]+\]", string.Empty, RegexOptions.IgnoreCase).Trim(); }
    }
}
