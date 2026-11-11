using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace VtM_LoCh_fP
{
    internal static class RandomCharacterGenerator
    {
        private static readonly Random Random = new Random();
        private static readonly object RandomSync = new object();
        private static readonly string[] AttributeGroups = { "physical", "social", "mental" };
        private static readonly string[] AbilityGroups = { "talent", "skill", "knowledge" };

        public static CharacterProfile GenerateAndImport()
        {
            lock (RandomSync)
            {
                GenerationPatternService.ValidateAgainstGameDatabase();
                var random = Random;
                var pattern = GenerationPatternService.ChooseRandomNameProfile(random);
                var clan = ChooseClan(pattern, random);
                var character = new CharacterProfile
                {
                    Name = pattern.Name,
                    CreationMode = "Свободный Неонат",
                    IsGenerated = true,
                    ClanName = clan.Name,
                    ClanSelectedFromSuggestions = true,
                    Generation = 13,
                    TraitMax = 5,
                    DisciplineMax = 5,
                    HumanityPathId = "humanity",
                    Nature = Pick(pattern.NatureCandidates, random),
                    NatureSelectedFromSuggestions = true,
                    Demeanor = Pick(pattern.DemeanorCandidates, random),
                    DemeanorSelectedFromSuggestions = true,
                    RoleName = string.Empty
                };

                // Маска выбирается один раз; амплуа всегда соответствует именно ей
                character.Demeanor = Pick(pattern.DemeanorCandidates, random);
                character.RoleName = GenerationPatternService.GetRoleForMask(character.Demeanor);
                character.SireName = GenerationPatternService.ChooseRandomSireProfile(pattern, random).Name;
                character.ClanWeakness = clan.WeaknessDescription;
                character.OriginalClanWeakness = clan.WeaknessDescription;
                var generation = GenerationService.Find(character.Generation.Value);
                if (generation != null)
                {
                    character.BloodPoolMax = generation.BloodPoolMax;
                    character.BloodPerTurn = generation.BloodPerTurn;
                    character.TraitMax = generation.TraitMax;
                    character.DisciplineMax = generation.DisciplineMax;
                }

                var state = character.CreationState;
                state.FreebiePoints = CharacterCreationRules.GetCreationLimit("freebies");
                state.ExperiencePoints = 0;
                state.BaseGeneration = character.Generation;
                GenerateTraits(state, pattern, clan.Name, random);
                GenerateVirtues(character, random);
                GenerateBackgrounds(character, pattern, random);
                GenerateDisciplines(character, clan.Name, random);
                ApplyFreebiePlan(character, random);
                BuildPurchaseLedger(character);
                GenerateBiography(character, random);
                character.CurrentBlood = 0;
                character.HealthDamageStates = new System.Collections.ObjectModel.ObservableCollection<int>(Enumerable.Repeat(0, 7));

                string temp = Path.Combine(Path.GetTempPath(), "vtm_generated_" + Guid.NewGuid().ToString("N") + ".json");
                try
                {
                    CharacterStorage.Export(character, temp);
                    return CharacterStorage.Import(temp);
                }
                finally
                {
                    try { if (File.Exists(temp)) File.Delete(temp); } catch { }
                }
            }
        }

        private static ClanInfo ChooseClan(NameGenerationPattern pattern, Random random)
        {
            var all = ClanService.GetClans();
            // До отдельного согласования вероятностей используем строгий первый приоритет.
            string preferred = pattern.ClanPriority[0];
            return all.First(x => string.Equals(x.Name, preferred, StringComparison.OrdinalIgnoreCase));
        }

        private static void GenerateTraits(CharacterCreationState state, NameGenerationPattern pattern, string clan, Random random)
        {
            var db = ReadDatabase();
            var attributes = Rows(db, "attributes");
            var abilities = Rows(db, "abilities");
            var attributePriority = GenerationPatternService.GetAttributePriority(clan);
            var abilityPriority = pattern.AbilityPriority;
            var attrGroupNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { ["physical"] = "Физические", ["social"] = "Социальные", ["mental"] = "Ментальные" };
            var abilityGroupNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            { ["talent"] = "Таланты", ["skill"] = "Навыки", ["knowledge"] = "Знания" };

            for (int i = 0; i < 3; i++)
            {
                string group = attributePriority[i];
                string key = ToAttributeKey(group);
                state.Priorities["attributes." + attrGroupNames[key]] = PriorityDisplay(i);
                int pool = CharacterCreationRules.GetPriorityPool(group, PriorityKey(i), false);
                var names = attributes.Where(x => Eq(Get(x, "group"), key)).Select(x => Get(x, "name")).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                foreach (string name in names) state.AttributeDots[name] = CharacterCreationRules.GetCreationLimit("attribute_base");
                Distribute(state.AttributeDots, names, pool, 5, random);
            }

            for (int i = 0; i < 3; i++)
            {
                string groupName = abilityPriority[i];
                string key = ToAbilityKey(groupName);
                state.Priorities["abilities." + groupName] = PriorityDisplay(i);
                int pool = CharacterCreationRules.GetPriorityPool(groupName, PriorityKey(i), true);
                var names = abilities.Where(x => Eq(Get(x, "group"), key)).Select(x => Get(x, "name")).Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                foreach (string name in names) state.AbilityDots[name] = 0;
                Distribute(state.AbilityDots, names, pool, CharacterCreationRules.GetCreationLimit("ability_creation_max"), random);
            }

            int attributeBase = CharacterCreationRules.GetCreationLimit("attribute_base");
            state.InitialAttributeDots = state.AttributeDots.ToDictionary(x => x.Key, x => Math.Max(0, x.Value - attributeBase), StringComparer.OrdinalIgnoreCase);
            state.InitialAbilityDots = state.AbilityDots.ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            state.ConfirmedInitialBlocks = new List<string> { "virtues", "backgrounds", "disciplines" };
            state.InitialVirtueBlockEverConfirmed = true;
            state.InitialBackgroundBlockEverConfirmed = true;
            state.InitialDisciplineBlockEverConfirmed = true;
            state.InitialPurchaseConfirmed = true;
            state.InitialAllocationCompleted = true;
            state.InitialAllocationEverCompleted = true;
        }

        private static void GenerateVirtues(CharacterProfile character, Random random)
        {
            var state = character.CreationState;
            string[] virtues = { "Совесть", "Самоконтроль", "Смелость" };
            int baseValue = CharacterCreationRules.GetCreationLimit("virtue_base");
            var values = virtues.ToDictionary(x => x, x => baseValue, StringComparer.OrdinalIgnoreCase);
            Distribute(values, virtues.ToList(), CharacterCreationRules.GetCreationLimit("virtues"), 5, random);
            foreach (var pair in values)
            {
                state.OtherDots["virtue:" + pair.Key] = pair.Value;
                state.InitialVirtueRatings[pair.Key] = pair.Value;
                state.InitialPurchases.Add(new CreationPurchase { Block = "Добродетели", ItemName = pair.Key, PreviousValue = baseValue, NewValue = pair.Value, Cost = Math.Max(0, pair.Value - baseValue), Source = "initial" });
            }
            state.OtherDots["willpower_permanent"] = values["Смелость"];
            character.HumanityPathRating = Math.Max(1, Math.Min(10, values["Совесть"] + values["Самоконтроль"]));
        }

        private static void GenerateBackgrounds(CharacterProfile character, NameGenerationPattern pattern, Random random)
        {
            var definitions = BioFactService.GetAll();
            var ranked = pattern.BackgroundPriority
                .SelectMany((row, rank) => row.Select(name => new { Name = name, Rank = rank }))
                .Select(x => new { Definition = definitions.FirstOrDefault(d => Eq(d.Name, x.Name)), x.Rank })
                .Where(x => x.Definition != null)
                .GroupBy(x => x.Definition.Name, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.OrderBy(x => x.Rank).First())
                .OrderBy(x => x.Rank).ToList();
            if (ranked.Count == 0) return;

            // Выбираем случайное количество от 1 до 5, не повторяя один и тот же факт
            int count = random.Next(1, Math.Min(5, ranked.Count) + 1);
            var pool = ranked.ToList();
            var chosen = new List<BioFactDefinition>();
            while (chosen.Count < count && pool.Count > 0)
            {
                int totalWeight = pool.Sum(x => Math.Max(1, 4 - x.Rank));
                int roll = random.Next(totalWeight);
                int selected = 0;
                for (int i = 0; i < pool.Count; i++)
                {
                    roll -= Math.Max(1, 4 - pool[i].Rank);
                    if (roll < 0) { selected = i; break; }
                }
                chosen.Add(pool[selected].Definition);
                pool.RemoveAt(selected);
            }

            // Все пять первоначальных пунктов распределяются случайно; каждый выбранный факт получает минимум одну точку
            int[] levels = Enumerable.Repeat(1, count).ToArray();
            int remaining = Math.Max(0, CharacterCreationRules.GetCreationLimit("backgrounds") - count);
            while (remaining > 0)
            {
                var eligible = Enumerable.Range(0, count).Where(i => levels[i] < 5).ToList();
                if (eligible.Count == 0) break;
                levels[eligible[random.Next(eligible.Count)]]++;
                remaining--;
            }

            for (int i = 0; i < chosen.Count; i++)
            {
                int level = levels[i];
                character.BiographyFacts.Add(new BioFactEntry { Name = chosen[i].Name, Level = level, IsCustom = false });
                character.CreationState.InitialBackgroundDots[chosen[i].Name] = level;
                character.CreationState.OtherDots["background:" + chosen[i].Name] = level;
                character.CreationState.InitialPurchases.Add(new CreationPurchase { Block = "Факты биографии", ItemName = chosen[i].Name, PreviousValue = 0, NewValue = level, Cost = 0, Source = "initial" });
            }
        }

        private static void GenerateDisciplines(CharacterProfile character, string clanName, Random random)
        {
            var clanIds = DisciplineService.GetClanDisciplineIds(clanName);
            var available = clanIds.Select(id => DisciplineService.GetAll().FirstOrDefault(x => Eq(x.Id, id) || Eq(x.Name, id)))
                .Where(x => x != null).GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
            if (available.Count == 0) return;
            var selected = available.OrderBy(_ => random.Next()).Take(Math.Min(3, available.Count)).ToList();
            foreach (var discipline in selected)
            {
                character.Disciplines.Add(new CharacterDiscipline
                {
                    DisciplineId = discipline.Id,
                    Name = discipline.Name,
                    Level = 1,
                    LevelCount = discipline.Powers == null ? 5 : Math.Max(5, discipline.Powers.Count),
                    IsCustom = discipline.IsCustom,
                    Justification = "Клановая дисциплина"
                });
                character.CreationState.InitialDisciplineDots[discipline.Name] = 1;
                character.CreationState.InitialPurchases.Add(new CreationPurchase { Block = "Дисциплины", ItemId = discipline.Id, ItemName = discipline.Name, PreviousValue = 0, NewValue = 1, Cost = 1, Source = "initial" });
            }
        }


        private static void ApplyFreebiePlan(CharacterProfile character, Random random)
        {
            var state = character.CreationState;
            int remaining = state.FreebiePoints;

            // Первый шаг обязателен: одна из уже выбранных клановых дисциплин получает уровень.
            var discipline = character.Disciplines.Where(x => x.Level < character.DisciplineMax).OrderBy(_ => random.Next()).FirstOrDefault();
            if (discipline != null)
            {
                int cost = CharacterCreationRules.GetFreebieCost("discipline");
                if (remaining >= cost)
                {
                    int previous = discipline.Level++;
                    remaining -= cost;
                    AddFreebiePurchase(state, "Дисциплины", discipline.DisciplineId, discipline.Name, previous, discipline.Level, cost);
                }
            }

            // Второй этап — отдельный бросок: 25% атрибут, 25% три способности, 50% предыстории + новая способность
            int branch = random.Next(100);
            if (branch < 25)
                TryImproveAttribute(character, random, ref remaining);
            else if (branch < 50)
                TryImproveThreeAbilities(character, random, ref remaining);
            else
                TryImproveBackgroundsAndNewAbility(character, random, ref remaining);

            // Остаток тратится на Человечность и Силу воли только если хватает на обе точки
            int humanityCost = CharacterCreationRules.GetFreebieCost("humanity_path");
            int willpowerCost = CharacterCreationRules.GetFreebieCost("willpower");
            int willpower = state.OtherDots.ContainsKey("willpower_permanent") ? state.OtherDots["willpower_permanent"] : 1;
            if (remaining >= humanityCost + willpowerCost && character.HumanityPathRating < 10 && willpower < character.TraitMax)
            {
                int oldHumanity = character.HumanityPathRating;
                character.HumanityPathRating++;
                AddFreebiePurchase(state, "Человечность", "humanity", "Человечность", oldHumanity, character.HumanityPathRating, humanityCost);
                int oldWillpower = willpower;
                state.OtherDots["willpower_permanent"] = ++willpower;
                AddFreebiePurchase(state, "Сила воли", "willpower", "Сила воли", oldWillpower, willpower, willpowerCost);
                remaining -= humanityCost + willpowerCost;
            }
            state.FreebiePoints = remaining;
            state.ConfirmedFreebieBlocks = state.FreebiePurchases.Select(x => x.Block)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            state.FreebiePurchasesConfirmed = state.FreebiePurchases.Count > 0;
        }

        private static void TryImproveAttribute(CharacterProfile character, Random random, ref int remaining)
        {
            int cost = CharacterCreationRules.GetFreebieCost("attribute");
            if (remaining < cost) return;
            var candidates = character.CreationState.AttributeDots.Where(x => x.Value < character.TraitMax).ToList();
            if (candidates.Count == 0) return;
            var selected = candidates[random.Next(candidates.Count)];
            int previous = selected.Value;
            character.CreationState.AttributeDots[selected.Key] = previous + 1;
            remaining -= cost;
            AddFreebiePurchase(character.CreationState, "Характеристики", selected.Key, selected.Key, previous, previous + 1, cost);
        }

        private static void TryImproveThreeAbilities(CharacterProfile character, Random random, ref int remaining)
        {
            int costPerAbility = CharacterCreationRules.GetFreebieCost("ability");
            int totalCost = costPerAbility * 3;
            if (remaining < totalCost) return;
            var candidates = character.CreationState.AbilityDots.Where(x => x.Value > 0 && x.Value < character.TraitMax)
                .OrderBy(_ => random.Next()).Take(3).ToList();
            if (candidates.Count < 3) return;
            foreach (var selected in candidates)
            {
                int previous = selected.Value;
                character.CreationState.AbilityDots[selected.Key] = previous + 1;
                AddFreebiePurchase(character.CreationState, "Способности", selected.Key, selected.Key, previous, previous + 1, costPerAbility);
            }
            remaining -= totalCost;
        }

        private static void TryImproveBackgroundsAndNewAbility(CharacterProfile character, Random random, ref int remaining)
        {
            int backgroundCost = CharacterCreationRules.GetFreebieCost("background");
            int abilityCost = CharacterCreationRules.GetFreebieCost("ability");
            int totalCost = backgroundCost * 3 + abilityCost;
            if (remaining < totalCost) return;
            var backgrounds = character.BiographyFacts.Where(x => x.Level < 5).OrderBy(_ => random.Next()).Take(3).ToList();
            var newAbility = character.CreationState.AbilityDots.Where(x => x.Value == 0).OrderBy(_ => random.Next()).FirstOrDefault();
            if (backgrounds.Count < 3 || string.IsNullOrWhiteSpace(newAbility.Key)) return;
            foreach (var background in backgrounds)
            {
                int previous = background.Level++;
                character.CreationState.OtherDots["background:" + background.Name] = background.Level;
                AddFreebiePurchase(character.CreationState, "Факты биографии", background.Name, background.Name, previous, background.Level, backgroundCost);
            }
            character.CreationState.AbilityDots[newAbility.Key] = 1;
            AddFreebiePurchase(character.CreationState, "Способности", newAbility.Key, newAbility.Key, 0, 1, abilityCost);
            remaining -= totalCost;
        }

        private static void AddFreebiePurchase(CharacterCreationState state, string block, string id, string name, int previous, int next, int cost)
        {
            state.FreebiePurchases.Add(new CreationPurchase
            {
                Block = block,
                ItemId = id,
                ItemName = name,
                PreviousValue = previous,
                NewValue = next,
                Cost = cost,
                Source = "freebie"
            });
        }

        private static void BuildPurchaseLedger(CharacterProfile character)
        {
            var state = character.CreationState;
            state.PurchaseLedger.Clear();
            state.NextPurchaseOrder = 1;

            void AddLevels(string block, string category, string itemId, string itemName, int from, int to, string resource, int cost, bool virtue = false)
            {
                for (int level = from; level < to; level++)
                    state.PurchaseLedger.Add(new PurchaseTransaction
                    {
                        Order = state.NextPurchaseOrder++, Block = block, Category = category,
                        ItemId = itemId, ItemName = itemName, PreviousValue = level, NewValue = level + 1,
                        Resource = resource, Cost = cost, AffectsVirtueDerivedValues = virtue
                    });
            }

            int attributeBase = CharacterCreationRules.GetCreationLimit("attribute_base");
            foreach (var pair in state.InitialAttributeDots)
                AddLevels("Характеристики", "attribute", pair.Key, pair.Key, attributeBase, attributeBase + pair.Value, "initial_pool", 0);
            foreach (var pair in state.InitialAbilityDots)
                AddLevels("Способности", "ability", pair.Key, pair.Key, 0, pair.Value, "initial_pool", 0);

            foreach (var purchase in state.InitialPurchases ?? new List<CreationPurchase>())
            {
                string category = purchase.Block == "Добродетели" ? "virtue" :
                    purchase.Block == "Дисциплины" ? "discipline" :
                    purchase.Block == "Факты биографии" ? "background" : null;
                if (category == null) continue;
                int baseValue = category == "virtue" ? CharacterCreationRules.GetCreationLimit("virtue_base") : 0;
                string block = category == "background" ? "Фоны" : purchase.Block;
                AddLevels(block, category, purchase.ItemId ?? purchase.ItemName, purchase.ItemName,
                    Math.Max(baseValue, purchase.PreviousValue), purchase.NewValue, "initial_pool", 0, category == "virtue");
            }

            foreach (var purchase in state.FreebiePurchases ?? new List<CreationPurchase>())
            {
                string category = purchase.Block == "Характеристики" ? "attribute" :
                    purchase.Block == "Способности" ? "ability" :
                    purchase.Block == "Дисциплины" ? "discipline" :
                    purchase.Block == "Факты биографии" ? "background" :
                    purchase.Block == "Человечность" ? "humanity" :
                    purchase.Block == "Сила воли" ? "willpower" : null;
                int span = purchase.NewValue - purchase.PreviousValue;
                if (category == null || span <= 0) continue;
                int unitCost = span == 0 ? 0 : purchase.Cost / span;
                string block = category == "background" ? "Фоны" : purchase.Block;
                AddLevels(block, category, purchase.ItemId ?? purchase.ItemName, purchase.ItemName,
                    purchase.PreviousValue, purchase.NewValue, "freebie", unitCost, category == "virtue");
            }
        }

        private static void GenerateBiography(CharacterProfile character, Random random)
        {
            int birthYear = random.Next(1400, 2008);
            int birthMonth = random.Next(1, 13);
            int birthDay = random.Next(1, DateTime.DaysInMonth(birthYear, birthMonth) + 1);
            int age = random.Next(16, 98);
            int deathYear = birthYear + age;
            int deathMonth = random.Next(1, 13);
            int deathDay = random.Next(1, 29);
            character.BiographyBirthDate = new DateTime(birthYear, birthMonth, birthDay).ToString("dd.MM.yyyy");
            character.BiographyDeathDate = new DateTime(deathYear, deathMonth, deathDay).ToString("dd.MM.yyyy");
            character.BiographyTrueAge = "???";
            character.BiographyApparentAge = age.ToString();
            character.BiographyHeight = random.Next(100, 215).ToString();
            character.BiographyWeight = random.Next(29, 131).ToString();
            character.BiographyBackstory = "Придумайте сами.";
            character.BiographyAppearance = "Придумайте сами.";
        }

        private static void Distribute(Dictionary<string, int> values, List<string> names, int points, int maximum, Random random)
        {
            if (names == null || names.Count == 0) return;
            int guard = 0;
            while (points > 0 && guard++ < 10000)
            {
                var eligible = names.Where(n => values[n] < maximum).ToList();
                if (eligible.Count == 0) break;
                string selected = eligible[random.Next(eligible.Count)];
                values[selected]++;
                points--;
            }
        }

        private static Dictionary<string, object> ReadDatabase()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "data.json");
            if (!File.Exists(path)) path = Path.Combine(Environment.CurrentDirectory, "Data", "data.json");
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 }.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
        }
        private static List<Dictionary<string, object>> Rows(Dictionary<string, object> db, string key)
        {
            return (db[key] as IList)?.Cast<object>().OfType<Dictionary<string, object>>().ToList() ?? new List<Dictionary<string, object>>();
        }
        private static string Get(Dictionary<string, object> row, string key) => row.ContainsKey(key) ? Convert.ToString(row[key]) : string.Empty;
        private static bool Eq(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        private static string Pick(IList<string> values, Random random) => values == null || values.Count == 0 ? string.Empty : values[random.Next(values.Count)];
        private static string PriorityKey(int index) => index == 0 ? "primary" : index == 1 ? "secondary" : "tertiary";
        private static string PriorityDisplay(int index) => index == 0 ? "Первичная" : index == 1 ? "Вторичная" : "Третичная";
        private static string ToAttributeKey(string value)
        {
            if (value.IndexOf("Физ", StringComparison.OrdinalIgnoreCase) >= 0) return "physical";
            if (value.IndexOf("Соц", StringComparison.OrdinalIgnoreCase) >= 0) return "social";
            return value.Equals("mental", StringComparison.OrdinalIgnoreCase) ? "mental" : "mental";
        }
        private static string ToAbilityKey(string value)
        {
            if (value.IndexOf("Талант", StringComparison.OrdinalIgnoreCase) >= 0) return "talent";
            if (value.IndexOf("Навык", StringComparison.OrdinalIgnoreCase) >= 0) return "skill";
            return "knowledge";
        }
    }
}
