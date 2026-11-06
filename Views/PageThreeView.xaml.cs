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
            RenderDisciplines();
        }

        private void OpenDisciplineCatalog_Click(object sender,RoutedEventArgs e)
        {
            if(_character==null)return;
            var all=DisciplineService.GetAll().ToList();all.AddRange(_character.CustomDisciplines);
            var chosen=DisciplineWindows.Choose(Window.GetWindow(this),all,DisciplineService.GetClanDisciplineIds(_character.ClanName),
                string.Equals(_character.CreationMode,"Свободный Неонат",StringComparison.OrdinalIgnoreCase),
                custom=>{var existing=_character.CustomDisciplines.FirstOrDefault(x=>x.Id==custom.Id);if(existing==null)_character.CustomDisciplines.Add(custom);});
            if(chosen==null)return;
            var selected=_character.Disciplines.FirstOrDefault(x=>x.DisciplineId==chosen.Id);
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

        private void RenderDisciplines()
        {
            SelectedDisciplinesPanel.Children.Clear();
            foreach(var item in _character.Disciplines.ToList())
            {
                var definition=FindDefinition(item);
                var border=new Border{Padding=new Thickness(12),Margin=new Thickness(0,0,0,9),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5)};
                border.SetResourceReference(Border.BorderBrushProperty,"BorderBrush");border.SetResourceReference(Border.BackgroundProperty,"CardBgBrush");
                var root=new StackPanel();
                var top=new DockPanel();
                var remove=SmallButton("Удалить",false);DockPanel.SetDock(remove,Dock.Right);
                remove.Click+=(s,e)=>{_character.Disciplines.Remove(item);RenderDisciplines();};top.Children.Add(remove);
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
                    displayName += item.DisciplineId=="thaumaturgy"?". "+item.PathName:". "+item.PathName;
                top.Children.Add(new TextBlock{Text=displayName,FontSize=18,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center,TextWrapping=TextWrapping.Wrap});
                root.Children.Add(top);
                int count=GetDisciplinePointCount(definition);
                var track=new TrackControl{Label="",Count=count,Cascade=true,LabelWidth=0,Margin=new Thickness(0,8,0,0),Height=48,HorizontalAlignment=HorizontalAlignment.Left};
                double pipScale=string.Equals(AppSettings.PointMode,"Усиленные",StringComparison.OrdinalIgnoreCase)?1.5:1.25;
                track.LayoutTransform=new ScaleTransform(pipScale,pipScale);
                track.SetTrackValue(Math.Max(0,Math.Min(count,item.Level)));
                track.ValueChanged+=(s,e)=>{item.Level=Math.Min(count,track.Value);RenderCards();};
                root.Children.Add(track);
                border.Child=root;SelectedDisciplinesPanel.Children.Add(border);
            }
            if(_character.Disciplines.Count==0)SelectedDisciplinesPanel.Children.Add(new TextBlock{Text="Дисциплины ещё не выбраны.",Foreground=BrushResource("MutedBrush"),Margin=new Thickness(2,4,2,2)});
            RenderCards();
        }

        private int GetDisciplinePointCount(DisciplineDefinition definition)
        {
            if(definition!=null&&definition.IsCustom&&definition.LevelCount>0)return Math.Max(1,Math.Min(9,definition.LevelCount));
            if(_character.Generation.HasValue)
            {
                var rule=GenerationService.Find(_character.Generation.Value);
                if(rule!=null&&rule.DisciplineMax>0)return rule.DisciplineMax;
            }
            return 5;
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
                var wrap=new WrapPanel{Orientation=Orientation.Horizontal};
                int pointCount=GetDisciplinePointCount(definition);
                foreach(var power in powers)
                {
                    total++;
                    var card=new Border{Width=245,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5),Padding=new Thickness(12),Margin=new Thickness(0,0,10,10)};
                    card.SetResourceReference(Border.BorderBrushProperty,"BorderBrush");card.SetResourceReference(Border.BackgroundProperty,"CardBgBrush");
                    var body=new StackPanel();
                    body.Children.Add(new TextBlock{Text=power.Name,FontSize=17,FontWeight=FontWeights.SemiBold,TextWrapping=TextWrapping.Wrap});
                    var head=new DockPanel{Margin=new Thickness(0,5,0,6)};
                    head.Children.Add(new TextBlock{Text=new string('●',Math.Min(pointCount,power.Level))+new string('○',Math.Max(0,pointCount-Math.Min(pointCount,power.Level))),FontSize=15,Foreground=BrushResource("AccentBrush")});
                    var level=new TextBlock{Text="УРОВЕНЬ "+power.Level,FontSize=12,Foreground=BrushResource("MutedBrush"),HorizontalAlignment=HorizontalAlignment.Right};
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
        private static Button SmallButton(string text,bool primary){var b=HumanityPathEditorWindow.MakeButton(text,primary);b.MinWidth=78;b.MinHeight=28;b.FontSize=12;return b;}
        private static Brush BrushResource(string key){var b=Application.Current.TryFindResource(key) as Brush;return b??Brushes.Firebrick;}
    }
}
