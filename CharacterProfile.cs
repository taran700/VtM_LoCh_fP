using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace VtM_LoCh_fP
{
    public sealed class CharacterProfile : INotifyPropertyChanged
    {
        private string _name;
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string Id { get; set; } = System.Guid.NewGuid().ToString("N");
        public string Name
        {
            get { return _name; }
            set
            {
                if (string.Equals(_name, value, System.StringComparison.Ordinal)) return;
                _name = value;
                OnPropertyChanged();
            }
        }
        public string PlayerName { get; set; }
        public string ChronicleName { get; set; }
        public string RoleName { get; set; }
        public string ChroniclePillars { get; set; }
        public string ChronicleTenets { get; set; }
        public string HuntingStyle { get; set; }
        public int CurrentBlood { get; set; }
        public string NotesImportantNpcs { get; set; }
        public string NotesGeneralOne { get; set; }
        public string NotesImportantLocations { get; set; }
        public string NotesGeneralTwo { get; set; }
        public string NotesGeneralThree { get; set; }
        public string NotesGeneralFour { get; set; }
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
        public int? TemporaryWillpower { get; set; }
        public bool HumanityPathIsCustom { get; set; }
        public string CustomHumanityPathName { get; set; }
        public string CustomHumanityPathDescription { get; set; }
        public string CustomHumanityBearingName { get; set; }
        public string CustomHumanityBearingEffect { get; set; }
        public string CustomHumanityEdicts { get; set; }
        public string CustomHumanityFirstVirtue { get; set; } = "Совесть";
        public string CustomHumanitySecondVirtue { get; set; } = "Самоконтроль";
        public System.Collections.Generic.List<HumanityPathSinEntry> CustomHumanityHierarchy { get; set; } = new System.Collections.Generic.List<HumanityPathSinEntry>();
        public string DisplayCreationMode
        {
            get
            {
                if (string.Equals(CreationMode, "Свободный Неонат", System.StringComparison.OrdinalIgnoreCase))
                    return "Свободный Неонат";
                if (string.Equals(CreationMode, "Птенец на поводке Сира", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(CreationMode, "Птенец", System.StringComparison.OrdinalIgnoreCase))
                    return "Птенец";
                return "Режим не определён";
            }
        }

        public bool CanMigrateToFreeNeonate =>
            string.Equals(CreationMode, "Птенец на поводке Сира", System.StringComparison.OrdinalIgnoreCase) ||
            string.Equals(CreationMode, "Птенец", System.StringComparison.OrdinalIgnoreCase);

        public override string ToString() => Name + " — " + DisplayCreationMode;
    }
}
