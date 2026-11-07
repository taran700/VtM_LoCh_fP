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
        public string DisplayName => Generation >= 13 ? (Generation == 13 ? "Тринадцатое" : Generation == 14 ? "Четырнадцатое" : "Пятнадцатое") :
            Generation == 3 ? "Третье" : Generation == 4 ? "Четвёртое" : Generation == 5 ? "Пятое" :
            Generation == 6 ? "Шестое" : Generation == 7 ? "Седьмое" : Generation == 8 ? "Восьмое" :
            Generation == 9 ? "Девятое" : Generation == 10 ? "Десятое" : Generation == 11 ? "Одиннадцатое" :
            Generation == 12 ? "Двенадцатое" : Generation.ToString(); 
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
            _rules = _rules.Where(x => x.Generation >= 4 && x.Generation <= 15).OrderBy(x => x.Generation).ToList();
            return _rules;
        }
        public static GenerationRule Find(int generation)
        {
            generation = Math.Max(4, Math.Min(15, generation));
            var exact = GetRules().FirstOrDefault(x => x.Generation == generation);
            if (exact != null) return exact;
            var last = GetRules().OrderBy(x => x.Generation).LastOrDefault();
            if (last == null) return null;
            return new GenerationRule { Generation = generation, BloodPoolMax = last.BloodPoolMax, BloodPerTurn = last.BloodPerTurn, TraitMax = last.TraitMax, DisciplineMax = last.DisciplineMax };
        }
    }
}
