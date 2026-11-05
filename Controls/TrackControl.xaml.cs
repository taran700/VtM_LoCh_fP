using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using VtM_LoCh_fP.ViewModels;
using System.Windows.Media.Effects;

namespace VtM_LoCh_fP.Controls
{
    public enum PipShape
    {
        Dots,
        Squares,
        SquaresH
    }

    /// <summary>
    /// Дорожка значений: подпись (возможно редактируемая) и ряд точек либо квадратов.
    /// Отметка каскадная: клик закрашивает фишку и всё левее, снятие очищает всё правее.
    /// </summary>
    public partial class TrackControl : UserControl
    {
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(TrackControl),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty CountProperty =
            DependencyProperty.Register(nameof(Count), typeof(int), typeof(TrackControl),
                new PropertyMetadata(5, OnCountChanged));

        public static readonly DependencyProperty ShapeProperty =
            DependencyProperty.Register(nameof(Shape), typeof(PipShape), typeof(TrackControl),
                new PropertyMetadata(PipShape.Dots, OnShapeChanged));

        /// <summary>Число колонок для квадратов: 0 — один ряд, 5 — два ряда по пять.</summary>
        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(nameof(Columns), typeof(int), typeof(TrackControl),
                new PropertyMetadata(0, OnColumnsChanged));

        public static readonly DependencyProperty LabelWidthProperty =
            DependencyProperty.Register(nameof(LabelWidth), typeof(double), typeof(TrackControl),
                new PropertyMetadata(140.0, OnLabelWidthChanged));

        public static readonly DependencyProperty IsEditableLabelProperty =
            DependencyProperty.Register(nameof(IsEditableLabel), typeof(bool), typeof(TrackControl),
                new PropertyMetadata(false, OnEditableChanged));

        public static readonly DependencyProperty CascadeProperty =
            DependencyProperty.Register(nameof(Cascade), typeof(bool), typeof(TrackControl),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ItemsProperty =
            DependencyProperty.Register(nameof(Items), typeof(ObservableCollection<PipItem>), typeof(TrackControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty IsForcedZeroProperty =
            DependencyProperty.Register(nameof(IsForcedZero), typeof(bool), typeof(TrackControl),
                new PropertyMetadata(false, OnForcedZeroChanged));

        private bool _syncing;
        private bool _userInputInProgress;
        private Popup _descriptionPopup;

        public string InfoDescription { get; set; }
        public string InfoAdditionalText { get; set; }
        public System.Func<string> InfoAdditionalTextProvider { get; set; }
        public System.Collections.Generic.List<VtM_LoCh_fP.TrackLevelDescription> InfoLevels { get; set; }

        public TrackControl()
        {
            InitializeComponent();
            Rebuild();
            ApplyShape();
            ApplyEditable();
            Loaded += (sender, args) => ApplySquareColumns();
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public int Count
        {
            get => (int)GetValue(CountProperty);
            set => SetValue(CountProperty, value);
        }

        public PipShape Shape
        {
            get => (PipShape)GetValue(ShapeProperty);
            set => SetValue(ShapeProperty, value);
        }

        public int Columns
        {
            get => (int)GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, value);
        }

        public double LabelWidth
        {
            get => (double)GetValue(LabelWidthProperty);
            set => SetValue(LabelWidthProperty, value);
        }

        public bool IsEditableLabel
        {
            get => (bool)GetValue(IsEditableLabelProperty);
            set => SetValue(IsEditableLabelProperty, value);
        }

        public bool Cascade
        {
            get => (bool)GetValue(CascadeProperty);
            set => SetValue(CascadeProperty, value);
        }

        public ObservableCollection<PipItem> Items
        {
            get => (ObservableCollection<PipItem>)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public bool IsForcedZero
        {
            get => (bool)GetValue(IsForcedZeroProperty);
            set => SetValue(IsForcedZeroProperty, value);
        }

        /// <summary>Текущее количество закрашенных фишек.</summary>
        public event System.EventHandler ValueChanged;

        public void SetTrackValue(int value)
        {
            if (Items == null) return;
            value = System.Math.Max(0, System.Math.Min(Count, value));
            _syncing = true;
            for (int i = 0; i < Items.Count; i++) Items[i].IsChecked = i < value;
            _syncing = false;
        }

        public int Value
        {
            get
            {
                if (Items == null) return 0;
                int value = 0;
                foreach (PipItem pip in Items) if (pip.IsChecked) value++;
                return value;
            }
        }

        private static void OnCountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((TrackControl)d).Rebuild();

        private static void OnShapeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((TrackControl)d).ApplyShape();

        private static void OnColumnsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((TrackControl)d).ApplySquareColumns();

        private static void OnLabelWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((TrackControl)d).LabelColumn.Width = new GridLength((double)e.NewValue);

        private static void OnEditableChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((TrackControl)d).ApplyEditable();

        private static void OnForcedZeroChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((TrackControl)d).ApplyForcedZero();

        private void Rebuild()
        {
            int count = Count < 0 ? 0 : Count;
            var items = new ObservableCollection<PipItem>();
            for (int i = 0; i < count; i++) items.Add(new PipItem(i));
            Items = items;
            if (IsForcedZero) ApplyForcedZero();
        }

        private void ApplyShape()
        {
            SquareItems.Visibility = Shape == PipShape.Squares ? Visibility.Visible : Visibility.Collapsed;
            DotItems.Visibility = Shape == PipShape.Dots ? Visibility.Visible : Visibility.Collapsed;
            SquareItemsH.Visibility = Shape == PipShape.SquaresH ? Visibility.Visible : Visibility.Collapsed;
            if (Shape == PipShape.Squares) ApplySquareColumns();
        }

        private void ApplyEditable()
        {
            LabelInput.Visibility = IsEditableLabel ? Visibility.Visible : Visibility.Collapsed;
            LabelText.Visibility = IsEditableLabel ? Visibility.Collapsed : Visibility.Visible;
        }

        private void ApplyForcedZero()
        {
            DotItems.IsEnabled = !IsForcedZero;
            SquareItems.IsEnabled = !IsForcedZero;
            SquareItemsH.IsEnabled = !IsForcedZero;
            LabelText.TextDecorations = null;
            ForcedZeroStrike.Visibility = IsForcedZero ? Visibility.Visible : Visibility.Collapsed;
            ForcedZeroStrikeSecond.Visibility = IsForcedZero ? Visibility.Visible : Visibility.Collapsed;
            if (!IsForcedZero || Items == null) return;

            _syncing = true;
            foreach (PipItem item in Items) item.IsChecked = false;
            _syncing = false;
        }

        private void ApplySquareColumns()
        {
            var panel = FindChild<UniformGrid>(SquareItems);
            if (panel == null) return;
            panel.Columns = Columns > 0 ? Columns : (Count > 0 ? Count : 1);
        }

        private void Pip_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _userInputInProgress = true;
        }

        private void Pip_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Space || e.Key == System.Windows.Input.Key.Enter)
                _userInputInProgress = true;
        }

        private void Pip_Checked(object sender, RoutedEventArgs e) => ApplyCascade(e, true);

        private void Pip_Unchecked(object sender, RoutedEventArgs e) => ApplyCascade(e, false);

        private void ApplyCascade(RoutedEventArgs e, bool isChecked)
        {
            if (!_userInputInProgress || _syncing || !Cascade || Items == null) return;
            if (!(e.OriginalSource is FrameworkElement source) || !(source.DataContext is PipItem pip)) return;
            // Программная пересборка дорожки тоже поднимает событие — реагируем только на реальный клик.
            if (pip.IsChecked != isChecked) return;

            int value = TrackLogic.ValueAfterToggle(pip.Index, isChecked);
            _syncing = true;
            for (int i = 0; i < Items.Count; i++) Items[i].IsChecked = i < value;
            _syncing = false;
            _userInputInProgress = false;
            ValueChanged?.Invoke(this, System.EventArgs.Empty);
        }


        private void LabelText_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            ShowInfoPopup(LabelText);
        }

        public void ShowInfoPopup(FrameworkElement placementTarget)
        {
            var info = VtM_LoCh_fP.TrackDescriptionService.Find(Label);
            string description = !string.IsNullOrWhiteSpace(InfoDescription) ? InfoDescription : info?.Description;
            var levels = InfoLevels ?? info?.Levels;
            if (string.IsNullOrWhiteSpace(description) && (levels == null || levels.Count == 0)) return;

            var panel = new StackPanel { MaxWidth = 360 };
            panel.Children.Add(new TextBlock
            {
                Text = Label,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("HeadingBrush"),
                Margin = new Thickness(0, 0, 0, 6)
            });
            panel.Children.Add(new TextBlock
            {
                Text = description ?? string.Empty,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("TextBrush"),
                Visibility = string.IsNullOrWhiteSpace(description) ? Visibility.Collapsed : Visibility.Visible
            });

            string additionalText = InfoAdditionalTextProvider != null ? InfoAdditionalTextProvider() : InfoAdditionalText;
            if (!string.IsNullOrWhiteSpace(additionalText))
            {
                panel.Children.Add(new Border
                {
                    Height = 1,
                    Background = (Brush)FindResource("BorderBrush"),
                    Margin = new Thickness(0, 8, 0, 8)
                });
                panel.Children.Add(new TextBlock
                {
                    Text = "ВОССТАНОВЛЕНИЕ ВОЛИ",
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("HeadingBrush"),
                    Margin = new Thickness(0, 0, 0, 4)
                });
                panel.Children.Add(new TextBlock
                {
                    Text = additionalText,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = (Brush)FindResource("TextBrush")
                });
            }

            if (Value > 0 && levels != null)
            {
                var level = levels.Find(x => x.Level == Value);
                if (level != null && !string.IsNullOrWhiteSpace(level.Description))
                {
                    panel.Children.Add(new Border
                    {
                        Height = 1,
                        Background = (Brush)FindResource("BorderBrush"),
                        Margin = new Thickness(0, 8, 0, 8)
                    });
                    panel.Children.Add(new TextBlock
                    {
                        Text = string.IsNullOrWhiteSpace(level.Rank) ? "Уровень " + level.Level : "Уровень " + level.Level + " — " + level.Rank,
                        FontWeight = FontWeights.SemiBold,
                        Foreground = (Brush)FindResource("HeadingBrush"),
                        Margin = new Thickness(0, 0, 0, 4)
                    });
                    panel.Children.Add(new TextBlock
                    {
                        Text = level.Description,
                        TextWrapping = TextWrapping.Wrap,
                        Foreground = (Brush)FindResource("TextBrush")
                    });
                }
            }

            var border = new Border
            {
                Child = panel,
                Padding = new Thickness(12),
                Background = (Brush)FindResource("CardBgBrush"),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Effect = new DropShadowEffect { BlurRadius = 14, ShadowDepth = 3, Opacity = 0.3 }
            };
            // Используем Popup напрямую, а не свойство FrameworkElement.ToolTip:
            // ToolTipService оборачивает ToolTip в собственную подсказку и не допускает
            // вложенный ToolTip с StaysOpen=false. Popup не пересоздаётся через ToolTipService.
            if (_descriptionPopup == null)
            {
                _descriptionPopup = new Popup
                {
                    PlacementTarget = placementTarget ?? LabelText,
                    Placement = PlacementMode.Right,
                    HorizontalOffset = 8,
                    AllowsTransparency = true,
                    StaysOpen = true,
                    Focusable = false
                };
            }

            _descriptionPopup.PlacementTarget = placementTarget ?? LabelText;
            _descriptionPopup.Child = border;
            _descriptionPopup.IsOpen = true;
        }

        private void LabelText_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            HideInfoPopup();
        }

        public void HideInfoPopup()
        {
            if (_descriptionPopup != null)
                _descriptionPopup.IsOpen = false;
        }

        private static T FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            if (parent == null) return null;
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T match) return match;
                var nested = FindChild<T>(child);
                if (nested != null) return nested;
            }
            return null;
        }
    }
}
