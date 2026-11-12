using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace VtM_LoCh_fP
{
    internal sealed class MeritFlawDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public string Category { get; set; }
        public List<int> Costs { get; set; } = new List<int>();
        public string Description { get; set; }
        public List<string> IncompatibleWith { get; set; } = new List<string>();
        public List<string> Requires { get; set; } = new List<string>();
        public int SelectedCost { get; set; }
        public string DependencyGroupId { get; set; }
    }

    internal static class MeritFlawService
    {
        private static List<MeritFlawDefinition> _items;

        public static List<MeritFlawDefinition> GetAll()
        {
            if (_items == null) Load();
            return _items.ToList();
        }

        private static void Load()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "data.json");
            if (!File.Exists(path)) throw new FileNotFoundException("Не найден файл базы данных Data\\data.json.", path);
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
            var db = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            var raw = db.ContainsKey("merits_flaws") ? db["merits_flaws"] as IList : null;
            _items = new List<MeritFlawDefinition>();
            if (raw == null) return;

            foreach (var value in raw)
            {
                var item = value as Dictionary<string, object>;
                if (item == null) continue;
                var definition = new MeritFlawDefinition
                {
                    Id = Read(item, "id"), Name = Read(item, "name"), Type = Read(item, "type"),
                    Category = Read(item, "category"), Description = Read(item, "description")
                };
                object costObject;
                if (item.TryGetValue("cost", out costObject) && costObject is Dictionary<string, object> cost && cost.ContainsKey("values"))
                {
                    var values = cost["values"] as IList;
                    if (values != null)
                        foreach (var costValue in values)
                        {
                            int parsed;
                            if (int.TryParse(Convert.ToString(costValue), out parsed) && parsed > 0 && !definition.Costs.Contains(parsed))
                                definition.Costs.Add(parsed);
                        }
                }
                if (definition.Costs.Count == 0) definition.Costs.Add(1);
                object incompatible;
                if (item.TryGetValue("incompatible_with", out incompatible) && incompatible is IList ids)
                    definition.IncompatibleWith = ids.Cast<object>().Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                object requires;
                if (item.TryGetValue("requires", out requires))
                {
                    if (requires is IList requiredIds) definition.Requires = requiredIds.Cast<object>().Select(Convert.ToString).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                    else if (!string.IsNullOrWhiteSpace(Convert.ToString(requires))) definition.Requires = Convert.ToString(requires).Split(",;|".ToCharArray(), StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).ToList();
                }
                if (!string.IsNullOrWhiteSpace(definition.Id) && !string.IsNullOrWhiteSpace(definition.Name) &&
                    (definition.Type == "merit" || definition.Type == "flaw"))
                    _items.Add(definition);
            }
        }

        public static void SynchronizeCharacterEntries(CharacterProfile character)
        {
            if (character == null || character.MeritsFlaws == null) return;
            try
            {
                if (_items == null) Load();
            }
            catch
            {
            
                return;
            }

            foreach (var owned in character.MeritsFlaws)
            {
                if (owned == null) continue;
                MeritFlawDefinition definition = null;

                if (!string.IsNullOrWhiteSpace(owned.Id))
                    definition = _items.FirstOrDefault(x => string.Equals(x.Id, owned.Id, StringComparison.OrdinalIgnoreCase));

                
                if (definition == null && !string.IsNullOrWhiteSpace(owned.Name))
                    definition = _items.FirstOrDefault(x =>
                        string.Equals(x.Name, owned.Name, StringComparison.OrdinalIgnoreCase) &&
                        (string.IsNullOrWhiteSpace(owned.Type) || string.Equals(x.Type, owned.Type, StringComparison.OrdinalIgnoreCase)));

                
                if (definition == null) continue;

                owned.Id = definition.Id;
                owned.Name = definition.Name;
                owned.Type = definition.Type;
                owned.Description = definition.Description;
                
            }
        }

        private static string Read(Dictionary<string, object> item, string key)
        {
            object value;
            return item.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : string.Empty;
        }
    }

    public sealed class CharacterMeritFlaw
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Type { get; set; }
        public int Cost { get; set; }
        public string Description { get; set; }
        public bool FreebieCharged { get; set; }
    }
}
