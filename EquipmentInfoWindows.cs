using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VtM_LoCh_fP
{
    internal static class EquipmentInfoWindows
    {
        public static void ShowWeapons(Window owner)
        {
            var window = Create(owner, "Справочник оружия", 1120, 780);
            window.MinWidth = 900;
            var tabs = new TabControl { Margin = new Thickness(12), Style = (Style)Application.Current.FindResource("SheetTabControlStyle"), ItemContainerStyle = (Style)Application.Current.FindResource("SheetTabItemStyle") };
            tabs.Items.Add(new TabItem { Header = "Холодное оружие", Content = MeleeContent() });
            tabs.Items.Add(new TabItem { Header = "Дистанционное оружие", Content = RangedContent() });
            window.Content = tabs;
            window.ShowDialog();
        }

        public static void ShowArmor(Window owner)
        {
            var window = Create(owner, "Справочник брони", 820, 650);
            var root = new DockPanel { Margin = new Thickness(16) };
            var close = HumanityPathEditorWindow.MakeButton("Закрыть", false);
            close.HorizontalAlignment = HorizontalAlignment.Right;
            close.Click += (s,e)=>window.Close();
            DockPanel.SetDock(close, Dock.Bottom); root.Children.Add(close);
            var body = new StackPanel();
            body.Children.Add(Heading("Таблица брони"));
            body.Children.Add(Table(new[]{"Класс брони","Показатель брони","Модификатор удобства"}, new[]{
                new[]{"Класс I (защитная одежда)","1","+0"},
                new[]{"Класс II (бронированная одежда)","2","−1"},
                new[]{"Класс III (лёгкий бронежилет)","3","−1"},
                new[]{"Класс IV (армейский бронежилет)","4","−2"},
                new[]{"Класс V (тяжёлая полицейская броня)","5","−3"}
            }, new[]{2.2,1.0,1.3}));
            body.Children.Add(Paragraph("Показатель брони прибавляется к пулу проверки на прочность против лёгких и тяжёлых повреждений, а также губительных повреждений, нанесённых клыками, когтями и т. п. От солнечного света и огня броня не защищает."));
            body.Children.Add(Paragraph("Модификатор удобства уменьшает пул всех проверок, связанных с проворством и координацией движений (в основном тех, которые требуют ловкости)."));
            body.Children.Add(Paragraph("Если противник облачён в броню, персонаж может атаковать незащищённые бронёй участки тела и исключить показатель брони из проверки на прочность. Рассказчик может повысить сложность такой атаки на один-два пункта."));
            root.Children.Add(new ScrollViewer { Content=body, VerticalScrollBarVisibility=ScrollBarVisibility.Auto });
            window.Content=root; window.ShowDialog();
        }

        private static UIElement MeleeContent()
        {
            var body = new StackPanel { Margin = new Thickness(14) };
            body.Children.Add(Heading("Таблица холодного оружия"));
            body.Children.Add(Table(new[]{"Оружие","Урон","Скрытное ношение"},new[]{
                new[]{"Дубинка¹","Сила + 1","ВК"},
                new[]{"Бита¹","Сила + 2","ПП"},
                new[]{"Нож","Сила + 1","ПК"},
                new[]{"Меч","Сила + 2","ПП"},
                new[]{"Топор","Сила + 3","Н"},
                new[]{"Кол²","Сила + 1","ПП"}
            },new[]{2.0,1.2,1.5}));
            body.Children.Add(Paragraph("¹ Дробящее оружие. Обычно наносит лёгкие повреждения; удар в голову может наносить тяжёлые повреждения."));
            body.Children.Add(Paragraph("² Если вонзить кол в сердце вампира, он может быть парализован. Для этого требуется проверка Фехтования (сложность 9) и нанесение трёх повреждений."));
            body.Children.Add(Heading("Скрытное ношение"));
            body.Children.Add(Paragraph("ВК — в кармане.\nПК — под курткой.\nПП — под плащом.\nН — скрытное ношение невозможно."));
            return new ScrollViewer { Content=body, VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
        }

        private static UIElement RangedContent()
        {
            var body = new StackPanel { Margin = new Thickness(14) };
            body.Children.Add(Heading("Таблица дистанционного оружия"));
            body.Children.Add(Table(new[]{"Тип оружия","Пример оружия (калибр)","Урон","Дистанция","Скорострельность","Боезапас","Скрытное ношение"},new[]{
                new[]{"Лёгкий револьвер","Смит-Вессон М36 (.38 специальный)","4","12","3","6","ВК"},
                new[]{"Тяжёлый револьвер","Ругер Редхок (.44 магнум)","6","35","2","6","ПК"},
                new[]{"Лёгкий самозарядный пистолет","Хеклер-Кох УСП (9 мм)","4","20","4","15+1","ВК"},
                new[]{"Тяжёлый самозарядный пистолет","Спрингфилд ХС 2000 (.45)","5","25","3","13+1","ПК"},
                new[]{"Винтовка","Беретта Тикка Т3 (.30–06)","8","200","1","3+1","Н"},
                new[]{"Лёгкий пистолет-пулемёт¹","Глок 18 (9 мм)","4","20","3","17+1","ПК"},
                new[]{"Тяжёлый пистолет-пулемёт¹","Хеклер-Кох МП5 (9 мм)","4","50","3","30+1","ПП"},
                new[]{"Автомат¹","ФН СКАР (5,56 НАТО)","7","150","3","30+1","Н"},
                new[]{"Дробовик","Ремингтон 870 (12-й калибр)","8","20","1","5+1","ПП"},
                new[]{"Самозарядный дробовик","Бенелли М4 Супер 90 (12-й калибр)","8","20","3","6+1","ПП"},
                new[]{"Арбалет²","—","5","20","1","1","ПП"}
            },new[]{2.0,2.45,.55,.68,.88,.72,.82}));
            body.Children.Add(Heading("Пояснения"));
            body.Children.Add(Paragraph("Урон — базовый пул урона. Смертным оружие наносит тяжёлые повреждения, вампирам — лёгкие. Выстрел в голову наносит тяжёлые повреждения."));
            body.Children.Add(Paragraph("Дистанция — эффективная дальность в метрах. На расстоянии до удвоенной дальности сложность атаки повышается до 8."));
            body.Children.Add(Paragraph("Скорострельность — число выстрелов или коротких очередей за ход; не применяется для длинной очереди и обстрела."));
            body.Children.Add(Paragraph("Боезапас — патроны в магазине. «+1» означает дополнительный патрон в патроннике."));
            body.Children.Add(Paragraph("Скрытное ношение: ВК — карман; ПК — под курткой; ПП — под плащом; Н — скрыть невозможно."));
            body.Children.Add(Paragraph("¹ Допускает обстрел, короткие и длинные очереди."));
            body.Children.Add(Paragraph("² Арбалет может пронзить сердце вампира деревянной стрелой. Перезарядка — пять ходов. Попадание не в голову и не в сердце наносит вампиру лёгкие, смертному — тяжёлые повреждения."));
            return new ScrollViewer { Content=body, VerticalScrollBarVisibility=ScrollBarVisibility.Auto, HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled };
        }

        private static Window Create(Window owner,string title,double width,double height)
        {
            var w=new Window { Title=title,Width=width,Height=height,MinWidth=650,MinHeight=480,
                Owner=owner,WindowStartupLocation=owner==null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner,
                ResizeMode=ResizeMode.CanResize,ShowInTaskbar=false,Background=BrushResource("BgBrush") };
            ThemeManager.AttachWindow(w); return w;
        }

        private static Border Heading(string text)
        {
            var t=new TextBlock { Text=text,FontSize=20,FontWeight=FontWeights.Bold,Foreground=BrushResource("HeadingBrush"),Margin=new Thickness(0,0,0,12) };
            return new Border { Child=t };
        }
        private static TextBlock Paragraph(string text)=>new TextBlock { Text=text,FontSize=15,LineHeight=22,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(2,8,2,8),Foreground=BrushResource("TextBrush") };

        public static UIElement Table(string[] headers,string[][] rows,double[] widths)
        {
            var grid=new Grid { Margin=new Thickness(0,0,0,10) };
            for(int i=0;i<headers.Length;i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(widths[i],GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            for(int r=0;r<rows.Length;r++) grid.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            for(int c=0;c<headers.Length;c++) AddCell(grid,headers[c],0,c,true);
            for(int r=0;r<rows.Length;r++) for(int c=0;c<headers.Length;c++) AddCell(grid,rows[r][c],r+1,c,false);
            return new Border { BorderBrush=BrushResource("BorderBrush"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(4),Child=grid };
        }
        private static void AddCell(Grid grid,string text,int row,int col,bool header)
        {
            var border=new Border { BorderBrush=BrushResource("BorderBrush"),BorderThickness=new Thickness(.5),Padding=new Thickness(4,4,4,4),
                Background=BrushResource(header?"SelectionBrush":"CardBgBrush") };
            border.Child=new TextBlock { Text=text,FontSize=header?11.5:12.5,FontWeight=header?FontWeights.Bold:FontWeights.Normal,
                TextWrapping=TextWrapping.Wrap,Foreground=BrushResource("TextBrush"),VerticalAlignment=VerticalAlignment.Center };
            Grid.SetRow(border,row);Grid.SetColumn(border,col);grid.Children.Add(border);
        }
        private static Brush BrushResource(string key)=>Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }
}