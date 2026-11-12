using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace VtM_LoCh_fP
{
    internal static class GenerationStageService
    {
        private static JObject _data;
        private static readonly object Sync = new object();

        private static JObject Data
        {
            get
            {
                if (_data != null) return _data;
                lock (Sync)
                {
                    if (_data != null) return _data;
                    string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Generation", "stages_3_7.json");
                    if (!File.Exists(path)) path = Path.Combine(Environment.CurrentDirectory, "Data", "Generation", "stages_3_7.json");
                    if (!File.Exists(path)) throw new FileNotFoundException("Не найден файл Data\\Generation\\stages_3_7.json.", path);
                    _data = JObject.Parse(File.ReadAllText(path));
                    return _data;
                }
            }
        }

        private static JArray ArrayAt(params string[] path)
        {
            JToken token = Data;
            foreach (string part in path) token = token == null ? null : token[part];
            return token as JArray ?? new JArray();
        }

        private static string StringAt(JToken token, string property)
            => token == null || token[property] == null ? string.Empty : token[property].ToString();

        private static List<string> Values(params string[] path)
            => ArrayAt(path).Values<string>().Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

        private static string Pick(IList<string> values, Random random)
        {
            if (values == null || values.Count == 0) return string.Empty;
            return values[random.Next(values.Count)];
        }

        private static string PickFromObject(string stage, string group, string key, Random random)
        {
            JToken token = Data[stage];
            foreach (string part in (group ?? string.Empty).Split('.')) token = token == null ? null : token[part];
            token = token == null ? null : token[key];
            var values = token == null ? new List<string>() : token.Values<string>().ToList();
            return Pick(values, random);
        }

        private static string GenderReplace(string text, bool female)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string[,] pairs = {
                {"{он/она}", female ? "она" : "он"}, {"{Он/она}", female ? "Она" : "Он"},
                {"{его/её}", female ? "её" : "его"}, {"{Его/её}", female ? "Её" : "Его"},
                {"{ему/ей}", female ? "ей" : "ему"}, {"{нём/ней}", female ? "ней" : "нём"},
                {"{него/неё}", female ? "неё" : "него"}, {"{ним/ней}", female ? "ней" : "ним"},
                {"{нему/ней}", female ? "ней" : "нему"}, {"{о нём/о ней}", female ? "о ней" : "о нём"},
                {"{сделал/сделала}", female ? "сделала" : "сделал"}, {"{профессия}", ""}, {"{сфера}", ""}
            };
            for (int i = 0; i < pairs.GetLength(0); i++) text = text.Replace(pairs[i, 0], pairs[i, 1]);
            string[,] plain = {
                {"был/была", female ? "была" : "был"}, {"жил/жила", female ? "жила" : "жил"},
                {"остался/осталась", female ? "осталась" : "остался"}, {"сохранил/сохранила", female ? "сохранила" : "сохранил"},
                {"снимал/снимала", female ? "снимала" : "снимал"}, {"выбрал/выбрала", female ? "выбрала" : "выбрал"},
                {"спас/спасла", female ? "спасла" : "спас"}, {"вылечил/вылечила", female ? "вылечила" : "вылечил"},
                {"научил/научила", female ? "научила" : "научил"}, {"собрал/собрала", female ? "собрала" : "собрал"},
                {"достал/достала", female ? "достала" : "достал"}, {"слил/слила", female ? "слила" : "слил"},
                {"прикрыл/прикрыла", female ? "прикрыла" : "прикрыл"}, {"устроил/устроила", female ? "устроила" : "устроил"},
                {"провалил/провалила", female ? "провалила" : "провалил"}, {"один/одна", female ? "одна" : "один"},
                {"одного/одну", female ? "одну" : "одного"}, {"сам/сама", female ? "сама" : "сам"},
                {"самим/самой", female ? "самой" : "самим"}, {"другим/другой", female ? "другой" : "другим"},
                {"другого/другую", female ? "другую" : "другого"}, {"уязвимым/уязвимой", female ? "уязвимой" : "уязвимым"},
                {"слабым/слабой", female ? "слабой" : "слабым"}, {"потерянным/потерянной", female ? "потерянной" : "потерянным"},
                {"безумным/безумной", female ? "безумной" : "безумным"}, {"победителем/победительницей", female ? "победительницей" : "победителем"},
                {"спешит/спешит", "спешит"}, {"знает/знает", "знает"}, {"входит/входит", "входит"}
            };
            for (int i = 0; i < plain.GetLength(0); i++) text = text.Replace(plain[i, 0], plain[i, 1]);
            text = Regex.Replace(text, @"\{?([А-Яа-яЁё-]+)/([А-Яа-яЁё-]+)\}?", m => female ? m.Groups[2].Value : m.Groups[1].Value);
            return text;
        }

        private static string Resolve(string text, bool female, string profession, string sphere, string sire, string nature)
        {
            if (string.IsNullOrEmpty(text)) return text;
            text = text.Replace("{профессия}", profession ?? string.Empty).Replace("{сфера}", sphere ?? string.Empty)
                .Replace("{сир}", sire ?? string.Empty).Replace("{Сир}", sire ?? string.Empty)
                .Replace("{натура}", nature ?? string.Empty);
            return GenderReplace(text, female);
        }

        public static void GenerateAll(CharacterProfile character, NameGenerationPattern profile, Random random)
        {
            if (character == null || profile == null || random == null) return;
            bool female = string.Equals(profile.Gender, "female", StringComparison.OrdinalIgnoreCase);
            GenerateBackstory(character, profile, female, random);
            GenerateAppearance(character, female, random);
            GenerateEquipment(character, profile, female, random);
        }

        private static void GenerateBackstory(CharacterProfile c, NameGenerationPattern profile, bool female, Random random)
        {
            JToken s = Data["stage3"];
            string nature = PickFromObject("stage3", "nature", c.Nature, random);
            string roleTemplate = Pick(Values("stage3", "role_templates"), random);
            string profession = Pick(Values("stage3", "profession"), random);
            string sphere = Pick(Values("stage3", "sphere"), random);
            string sire = Pick(Values("stage3", "sire_names", female ? "male" : "female"), random);
            c.SireName = sire;

            var blocks = new List<string>();
            blocks.Add(Resolve(roleTemplate, female, profession, sphere, sire, nature));

            int wealth = GetBackgroundDots(c, "Богатство");
            string wealthText = wealth > 0 ? PickBackground("Богатство", wealth, random) : null;
            if (!string.IsNullOrWhiteSpace(wealthText))
            {
                string[] split = wealthText.Split(new[] { "||" }, StringSplitOptions.None);
                blocks.Add(Resolve(split[0].Trim(), female, profession, sphere, sire, nature));
            }
            AddBackground(blocks, c, "Влияние", female, profession, sphere, sire, nature, random);
            AddBackground(blocks, c, "Слава", female, profession, sphere, sire, nature, random);

            string clanTemplate = PickFromObject("stage3", "clans", c.ClanName, random);
            blocks.Add(Resolve(clanTemplate, female, profession, sphere, sire, nature));
            string maskTemplate = PickFromObject("stage3", "mask", c.Demeanor, random);
            blocks.Add(Resolve(maskTemplate, female, profession, sphere, sire, nature));
            string overKey = random.Next(2) == 0 ? "Исход 1: Маска выше Натуры (10)" : "Исход 2: Натура выше Маски (10)";
            blocks.Add(Resolve(PickFromObject("stage3", "overbalance", overKey, random), female, profession, sphere, sire, nature));
            if (!string.IsNullOrWhiteSpace(wealthText))
            {
                string[] split = wealthText.Split(new[] { "||" }, StringSplitOptions.None);
                if (split.Length > 1) blocks.Add(Resolve(split[1].Trim(), female, profession, sphere, sire, nature));
            }
            AddBackground(blocks, c, "Статус", female, profession, sphere, sire, nature, random);
            c.BiographyBackstory = string.Join(" ", blocks
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => Regex.Replace(x.Trim(), @"\s+", " ")));
        }

        private static int GetBackgroundDots(CharacterProfile c, string name)
        {
            if (c.CreationState == null) return 0;
            int value;
            if (c.CreationState.OtherDots.TryGetValue("background:" + name, out value)) return value;
            if (c.CreationState.OtherDots.TryGetValue(name, out value)) return value;
            return 0;
        }

        private static string PickBackground(string name, int dots, Random random)
        {
            JArray rows = ArrayAt("stage3", "backgrounds", name);
            var options = rows.Where(x => (int?)x["dots"] == dots).Select(x => StringAt(x, "text")).ToList();
            return Pick(options, random);
        }

        private static void AddBackground(List<string> blocks, CharacterProfile c, string name, bool female, string profession, string sphere, string sire, string nature, Random random)
        {
            int dots = GetBackgroundDots(c, name);
            if (dots <= 0) return;
            string value = PickBackground(name, dots, random);
            if (!string.IsNullOrWhiteSpace(value)) blocks.Add(Resolve(value, female, profession, sphere, sire, nature));
        }

        private static void GenerateAppearance(CharacterProfile c, bool female, Random random)
        {
            JArray body = ArrayAt("stage4", "1. Часть тела");
            JArray types = ArrayAt("stage4", "2. Вид");
            JArray forms = ArrayAt("stage4", "3. Форма");
            JArray pain = ArrayAt("stage4", "4. Болезненность");
            JArray attitude = ArrayAt("stage4", "5. Отношение");
            var part = body[random.Next(body.Count)];
            string zone = StringAt(part, "zones_or_allowed");
            var type = types.Where(x => ContainsAllowed(StringAt(x, "zones_or_allowed"), zone)
                && forms.Any(f => ContainsAllowed(StringAt(f, "zones_or_allowed"), StringAt(x, "name")))
                && pain.Any(p => ContainsAllowed(StringAt(p, "zones_or_allowed"), StringAt(x, "name")))).ToList();
            var selectedType = type[random.Next(type.Count)];
            string typeName = StringAt(selectedType, "name");
            var shape = forms.Where(x => ContainsAllowed(StringAt(x, "zones_or_allowed"), typeName)).ToList();
            var selectedShape = shape[random.Next(shape.Count)];
            var pains = pain.Where(x => ContainsAllowed(StringAt(x, "zones_or_allowed"), typeName)).ToList();
            var selectedPain = pains[random.Next(pains.Count)];
            string painName = StringAt(selectedPain, "name");
            var attitudes = attitude.Where(x => RelationAllows(StringAt(x, "zones_or_allowed"), painName)).ToList();
            var selectedAttitude = attitudes[random.Next(attitudes.Count)];
            string formName = StringAt(selectedShape, "name");
            string first = "На " + ToBodyLocation(StringAt(part, "name")) + " — " + typeName + " " + ToFormPhrase(formName) + ", " + painName + ".";
            string second = (female ? "Она " : "Он ") + StringAt(selectedAttitude, "name") + ".";
            c.BiographyAppearance = first + " " + second;
        }

        private static bool RelationAllows(string raw, string painName)
        {
            var aliases = new List<string> { painName };
            string lower = (painName ?? string.Empty).ToLowerInvariant();
            string[] known = { "отталкивает взгляд", "гниёт", "кровоточит", "сочится", "чешется", "притягивает взгляд", "стильно", "часть образа" };
            foreach (string alias in known)
                if (lower.Contains(alias)) aliases.Add(alias);
            if (raw.Contains("все"))
            {
                int ex = raw.IndexOf("кроме", StringComparison.OrdinalIgnoreCase);
                if (ex < 0) return true;
                string excluded = raw.Substring(ex + 5);
                var quoted = Regex.Matches(excluded, "«([^»]+)»").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
                return !aliases.Any(a => quoted.Any(x => x.Equals(a, StringComparison.OrdinalIgnoreCase)));
            }
            var allowed = raw.Split(',').Select(x => x.Trim()).ToList();
            return aliases.Any(a => allowed.Any(x => x.Equals(a, StringComparison.OrdinalIgnoreCase)));
        }

        private static bool ContainsAllowed(string raw, string value)
        {
            if (string.IsNullOrWhiteSpace(raw)) return false;
            if (raw.Contains("все"))
            {
                int ex = raw.IndexOf("кроме", StringComparison.OrdinalIgnoreCase);
                if (ex < 0) return true;
                string excluded = raw.Substring(ex + 5);
                var quoted = Regex.Matches(excluded, "«([^»]+)»").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
                if (quoted.Count == 0) quoted = excluded.Split(',').Select(x => x.Trim()).ToList();
                return !quoted.Any(x => x.Equals(value, StringComparison.OrdinalIgnoreCase));
            }
            return raw.Split(',').Select(x => x.Trim()).Any(x => x.Equals(value, StringComparison.OrdinalIgnoreCase));
        }


        private static string ToBodyLocation(string bodyPart)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["левая щека"]="левой щеке", ["правая щека"]="правой щеке", ["лоб"]="лбу", ["подбородок"]="подбородке",
                ["переносица"]="переносице", ["шея спереди"]="передней стороне шеи", ["шея сбоку"]="боковой стороне шеи",
                ["левое плечо"]="левом плече", ["правое плечо"]="правом плече", ["левое предплечье"]="левом предплечье",
                ["правое предплечье"]="правом предплечье", ["тыльная сторона левой кисти"]="тыльной стороне левой кисти",
                ["тыльная сторона правой кисти"]="тыльной стороне правой кисти", ["левая ладонь"]="левой ладони", ["правая ладонь"]="правой ладони",
                ["грудь слева"]="левой стороне груди", ["грудь справа"]="правой стороне груди", ["живот"]="животе",
                ["левая лопатка"]="левой лопатке", ["поясница"]="пояснице"
            };
            return map.ContainsKey(bodyPart) ? map[bodyPart] : bodyPart;
        }

        private static string ToFormPhrase(string form)
        {
            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["круглая"]="круглой формы", ["овальная"]="овальной формы", ["вытянутая"]="вытянутой формы", ["рваная"]="рваной формы",
                ["ровная"]="ровной формы", ["асимметричная"]="асимметричной формы", ["в виде полосы"]="в виде полосы", ["в виде капли"]="в виде капли",
                ["звёздчатая"]="звёздчатой формы", ["симметричная"]="симметричной формы", ["в виде креста"]="в виде креста", ["в виде кольца"]="в виде кольца",
                ["в виде полумесяца"]="в виде полумесяца", ["в виде лабиринта"]="в виде лабиринта", ["в виде паутины"]="в виде паутины", ["в виде трещины"]="в виде трещины",
                ["в виде спирали"]="в виде спирали", ["в виде треугольника"]="в виде треугольника", ["в виде отпечатка"]="в виде отпечатка", ["в виде карты"]="в виде карты"
            };
            return map.ContainsKey(form) ? map[form] : form;
        }


        private static string ShortenClothingDescription(string description)
        {
            if (string.IsNullOrWhiteSpace(description)) return string.Empty;
            string value = Regex.Replace(description.Trim(), @"\s+", " ");
            value = Regex.Split(value, @"[,;—]|\s+(?:сидит|выглядит|лежит|висит|держит|добавляет|струится|подчёркивает|не стесняет|не требует|не мешает)\b", RegexOptions.IgnoreCase)[0];
            return value.Trim().TrimEnd('.', ',', ';', '—');
        }

        private static void GenerateEquipment(CharacterProfile c, NameGenerationPattern profile, bool female, Random random)
        {
            string genderKey = female ? "female" : "male";
            string top = PickFromObject("stage5", genderKey + ".mask", c.Demeanor, random);
            string bottom = PickFromObject("stage5", genderKey + ".nature", c.Nature, random);
            string accessory = PickFromObject("stage5", genderKey + ".clan", c.ClanName, random);
            c.EquipmentClothing = string.Join("; ", new[] { top, bottom, accessory }
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(ShortenClothingDescription));

            JArray items = ArrayAt("stage6", "items");
            JObject weights = Data["stage6"]?["weights"] as JObject;
            int totalWeight = weights.Properties().Sum(x => (int)x.Value);
            int roll = random.Next(totalWeight);
            int count = 1;
            foreach (var prop in weights.Properties())
            {
                roll -= (int)prop.Value;
                if (roll < 0) { count = int.Parse(prop.Name); break; }
            }
            var pool = items.Values<string>().ToList();
            var selectedItems = new List<string>();
            while (selectedItems.Count < count && pool.Count > 0)
            {
                int index = random.Next(pool.Count);
                selectedItems.Add(pool[index]);
                pool.RemoveAt(index);
            }
            c.EquipmentPockets = string.Join(" \n\n ", selectedItems);


            var skillValues = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string skill in new[] { "Драка", "Фехтование", "Стрельба" })
            {
                int value;
                skillValues[skill] = c.CreationState != null && c.CreationState.AbilityDots.TryGetValue(skill, out value) ? value : 0;
            }
            int highest = skillValues.Values.Max();
            var leaders = skillValues.Where(x => x.Value == highest).Select(x => x.Key).ToList();
            var weapons = new List<string>();
            foreach (string skill in leaders)
            {
                string weapon = Pick(Values("stage7", "weapons", skill), random);
                if (skill == "Стрельба") weapon += " — " + random.Next(1, 11) + " полных перезарядок";
                weapons.Add(weapon);
            }
            c.EquipmentWeapons = string.Join(Environment.NewLine, weapons);
            int armorRoll = random.Next(1, 101);
            c.EquipmentArmor = armorRoll <= 25 ? Pick(Values("stage7", "armor"), random) : "Принципиально не носит броню";
        }
    }
}
