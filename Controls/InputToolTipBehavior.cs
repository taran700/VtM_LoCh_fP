using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace VtM_LoCh_fP.Controls
{
    public static class InputToolTipBehavior
    {
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            EventManager.RegisterClassHandler(typeof(FrameworkElement), FrameworkElement.ToolTipOpeningEvent,
                new ToolTipEventHandler(Input_ToolTipOpening), true);
        }

        private static void Input_ToolTipOpening(object sender, ToolTipEventArgs e)
        {
            var owner = sender as FrameworkElement;
            if (owner == null || ToolTipService.GetToolTip(owner) == null) return;
            if (IsSuggestionItem(owner)) return;

            var field = FindInputField(owner);
            if (field == null) return;

            if (field.IsKeyboardFocusWithin || !IsInputEmpty(field))
                e.Handled = true;
        }

        private static bool IsSuggestionItem(DependencyObject element)
        {
            while (element != null)
            {
                if (element is ListBoxItem || element is ComboBoxItem)
                    return true;
                element = GetParent(element);
            }
            return false;
        }

        private static bool IsInputEmpty(FrameworkElement field)
        {
            if (field is TextBox textBox)
                return string.IsNullOrWhiteSpace(textBox.Text);

            if (field is RichTextBox richTextBox)
            {
                var range = new TextRange(richTextBox.Document.ContentStart, richTextBox.Document.ContentEnd);
                return string.IsNullOrWhiteSpace(range.Text);
            }

            if (field is ComboBox comboBox)
                return comboBox.SelectedItem == null && comboBox.SelectedValue == null &&
                    string.IsNullOrWhiteSpace(comboBox.Text);

            if (field is DatePicker datePicker)
                return !datePicker.SelectedDate.HasValue && string.IsNullOrWhiteSpace(datePicker.Text);

            if (field is PasswordBox passwordBox)
                return string.IsNullOrEmpty(passwordBox.Password);

            if (field is FieldControl fieldControl)
                return string.IsNullOrWhiteSpace(fieldControl.InputText);

            if (field is MarkdownFieldControl markdownField)
                return string.IsNullOrWhiteSpace(markdownField.InputText);

            return true;
        }

        private static FrameworkElement FindInputField(DependencyObject current)
        {
            while (current != null)
            {
                var element = current as FrameworkElement;
                if (IsInputField(element)) return element;
                current = GetParent(current);
            }
            return null;
        }

        private static bool IsInputField(FrameworkElement element)
        {
            return element is TextBoxBase || element is ComboBox || element is DatePicker ||
                element is PasswordBox || element is FieldControl || element is MarkdownFieldControl;
        }

        private static DependencyObject GetParent(DependencyObject element)
        {
            if (element is FrameworkContentElement contentElement)
                return contentElement.Parent;
            if (element is Visual || element is Visual3D)
                return VisualTreeHelper.GetParent(element);
            return LogicalTreeHelper.GetParent(element);
        }
    }
}
