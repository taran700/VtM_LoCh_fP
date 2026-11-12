using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace VtM_LoCh_fP.Controls
{

    /// Список однотипных дорожек значений. Названия задаются прямо во вьювсах

    [ContentProperty(nameof(Names))]
    public partial class TrackListControl : UserControl
    {
        public static readonly DependencyProperty CountProperty =
            DependencyProperty.Register(nameof(Count), typeof(int), typeof(TrackListControl),
                new PropertyMetadata(5));

        public static readonly DependencyProperty ShapeProperty =
            DependencyProperty.Register(nameof(Shape), typeof(PipShape), typeof(TrackListControl),
                new PropertyMetadata(PipShape.Dots));

        public static readonly DependencyProperty ColumnsProperty =
            DependencyProperty.Register(nameof(Columns), typeof(int), typeof(TrackListControl),
                new PropertyMetadata(0));

        public static readonly DependencyProperty LabelWidthProperty =
            DependencyProperty.Register(nameof(LabelWidth), typeof(double), typeof(TrackListControl),
                new PropertyMetadata(150.0));

        public static readonly DependencyProperty IsEditableLabelProperty =
            DependencyProperty.Register(nameof(IsEditableLabel), typeof(bool), typeof(TrackListControl),
                new PropertyMetadata(false));

        public static readonly DependencyProperty CascadeProperty =
            DependencyProperty.Register(nameof(Cascade), typeof(bool), typeof(TrackListControl),
                new PropertyMetadata(true));

        public static readonly DependencyProperty ShowNotesProperty = DependencyProperty.Register(nameof(ShowNotes), typeof(bool), typeof(TrackListControl), new PropertyMetadata(false));

        public static readonly DependencyProperty ForcedZeroNameProperty =
            DependencyProperty.Register(nameof(ForcedZeroName), typeof(string), typeof(TrackListControl),
                new PropertyMetadata(null, OnForcedZeroNameChanged));

        public TrackListControl()
        {
            Names = new ObservableCollection<string>();
            InitializeComponent();
            Loaded += (sender, args) => ApplyForcedZeroName();
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public ObservableCollection<string> Names { get; }

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

        public bool ShowNotes { get => (bool)GetValue(ShowNotesProperty); set => SetValue(ShowNotesProperty, value); }

        public string ForcedZeroName
        {
            get => (string)GetValue(ForcedZeroNameProperty);
            set => SetValue(ForcedZeroNameProperty, value);
        }

        private static void OnForcedZeroNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((TrackListControl)d).ApplyForcedZeroName();

        private void ApplyForcedZeroName()
        {
            ApplyForcedZeroNameToChildren(this);
        }

        private void ApplyForcedZeroNameToChildren(DependencyObject parent)
        {
            int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is TrackControl track)
                    track.IsForcedZero = !string.IsNullOrWhiteSpace(ForcedZeroName) &&
                        string.Equals(track.Label, ForcedZeroName, System.StringComparison.OrdinalIgnoreCase);
                ApplyForcedZeroNameToChildren(child);
            }
        }
    }
}
