using System.Collections.Generic;

namespace VtM_LoCh_fP
{
    public sealed class BioFactEntry
    {
        public string Name { get; set; }
        public int Level { get; set; }
        public bool IsCustom { get; set; }
        public string CustomDescription { get; set; }
        public List<BioFactLevelDescription> LevelDescriptions { get; set; } = new List<BioFactLevelDescription>();
        public List<BioFactLevelDescription> HolisticDescriptions { get; set; } = new List<BioFactLevelDescription>();
    }

    public sealed class BioFactLevelDescription
    {
        public int Level { get; set; }
        public string Description { get; set; }
    }
}
