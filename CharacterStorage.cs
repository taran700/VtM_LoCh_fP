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

        public static void Export(CharacterProfile character, string filePath)
        {
            if (character == null) throw new ArgumentNullException(nameof(character));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("Не указан путь экспорта.", nameof(filePath));
            string json = Serializer.Serialize(character);
            File.WriteAllText(filePath, json, new System.Text.UTF8Encoding(false));
        }

        public static CharacterProfile Import(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                throw new FileNotFoundException("JSON-файл не найден.", filePath);
            var character = Serializer.Deserialize<CharacterProfile>(File.ReadAllText(filePath));
            if (character == null) throw new InvalidDataException("Файл не содержит объект персонажа.");
            character.Id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrWhiteSpace(character.Name))
                character.Name = Path.GetFileNameWithoutExtension(filePath);
            EnsureIdentity(character);
            return character;
        }

        // Создаёт независимую копию персонажа; Исходный объект и его файл не меняются
        public static CharacterProfile CreateMigrationCopy(CharacterProfile source, string newName)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!source.CanMigrateToFreeNeonate)
                throw new InvalidOperationException("Мигрировать можно только персонажа в режиме «Птенец».");
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("Не указано имя новой копии.", nameof(newName));

            lock (Sync)
            {
                Directory.CreateDirectory(StorageDirectory);

                // JSON-клонирование переносит все сериализуемые поля модели, включая скрытые данные и историю покупок
                var copy = Serializer.Deserialize<CharacterProfile>(Serializer.Serialize(source));
                if (copy == null) throw new InvalidDataException("Не удалось создать копию персонажа.");

                copy.Id = Guid.NewGuid().ToString("N");
                copy.Name = newName;
                copy.CreationMode = "Свободный Неонат";

                string path = Path.Combine(StorageDirectory, "character_" + copy.Id + ".json");
                string json = Serializer.Serialize(copy);
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }

                CharacterSourceFiles[copy] = Path.GetFullPath(path);
                return copy;
            }
        }

        // Полное независимое клонирование
        public static CharacterProfile CreateClone(CharacterProfile source, string newName)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (string.IsNullOrWhiteSpace(newName))
                throw new ArgumentException("Не указано имя новой копии.", nameof(newName));

            lock (Sync)
            {
                Directory.CreateDirectory(StorageDirectory);
                var copy = Serializer.Deserialize<CharacterProfile>(Serializer.Serialize(source));
                if (copy == null) throw new InvalidDataException("Не удалось создать копию персонажа.");

                copy.Id = Guid.NewGuid().ToString("N");
                copy.Name = newName;
                EnsureIdentity(copy);

                string path = Path.Combine(StorageDirectory, "character_" + copy.Id + ".json");
                string json = Serializer.Serialize(copy);
                using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false)))
                {
                    writer.Write(json);
                    writer.Flush();
                    stream.Flush(true);
                }

                CharacterSourceFiles[copy] = Path.GetFullPath(path);
                return copy;
            }
        }

        public static void Delete(CharacterProfile character)
        {
            if (character == null || string.IsNullOrWhiteSpace(character.Id)) return;
            lock (Sync)
            {
                string target = Path.GetFullPath(Path.Combine(StorageDirectory, "character_" + character.Id + ".json"));
                string source;
                if (CharacterSourceFiles.TryGetValue(character, out source))
                {
                    TryDeleteFile(source);
                    ProtectedFiles.Remove(source);
                    CharacterSourceFiles.Remove(character);
                }
                TryDeleteFile(target);
                TryDeleteFile(target + ".bak");
                TryDeleteFile(target + ".tmp");
                ProtectedFiles.Remove(target);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try { if (!string.IsNullOrWhiteSpace(path) && File.Exists(path)) File.Delete(path); }
            catch { }
        }

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

                }
            }
        }

        private static void EnsureIdentity(CharacterProfile character)
        {
            if (character.AbilityNotes == null)
                character.AbilityNotes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(character.Id))
                character.Id = Guid.NewGuid().ToString("N");
            if (character.CreationState == null)
                character.CreationState = new CharacterCreationState();
            if (character.CreationState.PurchaseLedger == null)
                character.CreationState.PurchaseLedger = new List<PurchaseTransaction>();
            if (character.CreationState.ExperiencePurchases == null)
                character.CreationState.ExperiencePurchases = new List<ExperiencePurchase>();
            if (character.CreationState.AttributeDots == null) character.CreationState.AttributeDots = new Dictionary<string, int>();
            if (character.CreationState.AbilityDots == null) character.CreationState.AbilityDots = new Dictionary<string, int>();
            if (character.CreationState.OtherDots == null) character.CreationState.OtherDots = new Dictionary<string, int>();
            if (character.CreationState.Priorities == null) character.CreationState.Priorities = new Dictionary<string, string>();
            if (character.CreationState.FreebiePurchases == null) character.CreationState.FreebiePurchases = new List<CreationPurchase>();
            if (character.CreationState.InitialPurchases == null) character.CreationState.InitialPurchases = new List<CreationPurchase>();
            if (character.CreationState.InitialVirtueRatings == null) character.CreationState.InitialVirtueRatings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (character.CreationState.InitialBackgroundDots == null) character.CreationState.InitialBackgroundDots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (character.CreationState.InitialDisciplineDots == null) character.CreationState.InitialDisciplineDots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (character.CreationState.InitialAttributeDots == null) character.CreationState.InitialAttributeDots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (character.CreationState.InitialAbilityDots == null) character.CreationState.InitialAbilityDots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (character.CreationState.ConfirmedInitialBlocks == null) character.CreationState.ConfirmedInitialBlocks = new List<string>();
            if (character.CreationState.ConfirmedFreebieBlocks == null) character.CreationState.ConfirmedFreebieBlocks = new List<string>();
            if (character.Disciplines == null) character.Disciplines = new System.Collections.ObjectModel.ObservableCollection<CharacterDiscipline>();
            if (character.CustomDisciplines == null) character.CustomDisciplines = new System.Collections.ObjectModel.ObservableCollection<DisciplineDefinition>();
            if (character.Rituals == null) character.Rituals = new System.Collections.ObjectModel.ObservableCollection<CharacterRitual>();
            if (character.BiographyFacts == null) character.BiographyFacts = new System.Collections.ObjectModel.ObservableCollection<BioFactEntry>();
            if (character.MeritsFlaws == null) character.MeritsFlaws = new System.Collections.ObjectModel.ObservableCollection<CharacterMeritFlaw>();
            MeritFlawService.SynchronizeCharacterEntries(character);
            if (character.HealthDamageStates == null) character.HealthDamageStates = new System.Collections.ObjectModel.ObservableCollection<int>();
            if (character.CustomHumanityHierarchy == null) character.CustomHumanityHierarchy = new List<HumanityPathSinEntry>();
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
