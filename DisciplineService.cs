using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace VtM_LoCh_fP
{
    public sealed class DisciplinePower
    {
        public int Level { get; set; }
        public string Id { get; set; }
        public string Name { get; set; }
        public string SystemText { get; set; }
        public string DicePool { get; set; }
        public string Difficulty { get; set; }
        public string Cost { get; set; }
        public string Duration { get; set; }
        public string Features { get; set; }
    }
    public sealed class DisciplinePath
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public List<DisciplinePower> Powers { get; set; } = new List<DisciplinePower>();
    }
    public sealed class DisciplineDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool IsSorcery { get; set; }
        public bool IsCustom { get; set; }
        public int LevelCount { get; set; }
        public string PathId { get; set; }
        public string PathName { get; set; }
        public List<DisciplinePower> Powers { get; set; } = new List<DisciplinePower>();
        public List<DisciplinePath> Paths { get; set; } = new List<DisciplinePath>();
        public List<DisciplineRitual> Rituals { get; set; } = new List<DisciplineRitual>();
    }
    public sealed class DisciplineRitual
    {
        public string Id { get; set; }
        public int Level { get; set; }
        public string Name { get; set; }
        public string SystemText { get; set; }
        public string CastTime { get; set; }
    }
    public sealed class CharacterRitual
    {
        public string DisciplineId { get; set; }
        public string RitualId { get; set; }
        public string DisciplineName { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public string SystemText { get; set; }
        public string CastTime { get; set; }
    }
    public sealed class CharacterDiscipline
    {
        public string DisciplineId { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public bool IsCustom { get; set; }
        public int LevelCount { get; set; }
        public string PathId { get; set; }
        public string PathName { get; set; }
        public string Justification { get; set; }
        public bool IsSecondaryPath { get; set; }
    }
    internal static class DisciplineService
    {
        private static List<DisciplineDefinition> _all;
        // Кэш русских названий атрибутов и способностей из Data/data.json.
        private static Dictionary<string, string> _localizedTerms;
        public static IReadOnlyList<DisciplineDefinition> GetAll()
        {
            if (_all != null) return _all;
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "data.json");
            var db = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            var list = db["disciplines"] as IList;
            _all = new List<DisciplineDefinition>();
            foreach (var raw in list)
            {
                var d = raw as Dictionary<string, object>;
                if (d == null) continue;
                var item = new DisciplineDefinition { Id = S(d,"id"), Name = S(d,"name"), Description = Clean(S(d,"description")), IsSorcery = B(d,"is_sorcery") };
                item.Powers = ParsePowers(d.ContainsKey("powers") ? d["powers"] as IList : null);
                item.Rituals = ParseRituals(d.ContainsKey("rituals") ? d["rituals"] as IList : null);
                var paths = d.ContainsKey("paths") ? d["paths"] as IList : null;
                if (paths != null) foreach (var pathRaw in paths)
                {
                    var p = pathRaw as Dictionary<string, object>;
                    if (p == null) continue;
                    item.Paths.Add(new DisciplinePath { Id = S(p,"id"), Name = S(p,"name"), Description = Clean(S(p,"description")), Powers = ParsePowers(p.ContainsKey("powers") ? p["powers"] as IList : null) });
                }
                _all.Add(item);
            }
            return _all;
        }
        public static List<DisciplineRitual> GetRituals(string disciplineId)
        {
            var d = GetAll().FirstOrDefault(x => x.Id == disciplineId);
            return d == null ? new List<DisciplineRitual>() : d.Rituals;
        }
        private static List<DisciplineRitual> ParseRituals(IList rituals)
        {
            var result = new List<DisciplineRitual>();
            if (rituals == null) return result;
            foreach (var raw in rituals)
            {
                var r = raw as Dictionary<string, object>;
                if (r == null) continue;
                string cast = "";
                object rawTime;
                if (r.TryGetValue("cast_time", out rawTime) && rawTime is Dictionary<string, object>)
                {
                    var t = (Dictionary<string, object>)rawTime;
                    string value = S(t, "value"), unit = S(t, "unit");
                    if (!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(unit))
                        cast = value + " " + unit;
                }
                result.Add(new DisciplineRitual { Id=S(r,"id"), Level=I(r,"level"), Name=S(r,"name"), SystemText=Clean(S(r,"system_text")), CastTime=cast });
            }
            return result;
        }
        public static DisciplineDefinition ForSelection(DisciplineDefinition source, string pathId)
        {
            if (source == null || string.IsNullOrEmpty(pathId)) return source;
            var path = source.Paths.FirstOrDefault(x => x.Id == pathId);
            if (path == null) return source;
            return new DisciplineDefinition { Id=source.Id, Name=source.Name, Description=source.Description, IsSorcery=source.IsSorcery, IsCustom=source.IsCustom, PathId=path.Id, PathName=path.Name, Powers=path.Powers, Paths=source.Paths, Rituals=source.Rituals };
        }
        private static List<DisciplinePower> ParsePowers(IList powers)
        {
            var result = new List<DisciplinePower>();
            if (powers == null) return result;
            foreach (var raw in powers)
            {
                var p = raw as Dictionary<string, object>;
                if (p == null) continue;
                result.Add(new DisciplinePower { Level=I(p,"level"), Id=S(p,"id"), Name=S(p,"name"), SystemText=Clean(S(p,"system_text")), DicePool=Pool(p), Difficulty=Difficulty(p), Cost=Cost(p), Duration=Clean(S(p,"duration")), Features=Clean(S(p,"features")) });
            }
            return result;
        }
        public static List<string> GetClanDisciplineIds(string clanName)
        {
            if (string.IsNullOrWhiteSpace(clanName)) return new List<string>();
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "data.json");
            var db = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            var clans = db["clans"] as IList;
            foreach (var raw in clans)
            {
                var c = raw as Dictionary<string, object>;
                if (c != null && string.Equals(S(c,"name"), clanName, StringComparison.OrdinalIgnoreCase))
                    return (c["disciplines"] as IList)?.Cast<object>().Select(Convert.ToString).ToList() ?? new List<string>();
            }
            return new List<string>();
        }
        private static string Pool(Dictionary<string, object> p)
        {
            if (!p.ContainsKey("dice_pool") || !(p["dice_pool"] is Dictionary<string, object>)) return "";
            var d=(Dictionary<string,object>)p["dice_pool"]; string a=S(d,"attribute"), b=S(d,"ability");
            return (Translate(a)+(string.IsNullOrEmpty(b)?"":" + "+Translate(b))).Trim(' ','+');
        }
        private static string Difficulty(Dictionary<string, object> p)
        {
            if (!p.ContainsKey("difficulty") || !(p["difficulty"] is Dictionary<string, object>)) return "";
            var d = (Dictionary<string, object>)p["difficulty"];
            string type = S(d, "type");
            object value;
            d.TryGetValue("value", out value);
            if (type == "fixed") return value == null ? "" : Convert.ToString(value);
            if (type == "variable") return S(d, "min") + "–" + S(d, "max") + " (зависит от ситуации)";

            var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "target_self_control_or_instinct_plus_4", "Самоконтроль или Инстинкты цели + 4" },
                { "target_wits_plus_courage", "Сообразительность + Смелость цели" },
                { "target_willpower", "Сила воли цели" },
                { "target_stamina_plus_empathy", "Выносливость + Эмпатия цели" },
                { "target_willpower_plus_2", "Сила воли цели + 2" },
                { "target_wits_plus_stealth", "Сообразительность + Скрытность цели" },
                { "generations_affected_plus_4", "Поколение цели + 4" },
                { "target_willpower_min_7", "Сила воли цели (минимум 7)" },
                { "target_willpower_min_4", "Сила воли цели (минимум 4)" },
                { "target_stamina_plus_3_if_other", "Выносливость цели + 3 (если цель — другой персонаж)" },
                { "target_stamina_plus_3", "Выносливость цели + 3" },
                { "target_humanity_or_path", "Человечность или рейтинг Пути цели" },
                { "target_perception_plus_self_control", "Восприятие + Самоконтроль цели" },
                { "target_wits_plus_subterfuge", "Сообразительность + Хитрость цели" },
                { "target_stamina_plus_self_control", "Выносливость + Самоконтроль цели" },
                { "target_wits_plus_awareness", "Сообразительность + Осведомлённость цели" },
                { "target_appearance_plus_3", "Внешность цели + 3" },
                { "target_perception_plus_alertness", "Восприятие + Бдительность цели" },
                { "target_manipulation_plus_subterfuge", "Манипуляция + Хитрость цели" },
                { "target_manipulation_plus_combat_ability", "Манипуляция + боевая способность цели" }
            };
            string label;
            if (labels.TryGetValue(type, out label)) return label;
            return string.IsNullOrEmpty(type) ? "" : type.Replace('_', ' ');
        }
        private static string Cost(Dictionary<string, object> p)
        {
            if (!p.ContainsKey("cost") || !(p["cost"] is Dictionary<string, object>)) return "";
            var d = (Dictionary<string, object>)p["cost"];
            string type = S(d, "type");
            int amount = I(d, "amount");
            if (type == "none" || string.IsNullOrEmpty(type)) return "";

            var labels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "willpower_per_turn_after_first", "Воли за каждый ход после первого" },
                { "willpower_per_turn_if_active_frenzy", "Воли за ход при активном безумии" },
                { "blood_3_and_willpower_1", "крови и 1 Воли" },
                { "blood_optional_for_auto_success", "кровь за автоматический успех (по желанию)" },
                { "blood_1_and_willpower_1", "крови и 1 Воли" },
                { "willpower_1_plus_1_blood_per_zombie", "Воля и кровь за каждого зомби" },
                { "blood_2_and_willpower_2", "крови и 2 Воли" },
                { "blood_2_and_willpower_1", "крови и 1 Воля" },
                { "blood_variable_1_to_5", "крови (от 1 до 5 пунктов)" },
                { "willpower_1_plus_blood_per_extra_target", "Воля и кровь за каждую дополнительную цель" },
                { "blood_per_body_part", "крови за каждую часть тела" },
                { "blood_optional_for_higher_appearance", "кровь (по желанию, для повышения Внешности)" },
                { "willpower_1_plus_blood_per_level", "Воля и кровь за каждый уровень" },
                { "blood_per_extra_action", "крови за каждое дополнительное действие" },
                { "blood_1_and_willpower_check", "кровь и проверка Силы воли" },
                { "blood_and_willpower_check", "кровь и проверка Силы воли" },
                { "blood_per_ichor_point", "крови за каждый пункт ихора" },
                { "blood_equal_to_target_willpower", "крови по числу пунктов Воли цели" },
                { "willpower_2_plus_blood_per_target", "2 Воли и кровь за каждую цель" },
                { "blood_10_and_permanent_willpower_1", "крови и 1 постоянная Воля" },
                { "permanent_willpower", "постоянная Воля" },
                { "dead_blood", "мёртвая кровь" },
                { "blood", "крови" },
                { "willpower", "Воли" }
            };
            string label;
            if (!labels.TryGetValue(type, out label)) label = type.Replace('_', ' ');
            if (type == "blood_optional_for_auto_success") return amount + " " + label;
            if (type == "blood_3_and_willpower_1") return "3 " + label;
            if (type == "blood_1_and_willpower_1") return "1 " + label;
            if (type == "blood_2_and_willpower_2") return "2 " + label;
            if (type == "blood_2_and_willpower_1") return "2 " + label;
            if (type == "blood_1_and_willpower_check" || type == "blood_and_willpower_check") return "1 " + label;
            if (type == "blood_10_and_permanent_willpower_1") return "10 " + label;
            if (type == "willpower_1_plus_1_blood_per_zombie" || type == "willpower_1_plus_blood_per_extra_target" ||
                type == "willpower_1_plus_blood_per_level" || type == "willpower_2_plus_blood_per_target") return label;
            if (type == "blood_variable_1_to_5" || type == "blood_per_body_part" ||
                type == "blood_per_extra_action" || type == "blood_per_ichor_point" ||
                type == "blood_equal_to_target_willpower") return label;
            return amount > 0 ? amount + " " + label : label;
        }
        public static string Translate(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return string.Empty;
            if (_localizedTerms == null)
            {
                _localizedTerms = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "strength", "Сила" }, { "dexterity", "Ловкость" }, { "stamina", "Выносливость" },
                    { "charisma", "Обаяние" }, { "manipulation", "Манипуляция" }, { "appearance", "Внешность" },
                    { "perception", "Восприятие" }, { "intelligence", "Интеллект" }, { "wits", "Сообразительность" },
                    { "willpower", "Сила воли" }, { "courage", "Смелость" },
                    { "self_control", "Самоконтроль" }, { "instinct", "Инстинкты" },
                    { "combat_ability", "Боевая способность" }
                };

                try
                {
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "data.json");
                    var db = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
                    AddLocalizedTerms(db, "attributes");
                    AddLocalizedTerms(db, "abilities");
                }
                catch { /* Fallback to the built-in translations if optional data is unavailable. */ }
            }

            string localized;
            if (_localizedTerms.TryGetValue(id, out localized)) return localized;

            // Некоторые дайспулы допускают выбор одного из двух навыков.
            if (id.IndexOf("_or_", StringComparison.OrdinalIgnoreCase) >= 0)
                return string.Join(" или ", id.Split(new[] { "_or_" }, StringSplitOptions.None).Select(Translate));

            return id.Replace('_', ' ');
        }

        private static void AddLocalizedTerms(Dictionary<string, object> db, string section)
        {
            if (db == null || !db.ContainsKey(section) || !(db[section] is IList entries)) return;
            foreach (var raw in entries)
            {
                var entry = raw as Dictionary<string, object>;
                if (entry == null) continue;
                string id = S(entry, "id");
                string name = S(entry, "name");
                if (!string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(name))
                    _localizedTerms[id] = name;
            }
        }
        private static string S(Dictionary<string,object>d,string k){object v;return d.TryGetValue(k,out v)&&v!=null?Convert.ToString(v):"";}
        private static int I(Dictionary<string,object>d,string k){object v;int n;return d.TryGetValue(k,out v)&&int.TryParse(Convert.ToString(v),out n)?n:0;}
        private static bool B(Dictionary<string,object>d,string k){object v;return d.TryGetValue(k,out v)&&v is bool&&(bool)v;}
        private static string Clean(string s){return Regex.Replace(s??"",@"\[cite:\s*\d+\]","").Trim();}
    }
}
