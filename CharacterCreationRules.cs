using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;

namespace VtM_LoCh_fP
{

    public sealed class CharacterCreationState : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        private int _freebiePoints = 15;
        private int _experiencePoints;
        public CharacterCreationState()
        {
            try { _freebiePoints = CharacterCreationRules.GetCreationLimit("freebies"); }
            catch { _freebiePoints = 15; }
        }
        private void Notify(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        public Dictionary<string, int> AttributeDots { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> AbilityDots { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, int> OtherDots { get; set; } = new Dictionary<string, int>();
        public Dictionary<string, string> Priorities { get; set; } = new Dictionary<string, string>();
        public List<CreationPurchase> FreebiePurchases { get; set; } = new List<CreationPurchase>();
        public List<CreationPurchase> InitialPurchases { get; set; } = new List<CreationPurchase>();
        public List<ExperiencePurchase> ExperiencePurchases { get; set; } = new List<ExperiencePurchase>();
        public List<PurchaseTransaction> PurchaseLedger { get; set; } = new List<PurchaseTransaction>();
        public long NextPurchaseOrder { get; set; } = 1;
        public bool IsRestoringPurchase { get; set; }
        public Dictionary<string, int> InitialVirtueRatings { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> InitialBackgroundDots { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> InitialDisciplineDots { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public bool InitialVirtueBlockEverConfirmed { get; set; }
        public bool InitialBackgroundBlockEverConfirmed { get; set; }
        public bool InitialDisciplineBlockEverConfirmed { get; set; }
        public int? BaseGeneration { get; set; }
        public bool InitialAllocationCompleted { get; set; }
        public bool InitialAllocationEverCompleted { get; set; }
        public Dictionary<string, int> InitialAttributeDots { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, int> InitialAbilityDots { get; set; } = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        public bool InitialPurchaseConfirmed { get; set; }
        public List<string> ConfirmedInitialBlocks { get; set; } = new List<string>();
        public void UnconfirmInitialBlock(string key)
        {
            if (ConfirmedInitialBlocks == null) return;
            ConfirmedInitialBlocks.RemoveAll(x => string.Equals(x, key, StringComparison.OrdinalIgnoreCase));
            string[] all = { "virtues", "backgrounds", "disciplines" };
            InitialPurchaseConfirmed = all.All(k => ConfirmedInitialBlocks.Contains(k, StringComparer.OrdinalIgnoreCase));
        }
        public void ConfirmInitialBlock(string key)
        {
            if (ConfirmedInitialBlocks == null) ConfirmedInitialBlocks = new List<string>();
            if (!ConfirmedInitialBlocks.Contains(key, StringComparer.OrdinalIgnoreCase)) ConfirmedInitialBlocks.Add(key);
            string[] all = { "virtues", "backgrounds", "disciplines" };
            InitialPurchaseConfirmed = all.All(k => ConfirmedInitialBlocks.Contains(k, StringComparer.OrdinalIgnoreCase));
        }
        public bool FreebiePurchasesConfirmed { get; set; }
        public List<string> ConfirmedFreebieBlocks { get; set; } = new List<string>();
        public int FreebiePoints
        {
            get => _freebiePoints;
            set
            {
                if (_freebiePoints == value) return;
                _freebiePoints = value;
                FreebiePurchasesConfirmed = false;
                Notify(nameof(FreebiePoints));
            }
        }
        public int ExperiencePoints
        {
            get => _experiencePoints;
            set { if (_experiencePoints == value) return; _experiencePoints = value; Notify(nameof(ExperiencePoints)); }
        }

        public int FreebieSpent => FreebiePurchases == null ? 0 : FreebiePurchases.Sum(p => Math.Max(0, p.Cost));
    }

    public sealed class CreationPurchase
    {
        public string Block { get; set; }
        public string ItemId { get; set; }
        public string ItemName { get; set; }
        public int PreviousValue { get; set; }
        public int NewValue { get; set; }
        public int Cost { get; set; }
        public string Source { get; set; } = "initial";
    }

    public sealed class PurchaseTransaction
    {
        public long Order { get; set; }
        public string Block { get; set; }
        public string Category { get; set; }
        public string ItemId { get; set; }
        public string ItemName { get; set; }
        public int PreviousValue { get; set; }
        public int NewValue { get; set; }
        public string Resource { get; set; } 
        public int Cost { get; set; }
        public bool AffectsVirtueDerivedValues { get; set; }
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class ExperiencePurchase
    {
        public string Category { get; set; }
        public string ItemName { get; set; }
        public int PreviousValue { get; set; }
        public int NewValue { get; set; }
        public int Cost { get; set; }
        public string Source { get; set; } = "experience";
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    }

    public static class CharacterCreationRules
    {
        private static readonly string[] PriorityOrder = { "primary", "secondary", "tertiary" };

        public static bool ValidatePriorities(string first, string second, string third)
        {
            var values = new[] { first, second, third };
            return values.All(v => PriorityOrder.Contains(v)) && values.Distinct().Count() == 3;
        }

        public static int GetPriorityPool(string category, string priority, bool abilities)
        {
            if (!PriorityOrder.Contains(priority)) throw new ArgumentException("Неизвестный приоритет.");
            dynamic root = DataRules.Load();
            dynamic rules = root.ContainsKey("base_rules") ? root["base_rules"] : root;
            string key = abilities ? "abilities" : "attributes";
            return Convert.ToInt32(rules["character_creation_limits"][key][priority]);
        }

        public static int GetCreationLimit(string key)
        {
            dynamic root = DataRules.Load();
            dynamic rules = root.ContainsKey("base_rules") ? root["base_rules"] : root;
            return Convert.ToInt32(rules["character_creation_limits"][key]);
        }

        public static int GetExperienceCost(string kind, bool newTrait)
        {
            dynamic root = DataRules.Load();
            dynamic rules = root.ContainsKey("base_rules") ? root["base_rules"] : root;
            dynamic table = rules["experience_costs"];
            if (AppSettings.StorytellerExperiencePrices)
            {
                try
                {
                    dynamic storyteller = rules["storyteller_experience_costs"];
                    if (storyteller != null && storyteller.ContainsKey(kind))
                        table = storyteller;
                }
                catch { }
            }
            dynamic costs = table[kind];
            return Convert.ToInt32(costs[newTrait ? "new" : "current"]);
        }

        public static bool UsesCaitiffDisciplineRules(CharacterProfile character)
        {
            if (character == null || string.IsNullOrWhiteSpace(character.ClanName))
                return false;

            if (character.ClanName.IndexOf("каити", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            return string.Equals(character.CreationMode, "Свободный Неонат", StringComparison.OrdinalIgnoreCase)
                && !character.ClanSelectedFromSuggestions;
        }

        public static int GetExperienceCostForLevel(string kind, int currentLevel, bool isCaitiff = false, bool isClanDiscipline = false)
        {
            switch (kind)
            {
                case "attribute": return currentLevel == 0 ? 5 : currentLevel * 4;
                case "ability": return currentLevel == 0 ? 3 : currentLevel * 2;
                case "discipline":
                    if (currentLevel == 0) return 10;
                    return currentLevel * (isCaitiff ? 6 : isClanDiscipline ? 5 : 7);
                case "secondary_path": return currentLevel == 0 ? 7 : currentLevel * 4;
                case "virtue":
                case "humanity_path": return currentLevel * 2;
                case "willpower": return currentLevel;
                default: return GetExperienceCost(kind, currentLevel == 0);
            }
        }

        public static int GetCaitiffDisciplineMultiplier()
        {
            dynamic root = DataRules.Load();
            dynamic rules = root.ContainsKey("base_rules") ? root["base_rules"] : root;
            return Convert.ToInt32(rules["caitiff_chargen"]["experience_costs"]["discipline_multiplier"]);
        }

        public static int GetFreebieCost(string kind)
        {
            if (string.Equals(kind, "secondary_path", StringComparison.OrdinalIgnoreCase))
                kind = "discipline";
            dynamic root = DataRules.Load();
            dynamic rules = root.ContainsKey("base_rules") ? root["base_rules"] : root;
            try
            {
                dynamic costs = rules["freebie_costs"];
                if (costs != null && costs.ContainsKey(kind))
                    return Convert.ToInt32(costs[kind]);
            }
            catch { }
            dynamic caitiff = rules["caitiff_chargen"];
            dynamic fallback = caitiff["freebie_costs"];
            if (fallback == null || !fallback.ContainsKey(kind))
                throw new InvalidOperationException("В базе не задана стоимость свободных пунктов для категории: " + kind);
            return Convert.ToInt32(fallback[kind]);
        }
    }

    internal static class DataRules
    {
        private static IDictionary<string, dynamic> _cached;
        public static dynamic Load()
        {
            if (_cached != null) return _cached;
            string path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "data.json");
            if (!System.IO.File.Exists(path))
                path = System.IO.Path.Combine(Environment.CurrentDirectory, "Data", "data.json");
            var serializer = new System.Web.Script.Serialization.JavaScriptSerializer();
            _cached = serializer.Deserialize<Dictionary<string, dynamic>>(System.IO.File.ReadAllText(path));
            return _cached;
        }
    }
}
