using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace VtM_LoCh_fP
{
    internal sealed class BioFactDescriptionWindow : Window
    {
        public BioFactDescriptionWindow(Window owner, BioFactEntry fact, BioFactDefinition definition)
        {
            if (fact == null) throw new ArgumentNullException(nameof(fact));

            Title = fact.Name ?? "Факт биографии";
            Width = 760;
            Height = 560;
            MinWidth = 560;
            MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Owner = owner;
            ResizeMode = ResizeMode.CanResize;
            ShowInTaskbar = false;
            ThemeManager.AttachWindow(this);

            var root = new DockPanel { Margin = new Thickness(18) };
            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var close = HumanityPathEditorWindow.MakeButton("Закрыть", false);
            close.Click += (s, e) => Close();
            buttons.Children.Add(close);
            DockPanel.SetDock(buttons, Dock.Bottom);
            root.Children.Add(buttons);

            var body = new StackPanel();
            body.Children.Add(new TextBlock
            {
                Text = fact.Name ?? string.Empty,
                FontSize = 25,
                FontWeight = FontWeights.Bold,
                Foreground = BrushResource("HeadingBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 12)
            });
            body.Children.Add(new TextBlock
            {
                Text = "ФАКТ БИОГРАФИИ",
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = BrushResource("MutedBrush"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14)
            });

            string description = !string.IsNullOrWhiteSpace(fact.CustomDescription)
                ? fact.CustomDescription
                : definition?.Description;
            body.Children.Add(new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(description) ? "Описание отсутствует." : description,
                FontSize = 15,
                LineHeight = 22,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(2, 0, 2, 16),
                Foreground = BrushResource("TextBrush")
            });

            string levelDescription = null;
            if (fact.Level > 0)
            {
                levelDescription = fact.LevelDescriptions?
                    .FirstOrDefault(x => x.Level == fact.Level)?.Description;

                if (string.IsNullOrWhiteSpace(levelDescription))
                    levelDescription = fact.HolisticDescriptions?
                        .FirstOrDefault(x => x.Level == fact.Level)?.Description;

                var databaseLevel = definition?.Levels?
                    .FirstOrDefault(x => x.Level == fact.Level);
                if (string.IsNullOrWhiteSpace(levelDescription))
                    levelDescription = databaseLevel?.Description;

                if (!string.IsNullOrWhiteSpace(levelDescription))
                {
                    body.Children.Add(new TextBlock
                    {
                        Text = "УРОВЕНЬ " + fact.Level +
                            (string.IsNullOrWhiteSpace(databaseLevel?.Rank) ? string.Empty : " — " + databaseLevel.Rank),
                        FontSize = 13,
                        FontWeight = FontWeights.Bold,
                        Foreground = BrushResource("HeadingBrush"),
                        Margin = new Thickness(2, 0, 2, 6)
                    });
                    body.Children.Add(new TextBlock
                    {
                        Text = levelDescription,
                        FontSize = 15,
                        LineHeight = 22,
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(2, 0, 2, 16),
                        Foreground = BrushResource("TextBrush")
                    });
                }
            }

            root.Children.Add(new ScrollViewer
            {
                Content = body,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            });
            Content = root;
        }

        private static Brush BrushResource(string key)
        {
            return Application.Current.Resources[key] as Brush ?? Brushes.Gray;
        }
    }
}
