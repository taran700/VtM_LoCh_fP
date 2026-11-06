using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VtM_LoCh_fP
{
    internal static class RitualWindows
    {
        public static void Show(Window owner, CharacterProfile character)
        {
            var available = character.Disciplines
                .Select(x => DisciplineService.GetAll().FirstOrDefault(d => d.Id == x.DisciplineId))
                .Where(x => x != null && x.Rituals != null && x.Rituals.Count > 0)
                .GroupBy(x => x.Id).Select(x => x.First()).ToList();
            if (available.Count == 0) return;

            var w = HumanityPathEditorWindow.CreateWindow(owner, "Изучение ритуалов", 720, 680);
            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var top = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(top, 0);
            top.Children.Add(new TextBlock { Text = "Дисциплина", FontSize = 15, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0,0,0,5) });
            var picker = new ComboBox { ItemsSource = available, DisplayMemberPath = "Name", MinHeight = 34, FontSize = 15 };
            picker.SetResourceReference(Control.BackgroundProperty, "InputBrush");
            picker.SetResourceReference(Control.ForegroundProperty, "TextBrush");
            picker.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
            top.Children.Add(picker);

            bool hideLearned = false;
            Action render = null;
            var filterButton = HumanityPathEditorWindow.MakeButton("Не отображать изученные: выключено", false);
            filterButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            filterButton.Margin = new Thickness(0, 8, 0, 0);
            filterButton.Click += (s, e) =>
            {
                hideLearned = !hideLearned;
                filterButton.Content = hideLearned ? "Не отображать изученные: включено" : "Не отображать изученные: выключено";
                render();
            };
            top.Children.Add(filterButton);
            root.Children.Add(top);

            var list = new StackPanel();
            var scroll = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            Grid.SetRow(scroll, 1);
            root.Children.Add(scroll);

            render = () =>
            {
                list.Children.Clear();
                var definition = picker.SelectedItem as DisciplineDefinition;
                if (definition == null) return;
                foreach (var ritual in definition.Rituals.OrderBy(x => x.Level).ThenBy(x => x.Name))
                {
                    bool learned = character.Rituals.Any(x => x.DisciplineId == definition.Id && x.RitualId == ritual.Id);
                    if (hideLearned && learned) continue;
                    var button = new Button { HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(12,9,12,9), Margin = new Thickness(0,0,0,7) };
                    button.SetResourceReference(Control.BackgroundProperty, "CardBgBrush");
                    button.SetResourceReference(Control.ForegroundProperty, "TextBrush");
                    button.SetResourceReference(Control.BorderBrushProperty, "BorderBrush");
                    var label = new TextBlock { Text = "Уровень " + ritual.Level + " — " + ritual.Name + (learned ? "  ✓ Изучен" : string.Empty), FontSize = 16, FontWeight = FontWeights.SemiBold, TextWrapping = TextWrapping.Wrap };
                    if (learned) label.Foreground = BrushResource("MutedBrush");
                    button.Content = label;
                    button.Click += (s,e) =>
                    {
                        if (learned)
                        {
                            var answer = ThemedDialog.Show(w,
                                "Этот ритуал уже изучен. Карточка персонажа обновится после закрытия окна списка ритуалов. Включить фильтр «Не отображать изученные»?",
                                "Ритуал уже изучен", MessageBoxButton.YesNo, MessageBoxImage.Information);
                            if (answer == MessageBoxResult.Yes)
                            {
                                hideLearned = true;
                                filterButton.Content = "Не отображать изученные: включено";
                                render();
                            }
                            return;
                        }

                        var detail = HumanityPathEditorWindow.CreateWindow(w, ritual.Name, 650, 560);
                        var panel = new Grid { Margin = new Thickness(16) };
                        panel.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        var body = new StackPanel();
                        body.Children.Add(new TextBlock { Text=ritual.Name, FontSize=23, FontWeight=FontWeights.Bold, Foreground=BrushResource("HeadingBrush"), TextAlignment=TextAlignment.Center, Margin=new Thickness(0,0,0,12) });
                        body.Children.Add(new TextBlock { Text="Уровень: " + ritual.Level, FontSize=15, Margin=new Thickness(0,0,0,8) });
                        if (!string.IsNullOrWhiteSpace(ritual.CastTime)) body.Children.Add(new TextBlock { Text="ВРЕМЯ ПРОВЕДЕНИЯ: " + ritual.CastTime, FontSize=14, Margin=new Thickness(0,0,0,8) });
                        if (!string.IsNullOrWhiteSpace(ritual.SystemText)) body.Children.Add(new TextBlock { Text=ritual.SystemText, FontSize=16, LineHeight=23, TextWrapping=TextWrapping.Wrap });
                        var detailScroll = new ScrollViewer { Content=body, VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
                        Grid.SetRow(detailScroll, 0);
                        panel.Children.Add(detailScroll);

                        var actions = new StackPanel { Margin = new Thickness(0,12,0,0) };
                        var take = HumanityPathEditorWindow.MakeButton("Изучить ритуал", true);
                        take.HorizontalAlignment = HorizontalAlignment.Stretch;
                        take.Click += (a,b) =>
                        {
                            if (!character.Rituals.Any(x => x.DisciplineId == definition.Id && x.RitualId == ritual.Id))
                                character.Rituals.Add(new CharacterRitual { DisciplineId=definition.Id, RitualId=ritual.Id, DisciplineName=definition.Name, Name=ritual.Name, Level=ritual.Level, SystemText=ritual.SystemText, CastTime=ritual.CastTime });
                            detail.DialogResult = true;
                        };
                        var closeDetail = HumanityPathEditorWindow.MakeButton("Закрыть", false);
                        closeDetail.HorizontalAlignment = HorizontalAlignment.Stretch;
                        closeDetail.Margin = new Thickness(0,7,0,0);
                        closeDetail.Click += (a,b) => detail.DialogResult = false;
                        actions.Children.Add(take);
                        actions.Children.Add(closeDetail);
                        Grid.SetRow(actions, 1);
                        panel.Children.Add(actions);
                        detail.Content=panel;
                        detail.ShowDialog();
                        render();
                    };
                    list.Children.Add(button);
                }
                if (list.Children.Count == 0)
                    list.Children.Add(new TextBlock { Text = hideLearned ? "Все ритуалы этой дисциплины уже изучены." : "Ритуалы не найдены.", Foreground = BrushResource("MutedBrush"), Margin = new Thickness(4,8,4,4) });
            };
            picker.SelectionChanged += (s,e) => render();
            picker.SelectedIndex = 0;
            var closeWindow = HumanityPathEditorWindow.MakeButton("Закрыть", false);
            closeWindow.Height = 42;
            closeWindow.HorizontalAlignment = HorizontalAlignment.Stretch;
            closeWindow.Margin = new Thickness(0, 10, 0, 0);
            closeWindow.Click += (s,e) => w.Close();
            Grid.SetRow(closeWindow, 2);
            root.Children.Add(closeWindow);
            w.Content = root;
            w.ShowDialog();
        }
        private static Brush BrushResource(string key)
        {
            var brush = Application.Current.TryFindResource(key) as Brush;
            return brush ?? Brushes.Firebrick;
        }
    }
}
