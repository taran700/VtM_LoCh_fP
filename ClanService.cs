using System;
using System.Collections;
using System.Collections.Generic;
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

            var serializer = new JavaScriptSerializer();
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

            if (result.Count != 13)
                throw new InvalidDataException("Ожидалось 13 кланов, загружено: " + result.Count + ".");

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
}
