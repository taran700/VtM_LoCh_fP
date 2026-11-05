using System.Collections.ObjectModel;

namespace VtM_LoCh_fP
{
    public sealed class CharacterProfile
    {
        public string Name { get; set; }
        public string CreationMode { get; set; }
        public string ClanName { get; set; }
        public bool ClanSelectedFromSuggestions { get; set; }
        public string Nature { get; set; }
        public bool NatureSelectedFromSuggestions { get; set; }
        public string NatureCustomDescription { get; set; }
        public string Demeanor { get; set; }
        public bool DemeanorSelectedFromSuggestions { get; set; }
        public string DemeanorCustomDescription { get; set; }
        public string ClanWeakness { get; set; }
        public string OriginalClanWeakness { get; set; }
        public string VentruePreyCategory { get; set; }
        public bool VentruePreyCategoryLocked { get; set; }
        public bool NosferatuPlasticSurgery { get; set; }
        public ObservableCollection<BioFactEntry> BiographyFacts { get; set; } = new ObservableCollection<BioFactEntry>();
        public override string ToString() => Name + " — " + CreationMode;
    }
}
