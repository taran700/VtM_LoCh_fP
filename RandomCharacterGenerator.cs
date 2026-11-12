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
                state.GenerationBatchId = Guid.NewGuid().ToString("N");
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
                GenerationStageService.GenerateAll(character, pattern, random);
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
            // До отдельного согласования вероятностей используем строгий первый приоритет
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
            state.FreebiePurchases = new List<CreationPurchase>();
            state.FreebiePoints = CharacterCreationRules.GetCreationLimit("freebies");

            // Этап 1: достоинства/недостатки могут изменить исходный бюджет
            ApplyMeritsAndFlaws(character, random);

            // Этап 2: весь оставшийся бюджет распределяется за один проход
            int remaining = Math.Max(0, state.FreebiePoints);
            if (remaining >= 7)
            {
                ApplyMandatoryDiscipline(character, random, ref remaining);
                ApplyFreebiePackage(character, random, ref remaining);
            }

            state.FreebiePoints = 0;
            state.ConfirmedFreebieBlocks = state.FreebiePurchases.Select(x => x.Block)
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            state.FreebiePurchasesConfirmed = true;
        }

        private static void ApplyMeritsAndFlaws(CharacterProfile character, Random random)
        {
            if (random.Next(1, 101) <= 50) return;

            int mode = random.Next(1, 101);
            bool takeMerits = mode <= 50 || mode >= 76;
            bool takeFlaws = mode <= 50 || (mode >= 51 && mode <= 75);
            var definitions = MeritFlawService.GetAll();
            foreach (var definition in definitions) { definition.SelectedCost = 0; definition.DependencyGroupId = null; }
            var selected = new List<MeritFlawDefinition>();
            int meritTotal = 0, flawTotal = 0;

            if (takeMerits)
                meritTotal = SelectMeritFlawSet(definitions.Where(x => Eq(x.Type, "merit")).ToList(), "merit", selected, random);
            if (takeFlaws)
                flawTotal = SelectMeritFlawSet(definitions.Where(x => Eq(x.Type, "flaw")).ToList(), "flaw", selected, random);

            foreach (var item in selected)
            {
                character.MeritsFlaws.Add(new CharacterMeritFlaw
                {
                    Id = item.Id, Name = item.Name, Type = item.Type, Cost = item.SelectedCost,
                    Description = item.Description, FreebieCharged = true
                });
                AddMeritFlawPurchase(character.CreationState, item);
            }

            character.CreationState.FreebiePoints = CharacterCreationRules.GetCreationLimit("freebies") - meritTotal + flawTotal;
        }

        private static int SelectMeritFlawSet(List<MeritFlawDefinition> pool, string type, List<MeritFlawDefinition> selected, Random random)
        {
            if (pool.Count == 0) return 0;
            int total = random.Next(1, 5);
            var parts = SplitIntoParts(total, random);
            int spent = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                int price = parts[i];
                if (price <= 0) continue;
                var available = pool.Where(x => x.Costs.Contains(price) && !Conflicts(x, selected)).ToList();
                if (available.Count == 0)
                {
                    // По правилам исчерпанное слагаемое присоединяется к следующему
                    if (i + 1 < parts.Count) { parts[i + 1] += price; continue; }
                    continue;
                }

                var chosen = available[random.Next(available.Count)];
                var requirements = chosen.Requires ?? new List<string>();
                MeritFlawDefinition dependency = null;
                int dependencyPart = -1;
                if (requirements.Count > 0)
                {
                    foreach (string requiredId in requirements)
                    {
                        dependency = pool.FirstOrDefault(x => Eq(x.Id, requiredId) && !selected.Any(s => Eq(s.Id, requiredId)));
                        if (dependency == null) continue;
                        dependencyPart = -1;
                        for (int partIndex = 0; partIndex < parts.Count; partIndex++)
                            if (partIndex > i && dependency.Costs.Contains(parts[partIndex])) { dependencyPart = partIndex; break; }
                        if (dependencyPart >= 0 && !Conflicts(dependency, selected) && !Conflicts(chosen, selected.Concat(new[] { dependency }).ToList())) break;
                        dependency = null;
                        dependencyPart = -1;
                    }
                    if (dependency == null)
                    {
                        // Зависимый элемент не подходит: перебираем только тот же ценовой пул
                        available = available.Where(x => (x.Requires == null || x.Requires.Count == 0) && !Conflicts(x, selected)).ToList();
                        if (available.Count == 0) { if (i + 1 < parts.Count) parts[i + 1] += price; continue; }
                        chosen = available[random.Next(available.Count)];
                    }
                }

                if (dependency != null && dependencyPart >= 0)
                {
                    dependency.SelectedCost = parts[dependencyPart];
                    dependency.DependencyGroupId = Guid.NewGuid().ToString("N");
                    chosen.SelectedCost = price;
                    chosen.DependencyGroupId = dependency.DependencyGroupId;
                    selected.Add(dependency);
                    spent += dependency.SelectedCost;
                    parts[dependencyPart] = 0;
                }
                chosen.SelectedCost = price;
                selected.Add(chosen);
                spent += price;
            }
            return spent;
        }

        private static bool Conflicts(MeritFlawDefinition candidate, IEnumerable<MeritFlawDefinition> selected)
        {
            foreach (var existing in selected)
            {
                if (Eq(candidate.Id, existing.Id)) return true;
                if ((candidate.IncompatibleWith ?? new List<string>()).Any(x => Eq(x, existing.Id))) return true;
                if ((existing.IncompatibleWith ?? new List<string>()).Any(x => Eq(x, candidate.Id))) return true;
            }
            return false;
        }

        private static List<int> SplitIntoParts(int total, Random random)
        {
            if (total <= 1) return new List<int> { total };
            int maxParts = Math.Min(total, 3);
            int count = random.Next(2, maxParts + 1);
            var cuts = Enumerable.Range(1, total - 1).OrderBy(_ => random.Next()).Take(count - 1).OrderBy(x => x).ToList();
            cuts.Insert(0, 0); cuts.Add(total);
            var result = new List<int>();
            for (int i = 1; i < cuts.Count; i++) result.Add(cuts[i] - cuts[i - 1]);
            return result;
        }

        private static void AddMeritFlawPurchase(CharacterCreationState state, MeritFlawDefinition item)
        {
            if (state.FreebiePurchases == null) state.FreebiePurchases = new List<CreationPurchase>();
            int signedCost = Eq(item.Type, "flaw") ? -item.SelectedCost : item.SelectedCost;
            state.FreebiePurchases.Add(new CreationPurchase
            {
                Block = "Достоинства и недостатки", ItemId = item.Id, ItemName = item.Name,
                PreviousValue = 0, NewValue = 1, Cost = signedCost, Source = "freebie",
                DependencyGroupId = item.DependencyGroupId
            });
        }

        private static void ApplyMandatoryDiscipline(CharacterProfile character, Random random, ref int remaining)
        {
            bool poor = remaining < 15;
            int roll = random.Next(1, 101);
            bool chooseNew = roll > (poor ? 90 : 80);
            var existingDisciplines = character.Disciplines.ToList();
            CharacterDiscipline selectedExisting = null;
            if (!chooseNew && existingDisciplines.Count > 0)
            {
                selectedExisting = existingDisciplines[random.Next(existingDisciplines.Count)];
                if (selectedExisting.Level >= character.DisciplineMax) chooseNew = true;
            }
            if (existingDisciplines.Count == 0) chooseNew = true;

            if (chooseNew)
            {
                var existingIds = new HashSet<string>(character.Disciplines.Select(x => x.DisciplineId), StringComparer.OrdinalIgnoreCase);
                var available = DisciplineService.GetAll().Where(x => !existingIds.Contains(x.Id) &&
                    x.Name.IndexOf("Некромант", StringComparison.OrdinalIgnoreCase) < 0 &&
                    x.Name.IndexOf("Тауматург", StringComparison.OrdinalIgnoreCase) < 0 &&
                    x.Id.IndexOf("necrom", StringComparison.OrdinalIgnoreCase) < 0 &&
                    x.Id.IndexOf("thaum", StringComparison.OrdinalIgnoreCase) < 0).ToList();
                if (available.Count > 0)
                {
                    var definition = available[random.Next(available.Count)];
                    character.Disciplines.Add(new CharacterDiscipline
                    {
                        DisciplineId = definition.Id, Name = definition.Name, Level = 1,
                        LevelCount = definition.Powers == null ? 5 : Math.Max(5, definition.Powers.Count),
                        IsCustom = definition.IsCustom, Justification = "Случайная генерация: новая Дисциплина"
                    });
                    AddFreebiePurchase(character.CreationState, "Дисциплины", definition.Id, definition.Name, 0, 1, 7);
                }
                else
                {
                    chooseNew = false;
                    var fallbackExisting = existingDisciplines.Where(x => x.Level < character.DisciplineMax).ToList();
                    selectedExisting = fallbackExisting.Count > 0 ? fallbackExisting[random.Next(fallbackExisting.Count)] : null;
                }
            }

            if (!chooseNew && selectedExisting != null && selectedExisting.Level < character.DisciplineMax)
            {
                var target = selectedExisting;
                int old = target.Level++;
                AddFreebiePurchase(character.CreationState, "Дисциплины", target.DisciplineId, target.Name, old, target.Level, 7);
            }
            remaining -= 7;
        }

        private static void ApplyFreebiePackage(CharacterProfile character, Random random, ref int remaining)
        {
            string mode = remaining == 8 ? "normal" : remaining > 8 ? "rich" : "poor";
            for (int attempt = 0; attempt < 100; attempt++)
            {
                int roll = random.Next(1, 101);
                string package;
                if (mode == "normal")
                    package = roll <= 5 ? "discipline" : roll <= 20 ? "strong_attribute_ability" : roll <= 50 ? "two_abilities_facts" : roll <= 70 ? "attribute_three_facts" : "four_facts_ability";
                else if (mode == "rich")
                    package = roll <= 25 ? "discipline_two_facts" : roll <= 35 ? "strong_attribute_two_abilities" : roll <= 45 ? "three_abilities_facts" : roll <= 80 ? "attribute_three_facts" : "facts_strong_new_virtue";
                else
                    package = roll <= 5 ? "two_abilities" : roll <= 30 ? "strong_ability_weak_virtue" : roll <= 45 ? "three_facts" : roll <= 80 ? "new_ability_two_facts" : "two_strong_abilities";

                int cost = PackageCost(package);
                if (cost > remaining) continue;
                if (!ApplyPackage(character, package, random)) continue;
                remaining -= cost;
                if (mode == "poor" && package == "two_abilities" && remaining > 0)
                {
                    AddRandomFacts(character, remaining, random);
                    remaining = 0;
                }
                break;
            }

            if (remaining > 0) ApplyRemainder(character, random, ref remaining);
        }

        private static int PackageCost(string package)
        {
            switch (package)
            {
                case "discipline": case "discipline_two_facts": return 7 + (package == "discipline_two_facts" ? 2 : 0);
                case "strong_attribute_ability": return 7;
                case "two_abilities_facts": return 6;
                case "attribute_three_facts": return 8;
                case "four_facts_ability": return 6;
                case "strong_attribute_two_abilities": case "three_abilities_facts": return 9;
                case "facts_strong_new_virtue": return 8;
                case "two_abilities": case "strong_ability_weak_virtue": case "new_ability_two_facts": case "two_strong_abilities": return 4;
                case "three_facts": return 3;
                default: return int.MaxValue;
            }
        }

        private static bool ApplyPackage(CharacterProfile character, string package, Random random)
        {
            // Проверяем вместимость до изменения листа: пакет либо применяется целиком, либо перебрасывается по таблице текущего режима
            switch (package)
            {
                case "strong_attribute_ability":
                    if (!CanImproveTrait(character, true, 1) || !CanImproveTrait(character, false, 1)) return false;
                    break;
                case "two_abilities_facts":
                case "two_abilities":
                case "two_strong_abilities":
                    if (!CanImproveTrait(character, false, 2)) return false;
                    break;
                case "four_facts_ability":
                    if (!CanImproveTrait(character, false, 1)) return false;
                    break;
                case "strong_attribute_two_abilities":
                    if (!CanImproveTrait(character, true, 1) || !CanImproveTrait(character, false, 2)) return false;
                    break;
                case "three_abilities_facts":
                    if (!CanImproveTrait(character, false, 3)) return false;
                    break;
                case "strong_ability_weak_virtue":
                    if (!CanImproveTrait(character, false, 1) || !CanImproveVirtue(character, true)) return false;
                    break;
                case "new_ability_two_facts":
                    if (!CanImproveNewAbility(character)) return false;
                    break;
                case "facts_strong_new_virtue":
                    if (!CanImproveTrait(character, false, 1) || !CanImproveNewAbility(character) || !CanImproveVirtue(character, false)) return false;
                    break;
            }
            switch (package)
            {
                case "discipline": return AddDisciplineDot(character, random);
                case "discipline_two_facts": return AddDisciplineDot(character, random) && AddRandomFacts(character, 2, random);
                case "strong_attribute_ability": return ImproveTrait(character, true, true, random) && ImproveTrait(character, false, true, random);
                case "two_abilities_facts": return ImproveTrait(character, false, false, random) && ImproveTrait(character, false, false, random) && AddRandomFacts(character, 2, random);
                case "attribute_three_facts": return ImproveTrait(character, true, false, random) && AddRandomFacts(character, 3, random);
                case "four_facts_ability": return AddRandomFacts(character, 4, random) && ImproveTrait(character, false, true, random);
                case "strong_attribute_two_abilities": return ImproveTrait(character, true, true, random) && ImproveTrait(character, false, true, random) && ImproveTrait(character, false, true, random);
                case "three_abilities_facts": return ImproveTrait(character, false, false, random) && ImproveTrait(character, false, false, random) && ImproveTrait(character, false, false, random) && AddRandomFacts(character, 3, random);
                case "facts_strong_new_virtue": return AddRandomFacts(character, 2, random) && ImproveTrait(character, false, true, random) && ImproveNewAbility(character, random) && ImproveVirtue(character, false, random);
                case "two_abilities": return ImproveTrait(character, false, false, random) && ImproveTrait(character, false, false, random);
                case "strong_ability_weak_virtue": return ImproveTrait(character, false, true, random) && ImproveVirtue(character, true, random);
                case "three_facts": return AddRandomFacts(character, 3, random);
                case "new_ability_two_facts": return ImproveNewAbility(character, random) && AddRandomFacts(character, 2, random);
                case "two_strong_abilities": return ImproveTrait(character, false, true, random) && ImproveTrait(character, false, true, random);
                default: return false;
            }
        }

        private static bool AddDisciplineDot(CharacterProfile character, Random random)
        {
            var existing = character.Disciplines.Where(x => x.Level < character.DisciplineMax).ToList();
            if (existing.Count > 0)
            {
                var target = existing[random.Next(existing.Count)]; int old = target.Level++;
                AddFreebiePurchase(character.CreationState, "Дисциплины", target.DisciplineId, target.Name, old, target.Level, 7);
                return true;
            }
            return false;
        }

        private static bool CanImproveTrait(CharacterProfile character, bool attribute, int dots)
        {
            var values = attribute ? character.CreationState.AttributeDots : character.CreationState.AbilityDots;
            return values.Sum(x => Math.Max(0, character.TraitMax - x.Value)) >= dots;
        }

        private static bool CanImproveNewAbility(CharacterProfile character)
        {
            return character.CreationState.AbilityDots.Any(x => x.Value == 0);
        }

        private static bool CanImproveVirtue(CharacterProfile character, bool weakest)
        {
            string[] names = { "Совесть", "Самоконтроль", "Смелость" };
            return names.Any(n => (character.CreationState.OtherDots.ContainsKey("virtue:" + n) ? character.CreationState.OtherDots["virtue:" + n] : 1) < 5);
        }

        private static bool ImproveTrait(CharacterProfile character, bool attribute, bool strongest, Random random)
        {
            var values = attribute ? character.CreationState.AttributeDots : character.CreationState.AbilityDots;
            var candidates = values.Where(x => x.Value < character.TraitMax).ToList();
            if (strongest && candidates.Count > 0)
            {
                int max = candidates.Max(x => x.Value);
                candidates = candidates.Where(x => x.Value == max).ToList();
            }
            if (candidates.Count == 0) return false;
            var selected = candidates[random.Next(candidates.Count)]; int old = selected.Value;
            values[selected.Key] = old + 1;
            AddFreebiePurchase(character.CreationState, attribute ? "Характеристики" : "Способности", selected.Key, selected.Key, old, old + 1,
                CharacterCreationRules.GetFreebieCost(attribute ? "attribute" : "ability"));
            return true;
        }

        private static bool ImproveNewAbility(CharacterProfile character, Random random)
        {
            var candidates = character.CreationState.AbilityDots.Where(x => x.Value == 0).ToList();
            if (candidates.Count == 0) return false;
            var selected = candidates[random.Next(candidates.Count)]; character.CreationState.AbilityDots[selected.Key] = 1;
            AddFreebiePurchase(character.CreationState, "Способности", selected.Key, selected.Key, 0, 1, 2);
            return true;
        }

        private static bool ImproveVirtue(CharacterProfile character, bool weakest, Random random)
        {
            var state = character.CreationState;
            var names = new[] { "Совесть", "Самоконтроль", "Смелость" };
            var candidates = names.Select(n => new { Name = n, Value = state.OtherDots.ContainsKey("virtue:" + n) ? state.OtherDots["virtue:" + n] : 1 })
                .Where(x => x.Value < 5).ToList();
            if (weakest && candidates.Count > 0)
            {
                int min = candidates.Min(x => x.Value); candidates = candidates.Where(x => x.Value == min).ToList();
            }
            if (candidates.Count == 0) return false;
            var chosen = candidates[random.Next(candidates.Count)];
            state.OtherDots["virtue:" + chosen.Name] = chosen.Value + 1;
            AddFreebiePurchase(state, "Добродетели", chosen.Name, chosen.Name, chosen.Value, chosen.Value + 1, 2);
            return true;
        }

        private static bool AddRandomFacts(CharacterProfile character, int count, Random random)
        {
            for (int i = 0; i < count; i++)
            {
                var candidates = character.BiographyFacts.Where(x => x.Level < 5).ToList();
                if (candidates.Count == 0)
                {
                    int willpower = character.CreationState.OtherDots.ContainsKey("willpower_permanent") ? character.CreationState.OtherDots["willpower_permanent"] : 1;
                    if (willpower < 10)
                    {
                        character.CreationState.OtherDots["willpower_permanent"] = willpower + 1;
                        AddFreebiePurchase(character.CreationState, "Сила воли", "willpower", "Сила воли", willpower, willpower + 1, 1);
                    }
                    continue;
                }
                var fact = candidates[random.Next(candidates.Count)]; int old = fact.Level++;
                character.CreationState.OtherDots["background:" + fact.Name] = fact.Level;
                AddFreebiePurchase(character.CreationState, "Факты биографии", fact.Name, fact.Name, old, fact.Level, 1);
            }
            return true;
        }

        private static void ApplyRemainder(CharacterProfile character, Random random, ref int remaining)
        {
            var state = character.CreationState;
            while (remaining > 0)
            {
                int willpower = state.OtherDots.ContainsKey("willpower_permanent") ? state.OtherDots["willpower_permanent"] : 1;
                if (willpower < 5 || character.HumanityPathRating >= 10 || remaining == 1)
                {
                    if (willpower >= 10) break;
                    state.OtherDots["willpower_permanent"] = willpower + 1;
                    AddFreebiePurchase(state, "Сила воли", "willpower", "Сила воли", willpower, willpower + 1, 1);
                    remaining--;
                }
                else if (willpower >= 5 && remaining >= 2 && character.HumanityPathRating < 10)
                {
                    int old = character.HumanityPathRating++;
                    AddFreebiePurchase(state, "Человечность", "humanity", "Человечность", old, old + 1, 2);
                    remaining -= 2;
                }
                else break;
            }
            if (remaining > 0 && state.OtherDots.ContainsKey("willpower_permanent") && state.OtherDots["willpower_permanent"] >= 10 && character.HumanityPathRating >= 10)
            {
                AddRandomFacts(character, remaining, random);
                remaining = 0;
            }
            // Если больше нечего повысить, оставшиеся СП сгорают согласно инструкции
            remaining = 0;
        }

        private static void AddFreebiePurchase(CharacterCreationState state, string block, string id, string name, int previous, int next, int cost)
        {
            if (state.FreebiePurchases == null) state.FreebiePurchases = new List<CreationPurchase>();
            state.FreebiePurchases.Add(new CreationPurchase
            {
                Block = block, ItemId = id, ItemName = name, PreviousValue = previous,
                NewValue = next, Cost = cost, Source = "freebie"
            });
        }

        private static void BuildPurchaseLedger(CharacterProfile character)
        {
            var state = character.CreationState;
            state.PurchaseLedger.Clear();
            state.NextPurchaseOrder = 1;

            void AddLevels(string block, string category, string itemId, string itemName, int from, int to, string resource, int cost, bool virtue = false, string dependencyGroupId = null)
            {
                for (int level = from; level < to; level++)
                    state.PurchaseLedger.Add(new PurchaseTransaction
                    {
                        GenerationBatchId = state.GenerationBatchId,
                        Order = state.NextPurchaseOrder++, Block = block, Category = category,
                        ItemId = itemId, ItemName = itemName, PreviousValue = level, NewValue = level + 1,
                        Resource = resource, Cost = cost, AffectsVirtueDerivedValues = virtue,
                        DependencyGroupId = dependencyGroupId, Source = resource == "freebie" ? "Generation" : "Initial"
                    });
            }

            int attributeBase = CharacterCreationRules.GetCreationLimit("attribute_base");
            foreach (var pair in state.InitialAttributeDots)
                AddLevels("Характеристики", "attribute", pair.Key, pair.Key, attributeBase, attributeBase + pair.Value, "initial_pool", 0);
            foreach (var pair in state.InitialAbilityDots)
                AddLevels("Способности", "ability", pair.Key, pair.Key, 0, pair.Value, "initial_pool", 0);

            foreach (var purchase in state.InitialPurchases ?? new List<CreationPurchase>())
            {
                string category = purchase.Block == "Добродетели" ? "virtue" : purchase.Block == "Дисциплины" ? "discipline" : purchase.Block == "Факты биографии" ? "background" : null;
                if (category == null) continue;
                int baseValue = category == "virtue" ? CharacterCreationRules.GetCreationLimit("virtue_base") : 0;
                string block = category == "background" ? "Фоны" : purchase.Block;
                AddLevels(block, category, purchase.ItemId ?? purchase.ItemName, purchase.ItemName, Math.Max(baseValue, purchase.PreviousValue), purchase.NewValue, "initial_pool", 0, category == "virtue");
            }

            foreach (var purchase in state.FreebiePurchases ?? new List<CreationPurchase>())
            {
                string category = purchase.Block == "Характеристики" ? "attribute" : purchase.Block == "Способности" ? "ability" :
                    purchase.Block == "Дисциплины" ? "discipline" : purchase.Block == "Факты биографии" ? "background" :
                    purchase.Block == "Добродетели" ? "virtue" : purchase.Block == "Человечность" ? "humanity" :
                    purchase.Block == "Сила воли" ? "willpower" : purchase.Block == "Достоинства и недостатки" ?
                    (character.MeritsFlaws.FirstOrDefault(x => Eq(x.Id, purchase.ItemId))?.Type ?? "merit") : null;
                if (category == null) continue;
                string block = category == "background" ? "Фоны" : purchase.Block;
                if (category == "merit" || category == "flaw")
                {
                    state.PurchaseLedger.Add(new PurchaseTransaction
                    {
                        GenerationBatchId = state.GenerationBatchId,
                        Order = state.NextPurchaseOrder++, Block = "Достоинства и недостатки", Category = category,
                        ItemId = purchase.ItemId, ItemName = purchase.ItemName, PreviousValue = 0, NewValue = 1,
                        Resource = "freebie", Cost = purchase.Cost, DependencyGroupId = purchase.DependencyGroupId,
                        Source = "Generation"
                    });
                    continue;
                }
                int span = purchase.NewValue - purchase.PreviousValue;
                if (span <= 0) continue;
                int unitCost = purchase.Cost / span;
                AddLevels(block, category, purchase.ItemId ?? purchase.ItemName, purchase.ItemName,
                    purchase.PreviousValue, purchase.NewValue, "freebie", unitCost, false, purchase.DependencyGroupId);
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
