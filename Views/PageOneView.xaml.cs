using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Collections.Generic;
using System.Linq;
using VtM_LoCh_fP.Controls;

namespace VtM_LoCh_fP.Views
{
    /// <summary>Страница 1: лист персонажа.</summary>
    public partial class PageOneView : UserControl
    {
        private CharacterProfile _character;
        private ComboBox _clanSelector;
        private TextBox _clanInput;
        private Popup _clanSuggestionsPopup;
        private ListBox _clanSuggestions;
        private bool _settingClanText;
        private string _currentClanId;
        private bool _updatingWeakness;
        private TextBox _natureInput;
        private TextBox _demeanorInput;
        private Button _natureDescriptionButton;
        private Button _demeanorDescriptionButton;
        private Popup _naturePopup;
        private Popup _demeanorPopup;
        private ListBox _natureSuggestions;
        private ListBox _demeanorSuggestions;
        private bool _settingArchetypeText;

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
            ClanFieldHost.Content = CreateClanSelector();
            NatureFieldHost.Content = CreateArchetypeEditor("Натура", true);
            DemeanorFieldHost.Content = CreateArchetypeEditor("Маска", false);

            if (!string.IsNullOrWhiteSpace(character.ClanName))
            {
                var clan = FindClanByName(character.ClanName);
                bool guided = !IsFreeNeonate();
                if (clan != null && (guided || character.ClanSelectedFromSuggestions))
                    ApplyClan(clan, false);
                else
                    ClearAppliedClanRules();
            }
        }

        private bool IsFreeNeonate()
        {
            return string.Equals(_character?.CreationMode, "Свободный Неонат", StringComparison.OrdinalIgnoreCase);
        }

        private UIElement CreateClanSelector()
        {
            var field = new StackPanel { Margin = new Thickness(2, 0, 4, 4) };
            field.Children.Add(new TextBlock { Text = "Клан", Style = (Style)FindResource("FieldLabelStyle") });

            if (IsFreeNeonate())
            {
                var host = new Grid();
                _clanInput = new TextBox
                {
                    MinHeight = 32,
                    Padding = new Thickness(6, 3, 6, 3),
                    Foreground = (Brush)FindResource("TextBrush"),
                    Background = (Brush)FindResource("InputBrush"),
                    BorderBrush = (Brush)FindResource("BorderBrush"),
                    VerticalContentAlignment = VerticalAlignment.Center
                };
                _clanSuggestions = new ListBox
                {
                    MaxHeight = 230,
                    DisplayMemberPath = "Name",
                    Background = (Brush)FindResource("CardBgBrush"),
                    Foreground = (Brush)FindResource("TextBrush"),
                    BorderBrush = (Brush)FindResource("BorderBrush")
                };
                _clanSuggestions.ItemContainerStyle = new Style(typeof(ListBoxItem));
                _clanSuggestions.ItemContainerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
                _clanSuggestions.ItemContainerStyle.Setters.Add(new Setter(Control.BackgroundProperty, FindResource("CardBgBrush")));
                _clanSuggestions.ItemContainerStyle.Setters.Add(new Setter(Control.ForegroundProperty, FindResource("TextBrush")));
                _clanSuggestions.ItemContainerStyle.Setters.Add(new Setter(ToolTipService.ToolTipProperty, new Binding(".")));
                _clanSuggestions.ItemContainerStyle.Setters.Add(new Setter(ToolTipService.InitialShowDelayProperty, 250));
                _clanSuggestions.ItemContainerStyle.Setters.Add(new Setter(ToolTipService.ShowDurationProperty, 60000));
                var tipStyle = new Style(typeof(ToolTip), (Style)FindResource("PopupToolTipStyle"));
                tipStyle.Setters.Add(new Setter(ContentControl.ContentTemplateProperty, CreateClanTooltipTemplate()));
                _clanSuggestions.Resources.Add(typeof(ToolTip), tipStyle);

                _clanSuggestionsPopup = new Popup
                {
                    PlacementTarget = _clanInput,
                    Placement = PlacementMode.Bottom,
                    StaysOpen = false,
                    AllowsTransparency = true,
                    Child = _clanSuggestions
                };
                BindingOperations.SetBinding(_clanSuggestionsPopup, FrameworkElement.WidthProperty,
                    new Binding("ActualWidth") { Source = _clanInput });
                _clanInput.Text = _character.ClanName ?? string.Empty;
                _clanInput.TextChanged += FreeClanInput_TextChanged;
                _clanInput.PreviewKeyDown += FreeClanInput_PreviewKeyDown;
                _clanSuggestions.SelectionChanged += FreeClanSuggestions_SelectionChanged;
                _clanSuggestions.MouseLeftButtonUp += FreeClanSuggestions_MouseLeftButtonUp;
                host.Children.Add(_clanInput);
                field.Children.Add(host);
                return field;
            }

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
            tooltipStyle.Setters.Add(new Setter(ContentControl.ContentTemplateProperty, tooltipTemplate));
            _clanSelector.Resources.Add(typeof(ToolTip), tooltipStyle);
            var itemStyle = new Style(typeof(ComboBoxItem), (Style)FindResource(typeof(ComboBoxItem)));
            itemStyle.Setters.Add(new Setter(ToolTipService.ToolTipProperty, new Binding(".")));
            itemStyle.Setters.Add(new Setter(ToolTipService.InitialShowDelayProperty, 250));
            itemStyle.Setters.Add(new Setter(ToolTipService.ShowDurationProperty, 60000));
            _clanSelector.ItemContainerStyle = itemStyle;
            BindingOperations.SetBinding(_clanSelector, ToolTipService.ToolTipProperty, new Binding("SelectedItem") { Source = _clanSelector });
            ToolTipService.SetInitialShowDelay(_clanSelector, 250);
            ToolTipService.SetShowDuration(_clanSelector, 60000);
            if (!string.IsNullOrWhiteSpace(_character.ClanName)) _clanSelector.SelectedValue = _character.ClanName;
            _clanSelector.SelectionChanged += (sender, args) =>
            {
                if (args.AddedItems.Count > 0 && args.AddedItems[0] is ClanInfo clan)
                {
                    _character.ClanName = clan.Name;
                    _character.ClanSelectedFromSuggestions = true;
                    ApplyClan(clan, true);
                }
            };
            field.Children.Add(_clanSelector);
            return field;
        }

        private void FreeClanInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_settingClanText || _character == null) return;
            string query = _clanInput.Text.Trim();
            _character.ClanName = _clanInput.Text;
            _character.ClanSelectedFromSuggestions = false;
            ClearAppliedClanRules();
            var matches = string.IsNullOrWhiteSpace(query)
                ? new List<ClanInfo>()
                : ClanService.GetClans().Where(c => c.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (!string.IsNullOrEmpty(c.Nickname) && c.Nickname.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)).ToList();
            _clanSuggestions.ItemsSource = matches;
            _clanSuggestionsPopup.IsOpen = matches.Count > 0;
        }

        private void FreeClanInput_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Down && _clanSuggestions.Items.Count > 0)
            {
                _clanSuggestions.SelectedIndex = 0;
                _clanSuggestions.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape) _clanSuggestionsPopup.IsOpen = false;
            else if (e.Key == Key.Enter && _clanSuggestions.Items.Count > 0)
            {
                if (_clanSuggestions.SelectedItem == null) _clanSuggestions.SelectedIndex = 0;
                SelectSuggestedClan(_clanSuggestions.SelectedItem as ClanInfo);
                e.Handled = true;
            }
        }

        private void FreeClanSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void FreeClanSuggestions_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            SelectSuggestedClan(_clanSuggestions.SelectedItem as ClanInfo);
        }

        private void SelectSuggestedClan(ClanInfo clan)
        {
            if (clan == null) return;
            _settingClanText = true;
            _clanInput.Text = clan.Name;
            _clanInput.CaretIndex = _clanInput.Text.Length;
            _settingClanText = false;
            _character.ClanName = clan.Name;
            _character.ClanSelectedFromSuggestions = true;
            _clanSuggestionsPopup.IsOpen = false;
            ApplyClan(clan, true);
            _clanInput.Focus();
        }

        private void ClearAppliedClanRules()
        {
            _currentClanId = null;
            _character.OriginalClanWeakness = null;
            _character.ClanWeakness = string.Empty;
            SetWeaknessText(string.Empty);
            SocialAttributes.ForcedZeroName = null;
            NosferatuPlasticSurgeryButton.Visibility = Visibility.Collapsed;
            VentrueTastePanel.Visibility = Visibility.Collapsed;
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

        private UIElement CreateArchetypeEditor(string label, bool isNature)
        {
            var panel = new StackPanel { Margin = new Thickness(2, 0, 4, 4) };
            panel.Children.Add(new TextBlock { Text = label, Style = (Style)FindResource("FieldLabelStyle") });
            var input = new TextBox
            {
                MinHeight = 32, Padding = new Thickness(6, 3, 6, 3),
                Foreground = (Brush)FindResource("TextBrush"),
                Background = (Brush)FindResource("InputBrush"),
                BorderBrush = (Brush)FindResource("BorderBrush"),
                VerticalContentAlignment = VerticalAlignment.Center
            };
            var list = new ListBox
            {
                MaxHeight = 230, DisplayMemberPath = "Name",
                Background = (Brush)FindResource("CardBgBrush"),
                Foreground = (Brush)FindResource("TextBrush"),
                BorderBrush = (Brush)FindResource("BorderBrush")
            };
            list.ItemContainerStyle = new Style(typeof(ListBoxItem));
            list.ItemContainerStyle.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(8, 6, 8, 6)));
            list.ItemContainerStyle.Setters.Add(new Setter(Control.BackgroundProperty, FindResource("CardBgBrush")));
            list.ItemContainerStyle.Setters.Add(new Setter(Control.ForegroundProperty, FindResource("TextBrush")));
            list.ItemContainerStyle.Setters.Add(new Setter(ToolTipService.ToolTipProperty, new Binding("Description")));
            list.ItemContainerStyle.Setters.Add(new Setter(ToolTipService.InitialShowDelayProperty, 250));
            list.ItemContainerStyle.Setters.Add(new Setter(ToolTipService.ShowDurationProperty, 60000));
            var tipStyle = new Style(typeof(ToolTip), (Style)FindResource("PopupToolTipStyle"));
            list.Resources.Add(typeof(ToolTip), tipStyle);
            var popup = new Popup { PlacementTarget = input, Placement = PlacementMode.Bottom, StaysOpen = false, AllowsTransparency = true, Child = list };
            BindingOperations.SetBinding(popup, FrameworkElement.WidthProperty, new Binding("ActualWidth") { Source = input });
            var addDescription = new Button
            {
                Content = "Добавить описание", HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(0, 5, 0, 0), Padding = new Thickness(9, 4, 9, 4), Visibility = Visibility.Collapsed
            };
            panel.Children.Add(input);
            panel.Children.Add(addDescription);

            if (isNature)
            {
                _natureInput = input; _natureSuggestions = list; _naturePopup = popup; _natureDescriptionButton = addDescription;
                input.Text = _character.Nature ?? string.Empty;
                if (_character.NatureSelectedFromSuggestions)
                {
                    var selected = ArchetypeService.GetArchetypes().FirstOrDefault(a => a.Name == _character.Nature);
                    if (selected != null) input.ToolTip = CreateArchetypeDescriptionToolTip(selected.Description, "Натура");
                }
                else if (!string.IsNullOrWhiteSpace(_character.NatureCustomDescription)) input.ToolTip = CreateArchetypeDescriptionToolTip(_character.NatureCustomDescription, "Натура");
                UpdateArchetypeDescriptionButton(true);
            }
            else
            {
                _demeanorInput = input; _demeanorSuggestions = list; _demeanorPopup = popup; _demeanorDescriptionButton = addDescription;
                input.Text = _character.Demeanor ?? string.Empty;
                if (_character.DemeanorSelectedFromSuggestions)
                {
                    var selected = ArchetypeService.GetArchetypes().FirstOrDefault(a => a.Name == _character.Demeanor);
                    if (selected != null) input.ToolTip = CreateArchetypeDescriptionToolTip(selected.Description, "Маска");
                }
                else if (!string.IsNullOrWhiteSpace(_character.DemeanorCustomDescription)) input.ToolTip = CreateArchetypeDescriptionToolTip(_character.DemeanorCustomDescription, "Маска");
                UpdateArchetypeDescriptionButton(false);
            }

            input.TextChanged += (sender, args) =>
            {
                if (_settingArchetypeText || _character == null) return;
                string query = input.Text.Trim();
                input.ToolTip = null;
                var matches = string.IsNullOrWhiteSpace(query) ? new List<ArchetypeInfo>() :
                    ArchetypeService.GetArchetypes().Where(a => a.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                list.ItemsSource = matches;
                popup.IsOpen = matches.Count > 0;
                if (isNature)
                {
                    _character.Nature = input.Text;
                    _character.NatureSelectedFromSuggestions = false;
                    _character.NatureCustomDescription = string.Empty;
                }
                else
                {
                    _character.Demeanor = input.Text;
                    _character.DemeanorSelectedFromSuggestions = false;
                    _character.DemeanorCustomDescription = string.Empty;
                }
                UpdateArchetypeDescriptionButton(isNature);
            };
            input.PreviewKeyDown += (sender, args) =>
            {
                if (args.Key == Key.Down && list.Items.Count > 0)
                {
                    list.SelectedIndex = 0; list.Focus(); args.Handled = true;
                }
                else if (args.Key == Key.Escape) popup.IsOpen = false;
                else if (args.Key == Key.Enter && list.Items.Count > 0)
                {
                    if (list.SelectedItem == null) list.SelectedIndex = 0;
                    SelectArchetype(list.SelectedItem as ArchetypeInfo, isNature); args.Handled = true;
                }
            };
            list.MouseLeftButtonUp += (sender, args) => SelectArchetype(list.SelectedItem as ArchetypeInfo, isNature);
            addDescription.Click += (sender, args) => OpenArchetypeDescription(isNature);
            return panel;
        }

        private void SelectArchetype(ArchetypeInfo archetype, bool isNature)
        {
            if (archetype == null) return;
            var input = isNature ? _natureInput : _demeanorInput;
            _settingArchetypeText = true;
            input.Text = archetype.Name;
            input.CaretIndex = input.Text.Length;
            _settingArchetypeText = false;
            input.ToolTip = CreateArchetypeDescriptionToolTip(archetype.Description, isNature ? "Натура" : "Маска");
            if (isNature)
            {
                _character.Nature = archetype.Name;
                _character.NatureSelectedFromSuggestions = true;
                _character.NatureCustomDescription = archetype.Description;
                _naturePopup.IsOpen = false;
            }
            else
            {
                _character.Demeanor = archetype.Name;
                _character.DemeanorSelectedFromSuggestions = true;
                _character.DemeanorCustomDescription = archetype.Description;
                _demeanorPopup.IsOpen = false;
            }
            UpdateArchetypeDescriptionButton(isNature);
            input.Focus();
        }

        private void UpdateArchetypeDescriptionButton(bool isNature)
        {
            var input = isNature ? _natureInput : _demeanorInput;
            var button = isNature ? _natureDescriptionButton : _demeanorDescriptionButton;
            if (input == null || button == null || _character == null) return;
            bool selected = isNature ? _character.NatureSelectedFromSuggestions : _character.DemeanorSelectedFromSuggestions;
            button.Visibility = !selected && !string.IsNullOrWhiteSpace(input.Text) ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OpenArchetypeDescription(bool isNature)
        {
            var input = isNature ? _natureInput : _demeanorInput;
            string current = isNature ? _character.NatureCustomDescription : _character.DemeanorCustomDescription;
            var window = new Window
            {
                Title = "Описание — " + (isNature ? "Натура" : "Маска"),
                Width = 460, Height = 300, MinWidth = 400, MinHeight = 260,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this), ResizeMode = ResizeMode.NoResize,
                Background = (Brush)FindResource("BgBrush"), Foreground = (Brush)FindResource("TextBrush")
            };
            var root = new StackPanel { Margin = new Thickness(16) };
            root.Children.Add(new TextBlock { Text = "Собственное описание (не более 500 символов)", Margin = new Thickness(0, 0, 0, 8), FontWeight = FontWeights.SemiBold });
            var editor = new TextBox { Text = current ?? string.Empty, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxLength = 500, MinHeight = 145 };
            var count = new TextBlock { Text = editor.Text.Length + "/500", HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 5, 0, 10) };
            editor.TextChanged += (sender, args) => count.Text = editor.Text.Length + "/500";
            root.Children.Add(editor); root.Children.Add(count);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var save = new Button { Content = "Сохранить", Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
            var cancel = new Button { Content = "Отмена", Padding = new Thickness(12, 5, 12, 5), IsCancel = true };
            save.Click += (sender, args) => { window.DialogResult = true; window.Close(); };
            buttons.Children.Add(save); buttons.Children.Add(cancel); root.Children.Add(buttons); window.Content = root;
            if (window.ShowDialog() == true)
            {
                if (isNature) _character.NatureCustomDescription = editor.Text;
                else _character.DemeanorCustomDescription = editor.Text;
                input.ToolTip = CreateArchetypeDescriptionToolTip(editor.Text, isNature ? "Натура" : "Маска");
            }
        }

        private ToolTip CreateArchetypeDescriptionToolTip(string description, string title)
        {
            var content = new StackPanel { MaxWidth = 390 };
            content.Children.Add(new TextBlock { Text = title.ToUpperInvariant(), FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6), Foreground = (Brush)FindResource("HeadingBrush") });
            content.Children.Add(new TextBlock { Text = description, TextWrapping = TextWrapping.Wrap, Foreground = (Brush)FindResource("TextBrush") });
            return new ToolTip { Content = content, Style = (Style)FindResource("PopupToolTipStyle"), MaxWidth = 410 };
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
            bool plasticSurgeryDone = isNosferatu && _character.NosferatuPlasticSurgery;
            SocialAttributes.ForcedZeroName = isNosferatu && !plasticSurgeryDone ? "Привлекательность" : null;
            NosferatuPlasticSurgeryButton.Visibility = isNosferatu
                ? Visibility.Visible : Visibility.Collapsed;
            NosferatuPlasticSurgeryButton.Content = plasticSurgeryDone ? "Изуродовать себя" : "Пластическая хирургия";

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
            var result = ThemedDialog.Show(
                "Вернуть исходный изъян выбранного клана? Все внесённые изменения в этом поле будут потеряны.",
                "Сброс кланового изъяна", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            _character.ClanWeakness = _character.OriginalClanWeakness ?? string.Empty;
            SetWeaknessText(_character.ClanWeakness);
        }

        private void NosferatuPlasticSurgery_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null) return;
            bool surgeryDone = _character.NosferatuPlasticSurgery;
            string message = surgeryDone
                ? "Изуродовать себя снова? Привлекательность будет обнулена и заблокирована."
                : "Применить пластическую хирургию? Привлекательность снова станет доступна для распределения.";
            string title = surgeryDone ? "Изуродовать себя" : "Пластическая хирургия";
            var result = ThemedDialog.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            _character.NosferatuPlasticSurgery = !surgeryDone;
            SocialAttributes.ForcedZeroName = _character.NosferatuPlasticSurgery ? null : "Привлекательность";
            NosferatuPlasticSurgeryButton.Content = _character.NosferatuPlasticSurgery ? "Изуродовать себя" : "Пластическая хирургия";
        }

        private void VentrueTasteInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_character == null) return;
            _character.VentruePreyCategory = VentrueTasteInput.Text;
        }
    }
}
