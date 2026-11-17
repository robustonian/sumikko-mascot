using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace SumikkoCompanion
{
    // Sliders for the numbers of the motion. Nothing is confirmed or cancelled here: a change
    // shows on the character at once and is saved a moment later, so a value can be judged
    // while typing and clicking as usual. The window does not block the companion.
    public sealed class TuningWindow : Window
    {
        private sealed class Row
        {
            public TuningValue Value;
            public Slider Slider;
            public TextBlock Label, Number;
            public Button Reset;
            public CheckBox Check;
        }

        private readonly MotionTuning tuning;
        private readonly Action changed;
        private readonly Func<string> save;
        private readonly List<Row> rows = new List<Row>();
        private readonly TextBlock status;
        private readonly Button resetAll, copy;
        private readonly CheckBox demo;
        private readonly DispatcherTimer saveTimer;
        private bool updating, unsaved;

        // changed: called after any value changes. save: writes the values and returns null,
        // or the reason it could not. setDemo: switches the automatic typing and clicking.
        public TuningWindow(MotionTuning tuning, Action changed, Func<string> save, bool demoRunning, Action<bool> setDemo)
        {
            this.tuning = tuning;
            this.changed = changed;
            this.save = save;
            Title = "動きの調整";
            Width = 600; Height = 760; MinWidth = 480; MinHeight = 420;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = Brush(250, 247, 240);
            FontFamily = new FontFamily("Yu Gothic UI"); FontSize = 14;

            var root = new DockPanel { Margin = new Thickness(20), LastChildFill = true };
            Content = root;

            var actions = new DockPanel { LastChildFill = false, Margin = new Thickness(0, 14, 0, 0) };
            DockPanel.SetDock(actions, Dock.Bottom); root.Children.Add(actions);
            var close = MakeButton("閉じる", true); close.IsCancel = true;
            close.Click += delegate { Close(); };
            DockPanel.SetDock(close, Dock.Right); actions.Children.Add(close);
            copy = MakeButton("変えた値をコピー", false);
            copy.ToolTip = "初期値から変えた値の一覧をクリップボードへコピーします。";
            copy.Click += delegate { CopySummary(); };
            DockPanel.SetDock(copy, Dock.Right); actions.Children.Add(copy);
            resetAll = MakeButton("すべて初期値に戻す", false);
            resetAll.Margin = new Thickness(0);
            resetAll.Click += delegate { ResetAll(); };
            DockPanel.SetDock(resetAll, Dock.Left); actions.Children.Add(resetAll);

            status = Text("", 12, FontWeights.Normal);
            status.Margin = new Thickness(0, 10, 0, 0);
            status.Foreground = Brush(118, 71, 54);
            AutomationProperties.SetLiveSetting(status, AutomationLiveSetting.Polite);
            DockPanel.SetDock(status, Dock.Bottom); root.Children.Add(status);

            var top = new StackPanel();
            DockPanel.SetDock(top, Dock.Top); root.Children.Add(top);
            top.Children.Add(Text("動きの調整", 22, FontWeights.SemiBold));
            var explanation = Text("スライダーを動かすと、すぐにキャラへ反映されます。この画面を開いたまま、キーを打ったりクリックしたりして確かめてください。" +
                "変えた値は自動で保存され、次回の起動時にも使われます。", 13, FontWeights.Normal);
            explanation.Margin = new Thickness(0, 6, 0, 10); top.Children.Add(explanation);
            demo = new CheckBox { Content = "デモを流す（自動で打鍵とクリックを繰り返す）", IsChecked = demoRunning, Margin = new Thickness(0, 0, 0, 12),
                Foreground = Brush(53, 52, 47) };
            demo.Checked += delegate { if (setDemo != null) setDemo(true); };
            demo.Unchecked += delegate { if (setDemo != null) setDemo(false); };
            top.Children.Add(demo);

            var tabs = new TabControl { Background = Brush(255, 255, 255), BorderBrush = Brush(210, 199, 184) };
            root.Children.Add(tabs);
            foreach (string group in MotionTuning.Groups)
            {
                var list = new StackPanel { Margin = new Thickness(14, 8, 14, 8) };
                foreach (TuningValue value in tuning.Values)
                    if (value.Group == group) list.Children.Add(MakeRow(value));
                tabs.Items.Add(new TabItem { Header = group, Padding = new Thickness(10, 5, 10, 5),
                    Content = new ScrollViewer { Content = list, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
            }

            saveTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(400) };
            saveTimer.Tick += delegate { SaveNow(); };
            Closed += delegate { SaveNow(); };
            Refresh();
        }

        private UIElement MakeRow(TuningValue value)
        {
            var row = new Row { Value = value };
            if (value.IsToggle)
            {
                // An on/off choice: a check box with its explanation underneath.
                var choice = new StackPanel { Margin = new Thickness(0, 8, 0, 8) };
                row.Check = new CheckBox { Content = value.Label, IsChecked = value.Value > 0, Foreground = Brush(53, 52, 47) };
                AutomationProperties.SetHelpText(row.Check, value.Hint);
                row.Check.Checked += delegate { if (!updating) SetValue(value.Key, 1); };
                row.Check.Unchecked += delegate { if (!updating) SetValue(value.Key, 0); };
                choice.Children.Add(row.Check);
                var about = Text(value.Hint, 12, FontWeights.Normal);
                about.Foreground = Brush(110, 104, 94);
                about.Margin = new Thickness(22, 2, 0, 0);
                choice.Children.Add(about);
                rows.Add(row);
                return choice;
            }
            var panel = new StackPanel { Margin = new Thickness(0, 8, 0, 8) };
            var head = new DockPanel { LastChildFill = true };
            row.Reset = new Button { Content = "戻す", MinWidth = 52, Padding = new Thickness(8, 1, 8, 1), Margin = new Thickness(8, 0, 0, 0),
                Background = Brush(255, 255, 255), BorderBrush = Brush(210, 199, 184), Foreground = Brush(53, 52, 47), FontSize = 12 };
            AutomationProperties.SetName(row.Reset, value.Label + "を初期値に戻す");
            row.Reset.Click += delegate { SetValue(value.Key, value.Default); };
            DockPanel.SetDock(row.Reset, Dock.Right); head.Children.Add(row.Reset);
            row.Number = Text("", 13, FontWeights.Normal);
            row.Number.Margin = new Thickness(8, 0, 0, 0);
            row.Number.VerticalAlignment = VerticalAlignment.Center;
            DockPanel.SetDock(row.Number, Dock.Right); head.Children.Add(row.Number);
            row.Label = Text(value.Label, 14, FontWeights.Normal);
            row.Label.VerticalAlignment = VerticalAlignment.Center;
            head.Children.Add(row.Label);
            panel.Children.Add(head);
            row.Slider = new Slider { Minimum = value.Min, Maximum = value.Max, SmallChange = value.Step, LargeChange = value.Step * 10,
                Value = value.Value, Margin = new Thickness(0, 4, 0, 2) };
            AutomationProperties.SetName(row.Slider, value.Label);
            AutomationProperties.SetHelpText(row.Slider, value.Hint);
            row.Slider.ValueChanged += delegate { if (!updating) SetValue(value.Key, row.Slider.Value); };
            panel.Children.Add(row.Slider);
            var hint = Text(value.Hint, 12, FontWeights.Normal);
            hint.Foreground = Brush(110, 104, 94);
            panel.Children.Add(hint);
            rows.Add(row);
            return panel;
        }

        // Sets one value as the slider would: rounded to the slider's step, and exactly the
        // built-in value when it lands on it, so "changed" means visibly changed.
        internal void SetValue(string key, double value)
        {
            TuningValue target = tuning.Find(key);
            if (target == null) return;
            double step = target.Step;
            double snapped = Math.Round(value / step) * step;
            if (Math.Abs(value - target.Default) <= step / 2) snapped = target.Default;
            double before = target.Value;
            target.Set(snapped);
            Refresh();
            if (target.Value != before) Changed();
        }

        internal void ResetAll()
        {
            bool any = tuning.AnyChanged;
            tuning.ResetAll();
            Refresh();
            if (any) Changed();
        }

        private void Changed()
        {
            unsaved = true;
            if (changed != null) changed();
            // One save after the slider has stopped, not one per step of a drag.
            saveTimer.Stop();
            saveTimer.Start();
        }

        internal void SaveNow()
        {
            saveTimer.Stop();
            if (!unsaved || save == null) return;
            string error = save();
            unsaved = error != null;
            status.Text = error == null ? (tuning.AnyChanged ? "保存しました。" : "すべて初期値です。")
                : "保存できませんでした（今回の起動中は有効です）: " + error;
        }

        internal string StatusText { get { return status.Text; } }
        internal bool Unsaved { get { return unsaved; } }

        private void CopySummary()
        {
            try
            {
                Clipboard.SetText(tuning.Summary());
                status.Text = "変えた値の一覧をコピーしました。";
            }
            catch (Exception e)
            {
                status.Text = "コピーできませんでした: " + e.Message;
            }
        }

        // Shows the current values: a changed one is marked and can be put back by itself.
        private void Refresh()
        {
            updating = true;
            try
            {
                foreach (Row row in rows)
                {
                    TuningValue value = row.Value;
                    if (row.Check != null)
                    {
                        if (row.Check.IsChecked != (value.Value > 0)) row.Check.IsChecked = value.Value > 0;
                        row.Check.FontWeight = value.Changed ? FontWeights.SemiBold : FontWeights.Normal;
                        continue;
                    }
                    if (row.Slider.Value != value.Value) row.Slider.Value = value.Value;
                    bool isChanged = value.Changed;
                    row.Number.Text = isChanged ? value.Format(value.Value) + "（初期値 " + value.Format(value.Default) + "）"
                        : value.Format(value.Value);
                    row.Number.FontWeight = row.Label.FontWeight = isChanged ? FontWeights.SemiBold : FontWeights.Normal;
                    row.Number.Foreground = isChanged ? Brush(170, 84, 56) : Brush(53, 52, 47);
                    row.Reset.Visibility = isChanged ? Visibility.Visible : Visibility.Hidden;
                }
                resetAll.IsEnabled = copy.IsEnabled = tuning.AnyChanged;
            }
            finally { updating = false; }
        }

        internal void ShowDemo(bool running)
        {
            if (demo.IsChecked != running) demo.IsChecked = running;
        }

        private static TextBlock Text(string text, double size, FontWeight weight)
        {
            return new TextBlock { Text = text, FontSize = size, FontWeight = weight, TextWrapping = TextWrapping.Wrap, Foreground = Brush(53, 52, 47) };
        }

        private static SolidColorBrush Brush(byte r, byte g, byte b)
        {
            return new SolidColorBrush(Color.FromRgb(r, g, b));
        }

        private static Button MakeButton(string text, bool primary)
        {
            return new Button { Content = text, MinHeight = 36, MinWidth = 96, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(4, 0, 0, 0),
                Background = primary ? Brush(198, 117, 89) : Brush(255, 255, 255), Foreground = primary ? Brush(255, 255, 255) : Brush(53, 52, 47),
                BorderBrush = Brush(210, 199, 184), BorderThickness = new Thickness(1) };
        }
    }
}
