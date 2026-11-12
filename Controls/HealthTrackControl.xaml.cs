using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VtM_LoCh_fP.ViewModels;

namespace VtM_LoCh_fP.Controls
{
    public partial class HealthTrackControl : UserControl
    {
        public ObservableCollection<WoundLevel> Levels { get; private set; }
        public Func<int, bool> TrySpendBlood { get; set; }
        public Action StateChanged { get; set; }
        public Func<string> HealingMode { get; set; }
        public Func<bool> AllowAggravatedDamage { get; set; }

        public HealthTrackControl()
        {
            Levels = WoundLevels.CreateDefault();
            InitializeComponent();
            LoadStatusDescriptions();
        }

        public void LoadStates(System.Collections.Generic.IEnumerable<int> states)
        {
            var values = (states ?? Enumerable.Empty<int>()).Take(7).Select(x => Math.Max(0, Math.Min(3, x))).ToList();
            while (values.Count < 7) values.Add(0);
            for (int i = 0; i < 7; i++) Levels[i].State = values[i];
            Normalize();
        }

        public System.Collections.Generic.List<int> GetStates() => Levels.Select(x => x.State).ToList();

        private void Mark_Click(object sender, RoutedEventArgs e)
        {
            var level = (sender as FrameworkElement)?.DataContext as WoundLevel;
            if (level == null) return;

            bool aggravatedMode = AllowAggravatedDamage != null && AllowAggravatedDamage();
            if (aggravatedMode)
            {
                // Губительное повреждение всегда занимает верхний слот
                var states = Levels.Select(x => x.State).ToList();
                states.Insert(0, 3);
                states = states.Take(7).ToList();
                for (int i = 0; i < 7; i++) Levels[i].State = states[i];
            }
            else if (level.State == 0)
            {
                // Пока есть лёгкие повреждения, новое попадание повышает самое верхнее из них
                var light = Levels.FirstOrDefault(x => x.State == 1);
                if (light != null) light.State = 2;
                else level.State = 1;
            }
            else if (level.State == 1) level.State = 2;
            // Левый клик наносит или повышает повреждение; снятие — только ПКМ

            Normalize(); StateChanged?.Invoke();
        }

        private void Heal_Click(object sender, MouseButtonEventArgs e)
        {
            e.Handled = true;
            var clicked = (sender as FrameworkElement)?.DataContext as WoundLevel;
            if (clicked == null || !Levels.Any(x => x.State > 0)) return;
            // Лечение всегда идёт от самых лёгких ран к самым тяжёлым, независимо от нажатого слота
            var level = Levels.LastOrDefault(x => x.State == 1) ??
                        Levels.LastOrDefault(x => x.State == 2) ??
                        Levels.LastOrDefault(x => x.State == 3);
            if (level == null) return;


            // В режиме «Сир, я сделаю сам» кровь приложение не списывает
            bool automaticPayment = HealingMode == null || HealingMode() == "Работать Гуль!";
            int cost = level.State == 3 ? 5 : 1;
            if (automaticPayment && (TrySpendBlood == null || !TrySpendBlood(cost)))
            {
                ThemedDialog.Show(Window.GetWindow(this),
                    "Недостаточно крови для лечения. Требуется: " + cost + ". Повреждение не изменено.",
                    "Лечение невозможно", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            level.State = 0;
            Normalize();
            StateChanged?.Invoke();
        }

        private void LoadStatusDescriptions()
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "data.json");
                if (!File.Exists(path)) return;
                var root = new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 256 }.Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
                var healthLevels = FindHealthLevels(root);
                if (healthLevels == null) return;
                foreach (var raw in healthLevels)
                {
                    var item = raw as Dictionary<string, object>;
                    if (item == null) continue;
                    string name = Read(item, "name");
                    string description = Read(item, "description");
                    var target = Levels.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase));
                    if (target != null) target.StatusDescription = Clean(description);
                }
            }
            catch { }
        }

        private static IList FindHealthLevels(object node)
        {
            var dictionary = node as Dictionary<string, object>;
            if (dictionary != null)
            {
                object value;
                if (dictionary.TryGetValue("health_levels", out value) && value is IList) return (IList)value;
                foreach (var child in dictionary.Values)
                {
                    var found = FindHealthLevels(child);
                    if (found != null) return found;
                }
            }
            var list = node as IList;
            if (list != null)
                foreach (var child in list)
                {
                    var found = FindHealthLevels(child);
                    if (found != null) return found;
                }
            return null;
        }

        private static string Read(Dictionary<string, object> item, string key)
        {
            object value;
            return item.TryGetValue(key, out value) && value != null ? Convert.ToString(value) : string.Empty;
        }

        private static string Clean(string value)
        {
            return System.Text.RegularExpressions.Regex.Replace(value ?? string.Empty, @"\s*\[cite:\s*[^\]]+\]", string.Empty, System.Text.RegularExpressions.RegexOptions.IgnoreCase).Trim();
        }

        private void Normalize()
        {
            var sorted = Levels.Select(x => x.State).Where(x => x > 0).OrderByDescending(x => x).ToList();
            while (sorted.Count < 7) sorted.Add(0);
            for (int i = 0; i < 7; i++) Levels[i].State = sorted[i];
        }
    }
}
