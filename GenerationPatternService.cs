using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace VtM_LoCh_fP
{
    internal sealed class NameGenerationPattern
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("gender")]
        public string Gender { get; set; }
        [JsonProperty("clan_priority")]
        public List<string> ClanPriority { get; set; } = new List<string>();
        [JsonProperty("ability_priority")]
        public List<string> AbilityPriority { get; set; } = new List<string>();
        [JsonProperty("nature_candidates")]
        public List<string> NatureCandidates { get; set; } = new List<string>();
        [JsonProperty("demeanor_candidates")]
        public List<string> DemeanorCandidates { get; set; } = new List<string>();
        [JsonProperty("background_priority")]
        public List<List<string>> BackgroundPriority { get; set; } = new List<List<string>>();
    }

    internal sealed class GenerationPatternDatabase
    {
        [JsonProperty("schema_version")]
        public int SchemaVersion { get; set; }
        [JsonProperty("clan_attribute_priority")]
        public Dictionary<string, List<string>> ClanAttributePriority { get; set; } = new Dictionary<string, List<string>>();
        [JsonProperty("mask_roles")]
        public Dictionary<string, string> MaskRoles { get; set; } = new Dictionary<string, string>();
        [JsonProperty("name_profiles")]
        public List<NameGenerationPattern> NameProfiles { get; set; } = new List<NameGenerationPattern>();
    }

    internal static class GenerationPatternService
    {
        private static readonly object Sync = new object();
        private static GenerationPatternDatabase _database;

        public static IReadOnlyList<NameGenerationPattern> GetNameProfiles()
        {
            return Load().NameProfiles.AsReadOnly();
        }

        public static NameGenerationPattern FindNameProfile(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return Load().NameProfiles.FirstOrDefault(x =>
                string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        public static NameGenerationPattern ChooseRandomNameProfile(Random random)
        {
            if (random == null) throw new ArgumentNullException(nameof(random));
            var profiles = Load().NameProfiles;
            if (profiles.Count == 0) throw new InvalidDataException("В паттернах генерации нет именных профилей.");
            return profiles[random.Next(profiles.Count)];
        }

        public static NameGenerationPattern ChooseRandomSireProfile(NameGenerationPattern character, Random random)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (random == null) throw new ArgumentNullException(nameof(random));
            string oppositeGender = string.Equals(character.Gender, "female", StringComparison.OrdinalIgnoreCase)
                ? "male" : "female";
            var candidates = Load().NameProfiles.Where(x =>
                string.Equals(x.Gender, oppositeGender, StringComparison.OrdinalIgnoreCase)).ToList();
            if (candidates.Count == 0)
                throw new InvalidDataException("Не найдены имена Сиры противоположного пола.");
            return candidates[random.Next(candidates.Count)];
        }

        public static IReadOnlyList<string> GetAttributePriority(string clanName)
        {
            List<string> priority;
            if (!Load().ClanAttributePriority.TryGetValue(clanName ?? string.Empty, out priority))
                throw new InvalidDataException("Для клана «" + clanName + "» не задан приоритет атрибутов.");
            return priority.AsReadOnly();
        }

        public static string GetRoleForMask(string mask)
        {
            string role;
            return Load().MaskRoles.TryGetValue(mask ?? string.Empty, out role) ? role : string.Empty;
        }

        public static void ValidateAgainstGameDatabase()
        {
            var db = Load();
            var clans = new HashSet<string>(ClanService.GetClans().Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            var archetypes = new HashSet<string>(ArchetypeService.GetArchetypes().Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            var backgrounds = new HashSet<string>(BioFactService.GetAll().Select(x => x.Name), StringComparer.OrdinalIgnoreCase);
            var abilityGroups = new HashSet<string>(new[] { "Таланты", "Навыки", "Знания" }, StringComparer.OrdinalIgnoreCase);

            if (db.NameProfiles.Count != 20)
                throw new InvalidDataException("Ожидалось 20 именных профилей, найдено: " + db.NameProfiles.Count + ".");

            foreach (var profile in db.NameProfiles)
            {
                if (profile.ClanPriority == null || profile.ClanPriority.Count != 3 || profile.ClanPriority.Any(x => !clans.Contains(x)))
                    throw new InvalidDataException("Некорректный список кланов в профиле «" + profile.Name + "».");
                if (profile.AbilityPriority == null || profile.AbilityPriority.Count != 3 ||
                    profile.AbilityPriority.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3 ||
                    profile.AbilityPriority.Any(x => !abilityGroups.Contains(x)))
                    throw new InvalidDataException("Некорректные приоритеты способностей в профиле «" + profile.Name + "».");
                ValidateArchetypeList(profile.Name, "Натуры", profile.NatureCandidates, archetypes);
                ValidateArchetypeList(profile.Name, "Маски", profile.DemeanorCandidates, archetypes);
                if (profile.BackgroundPriority == null || profile.BackgroundPriority.Count != 4 ||
                    profile.BackgroundPriority.Any(x => x == null || x.Count != 3 || x.Any(y => !backgrounds.Contains(y))))
                    throw new InvalidDataException("Некорректные приоритеты предысторий в профиле «" + profile.Name + "».");
            }

            foreach (var clanPriority in db.ClanAttributePriority)
            {
                if (!clans.Contains(clanPriority.Key) || clanPriority.Value == null || clanPriority.Value.Count != 3 ||
                    clanPriority.Value.Distinct(StringComparer.OrdinalIgnoreCase).Count() != 3)
                    throw new InvalidDataException("Некорректный приоритет атрибутов для клана «" + clanPriority.Key + "».");
            }

            foreach (var mask in db.MaskRoles)
                if (!archetypes.Contains(mask.Key) || string.IsNullOrWhiteSpace(mask.Value))
                    throw new InvalidDataException("Некорректное амплуа для Маски «" + mask.Key + "».");
        }

        private static void ValidateArchetypeList(string profileName, string label, List<string> values, HashSet<string> archetypes)
        {
            if (values == null || values.Count != 5 || values.Any(x => !archetypes.Contains(x)))
                throw new InvalidDataException("Некорректный список «" + label + "» в профиле «" + profileName + "».");
        }

        private static GenerationPatternDatabase Load()
        {
            if (_database != null) return _database;
            lock (Sync)
            {
                if (_database != null) return _database;
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Generation", "patterns.json");
                if (!File.Exists(path))
                    path = Path.Combine(Environment.CurrentDirectory, "Data", "Generation", "patterns.json");
                if (!File.Exists(path))
                    throw new FileNotFoundException("Не найден файл паттернов Data\\Generation\\patterns.json.", path);

                var parsed = JsonConvert.DeserializeObject<GenerationPatternDatabase>(File.ReadAllText(path));
                if (parsed == null || parsed.SchemaVersion != 1 || parsed.NameProfiles == null ||
                    parsed.ClanAttributePriority == null || parsed.MaskRoles == null)
                    throw new InvalidDataException("Файл паттернов генерации имеет неподдерживаемую структуру.");
                _database = parsed;
                return _database;
            }
        }
    }
}
