using System.Windows;
using System.Windows.Controls;

namespace VtM_LoCh_fP.Controls
{
    /// <summary>Подпись и поле ввода под ней. Обычное или многострочное.</summary>
    public partial class FieldControl : UserControl
    {
        public static readonly DependencyProperty LabelProperty =
            DependencyProperty.Register(nameof(Label), typeof(string), typeof(FieldControl),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty MultilineProperty =
            DependencyProperty.Register(nameof(Multiline), typeof(bool), typeof(FieldControl),
                new PropertyMetadata(false, OnMultilineChanged));

        public static readonly DependencyProperty FieldHeightProperty =
            DependencyProperty.Register(nameof(FieldHeight), typeof(double), typeof(FieldControl),
                new PropertyMetadata(32.0));

        public FieldControl()
        {
            InitializeComponent();
            ApplyModes();
        }

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public bool Multiline
        {
            get => (bool)GetValue(MultilineProperty);
            set => SetValue(MultilineProperty, value);
        }

        public double FieldHeight
        {
            get => (double)GetValue(FieldHeightProperty);
            set => SetValue(FieldHeightProperty, value);
        }

        public string InputText
        {
            get => Input.Text;
            set => Input.Text = value;
        }

        /// <summary>Режимы, которые нельзя выразить биндингом напрямую (enum'ы).</summary>
        public TextWrapping WrapMode => Multiline ? TextWrapping.Wrap : TextWrapping.NoWrap;

        public ScrollBarVisibility ScrollMode =>
            Multiline ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;

        private static void OnMultilineChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
            => ((FieldControl)d).ApplyModes();

        private void ApplyModes()
        {
            Input.AcceptsReturn = Multiline;
            Input.TextWrapping = WrapMode;
            Input.VerticalScrollBarVisibility = ScrollMode;
        }
    }
}
