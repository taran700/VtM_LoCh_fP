using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace VtM_LoCh_fP
{
    internal sealed class GenerationRule
    {
        public int Generation { get; set; }
        public int BloodPoolMax { get; set; }
        public int BloodPerTurn { get; set; }
        public int TraitMax { get; set; }
        public int DisciplineMax { get; set; }
    }

    internal static class GenerationService
    {
        private static List<GenerationRule> _rules;
        public static IReadOnlyList<GenerationRule> GetRules()
        {
            if (_rules != null) return _rules;
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "data.json");
            var serializer = new JavaScriptSerializer();
            var db = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            var baseRules = db["base_rules"] as Dictionary<string, object>;
            var rows = baseRules["generation_table"] as IList;
            _rules = new List<GenerationRule>();
            foreach (var rowObject in rows)
            {
                var row = rowObject as Dictionary<string, object>;
                if (row == null) continue;
                _rules.Add(new GenerationRule
                {
                    Generation = Convert.ToInt32(row["generation"]),
                    BloodPoolMax = Convert.ToInt32(row["blood_pool_max"]),
                    BloodPerTurn = Convert.ToInt32(row["blood_per_turn"]),
                    TraitMax = Convert.ToInt32(row["trait_max"]),
                    DisciplineMax = row.ContainsKey("discipline_max") ? Convert.ToInt32(row["discipline_max"]) : 5
                });
            }
            _rules = _rules.OrderByDescending(x => x.Generation).ToList();
            return _rules;
        }
        public static GenerationRule Find(int generation) => GetRules().FirstOrDefault(x => x.Generation == generation);
    }
}
