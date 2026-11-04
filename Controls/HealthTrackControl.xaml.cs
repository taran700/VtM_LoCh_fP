using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using VtM_LoCh_fP.ViewModels;

namespace VtM_LoCh_fP.Controls
{
    /// <summary>
    /// Уровни здоровья. Каждая отметка циклично меняет состояние:
    /// пусто → одна палка → две палки (крест) → пусто.
    /// </summary>
    public partial class HealthTrackControl : UserControl
    {
        public static readonly DependencyProperty LevelsProperty =
            DependencyProperty.Register(nameof(Levels), typeof(ObservableCollection<WoundLevel>),
                typeof(HealthTrackControl), new PropertyMetadata(null));

        public HealthTrackControl()
        {
            Levels = WoundLevels.CreateDefault();
            InitializeComponent();
        }

        public ObservableCollection<WoundLevel> Levels
        {
            get => (ObservableCollection<WoundLevel>)GetValue(LevelsProperty);
            set => SetValue(LevelsProperty, value);
        }

        private void Mark_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is FrameworkElement element) || !(element.DataContext is WoundLevel level)) return;
            level.State = TrackLogic.NextHealthState(level.State);
        }
    }
}
