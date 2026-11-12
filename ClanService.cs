using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Web.Script.Serialization;

namespace VtM_LoCh_fP
{
    internal sealed class ClanInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Nickname { get; set; }
        public string Description { get; set; }
        public string Quote { get; set; }
        public string WeaknessDescription { get; set; }
    }

    internal static class ClanService
    {
        private static List<ClanInfo> _clans;

        public static IReadOnlyList<ClanInfo> GetClans()
        {
            if (_clans != null) return _clans;

            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "data.json");
            if (!File.Exists(path))
                throw new FileNotFoundException("Не найден файл базы данных Data\\data.json.", path);

            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
            var database = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            if (database == null || !database.ContainsKey("clans"))
                throw new InvalidDataException("В базе данных отсутствует раздел clans.");

            var source = database["clans"] as IList;
            if (source == null)
                throw new InvalidDataException("Раздел clans имеет неверный формат.");

            var result = new List<ClanInfo>();
            foreach (var item in source)
            {
                var clan = item as Dictionary<string, object>;
                if (clan == null) continue;
                result.Add(new ClanInfo
                {
                    Id = ReadString(clan, "id"),
                    Name = ReadString(clan, "name"),
                    Nickname = ReadString(clan, "nickname"),
                    Description = ReadString(clan, "description"),
                    Quote = ReadString(clan, "quote"),
                    WeaknessDescription = ReadWeaknessDescription(clan)
                });
            }

            if (result.Count == 0)
                throw new InvalidDataException("В базе данных не найдено ни одного клана.");

            var duplicateIds = result.Where(x => !string.IsNullOrWhiteSpace(x.Id))
                .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            if (duplicateIds.Count > 0)
                throw new InvalidDataException("В базе данных обнаружены повторяющиеся ID кланов: " + string.Join(", ", duplicateIds));

            _clans = result;
            return _clans;
        }

        private static string ReadString(Dictionary<string, object> source, string key)
        {
            return source.ContainsKey(key) ? Convert.ToString(source[key]) : string.Empty;
        }

        private static string ReadWeaknessDescription(Dictionary<string, object> source)
        {
            if (!source.ContainsKey("weakness")) return string.Empty;
            var weakness = source["weakness"] as Dictionary<string, object>;
            return weakness != null ? ReadString(weakness, "description") : string.Empty;
        }
    }

    internal sealed class ArchetypeInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string RegainWillpowerCondition { get; set; }
    }

    internal static class ArchetypeService
    {
        private static List<ArchetypeInfo> _archetypes;

        public static IReadOnlyList<ArchetypeInfo> GetArchetypes()
        {
            if (_archetypes != null) return _archetypes;
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "data.json");
            if (!File.Exists(path)) throw new FileNotFoundException("Не найден файл базы данных Data\\data.json.", path);
            var serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 };
            var database = serializer.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            if (database == null || !database.ContainsKey("archetypes"))
                throw new InvalidDataException("В базе данных отсутствует раздел archetypes.");
            var source = database["archetypes"] as IList;
            if (source == null) throw new InvalidDataException("Раздел archetypes имеет неверный формат.");
            _archetypes = new List<ArchetypeInfo>();
            foreach (var item in source)
            {
                var entry = item as Dictionary<string, object>;
                if (entry == null) continue;
                _archetypes.Add(new ArchetypeInfo
                {
                    Id = Read(entry, "id"), Name = Read(entry, "name"),
                    Description = Read(entry, "description"),
                    RegainWillpowerCondition = Read(entry, "regain_willpower_condition")
                });
            }
            if (_archetypes.Count == 0) throw new InvalidDataException("Список архетипов пуст.");
            return _archetypes;
        }

        public static ArchetypeInfo FindByName(string name)
        {
            return GetArchetypes().FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static string Read(Dictionary<string, object> source, string key)
        {
            return source.ContainsKey(key) ? Convert.ToString(source[key]) : string.Empty;
        }
    }

}
