using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace VtM_LoCh_fP
{
    internal sealed class CombatManeuverEntry
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Note { get; set; }
        public string Category { get; set; }
        public Dictionary<string, string> Stats { get; set; }
        public IList<Dictionary<string, string>> TargetModifiers { get; set; }
        public string Source { get; set; }
        public object Page { get; set; }
        public IList<string> RelatedIds { get; set; }
    }

    internal static class CombatManeuverService
    {
        private static readonly string[] CategoryOrder = { "defensive", "melee", "ranged" };

        public static IList<CombatManeuverEntry> GetAll()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "data.json");
            if (!File.Exists(path))
                throw new FileNotFoundException("Не найдена база боевых манёвров Data\\data.json.", path);

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
            var root = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            object rawValue;
            if (root == null || !root.TryGetValue("combat_maneuvers", out rawValue) || !(rawValue is ArrayList))
                return new List<CombatManeuverEntry>();

            var result = new List<CombatManeuverEntry>();
            foreach (object value in (ArrayList)rawValue)
            {
                var item = value as Dictionary<string, object>;
                if (item == null) continue;
                string category = Read(item, "category");
                if (!CategoryOrder.Contains(category, StringComparer.OrdinalIgnoreCase)) continue;

                var stats = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                var targetModifiers = new List<Dictionary<string, string>>();
                object statsValue;
                if (item.TryGetValue("stats", out statsValue) && statsValue is Dictionary<string, object>)
                {
                    foreach (var pair in (Dictionary<string, object>)statsValue)
                    {
                        if (pair.Key == "target_modifiers" && pair.Value is ArrayList)
                        {
                            foreach (object target in (ArrayList)pair.Value)
                            {
                                var targetData = target as Dictionary<string, object>;
                                if (targetData == null) continue;
                                var mapped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                                foreach (var field in targetData) mapped[field.Key] = field.Value == null ? string.Empty : Convert.ToString(field.Value);
                                targetModifiers.Add(mapped);
                            }
                        }
                        else if (pair.Value != null && !(pair.Value is ArrayList) && !(pair.Value is Dictionary<string, object>))
                            stats[pair.Key] = Convert.ToString(pair.Value);
                    }
                }

                result.Add(new CombatManeuverEntry
                {
                    Id = Read(item, "id"),
                    Name = Read(item, "name"),
                    Description = Read(item, "description"),
                    Note = Read(item, "note"),
                    Category = category,
                    Stats = stats,
                    TargetModifiers = targetModifiers,
                    Source = Read(item, "source"),
                    Page = item.ContainsKey("page") ? item["page"] : null,
                    RelatedIds = ReadStringList(item, "related_ids")
                });
            }
            return result.Where(x => !string.IsNullOrWhiteSpace(x.Id) && !string.IsNullOrWhiteSpace(x.Name)).ToList();
        }

        public static string GetCategoryTitle(string category)
        {
            switch ((category ?? string.Empty).ToLowerInvariant())
            {
                case "defensive": return "Защитные";
                case "melee": return "Манёвры ближнего боя";
                case "ranged": return "Манёвры дистанционного боя";
                default: return category ?? string.Empty;
            }
        }

        private static string Read(Dictionary<string, object> source, string key)
        {
            object value;
            return source.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : string.Empty;
        }

        private static IList<string> ReadStringList(Dictionary<string, object> source, string key)
        {
            var result = new List<string>();
            object value;
            if (source.TryGetValue(key, out value) && value is ArrayList)
                foreach (object item in (ArrayList)value) if (item != null) result.Add(Convert.ToString(item));
            return result;
        }
    }
}
