using System.Collections.ObjectModel;
using System.ComponentModel;

namespace VtM_LoCh_fP.ViewModels
{
    /// Одна точка или один квадрат в дорожке значений
    public sealed class PipItem : INotifyPropertyChanged
    {
        private bool _isChecked;
        private bool _isEnabled = true;

        public PipItem(int index) => Index = index;

        public int Index { get; }

        public bool IsEnabled
        {
            get => _isEnabled;
            set { if (_isEnabled == value) return; _isEnabled = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled))); }
        }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked == value) return;
                _isChecked = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// Один слот здоровья: 0 — пусто, 1 — лёгкое, 2 — тяжёлое, 3 — губительное
    public sealed class WoundLevel : INotifyPropertyChanged
    {
        private int _state;
        public WoundLevel(string name, string penalty) { Name = name; Penalty = penalty; }
        public string Name { get; }
        public string Penalty { get; }
        private string _statusDescription;
        public string StatusDescription
        {
            get => _statusDescription;
            set
            {
                if (_statusDescription == value) return;
                _statusDescription = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusDescription)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusTooltip)));
            }
        }
        public string StatusTooltip => Name + "\n\nСкорость и состояние персонажа:\n" + (string.IsNullOrWhiteSpace(StatusDescription) ? "Не указано в базе данных." : StatusDescription);
        public int State { get => _state; set { if (_state == value) return; _state = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StateTag))); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Mark))); } }
        public string StateTag => _state.ToString();
        public string Mark => _state == 1 ? "/" : _state == 2 ? "X" : _state == 3 ? "■" : string.Empty;
        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// Общий список уровней здоровья V20
    public static class WoundLevels
    {
        public static ObservableCollection<WoundLevel> CreateDefault() => new ObservableCollection<WoundLevel>
        {
            new WoundLevel("Помят", "0"),
            new WoundLevel("Легко ранен", "−1"),
            new WoundLevel("Ранен", "−1"),
            new WoundLevel("Серьёзно ранен", "−2"),
            new WoundLevel("Тяжело ранен", "−2"),
            new WoundLevel("Едва жив", "−5"),
            new WoundLevel("При смерти", "—")
        };
    }
}
