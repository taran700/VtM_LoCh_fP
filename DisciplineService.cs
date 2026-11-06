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
    }
    internal static class DisciplineService
    {
        private static List<DisciplineDefinition> _all;
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
            var d=(Dictionary<string,object>)p["difficulty"]; string type=S(d,"type"); object v; d.TryGetValue("value",out v);
            if(type=="fixed") return v==null?"":Convert.ToString(v);
            if(type=="variable") return S(d,"min")+"–"+S(d,"max")+" (зависит от ситуации)";
            return string.IsNullOrEmpty(type)?"":type.Replace('_',' ');
        }
        private static string Cost(Dictionary<string, object> p)
        {
            if (!p.ContainsKey("cost") || !(p["cost"] is Dictionary<string, object>)) return "";
            var d=(Dictionary<string,object>)p["cost"]; string type=S(d,"type"); int amount=I(d,"amount");
            if(type=="none"||string.IsNullOrEmpty(type))return "";
            string label=type=="blood"?"крови":type.StartsWith("willpower")||type=="willpower"?"Воли":type.Replace('_',' ');
            return amount+" "+label;
        }
        public static string Translate(string id)
        {
            var map=new Dictionary<string,string>{{"strength","Сила"},{"dexterity","Ловкость"},{"stamina","Выносливость"},{"charisma","Обаяние"},{"manipulation","Манипуляция"},{"appearance","Внешность"},{"perception","Восприятие"},{"intelligence","Интеллект"},{"wits","Сообразительность"},{"animal_ken","Обращение с животными"},{"survival","Выживание"},{"intimidation","Запугивание"},{"empathy","Эмпатия"},{"performance","Исполнение"},{"subterfuge","Хитрость"},{"willpower","Сила воли"}};
            if(string.IsNullOrEmpty(id))return ""; return map.ContainsKey(id)?map[id]:id.Replace('_',' ');
        }
        private static string S(Dictionary<string,object>d,string k){object v;return d.TryGetValue(k,out v)&&v!=null?Convert.ToString(v):"";}
        private static int I(Dictionary<string,object>d,string k){object v;int n;return d.TryGetValue(k,out v)&&int.TryParse(Convert.ToString(v),out n)?n:0;}
        private static bool B(Dictionary<string,object>d,string k){object v;return d.TryGetValue(k,out v)&&v is bool&&(bool)v;}
        private static string Clean(string s){return Regex.Replace(s??"",@"\[cite:\s*\d+\]","").Trim();}
    }
}
