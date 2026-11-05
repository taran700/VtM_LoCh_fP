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
        private string _currentClanId;
        private bool _updatingWeakness;

        private const string VentrueTasteHelp =
            "Выбор категории смертных согласуется с Рассказчиком при создании персонажа. " +
            "После начала игры изменить его нельзя. Кровь смертных вне выбранной категории, " +
            "включая кровь животных, не пополняет запас крови. Категория может быть узкой " +
            "или широкой. Кровь Сородичей не подпадает под это ограничение.";

        public PageOneView()
        {
            InitializeComponent();
            VentrueTasteInput.ToolTip = CreateVentrueTasteToolTip();
            VentrueTasteLabel.ToolTip = CreateVentrueTasteToolTip();
        }


        private ToolTip CreateVentrueTasteToolTip()
        {
            var content = new StackPanel { MaxWidth = 410 };
            content.Children.Add(new TextBlock
            {
                Text = "УТОНЧЁННЫЙ ВКУС",
                FontWeight = FontWeights.Bold,
                FontSize = 14,
                Foreground = (Brush)FindResource("HeadingBrush"),
                Margin = new Thickness(0, 0, 0, 7)
            });
            content.Children.Add(new TextBlock
            {
                Text = VentrueTasteHelp,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)FindResource("TextBrush")
            });
            return new ToolTip
            {
                Content = content,
                Style = (Style)FindResource("PopupToolTipStyle"),
                Placement = System.Windows.Controls.Primitives.PlacementMode.Mouse,
                MaxWidth = 430
            };
        }

        public void InitializeCharacter(CharacterProfile character)
        {
            _character = character;
            ClanFieldHost.Content = character.CreationMode == "Птенец на поводке Сира"
                ? CreateClanSelector()
                : (object)new FieldControl { Label = "Клан" };

            if (!string.IsNullOrWhiteSpace(character.ClanName) &&
                character.CreationMode == "Птенец на поводке Сира")
            {
                var clan = FindClanByName(character.ClanName);
                if (clan != null) ApplyClan(clan, false);
            }
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

            var tooltipTemplate = CreateClanTooltipTemplate();
            var tooltipStyle = new Style(typeof(ToolTip), (Style)FindResource("PopupToolTipStyle"));
            tooltipStyle.Setters.Add(new Setter(Control.BackgroundProperty, FindResource("CardBgBrush")));
            tooltipStyle.Setters.Add(new Setter(Control.ForegroundProperty, FindResource("TextBrush")));
            tooltipStyle.Setters.Add(new Setter(Control.BorderBrushProperty, FindResource("BorderBrush")));
            tooltipStyle.Setters.Add(new Setter(Control.BorderThicknessProperty, new Thickness(1)));
            tooltipStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(12)));
            tooltipStyle.Setters.Add(new Setter(ContentControl.ContentTemplateProperty, tooltipTemplate));
            _clanSelector.Resources.Add(typeof(ToolTip), tooltipStyle);

            var itemStyle = new Style(typeof(ComboBoxItem), (Style)FindResource(typeof(ComboBoxItem)));
            itemStyle.Setters.Add(new Setter(ToolTipService.ToolTipProperty, new Binding(".")));
            itemStyle.Setters.Add(new Setter(ToolTipService.InitialShowDelayProperty, 250));
            itemStyle.Setters.Add(new Setter(ToolTipService.ShowDurationProperty, 60000));
            _clanSelector.ItemContainerStyle = itemStyle;

            BindingOperations.SetBinding(_clanSelector, ToolTipService.ToolTipProperty,
                new Binding("SelectedItem") { Source = _clanSelector });
            ToolTipService.SetInitialShowDelay(_clanSelector, 250);
            ToolTipService.SetShowDuration(_clanSelector, 60000);

            if (!string.IsNullOrWhiteSpace(_character.ClanName))
                _clanSelector.SelectedValue = _character.ClanName;

            _clanSelector.SelectionChanged += (sender, args) =>
            {
                if (args.AddedItems.Count > 0 && args.AddedItems[0] is ClanInfo clan)
                {
                    _character.ClanName = clan.Name;
                    ApplyClan(clan, true);
                }
            };

            field.Children.Add(_clanSelector);
            return field;
        }

        private DataTemplate CreateClanTooltipTemplate()
        {
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
            return tooltipTemplate;
        }

        private ClanInfo FindClanByName(string name)
        {
            foreach (var clan in ClanService.GetClans())
                if (string.Equals(clan.Name, name, StringComparison.OrdinalIgnoreCase)) return clan;
            return null;
        }

        private void ApplyClan(ClanInfo clan, bool selectionChanged)
        {
            bool isNewClan = selectionChanged || !string.Equals(_currentClanId, clan.Id, StringComparison.OrdinalIgnoreCase);
            _currentClanId = clan.Id;

            if (isNewClan || _character.OriginalClanWeakness == null)
            {
                _character.OriginalClanWeakness = clan.WeaknessDescription ?? string.Empty;
                _character.ClanWeakness = _character.OriginalClanWeakness;
                SetWeaknessText(_character.ClanWeakness);
            }
            else
            {
                SetWeaknessText(_character.ClanWeakness ?? _character.OriginalClanWeakness);
            }

            bool isNosferatu = string.Equals(clan.Id, "nosferatu", StringComparison.OrdinalIgnoreCase);
            SocialAttributes.ForcedZeroName = isNosferatu ? "Привлекательность" : null;

            bool isVentrue = string.Equals(clan.Id, "ventrue", StringComparison.OrdinalIgnoreCase);
            VentrueTastePanel.Visibility = isVentrue ? Visibility.Visible : Visibility.Collapsed;
            VentrueTasteInput.Text = _character.VentruePreyCategory ?? string.Empty;
            VentrueTasteInput.IsReadOnly = _character.VentruePreyCategoryLocked;
            VentrueTasteInput.TextChanged -= VentrueTasteInput_TextChanged;
            VentrueTasteInput.TextChanged += VentrueTasteInput_TextChanged;
        }

        private void SetWeaknessText(string value)
        {
            _updatingWeakness = true;
            ClanWeaknessInput.Text = value ?? string.Empty;
            _updatingWeakness = false;
            UpdateResetButton();
        }

        private void ClanWeaknessInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_updatingWeakness || _character == null) return;
            _character.ClanWeakness = ClanWeaknessInput.Text;
            UpdateResetButton();
        }

        private void UpdateResetButton()
        {
            bool changed = _character != null &&
                !string.Equals(ClanWeaknessInput.Text, _character.OriginalClanWeakness ?? string.Empty,
                    StringComparison.Ordinal);
            ResetClanWeaknessButton.Visibility = changed ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ResetClanWeakness_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Вернуть исходный изъян выбранного клана? Все внесённые изменения в этом поле будут потеряны.",
                "Сброс кланового изъяна", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            _character.ClanWeakness = _character.OriginalClanWeakness ?? string.Empty;
            SetWeaknessText(_character.ClanWeakness);
        }

        private void VentrueTasteInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_character == null) return;
            _character.VentruePreyCategory = VentrueTasteInput.Text;
        }
    }
}
