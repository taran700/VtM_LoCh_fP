namespace VtM_LoCh_fP
{
    public sealed class CharacterProfile
    {
        public string Name { get; set; }
        public string CreationMode { get; set; }
        public string ClanName { get; set; }
        public string ClanWeakness { get; set; }
        public string OriginalClanWeakness { get; set; }
        public string VentruePreyCategory { get; set; }
        public bool VentruePreyCategoryLocked { get; set; }
        public override string ToString() => Name + " — " + CreationMode;
    }
}
