using System.Windows.Controls;

namespace VtM_LoCh_fP.Views
{
    public partial class PageFiveView : UserControl
    {
        private CharacterProfile _character;
        private bool _loading;

        public PageFiveView()
        {
            InitializeComponent();
        }

        public void InitializeCharacter(CharacterProfile character)
        {
            _character = character;
            _loading = true;
            ImportantNpcsField.InputText = character.NotesImportantNpcs ?? string.Empty;
            GeneralNotesOneField.InputText = character.NotesGeneralOne ?? string.Empty;
            ImportantLocationsField.InputText = character.NotesImportantLocations ?? string.Empty;
            GeneralNotesTwoField.InputText = character.NotesGeneralTwo ?? string.Empty;
            GeneralNotesThreeField.InputText = character.NotesGeneralThree ?? string.Empty;
            GeneralNotesFourField.InputText = character.NotesGeneralFour ?? string.Empty;
            _loading = false;
        }

        private void NotesField_InputTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || _character == null) return;
            var field = sender as VtM_LoCh_fP.Controls.FieldControl;
            if (field == null) return;
            switch (System.Convert.ToString(field.Tag))
            {
                case "Npcs": _character.NotesImportantNpcs = field.InputText; break;
                case "GeneralOne": _character.NotesGeneralOne = field.InputText; break;
                case "Locations": _character.NotesImportantLocations = field.InputText; break;
                case "GeneralTwo": _character.NotesGeneralTwo = field.InputText; break;
                case "GeneralThree": _character.NotesGeneralThree = field.InputText; break;
                case "GeneralFour": _character.NotesGeneralFour = field.InputText; break;
            }
        }
    }
}
