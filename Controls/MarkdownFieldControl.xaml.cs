using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace VtM_LoCh_fP.Controls
{
    /// <summary>
    /// Единое Markdown-поле: хранит исходную разметку, показывает превью и
    /// переключается в редактор. Используется всеми Markdown-полями приложения.
    /// </summary>
    public partial class MarkdownFieldControl : UserControl
    {
        public static readonly DependencyProperty InputTextProperty = DependencyProperty.Register(
            nameof(InputText), typeof(string), typeof(MarkdownFieldControl),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnInputTextChanged));

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(MarkdownFieldControl),
            new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty FieldHeightProperty = DependencyProperty.Register(
            nameof(FieldHeight), typeof(double), typeof(MarkdownFieldControl),
            new PropertyMetadata(180d, OnFieldHeightChanged));

        public event TextChangedEventHandler InputTextChanged;

        private bool _updating;
        private bool _editing;
        private bool _resizeQueued;

        public MarkdownFieldControl()
        {
            InitializeComponent();

            Preview.PreviewMouseLeftButtonDown += Preview_MouseLeftButtonDown;
            Preview.MouseEnter += Preview_MouseEnter;
            Preview.MouseLeave += Preview_MouseLeave;
            Preview.Loaded += Preview_Loaded;
            Loaded += Control_Loaded;
        }

        public string InputText
        {
            get { return (string)GetValue(InputTextProperty); }
            set { SetValue(InputTextProperty, value); }
        }

        public string Label
        {
            get { return (string)GetValue(LabelProperty); }
            set { SetValue(LabelProperty, value); }
        }

        public double FieldHeight
        {
            get { return (double)GetValue(FieldHeightProperty); }
            set { SetValue(FieldHeightProperty, value); }
        }

        private static void OnInputTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            MarkdownFieldControl control = (MarkdownFieldControl)d;
            if (control.Editor == null) return;

            string text = e.NewValue as string ?? string.Empty;
            if (control.Editor.Text != text)
            {
                control._updating = true;
                control.Editor.Text = text;
                control._updating = false;
            }

            control.QueueResize();
            if (control.IsLoaded)
            {
                control.Dispatcher.BeginInvoke(new Action(delegate
                {
                    control.ApplyDocumentTheme();
                    control.QueueResize();
                }), DispatcherPriority.Loaded);
            }
        }

        private static void OnFieldHeightChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((MarkdownFieldControl)d).QueueResize();
        }

        private void Control_Loaded(object sender, RoutedEventArgs e)
        {
            QueueResize();
            UpdateEmptyTooltip();
        }

        private void Preview_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyDocumentTheme();
            QueueResize();
        }

        private void Preview_MouseEnter(object sender, MouseEventArgs e)
        {
            Preview.Cursor = Cursors.IBeam;
        }

        private void Preview_MouseLeave(object sender, MouseEventArgs e)
        {
            Preview.Cursor = Cursors.Arrow;
        }

        private void Preview_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (IsHyperlinkSource(e.OriginalSource as DependencyObject))
                return;

            e.Handled = true;
            BeginEdit();
        }

        private static bool IsHyperlinkSource(DependencyObject source)
        {
            DependencyObject current = source;
            while (current != null)
            {
                if (current is Hyperlink) return true;
                current = GetParent(current);
            }
            return false;
        }

        private static DependencyObject GetParent(DependencyObject value)
        {
            if (value == null) return null;

            FrameworkContentElement contentElement = value as FrameworkContentElement;
            if (contentElement != null)
                return contentElement.Parent;

            if (value is Visual || value is System.Windows.Media.Media3D.Visual3D)
                return VisualTreeHelper.GetParent(value);

            return LogicalTreeHelper.GetParent(value);
        }

        private void BeginEdit()
        {
            if (_editing) return;

            _editing = true;
            _updating = true;
            Editor.Text = InputText ?? string.Empty;
            _updating = false;

            Preview.Visibility = Visibility.Collapsed;
            Editor.Visibility = Visibility.Visible;
            QueueResize();

            Dispatcher.BeginInvoke(new Action(delegate
            {
                Editor.Focus();
                Keyboard.Focus(Editor);
                Editor.CaretIndex = Editor.Text.Length;
            }), DispatcherPriority.Input);
        }

        private void Editor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updating) return;

            SetCurrentValue(InputTextProperty, Editor.Text);
            TextChangedEventHandler handler = InputTextChanged;
            if (handler != null) handler(this, e);

            QueueResize();
            UpdateEmptyTooltip();
        }

        private void Editor_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            Dispatcher.BeginInvoke(new Action(delegate
            {
                if (!_editing || IsKeyboardFocusWithin) return;

                _editing = false;
                Preview.Visibility = Visibility.Visible;
                Editor.Visibility = Visibility.Collapsed;
                ApplyDocumentTheme();
                QueueResize();
                UpdateEmptyTooltip();
            }), DispatcherPriority.Background);
        }

        private void Preview_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (!_editing) QueueResize();
        }

        private void Editor_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_editing) QueueResize();
        }

        private void QueueResize()
        {
            if (!IsLoaded || _resizeQueued) return;
            _resizeQueued = true;
            Dispatcher.BeginInvoke(new Action(delegate
            {
                _resizeQueued = false;
                ResizeContent();
            }), DispatcherPriority.Loaded);
        }

        private void ResizeContent()
        {
            if (ContentHost == null || FieldBorder == null) return;

            double availableWidth = Math.Max(80, ContentHost.ActualWidth - 10);
            if (availableWidth <= 80) return;

            if (_editing)
            {
                Editor.Width = double.NaN;
                Editor.Height = double.NaN;
                Editor.Measure(new Size(availableWidth, double.PositiveInfinity));
                double editorHeight = Math.Max(32, Editor.DesiredSize.Height);
                Editor.Height = Math.Max(FieldHeight, editorHeight + 2);
            }
            else
            {
                Preview.Width = double.NaN;
                Preview.Height = double.NaN;
                Preview.Measure(new Size(availableWidth, double.PositiveInfinity));
                double previewHeight = Math.Max(32, Preview.DesiredSize.Height);
                Preview.Height = Math.Max(FieldHeight, previewHeight + 2);
            }
        }

        private void ApplyDocumentTheme()
        {
            FlowDocument document = Preview == null ? null : Preview.Document;
            if (document == null) return;

            document.Background = Brushes.Transparent;
            document.PagePadding = new Thickness(0);
            document.SetResourceReference(TextElement.ForegroundProperty, "TextBrush");

            ApplyTextElementTheme(document);
            ApplyTableTheme(document);
        }

        private void ApplyTextElementTheme(DependencyObject parent)
        {
            foreach (object item in LogicalTreeHelper.GetChildren(parent))
            {
                DependencyObject child = item as DependencyObject;
                if (child == null) continue;

                TextElement textElement = child as TextElement;
                if (textElement != null)
                {
                    textElement.SetResourceReference(TextElement.ForegroundProperty, "TextBrush");
                    Hyperlink hyperlink = textElement as Hyperlink;
                    if (hyperlink != null)
                        hyperlink.SetResourceReference(TextElement.ForegroundProperty, "AccentBrush");
                }

                ApplyTextElementTheme(child);
            }
        }

        private void ApplyTableTheme(FlowDocument document)
        {
            foreach (Block block in document.Blocks)
                ApplyTableThemeToBlock(block);
        }

        private void ApplyTableThemeToBlock(Block block)
        {
            Table table = block as Table;
            if (table != null)
            {
                table.SetResourceReference(TextElement.ForegroundProperty, "TextBrush");
                SetResourceReferenceIfAvailable(table, "Background", "InputBrush");
                SetResourceReferenceIfAvailable(table, "BorderBrush", "BorderBrush");
                foreach (TableRowGroup group in table.RowGroups)
                {
                    for (int rowIndex = 0; rowIndex < group.Rows.Count; rowIndex++)
                    {
                        TableRow row = group.Rows[rowIndex];
                        foreach (TableCell cell in row.Cells)
                        {
                            cell.SetResourceReference(TextElement.ForegroundProperty, "TextBrush");
                            SetResourceReferenceIfAvailable(cell, "Background",
                                rowIndex == 0 ? "CardHeaderBrush" : "InputBrush");
                            SetResourceReferenceIfAvailable(cell, "BorderBrush", "BorderBrush");
                            cell.BorderThickness = new Thickness(0.5);
                            cell.Padding = new Thickness(6, 3, 6, 3);
                        }
                    }
                }
                return;
            }

            Section section = block as Section;
            if (section != null)
            {
                foreach (Block nested in section.Blocks)
                    ApplyTableThemeToBlock(nested);
            }
        }

        private static void SetResourceReferenceIfAvailable(DependencyObject target, string propertyName, string resourceKey)
        {
            DependencyPropertyDescriptor descriptor = DependencyPropertyDescriptor.FromName(
                propertyName, target.GetType(), target.GetType());
            if (descriptor == null) return;
            DependencyProperty dependencyProperty = descriptor.DependencyProperty;

            FrameworkElement frameworkElement = target as FrameworkElement;
            if (frameworkElement != null)
                frameworkElement.SetResourceReference(dependencyProperty, resourceKey);
            else
            {
                FrameworkContentElement contentElement = target as FrameworkContentElement;
                if (contentElement != null)
                    contentElement.SetResourceReference(dependencyProperty, resourceKey);
            }
        }

        private void UpdateEmptyTooltip()
        {
            if (Preview == null) return;
            Preview.ToolTip = string.IsNullOrWhiteSpace(InputText)
                ? "Нажмите, чтобы ввести текст (поддерживается Markdown)"
                : null;
        }

        private void Root_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (e.Handled) return;

            ScrollViewer parent = FindParentScrollViewer(this);
            if (parent == null) return;

            int lines = Math.Max(1, Math.Abs(e.Delta) / 120) * 3;
            for (int i = 0; i < lines; i++)
            {
                if (e.Delta > 0) parent.LineUp();
                else parent.LineDown();
            }
            e.Handled = true;
        }

        private static ScrollViewer FindParentScrollViewer(DependencyObject element)
        {
            DependencyObject current = element;
            while (current != null)
            {
                current = GetParent(current);
                ScrollViewer viewer = current as ScrollViewer;
                if (viewer != null) return viewer;
            }
            return null;
        }
    }
}
