namespace VtM_LoCh_fP
{
    internal sealed class CharacterRulesService
    {
        private readonly CharacterProfile _character;

        public CharacterRulesService(CharacterProfile character)
        {
            _character = character;
        }

        public string GetCreationRulesStatus()
        {
            return "ЗАГЛУШКА";
        }

        public string GetPointsStatus()
        {
            return "ЗАГЛУШКА";
        }
    }
}
