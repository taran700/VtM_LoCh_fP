namespace VtM_LoCh_fP
{
    public sealed class CharacterProfile
    {
        public string Name { get; set; }
        public string CreationMode { get; set; }
        public string ClanName { get; set; }
        public override string ToString() => Name + " — " + CreationMode;
    }
}
