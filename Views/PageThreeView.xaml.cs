using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using VtM_LoCh_fP.Controls;

namespace VtM_LoCh_fP.Views
{
    public partial class PageThreeView : UserControl
    {
        private CharacterProfile _character;
        public event Action InitialPurchaseProgressChanged;
        public event Func<string, string, int, int, bool> AdvancementCheck;
        public PageThreeView() { InitializeComponent(); }

        public void InitializeCharacter(CharacterProfile character)
        {
            _character=character;
            if(_character.Disciplines==null)_character.Disciplines=new System.Collections.ObjectModel.ObservableCollection<CharacterDiscipline>();
            if(_character.CustomDisciplines==null)_character.CustomDisciplines=new System.Collections.ObjectModel.ObservableCollection<DisciplineDefinition>();
            if(_character.Rituals==null)_character.Rituals=new System.Collections.ObjectModel.ObservableCollection<CharacterRitual>();
            foreach (var group in _character.Disciplines.GroupBy(x => x.DisciplineId, StringComparer.OrdinalIgnoreCase))
            {
                bool first = true;
                foreach (var path in group)
                {
                    if (!first) path.IsSecondaryPath = true;
                    first = false;
                }
            }
            RenderDisciplines();
        }

        public void RefreshGenerationLimits()
        {
            if (_character != null) RenderDisciplines();
        }

        public void OpenDisciplineCatalog()
        {
            OpenDisciplineCatalog_Click(this, new RoutedEventArgs());
        }

        private void OpenDisciplineCatalog_Click(object sender,RoutedEventArgs e)
        {
            if(_character==null)return;
            if (string.IsNullOrWhiteSpace(_character.ClanName))
            {
                ThemedDialog.Show(Window.GetWindow(this), "Сначала выберите клан на первой вкладке листа персонажа, затем можно будет выбрать дисциплину.", "Не выбран клан", MessageBoxButton.OK, MessageBoxImage.Warning, "Перейти к листу", () => (Window.GetWindow(this) as MainWindow)?.NavigateToTab(0));
                return;
            }

            var all=DisciplineService.GetAll().ToList();
            all.AddRange(_character.CustomDisciplines);
            var clanIds = new HashSet<string>(DisciplineService.GetClanDisciplineIds(_character.ClanName), StringComparer.OrdinalIgnoreCase);
            bool caitiff = CharacterCreationRules.UsesCaitiffDisciplineRules(_character);
            bool initialDisciplinesConfirmed = _character.CreationState != null &&
                _character.CreationState.ConfirmedInitialBlocks != null &&
                _character.CreationState.ConfirmedInitialBlocks.Contains("disciplines");

            // До подтверждения первоначальной закупки обычный клан может выбирать
            // только свои клановые дисциплины. Каитифы и созданные вручную кланы
            // в режиме свободного неоната сразу видят весь каталог.
            // После подтверждения первоначальной закупки каталог открывается полностью.
            if (!initialDisciplinesConfirmed && !caitiff)
                all = all.Where(d => clanIds.Contains(d.Id)).ToList();

            var chosen=DisciplineWindows.Choose(Window.GetWindow(this),all,clanIds,
                string.Equals(_character.CreationMode,"Свободный Неонат",StringComparison.OrdinalIgnoreCase),
                custom=>{var existing=_character.CustomDisciplines.FirstOrDefault(x=>x.Id==custom.Id);if(existing==null)_character.CustomDisciplines.Add(custom);});
            if(chosen==null)return;
            var selected=_character.Disciplines.FirstOrDefault(x=>
                x.DisciplineId==chosen.Id &&
                string.Equals(x.PathId ?? string.Empty, chosen.PathId ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            if(selected==null)
            {
                bool secondaryPath = _character.Disciplines.Any(x =>
                    string.Equals(x.DisciplineId, chosen.Id, StringComparison.OrdinalIgnoreCase));
                _character.Disciplines.Add(new CharacterDiscipline
                {
                    DisciplineId=chosen.Id, Name=chosen.Name, Level=0, IsCustom=chosen.IsCustom,
                    PathId=chosen.PathId, PathName=chosen.PathName, IsSecondaryPath=secondaryPath
                });
            }
            else if(!string.IsNullOrEmpty(chosen.PathId))
            {
                selected.PathId=chosen.PathId;
                selected.PathName=chosen.PathName;
                selected.Name=chosen.Name;
            }
            RenderDisciplines();
            InitialPurchaseProgressChanged?.Invoke();
        }

        private void StudyRitual_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null) return;
            RitualWindows.Show(Window.GetWindow(this), _character);
            RenderRitualCards();
        }

        private static string GetDisciplineSnapshotKey(CharacterDiscipline discipline)
        {
            return discipline.IsSecondaryPath && !string.IsNullOrWhiteSpace(discipline.PathName)
                ? discipline.Name + " — " + discipline.PathName : discipline.Name;
        }

        private int GetInitialDisciplineDotsSpent()
        {
            if (_character == null || _character.Disciplines == null) return 0;
            if (!_character.CreationState.InitialDisciplineBlockEverConfirmed)
                return _character.Disciplines.Sum(x => Math.Max(0, x.Level));

            var snapshot = _character.CreationState.InitialDisciplineDots;
            if (snapshot == null) return 0;
            // Считаем только дисциплины, которые реально присутствуют у персонажа:
            // удалённая карточка не должна оставлять «фантомно потраченные» пункты.
            return _character.Disciplines.Sum(x =>
            {
                int value;
                return snapshot.TryGetValue(GetDisciplineSnapshotKey(x), out value) ? Math.Max(0, value) : 0;
            });
        }

        private void RenderDisciplines()
        {
            // Раздел дисциплин и карточки способностей всегда остаются видимыми.
            // Ограничивается только содержимое каталога до подтверждения закупки.
            bool initialDisciplinesConfirmed = _character != null && _character.CreationState != null &&
                _character.CreationState.ConfirmedInitialBlocks != null &&
                _character.CreationState.ConfirmedInitialBlocks.Contains("disciplines");
            if (DisciplineStageNotice != null)
                DisciplineStageNotice.Visibility = initialDisciplinesConfirmed ? Visibility.Collapsed : Visibility.Visible;
            if (DisciplinesGroupBox != null)
                DisciplinesGroupBox.Visibility = Visibility.Visible;
            if (DisciplineCardsGroupBox != null)
                DisciplineCardsGroupBox.Visibility = Visibility.Visible;
            if (_character == null || _character.CreationState == null) return;

            SelectedDisciplinesPanel.Children.Clear();
            foreach(var item in _character.Disciplines.ToList())
            {
                var definition=FindDefinition(item);
                var border=new Border{Padding=new Thickness(12),Margin=new Thickness(0,0,0,9),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5)};
                border.SetResourceReference(Border.BorderBrushProperty,"BorderBrush");border.SetResourceReference(Border.BackgroundProperty,"CardBgBrush");
                var layout = new Grid();
                layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var root=new StackPanel();
                var top=new DockPanel();
                if(item.IsCustom)
                {
                    var edit=SmallButton("Редактировать",false);edit.Margin=new Thickness(0,0,6,0);DockPanel.SetDock(edit,Dock.Right);
                    edit.Click+=(s,e)=>
                    {
                        var edited=DisciplineWindows.EditCustom(Window.GetWindow(this),definition);
                        if(edited!=null)
                        {
                            int index=_character.CustomDisciplines.IndexOf(definition);
                            if(index>=0)_character.CustomDisciplines[index]=edited;
                            item.DisciplineId=edited.Id;item.Name=edited.Name;RenderDisciplines();
                            InitialPurchaseProgressChanged?.Invoke();
                        }
                    };
                    top.Children.Add(edit);
                }
                string displayName=item.Name;
                if(!string.IsNullOrWhiteSpace(item.PathName))
                    displayName += ". "+item.PathName;
                top.Children.Add(new TextBlock{Text=displayName,FontSize=18,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap});
                root.Children.Add(top);
                int count = GetDisciplinePointCount(definition);
                item.Level = Math.Max(0, Math.Min(count, item.Level));
                var track=new TrackControl{Label="",Count=count,Cascade=true,LegacyCascade=true,LabelWidth=0,Margin=new Thickness(0,8,0,0),Height=48,HorizontalAlignment=HorizontalAlignment.Left};
                track.SetTrackValue(item.Level);
                track.IsEnabled = true;
                track.MaxValue = count;
                track.ValueChanged+=(s,e)=>
                {
                    int previous = item.Level;
                    int next = Math.Min(count, track.Value);
                    if (!_character.CreationState.ConfirmedInitialBlocks.Contains("disciplines") && next > previous)
                    {
                        int remaining;
                        if (_character.CreationState.InitialDisciplineBlockEverConfirmed && _character.CreationState.InitialDisciplineDots != null)
                        {
                            int initialCurrent;
                            _character.CreationState.InitialDisciplineDots.TryGetValue((item.IsSecondaryPath && !string.IsNullOrWhiteSpace(item.PathName) ? item.Name + " — " + item.PathName : item.Name), out initialCurrent);
                            int initialOther = _character.CreationState.InitialDisciplineDots.Where(x => !string.Equals(x.Key, (item.IsSecondaryPath && !string.IsNullOrWhiteSpace(item.PathName) ? item.Name + " — " + item.PathName : item.Name), StringComparison.OrdinalIgnoreCase)).Sum(x => Math.Max(0, x.Value));
                            remaining = Math.Max(0, CharacterCreationRules.GetCreationLimit("disciplines") - initialOther - initialCurrent);
                            int paidDots = _character.CreationState.PurchaseLedger == null ? 0 : _character.CreationState.PurchaseLedger
                                .Where(t => string.Equals(t.ItemName, (item.IsSecondaryPath && !string.IsNullOrWhiteSpace(item.PathName) ? item.Name + " — " + item.PathName : item.Name), StringComparison.OrdinalIgnoreCase) && t.Resource != "initial_pool")
                                .Sum(t => Math.Max(0, t.NewValue - t.PreviousValue));
                            next = Math.Min(next, paidDots + initialCurrent + remaining);
                        }
                        else
                        {
                            remaining = Math.Max(0, CharacterCreationRules.GetCreationLimit("disciplines") - _character.Disciplines.Where(x => !ReferenceEquals(x, item)).Sum(x => Math.Max(0, x.Level)) - previous);
                            next = Math.Min(next, previous + remaining);
                        }
                    }
                    if (next != track.Value) track.SetTrackValue(next);
                    string advancementKey = item.IsSecondaryPath ? "secondary_path" : "discipline";
                    string advancementName = item.IsSecondaryPath && !string.IsNullOrWhiteSpace(item.PathName)
                        ? item.Name + " — " + item.PathName : item.Name;
                    bool hasRecordedPurchase = _character.CreationState.PurchaseLedger != null && _character.CreationState.PurchaseLedger.Any(t => string.Equals(t.ItemName, advancementName, StringComparison.OrdinalIgnoreCase));
                    if (next != previous && (_character.CreationState.ConfirmedInitialBlocks.Contains("disciplines") || (hasRecordedPurchase && next < previous)) &&
                        AdvancementCheck != null && !AdvancementCheck(advancementKey, advancementName, previous, next))
                    { track.SetTrackValue(previous); return; }
                    item.Level=next;
                    if (!_character.CreationState.ConfirmedInitialBlocks.Contains("disciplines") &&
                        _character.CreationState.InitialDisciplineBlockEverConfirmed && _character.CreationState.InitialDisciplineDots != null)
                    {
                        string ledgerName = item.IsSecondaryPath && !string.IsNullOrWhiteSpace(item.PathName) ? item.Name + " — " + item.PathName : item.Name;
                        int paidDots = _character.CreationState.PurchaseLedger == null ? 0 : _character.CreationState.PurchaseLedger
                            .Where(t => string.Equals(t.ItemName, ledgerName, StringComparison.OrdinalIgnoreCase) && t.Resource != "initial_pool")
                            .Sum(t => Math.Max(0, t.NewValue - t.PreviousValue));
                        _character.CreationState.InitialDisciplineDots[ledgerName] = Math.Max(0, next - paidDots);
                    }
                    RenderDisciplines();
                    InitialPurchaseProgressChanged?.Invoke();
                };
                root.Children.Add(track);
                root.Children.Add(new TextBlock { Text = "Обоснование изучения дисциплины", Margin = new Thickness(0, 8, 0, 3), Foreground = BrushResource("MutedBrush") });
                var justification = new TextBox
                {
                    Text = item.Justification ?? string.Empty,
                    MinHeight = 48,
                    AcceptsReturn = true,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };
                justification.TextChanged += (s, e) => item.Justification = justification.Text;
                root.Children.Add(justification);
                Grid.SetColumn(root, 0);
                layout.Children.Add(root);
                var remove = CreateDeleteIconButton("Удалить дисциплину", 64);
                remove.IsEnabled = true;
                remove.Margin = new Thickness(10, 0, 0, 0);
                remove.Click += (s,e)=>
                {
                    string advancementName = item.IsSecondaryPath && !string.IsNullOrWhiteSpace(item.PathName) ? item.Name + " — " + item.PathName : item.Name;
                    string advancementKey = item.IsSecondaryPath ? "secondary_path" : "discipline";
                    string creationStateSnapshot = UndoService.CaptureCreationState(_character);
                    bool hasRecordedPurchase = _character.CreationState.PurchaseLedger != null && _character.CreationState.PurchaseLedger.Any(t => string.Equals(t.ItemName, advancementName, StringComparison.OrdinalIgnoreCase));
                    if (item.Level > 0 && hasRecordedPurchase && AdvancementCheck != null && !AdvancementCheck(advancementKey, advancementName, item.Level, 0)) return;
                    int restoreIndex = _character.Disciplines.IndexOf(item);
                    string removedKey = GetDisciplineSnapshotKey(item);
                    bool hadSnapshot = _character.CreationState.InitialDisciplineDots != null && _character.CreationState.InitialDisciplineDots.ContainsKey(removedKey);
                    int snapshotValue = hadSnapshot ? _character.CreationState.InitialDisciplineDots[removedKey] : 0;
                    _character.Disciplines.Remove(item);
                    if (_character.CreationState.InitialDisciplineDots != null)
                        _character.CreationState.InitialDisciplineDots.Remove(removedKey);
                    UndoService.Store("дисциплину «" + advancementName + "»", () =>
                    {
                        if (!_character.Disciplines.Contains(item)) _character.Disciplines.Insert(Math.Max(0, Math.Min(restoreIndex, _character.Disciplines.Count)), item);
                        if (hadSnapshot) _character.CreationState.InitialDisciplineDots[removedKey] = snapshotValue;
                        UndoService.RestoreCreationState(_character, creationStateSnapshot);
                        RenderDisciplines();
                        InitialPurchaseProgressChanged?.Invoke();
                        CharacterSelectionWindow.SaveCharacters();
                    });
                    RenderDisciplines();
                    InitialPurchaseProgressChanged?.Invoke();
                };
                Grid.SetColumn(remove, 1);
                layout.Children.Add(remove);
                border.Child=layout;SelectedDisciplinesPanel.Children.Add(border);
            }
            bool hasRitualDiscipline = _character.Disciplines.Any(x => DisciplineService.GetRituals(x.DisciplineId).Count > 0);
            OpenDisciplineCatalogButton.IsEnabled = true;
            StudyRitualButton.Visibility = hasRitualDiscipline ? Visibility.Visible : Visibility.Collapsed;
            RitualsGroupBox.Visibility = hasRitualDiscipline || _character.Rituals.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if(_character.Disciplines.Count==0)SelectedDisciplinesPanel.Children.Add(new TextBlock{Text="Дисциплины ещё не выбраны.",Foreground=BrushResource("MutedBrush"),Margin=new Thickness(2,4,2,2)});
            int usedDots = GetInitialDisciplineDotsSpent();
            int poolDots = CharacterCreationRules.GetCreationLimit("disciplines");
            int remainingDots = Math.Max(0, poolDots - usedDots);
            DisciplinesRemainingLabel.Text = "Осталось точек: " + remainingDots;
            bool confirmed = _character.CreationState.ConfirmedInitialBlocks.Contains("disciplines");
            DisciplinesRemainingLabel.Visibility = confirmed ? Visibility.Collapsed : Visibility.Visible;
            ConfirmDisciplinesButton.Visibility = confirmed ? Visibility.Collapsed : Visibility.Visible;
            ConfirmDisciplinesButton.IsEnabled = !confirmed && usedDots == poolDots;
            ConfirmDisciplinesButton.Content = usedDots == poolDots
                ? "Подтвердить дисциплины"
                : "Потратьте все первоначальные пункты (" + usedDots + "/" + poolDots + ")";
            RenderCards();
            RenderRitualCards();
        }

        private void ConfirmDisciplines_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null || _character.CreationState == null) return;
            int requiredDots = CharacterCreationRules.GetCreationLimit("disciplines");
            int spentDots = GetInitialDisciplineDotsSpent();
            if (spentDots != requiredDots)
            {
                ThemedDialog.Show(Window.GetWindow(this),
                    "Нельзя подтвердить дисциплины: необходимо потратить все " + requiredDots +
                    " первоначальных пункта. Сейчас потрачено: " + spentDots + ".",
                    "Первоначальная закупка не завершена", MessageBoxButton.OK, MessageBoxImage.Warning,
                    "Открыть каталог", () => OpenDisciplineCatalog());
                RenderDisciplines();
                return;
            }
            if (_character.CreationState.ConfirmedInitialBlocks.Contains("disciplines")) return;
            if (_character.CreationState.InitialPurchases == null) _character.CreationState.InitialPurchases = new List<CreationPurchase>();
            _character.CreationState.InitialPurchases.RemoveAll(p => p.Block == "Дисциплины");
            _character.CreationState.PurchaseLedger.RemoveAll(t => t.Resource == "initial_pool" && t.Block == "Дисциплины");
            if (_character.CreationState.InitialDisciplineDots == null) _character.CreationState.InitialDisciplineDots = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            bool hadInitialDisciplineSnapshot = _character.CreationState.InitialDisciplineBlockEverConfirmed;
            if (!hadInitialDisciplineSnapshot) _character.CreationState.InitialDisciplineDots.Clear();
            foreach (var discipline in _character.Disciplines)
            {
                string snapshotKey = GetDisciplineSnapshotKey(discipline);
                if (!hadInitialDisciplineSnapshot && !_character.CreationState.InitialDisciplineDots.ContainsKey(snapshotKey))
                    _character.CreationState.InitialDisciplineDots[snapshotKey] = discipline.Level;
                int initialLevel;
                if (!_character.CreationState.InitialDisciplineDots.TryGetValue(snapshotKey, out initialLevel)) initialLevel = 0;
                _character.CreationState.InitialPurchases.Add(new CreationPurchase { Block = "Дисциплины", ItemId = discipline.DisciplineId, ItemName = snapshotKey, PreviousValue = 0, NewValue = initialLevel, Cost = 0, Source = "initial" });
                for (int level = 0; level < initialLevel; level++)
                    _character.CreationState.PurchaseLedger.Add(new PurchaseTransaction { Order = _character.CreationState.NextPurchaseOrder++, Block = "Дисциплины", Category = "discipline", ItemId = discipline.DisciplineId, ItemName = snapshotKey, PreviousValue = level, NewValue = level + 1, Resource = "initial_pool", Cost = 0 });
            }
            _character.CreationState.InitialDisciplineBlockEverConfirmed = true;
            _character.CreationState.ConfirmInitialBlock("disciplines");
            RenderDisciplines();
            InitialPurchaseProgressChanged?.Invoke();
            (Window.GetWindow(this) as MainWindow)?.RefreshGenerationFromCharacter();
        }

        private void RenderRitualCards()
        {
            RitualCardsPanel.Children.Clear();
            foreach (var ritual in _character.Rituals.OrderBy(x => x.DisciplineName).ThenBy(x => x.Level))
            {
                var card = new Border { Padding=new Thickness(12), Margin=new Thickness(0,0,0,8), BorderThickness=new Thickness(1), CornerRadius=new CornerRadius(5) };
                card.SetResourceReference(Border.BorderBrushProperty, "BorderBrush");
                card.SetResourceReference(Border.BackgroundProperty, "CardBgBrush");
                var layout = new Grid();
                layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                layout.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var body = new StackPanel();
                body.Children.Add(new TextBlock { Text=ritual.Name, FontSize=17, FontWeight=FontWeights.SemiBold, TextWrapping=TextWrapping.Wrap });
                body.Children.Add(new TextBlock { Text=ritual.DisciplineName + " · Уровень " + ritual.Level, FontSize=13, Foreground=BrushResource("MutedBrush"), Margin=new Thickness(0,3,0,7) });
                if (!string.IsNullOrWhiteSpace(ritual.CastTime)) body.Children.Add(new TextBlock { Text="Время проведения: " + ritual.CastTime, FontSize=14, Margin=new Thickness(0,0,0,5) });
                if (!string.IsNullOrWhiteSpace(ritual.SystemText)) body.Children.Add(new TextBlock { Text=ritual.SystemText, FontSize=15, LineHeight=21, TextWrapping=TextWrapping.Wrap });
                layout.Children.Add(body);
                var remove = CreateDeleteIconButton("Удалить ритуал", 64);
                remove.Margin = new Thickness(10, 0, 0, 0);
                remove.Click += (s,e) =>
                {
                    int restoreIndex = _character.Rituals.IndexOf(ritual);
                    _character.Rituals.Remove(ritual);
                    UndoService.Store("ритуал «" + ritual.Name + "»", () =>
                    {
                        if (!_character.Rituals.Contains(ritual)) _character.Rituals.Insert(Math.Max(0, Math.Min(restoreIndex, _character.Rituals.Count)), ritual);
                        RenderRitualCards();
                        CharacterSelectionWindow.SaveCharacters();
                    });
                    RenderRitualCards();
                };
                Grid.SetColumn(remove, 1);
                layout.Children.Add(remove);
                card.Child=layout;
                RitualCardsPanel.Children.Add(card);
            }
            if (_character.Rituals.Count == 0) RitualCardsPanel.Children.Add(new TextBlock { Text="Пока нет изученных ритуалов.", Foreground=BrushResource("MutedBrush") });
        }

        private int GetDisciplinePointCount(DisciplineDefinition definition)
        {
            int generationLimit = 5;
            if (AppSettings.GenerationPointIncrease && _character.Generation.HasValue)
            {
                var rule = GenerationService.Find(_character.Generation.Value);
                if (rule != null && rule.DisciplineMax > 0) generationLimit = rule.DisciplineMax;
            }
            if (definition != null && definition.IsCustom && definition.LevelCount > 0)
                return Math.Max(1, Math.Min(generationLimit, Math.Min(9, definition.LevelCount)));
            return generationLimit;
        }

        private DisciplineDefinition FindDefinition(CharacterDiscipline selected)
        {
            var definition=_character.CustomDisciplines.FirstOrDefault(x=>x.Id==selected.DisciplineId)??DisciplineService.GetAll().FirstOrDefault(x=>x.Id==selected.DisciplineId);
            if(definition!=null&&!string.IsNullOrEmpty(selected.PathId))return DisciplineService.ForSelection(definition,selected.PathId);
            return definition;
        }

        private void RenderCards()
        {
            DisciplineCardsPanel.Children.Clear();int total=0;
            foreach(var selected in _character.Disciplines.OrderBy(x=>x.Name))
            {
                var definition=FindDefinition(selected);if(definition==null)continue;
                var powers=definition.Powers.Where(x=>x.Level<=selected.Level).OrderBy(x=>x.Level).ToList();
                if(powers.Count==0)continue;
                var heading=new TextBlock{Text=string.IsNullOrWhiteSpace(selected.PathName)?selected.Name:selected.Name+". "+selected.PathName,FontSize=18,FontWeight=FontWeights.Bold,Foreground=BrushResource("HeadingBrush"),Margin=new Thickness(2,10,2,7)};
                DisciplineCardsPanel.Children.Add(heading);
                var wrap=new System.Windows.Controls.Primitives.UniformGrid { Columns = 4 };
                int pointCount=GetDisciplinePointCount(definition);
                foreach(var power in powers)
                {
                    total++;
                    var card=new Border{BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5),Padding=new Thickness(12),Margin=new Thickness(0,0,8,8)};
                    card.SetResourceReference(Border.BorderBrushProperty,"BorderBrush");card.SetResourceReference(Border.BackgroundProperty,"CardBgBrush");
                    var body=new StackPanel();
                    body.Children.Add(new TextBlock{Text=power.Name,FontSize=17,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
                    var head=new DockPanel{Margin=new Thickness(0,5,0,6),LastChildFill=true};
                    var dots = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    int filledDots = Math.Min(pointCount, power.Level);
                    for (int i = 0; i < pointCount; i++)
                    {
                        var dot = new System.Windows.Shapes.Ellipse
                        {
                            Width = 14, Height = 14,
                            Margin = new Thickness(0, 0, 4, 0),
                            Fill = i < filledDots ? BrushResource("AccentBrush") : Brushes.Transparent,
                            Stroke = BrushResource("AccentBrush"),
                            StrokeThickness = 2,
                            VerticalAlignment = VerticalAlignment.Center
                        };
                        dots.Children.Add(dot);
                    }
                    head.Children.Add(dots);
                    var level=new TextBlock{Text="УРОВЕНЬ "+power.Level,FontSize=12,Foreground=BrushResource("MutedBrush"),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center};
                    DockPanel.SetDock(level,Dock.Right);head.Children.Add(level);body.Children.Add(head);
                    body.Children.Add(new TextBlock{Text=selected.Name+(string.IsNullOrWhiteSpace(selected.PathName)?"": " · "+selected.PathName),FontSize=13,Foreground=BrushResource("MutedBrush"),Margin=new Thickness(0,0,0,8),TextWrapping=TextWrapping.Wrap});
                    AddCardParameter(body,"ПУЛ",power.DicePool);AddCardParameter(body,"СЛОЖНОСТЬ",power.Difficulty);AddCardParameter(body,"СТОИМОСТЬ",power.Cost);AddCardParameter(body,"ДЛИТЕЛЬНОСТЬ",power.Duration);
                    AddCardSection(body,"ОПИСАНИЕ",power.SystemText);AddCardSection(body,"ОСОБЕННОСТИ",power.Features);
                    card.Child=body;wrap.Children.Add(card);
                }
                DisciplineCardsPanel.Children.Add(wrap);
            }
            if(total==0)DisciplineCardsPanel.Children.Add(new TextBlock{Text="Пока нет изученных способностей.",Foreground=BrushResource("MutedBrush"),Margin=new Thickness(2,4,2,2)});
        }
        private static void AddCardParameter(Panel p,string label,string value)
        {
            if(string.IsNullOrWhiteSpace(value)||value.Trim()=="—"||value.Trim()=="-")return;
            var row=new DockPanel{Margin=new Thickness(0,2,0,2)};
            row.Children.Add(new TextBlock{Text=label,Width=100,FontSize=12,FontWeight=FontWeights.SemiBold});
            row.Children.Add(new TextBlock{Text=value,FontSize=14,TextWrapping=TextWrapping.Wrap});p.Children.Add(row);
        }
        private static void AddCardSection(Panel p,string title,string text)
        {
            if(string.IsNullOrWhiteSpace(text)||text.Trim()=="—"||text.Trim()=="-")return;
            p.Children.Add(new TextBlock{Text=title,FontSize=13,FontWeight=FontWeights.Bold,Margin=new Thickness(0,9,0,3)});
            p.Children.Add(new TextBlock{Text=text,FontSize=15,LineHeight=21,TextWrapping=TextWrapping.Wrap});
        }
        private static Button CreateDeleteIconButton(string tooltip, double size)
        {
            var icon = new TextBlock { Text = "\uE74D", FontFamily = new FontFamily("Segoe MDL2 Assets"), FontSize = 25,
                Foreground = new SolidColorBrush(Color.FromRgb(190, 55, 55)), HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center };
            return new Button { Content = icon, Width = size, Height = size, MinWidth = size, MinHeight = size,
                Padding = new Thickness(0), Background = Brushes.Transparent, BorderBrush = Brushes.Transparent,
                BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand, ToolTip = tooltip,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        }
        private static Button SmallButton(string text,bool primary){var b=HumanityPathEditorWindow.MakeButton(text,primary);b.MinWidth=78;b.MinHeight=28;b.FontSize=12;return b;}
        private static Brush BrushResource(string key){var b=Application.Current.TryFindResource(key) as Brush;return b??Brushes.Firebrick;}
    }
}
