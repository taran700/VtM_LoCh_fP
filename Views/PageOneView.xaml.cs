using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using VtM_LoCh_fP.Controls;

namespace VtM_LoCh_fP.Views
{
    /// <summary>Страница 1: лист персонажа.</summary>
    public partial class PageOneView : UserControl
    {
        private CharacterProfile _character;
        private ComboBox _clanSelector;

        public PageOneView()
        {
            InitializeComponent();
        }

        public void InitializeCharacter(CharacterProfile character)
        {
            _character = character;
            ClanFieldHost.Content = character.CreationMode == "Птенец на поводке Сира"
                ? CreateClanSelector()
                : (object)new FieldControl { Label = "Клан" };
        }

        private UIElement CreateClanSelector()
        {
            var field = new StackPanel { Margin = new Thickness(2, 0, 4, 4) };
            field.Children.Add(new TextBlock
            {
                Text = "Клан",
                Style = (Style)FindResource("FieldLabelStyle")
            });

            _clanSelector = new ComboBox
            {
                MinHeight = 32,
                ItemsSource = ClanService.GetClans(),
                DisplayMemberPath = "Name",
                SelectedValuePath = "Name",
                Foreground = (Brush)FindResource("TextBrush"),
                Background = (Brush)FindResource("InputBrush"),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                Padding = new Thickness(6, 3, 6, 3)
            };

            var tooltipTemplate = new DataTemplate();
            var tooltipPanel = new FrameworkElementFactory(typeof(StackPanel));
            tooltipPanel.SetValue(StackPanel.MaxWidthProperty, 390.0);

            var clanTitle = new FrameworkElementFactory(typeof(TextBlock));
            clanTitle.SetBinding(TextBlock.TextProperty, new Binding("Name"));
            clanTitle.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            clanTitle.SetValue(TextBlock.FontSizeProperty, 15.0);
            clanTitle.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 0, 7));
            tooltipPanel.AppendChild(clanTitle);

            var clanQuote = new FrameworkElementFactory(typeof(TextBlock));
            clanQuote.SetBinding(TextBlock.TextProperty, new Binding("Quote"));
            clanQuote.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            clanQuote.SetValue(TextBlock.FontStyleProperty, FontStyles.Italic);
            clanQuote.SetValue(TextBlock.FontSizeProperty, 13.0);
            clanQuote.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 0, 9));
            tooltipPanel.AppendChild(clanQuote);

            var clanDescription = new FrameworkElementFactory(typeof(TextBlock));
            clanDescription.SetBinding(TextBlock.TextProperty, new Binding("Description"));
            clanDescription.SetValue(TextBlock.TextWrappingProperty, TextWrapping.Wrap);
            clanDescription.SetValue(TextBlock.FontSizeProperty, 13.0);
            tooltipPanel.AppendChild(clanDescription);

            tooltipTemplate.VisualTree = tooltipPanel;

            var tooltipStyle = new Style(typeof(ToolTip));
            tooltipStyle.Setters.Add(new Setter(Control.BackgroundProperty, FindResource("CardBgBrush")));
            tooltipStyle.Setters.Add(new Setter(Control.ForegroundProperty, FindResource("TextBrush")));
            tooltipStyle.Setters.Add(new Setter(Control.BorderBrushProperty, FindResource("BorderBrush")));
            tooltipStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            tooltipStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12)));
            tooltipStyle.Setters.Add(new Setter(ContentControl.ContentTemplateProperty, tooltipTemplate));
            _clanSelector.Resources.Add(typeof(ToolTip), tooltipStyle);

            var itemStyle = new Style(typeof(ComboBoxItem));
            itemStyle.Setters.Add(new Setter(ToolTipService.ToolTipProperty, new Binding(".")));
            itemStyle.Setters.Add(new Setter(ToolTipService.InitialShowDelayProperty, 250));
            itemStyle.Setters.Add(new Setter(ToolTipService.ShowDurationProperty, 60000));
            _clanSelector.ItemContainerStyle = itemStyle;

            BindingOperations.SetBinding(
                _clanSelector,
                ToolTipService.ToolTipProperty,
                new Binding("SelectedItem") { Source = _clanSelector });
            ToolTipService.SetInitialShowDelay(_clanSelector, 250);
            ToolTipService.SetShowDuration(_clanSelector, 60000);

            if (!string.IsNullOrWhiteSpace(_character.ClanName))
                _clanSelector.SelectedValue = _character.ClanName;

            _clanSelector.SelectionChanged += (sender, args) =>
            {
                if (args.AddedItems.Count > 0 && args.AddedItems[0] is ClanInfo clan)
                    _character.ClanName = clan.Name;
            };

            field.Children.Add(_clanSelector);
            return field;
        }
    }
}
