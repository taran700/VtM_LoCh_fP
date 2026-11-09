using System.Collections.ObjectModel;

namespace VtM_LoCh_fP
{
    public sealed class CharacterProfile
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString("N");
        public string Name { get; set; }
        public CharacterCreationState CreationState { get; set; } = new CharacterCreationState();
        public string CreationMode { get; set; }
        public string ClanName { get; set; }
        public int? Generation { get; set; } = 13;
        public int BloodPoolMax { get; set; } = 10;
        public int BloodPerTurn { get; set; } = 1;
        public int TraitMax { get; set; } = 5;
        public int DisciplineMax { get; set; } = 5;
        public string SireName { get; set; }
        public string SireDescription { get; set; }
        public string EquipmentClothing { get; set; }
        public string EquipmentPockets { get; set; }
        public string EquipmentArmor { get; set; }
        public string EquipmentWeapons { get; set; }
        public System.Collections.ObjectModel.ObservableCollection<int> HealthDamageStates { get; set; } = new System.Collections.ObjectModel.ObservableCollection<int>();
        public System.Collections.ObjectModel.ObservableCollection<CharacterDiscipline> Disciplines { get; set; } = new System.Collections.ObjectModel.ObservableCollection<CharacterDiscipline>();
        public System.Collections.ObjectModel.ObservableCollection<DisciplineDefinition> CustomDisciplines { get; set; } = new System.Collections.ObjectModel.ObservableCollection<DisciplineDefinition>();
        public System.Collections.ObjectModel.ObservableCollection<CharacterRitual> Rituals { get; set; } = new System.Collections.ObjectModel.ObservableCollection<CharacterRitual>();
        public bool AllowAggravatedDamage { get; set; }
        public bool ClanSelectedFromSuggestions { get; set; }
        public string Nature { get; set; }
        public bool NatureSelectedFromSuggestions { get; set; }
        public string NatureCustomDescription { get; set; }
        public string NatureCustomWillpowerRecovery { get; set; }
        public string Demeanor { get; set; }
        public bool DemeanorSelectedFromSuggestions { get; set; }
        public string DemeanorCustomDescription { get; set; }
        public string DemeanorCustomWillpowerRecovery { get; set; }
        public string ClanWeakness { get; set; }
        public string OriginalClanWeakness { get; set; }
        public string VentruePreyCategory { get; set; }
        public bool VentruePreyCategoryLocked { get; set; }
        public bool NosferatuPlasticSurgery { get; set; }
        public ObservableCollection<BioFactEntry> BiographyFacts { get; set; } = new ObservableCollection<BioFactEntry>();
        public ObservableCollection<CharacterMeritFlaw> MeritsFlaws { get; set; } = new ObservableCollection<CharacterMeritFlaw>();
        public string BiographyBirthDate { get; set; }
        public string BiographyDeathDate { get; set; }
        public string BiographyTrueAge { get; set; }
        public string BiographyApparentAge { get; set; }
        public string BiographyHeight { get; set; }
        public string BiographyWeight { get; set; }
        public string BiographyBackstory { get; set; }
        public string BiographyAppearance { get; set; }
        public string HumanityPathId { get; set; } = "humanity";
        public int HumanityPathRating { get; set; } = 2;
        public bool HumanityPathIsCustom { get; set; }
        public string CustomHumanityPathName { get; set; }
        public string CustomHumanityPathDescription { get; set; }
        public string CustomHumanityBearingName { get; set; }
        public string CustomHumanityBearingEffect { get; set; }
        public string CustomHumanityEdicts { get; set; }
        public string CustomHumanityFirstVirtue { get; set; } = "Совесть";
        public string CustomHumanitySecondVirtue { get; set; } = "Самоконтроль";
        public System.Collections.Generic.List<HumanityPathSinEntry> CustomHumanityHierarchy { get; set; } = new System.Collections.Generic.List<HumanityPathSinEntry>();
        public override string ToString() => Name + " — " + CreationMode;
    }
}
