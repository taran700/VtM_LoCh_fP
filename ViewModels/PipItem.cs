using System.Collections.ObjectModel;
using System.ComponentModel;

namespace VtM_LoCh_fP.ViewModels
{
    /// <summary>Одна точка или один квадрат в дорожке значений.</summary>
    public sealed class PipItem : INotifyPropertyChanged
    {
        private bool _isChecked;

        public PipItem(int index) => Index = index;

        public int Index { get; }

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

    /// <summary>Уровень здоровья с трёхпозиционной отметкой: пусто, одна палка, крест.</summary>
    public sealed class WoundLevel : INotifyPropertyChanged
    {
        private int _state;

        public WoundLevel(string name) => Name = name;

        public string Name { get; }

        public int State
        {
            get => _state;
            set
            {
                if (_state == value) return;
                _state = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(State)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StateTag)));
            }
        }

        /// <summary>Состояние строкой: шаблон кнопки сравнивает его в DataTrigger.</summary>
        public string StateTag => _state.ToString();

        public event PropertyChangedEventHandler PropertyChanged;
    }

    /// <summary>Общий список уровней здоровья V20.</summary>
    public static class WoundLevels
    {
        public static ObservableCollection<WoundLevel> CreateDefault() => new ObservableCollection<WoundLevel>
        {
            new WoundLevel("Помят"),
            new WoundLevel("Легко ранен  (−1)"),
            new WoundLevel("Ранен  (−1)"),
            new WoundLevel("Серьёзно ранен  (−2)"),
            new WoundLevel("Тяжело ранен  (−2)"),
            new WoundLevel("Едва жив  (−5)"),
            new WoundLevel("При смерти")
        };
    }
}
