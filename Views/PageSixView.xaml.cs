using System.Windows;
using System.Windows.Controls;

namespace VtM_LoCh_fP.Views
{
    public partial class PageSixView : UserControl
    {
        private CharacterProfile _character;
        private bool _loading;
        public PageSixView() { InitializeComponent(); }

        public void InitializeCharacter(CharacterProfile character)
        {
            _character = character;
            _loading = true;
            ClothingInput.InputText = character.EquipmentClothing ?? "";
            PocketsInput.InputText = character.EquipmentPockets ?? "";
            ArmorInput.InputText = character.EquipmentArmor ?? "";
            WeaponsInput.InputText = character.EquipmentWeapons ?? "";
            _loading = false;
        }

        private void Equipment_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loading || _character == null) return;
            _character.EquipmentClothing = ClothingInput.InputText;
            _character.EquipmentPockets = PocketsInput.InputText;
            _character.EquipmentArmor = ArmorInput.InputText;
            _character.EquipmentWeapons = WeaponsInput.InputText;
        }

        private void WeaponDetails_Click(object sender, RoutedEventArgs e)
            => EquipmentInfoWindows.ShowWeapons(Window.GetWindow(this));

        private void ArmorDetails_Click(object sender, RoutedEventArgs e)
            => EquipmentInfoWindows.ShowArmor(Window.GetWindow(this));
    }
}