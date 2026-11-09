using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace VtM_LoCh_fP
{
    internal static class CharacterStorage
    {
        private static readonly object Sync = new object();
        private static readonly List<string> LoadIssuesInternal = new List<string>();
        private static readonly HashSet<string> ProtectedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<CharacterProfile, string> CharacterSourceFiles = new Dictionary<CharacterProfile, string>();
        public static IReadOnlyList<string> LoadIssues
        {
            get { lock (Sync) return LoadIssuesInternal.ToList().AsReadOnly(); }
        }

        public static void ProtectCharacter(CharacterProfile character)
        {
            if (character == null || string.IsNullOrWhiteSpace(character.Id)) return;
            lock (Sync)
            {
                string target = Path.GetFullPath(Path.Combine(StorageDirectory, "character_" + character.Id + ".json"));
                ProtectedFiles.Add(target);
                string source;
                if (CharacterSourceFiles.TryGetValue(character, out source))
                    ProtectedFiles.Add(source);
            }
        }

        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer
        {
            MaxJsonLength = int.MaxValue,
            RecursionLimit = 256
        };

        private static string StorageDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "VtM_LoCh_fP");
        private static string LegacyStorageFile => Path.Combine(StorageDirectory, "characters.json");

        public static List<CharacterProfile> Load()
        {
            lock (Sync)
            {
                var result = new List<CharacterProfile>();
                LoadIssuesInternal.Clear();
                ProtectedFiles.Clear();
                CharacterSourceFiles.Clear();
                try
                {
                    Directory.CreateDirectory(StorageDirectory);
                    if (File.Exists(LegacyStorageFile))
                    {
                        try
                        {
                            var legacy = Serializer.Deserialize<List<CharacterProfile>>(File.ReadAllText(LegacyStorageFile));
                            if (legacy != null)
                            {
                                foreach (var character in legacy)
                                {
                                    EnsureIdentity(character);
                                    WriteCharacter(character);
                                    result.Add(character);
                                }
                            }
                            string migrated = LegacyStorageFile + ".migrated";
                            if (!File.Exists(migrated)) File.Move(LegacyStorageFile, migrated);
                        }
                        catch (Exception ex)
                        {
                            ProtectedFiles.Add(Path.GetFullPath(LegacyStorageFile));
                            LoadIssuesInternal.Add("Старое общее сохранение characters.json: " + ex.Message);
                        }
                    }

                    foreach (string file in Directory.GetFiles(StorageDirectory, "character_*.json"))
                    {
                        try
                        {
                            var character = Serializer.Deserialize<CharacterProfile>(File.ReadAllText(file));
                            if (character == null) continue;
                            EnsureIdentity(character);
                            CharacterSourceFiles[character] = Path.GetFullPath(file);
                            result.RemoveAll(c => string.Equals(c.Id, character.Id, StringComparison.OrdinalIgnoreCase));
                            result.Add(character);
                        }
                        catch (Exception ex)
                        {
                            ProtectedFiles.Add(Path.GetFullPath(file));
                            LoadIssuesInternal.Add(Path.GetFileName(file) + ": " + ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    LoadIssuesInternal.Add("Не удалось прочитать папку сохранений: " + ex.Message);
                }
                return result.OrderBy(c => c.Name ?? string.Empty, StringComparer.CurrentCultureIgnoreCase).ToList();
            }
        }

        public static void Save(IEnumerable<CharacterProfile> characters)
        {
            if (characters == null) return;
            lock (Sync)
            {
                try
                {
                    Directory.CreateDirectory(StorageDirectory);
                    var list = characters.Where(c => c != null).ToList();
                    foreach (var character in list)
                    {
                        EnsureIdentity(character);
                        string characterPath = Path.GetFullPath(Path.Combine(StorageDirectory, "character_" + character.Id + ".json"));
                        string sourcePath;
                        bool sourceIsProtected = CharacterSourceFiles.TryGetValue(character, out sourcePath) && ProtectedFiles.Contains(sourcePath);
                        if (!sourceIsProtected && !ProtectedFiles.Contains(characterPath))
                            WriteCharacter(character);
                    }

                    // Удаляем только файлы персонажей, которые больше не присутствуют в списке
                    var activeIds = new HashSet<string>(list.Select(c => c.Id), StringComparer.OrdinalIgnoreCase);
                    foreach (string file in Directory.GetFiles(StorageDirectory, "character_*.json"))
                    {
                        string id = Path.GetFileNameWithoutExtension(file).Substring("character_".Length);
                        if (!activeIds.Contains(id) && !ProtectedFiles.Contains(Path.GetFullPath(file)))
                        {
                            try { File.Delete(file); } catch { }
                        }
                    }
                }
                catch
                {
                    // Ошибка диска или доступа не должна закрывать лист персонажа
                }
            }
        }

        private static void EnsureIdentity(CharacterProfile character)
        {
            if (string.IsNullOrWhiteSpace(character.Id))
                character.Id = Guid.NewGuid().ToString("N");
            if (character.CreationState == null)
                character.CreationState = new CharacterCreationState();
            if (character.CreationState.PurchaseLedger == null)
                character.CreationState.PurchaseLedger = new List<PurchaseTransaction>();
            if (character.CreationState.ExperiencePurchases == null)
                character.CreationState.ExperiencePurchases = new List<ExperiencePurchase>();
        }

        private static void WriteCharacter(CharacterProfile character)
        {
            string path = Path.Combine(StorageDirectory, "character_" + character.Id + ".json");
            string temp = path + ".tmp";
            string json = Serializer.Serialize(character);
            File.WriteAllText(temp, json);
            if (File.Exists(path))
            {
                try { File.Replace(temp, path, path + ".bak", true); }
                catch { File.Copy(temp, path, true); File.Delete(temp); }
            }
            else File.Move(temp, path);
        }
    }
}
