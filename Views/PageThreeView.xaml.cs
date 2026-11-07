using System;
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
        public PageThreeView() { InitializeComponent(); }

        public void InitializeCharacter(CharacterProfile character)
        {
            _character=character;
            if(_character.Disciplines==null)_character.Disciplines=new System.Collections.ObjectModel.ObservableCollection<CharacterDiscipline>();
            if(_character.CustomDisciplines==null)_character.CustomDisciplines=new System.Collections.ObjectModel.ObservableCollection<DisciplineDefinition>();
            if(_character.Rituals==null)_character.Rituals=new System.Collections.ObjectModel.ObservableCollection<CharacterRitual>();
            RenderDisciplines();
        }

        public void RefreshGenerationLimits()
        {
            if (_character != null) RenderDisciplines();
        }

        private void OpenDisciplineCatalog_Click(object sender,RoutedEventArgs e)
        {
            if(_character==null)return;
            var all=DisciplineService.GetAll().ToList();all.AddRange(_character.CustomDisciplines);
            var chosen=DisciplineWindows.Choose(Window.GetWindow(this),all,DisciplineService.GetClanDisciplineIds(_character.ClanName),
                string.Equals(_character.CreationMode,"Свободный Неонат",StringComparison.OrdinalIgnoreCase),
                custom=>{var existing=_character.CustomDisciplines.FirstOrDefault(x=>x.Id==custom.Id);if(existing==null)_character.CustomDisciplines.Add(custom);});
            if(chosen==null)return;
            var selected=_character.Disciplines.FirstOrDefault(x=>
                x.DisciplineId==chosen.Id &&
                string.Equals(x.PathId ?? string.Empty, chosen.PathId ?? string.Empty, StringComparison.OrdinalIgnoreCase));
            if(selected==null)
                _character.Disciplines.Add(new CharacterDiscipline{DisciplineId=chosen.Id,Name=chosen.Name,Level=0,IsCustom=chosen.IsCustom,PathId=chosen.PathId,PathName=chosen.PathName});
            else if(!string.IsNullOrEmpty(chosen.PathId))
            {
                selected.PathId=chosen.PathId;
                selected.PathName=chosen.PathName;
                selected.Name=chosen.Name;
            }
            RenderDisciplines();
        }

        private void StudyRitual_Click(object sender, RoutedEventArgs e)
        {
            if (_character == null) return;
            RitualWindows.Show(Window.GetWindow(this), _character);
            RenderRitualCards();
        }

        private void RenderDisciplines()
        {
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
                        }
                    };
                    top.Children.Add(edit);
                }
                string displayName=item.Name;
                if(!string.IsNullOrWhiteSpace(item.PathName))
                    displayName += ". "+item.PathName;
                top.Children.Add(new TextBlock{Text=displayName,FontSize=18,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap});
                root.Children.Add(top);
                int count=GetDisciplinePointCount(definition);
                var track=new TrackControl{Label="",Count=count,Cascade=true,LabelWidth=0,Margin=new Thickness(0,8,0,0),Height=48,HorizontalAlignment=HorizontalAlignment.Left};
                track.SetTrackValue(Math.Max(0,Math.Min(count,item.Level)));
                track.ValueChanged+=(s,e)=>{item.Level=Math.Min(count,track.Value);RenderCards();};
                root.Children.Add(track);
                Grid.SetColumn(root, 0);
                layout.Children.Add(root);
                var remove = CreateDeleteIconButton("Удалить дисциплину", 64);
                remove.Margin = new Thickness(10, 0, 0, 0);
                remove.Click += (s,e)=>{_character.Disciplines.Remove(item);RenderDisciplines();};
                Grid.SetColumn(remove, 1);
                layout.Children.Add(remove);
                border.Child=layout;SelectedDisciplinesPanel.Children.Add(border);
            }
            bool hasRitualDiscipline = _character.Disciplines.Any(x => DisciplineService.GetRituals(x.DisciplineId).Count > 0);
            StudyRitualButton.Visibility = hasRitualDiscipline ? Visibility.Visible : Visibility.Collapsed;
            RitualsGroupBox.Visibility = hasRitualDiscipline || _character.Rituals.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if(_character.Disciplines.Count==0)SelectedDisciplinesPanel.Children.Add(new TextBlock{Text="Дисциплины ещё не выбраны.",Foreground=BrushResource("MutedBrush"),Margin=new Thickness(2,4,2,2)});
            RenderCards();
            RenderRitualCards();
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
                remove.Click += (s,e) => { _character.Rituals.Remove(ritual); RenderRitualCards(); };
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
