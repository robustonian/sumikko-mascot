using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace SumikkoCompanion
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            bool created;
            using (var singleton = new Mutex(true, "Local\\SumikkoCompanion.DesktopHost", out created))
            {
                if (!created) return;
                bool demo = Array.Exists(args, a => string.Equals(a, "--demo", StringComparison.OrdinalIgnoreCase));
                bool smoke = Array.Exists(args, a => string.Equals(a, "--smoke-test", StringComparison.OrdinalIgnoreCase));
                var app = new Application();
                app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                bool inspect = Array.Exists(args, a => string.Equals(a, "--inspect", StringComparison.OrdinalIgnoreCase));
                var window = new CompanionWindow(demo, inspect);
                app.MainWindow = window;
                if (smoke)
                {
                    var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                    timer.Tick += delegate { timer.Stop(); window.Close(); };
                    timer.Start();
                }
                app.Run(window);
            }
        }
    }

    internal sealed class CompanionWindow : Window
    {
        private const int HotkeyId = 0x5355;
        private const int WmHotkey = 0x0312;
        private const int GwlExstyle = -20;
        private const int WsExTransparent = 0x20;
        private const int SwpNoMove = 0x0002;
        private const int SwpNoSize = 0x0001;
        private const int SwpNoZOrder = 0x0004;
        private const int SwpFrameChanged = 0x0020;
        // Every display frame while anything moves; once it has been quiet this long, about
        // 30 frames a second, which the slow idle sways do not need more than. Redrawing is
        // what costs (measured: ~17% of a core at 60 fps, ~2% without redraws), not the input.
        private const double QuietBeforeIdle = 1.0;
        // While idle the timer still reads the keys and buttons about 60 times a second, so a
        // short tap is not missed, and draws on about every other tick.
        private static readonly TimeSpan IdlePollInterval = TimeSpan.FromMilliseconds(15);
        private const double IdleFrameSeconds = .026;
        private readonly NativeSignals signals = new NativeSignals();
        // The character on screen. Another one can be chosen from the menu while running:
        // each has its own folder of artwork, its own mouse range and its own tuning file.
        private CharacterView character;
        private readonly Viewbox viewbox;
        private readonly string projectRoot, settingsDirectory;
        private string assetsDirectory;
        // "" for the character in assets/, otherwise its folder under characters/.
        private string characterId = "", wantedCharacter = "";
        // The saved mouse ranges of the characters not on screen (null/absent: never saved).
        private MouseMotionRange firstCharacterRange;
        private readonly Dictionary<string, MouseMotionRange> otherRanges = new Dictionary<string, MouseMotionRange>(StringComparer.Ordinal);
        private readonly Stopwatch clock = new Stopwatch();
        private readonly SettingsStore settingsStore;
        private readonly DispatcherTimer idleTimer;
        private Forms.NotifyIcon tray;
        private IntPtr trayIconHandle;
        private HwndSource source;
        private bool hotkeyRegistered;
        private bool clickThrough;
        private bool demo;
        private bool lipSyncTest;
        private double lipSyncTestStarted;
        private bool reducedMotion;
        private bool paused;
        private AudioSource selectedAudio = AudioSource.Off;
        // What NativeSignals is actually acquiring, which can differ from the user's choice.
        private AudioSource acquiredAudio = AudioSource.Off;
        private string acquiredDevice = "";
        private string inputDeviceId="",inputDeviceName="",outputDeviceId="",outputDeviceName="";
        private double lipSyncGain = CharacterView.DefaultLipSyncGain, lipSyncFloor = CharacterView.DefaultLipSyncFloor;
        private AudioDeviceWindow audioWindow;
        private int size = 440;
        private double motionStrength = 1;
        private MouseMotionRange mouseRange = MouseMotionRange.Default;
        private MouseMotionRange rangeBeforeEditing;
        private MouseRangeWindow rangeWindow;
        // The numbers of the motion changed on the tuning panel, kept beside the settings file.
        private string tuningFile;
        private TuningWindow tuningWindow;
        private bool tuningUnread;
        private double lastFrame;
        private double lastStatusUpdate;
        private double lastActive;
        private bool renderingHooked;
        // The monitor under the pointer, looked up again only when the pointer leaves it.
        private System.Drawing.Rectangle pointerScreen;
        private bool pointerScreenKnown;
        private bool promptingSave;

        internal CompanionWindow(bool startDemo, bool inspect)
            : this(startDemo, inspect, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.txt"))
        {
        }

        // The settings file is a parameter so the regression checks can use a scratch copy.
        internal CompanionWindow(bool startDemo, bool inspect, string settingsFile)
            : this(startDemo, inspect, settingsFile, Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..")))
        {
        }

        // The project folder (holding assets/ and characters/) is a parameter for the checks too.
        internal CompanionWindow(bool startDemo, bool inspect, string settingsFile, string projectFolder)
        {
            demo = startDemo;
            projectRoot = projectFolder;
            settingsDirectory = Path.GetDirectoryName(Path.GetFullPath(settingsFile));
            settingsStore = new SettingsStore(settingsFile);
            CompanionSettings saved = settingsStore.Load();
            if (saved != null) ApplySettings(saved, false);
            // A chosen character whose folder has gone (or cannot be read) gives way to the first.
            List<CharacterEntry> all = CharacterCatalog.Find(projectRoot);
            CharacterEntry chosen = all[0];
            foreach (CharacterEntry entry in all) if (entry.Id == wantedCharacter) chosen = entry;
            try { character = new CharacterView(chosen.Directory); }
            catch (Exception)
            {
                if (chosen == all[0]) throw;
                chosen = all[0];
                character = new CharacterView(chosen.Directory);
            }
            characterId = chosen.Id;
            assetsDirectory = chosen.Directory;
            mouseRange = StoredRange();
            ApplyToCharacter();
            LoadTuning();

            Title = "隅っこキャラ";
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = inspect;
            Topmost = true;
            SizeToContent = SizeToContent.Manual;
            SetSize(size);

            viewbox = new Viewbox { Stretch = Stretch.Uniform, Child = character };
            var surface = new Grid { Background = Brushes.Transparent };
            surface.Children.Add(viewbox);
            Content = surface;
            surface.MouseLeftButtonDown += delegate(object sender, MouseButtonEventArgs e)
            {
                if (e.ButtonState == MouseButtonState.Pressed) { DragMove(); SaveSettings(); }
            };
            surface.MouseRightButtonUp += delegate(object sender, MouseButtonEventArgs e)
            {
                var menu = BuildWindowMenu();
                menu.PlacementTarget = surface;
                menu.IsOpen = true;
                e.Handled = true;
            };
            idleTimer = new DispatcherTimer(DispatcherPriority.Normal) { Interval = IdlePollInterval };
            idleTimer.Tick += delegate { IdleTick(); };
            Loaded += OnLoaded;
            Closed += OnClosed;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ClampPosition();
            var handle = new WindowInteropHelper(this).Handle;
            source = HwndSource.FromHwnd(handle);
            if (source != null) source.AddHook(HandleMessage);
            // Ctrl+Alt+C remains available while the companion ignores mouse clicks.
            hotkeyRegistered = RegisterHotKey(handle, HotkeyId, 0x0001 | 0x0002, 0x43);
            SetClickThrough(clickThrough);
            CreateTray();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
            clock.Start();
            SetFullRate(true);
            character.Greet();
            if (character.Profile.Problems.Count > 0)
                ShowBalloon("キャラクターの設定に読めないところがあります", character.Profile.Problems[0]);
            if (settingsStore.LoadResult == SettingsLoadResult.Failed)
                ShowBalloon("設定ファイルを読み込めませんでした",
                    "前の設定を守るため、変更は保存しません。メニューの先頭から読み込み直しか上書き保存を選べます。");
            else if (clickThrough && !hotkeyRegistered)
                ShowBalloon("クリック透過中です",
                    "Ctrl+Alt+C を登録できませんでした。通知領域のアイコンをダブルクリックすると、透過を解除して操作できます。");
        }

        private void OnRendering(object sender, EventArgs e)
        {
            Frame(false);
        }

        private void IdleTick()
        {
            if (paused) return;
            if (clock.Elapsed.TotalSeconds - lastFrame < IdleFrameSeconds)
            {
                // Between idle frames only the input is read; a press is drawn at once (and the
                // frame uses this poll, since a press is reported by one poll only).
                signals.Poll();
                if (signals.KeyPressed || signals.MousePressed) Frame(true);
                return;
            }
            Frame(false);
        }

        private void Frame(bool alreadyPolled)
        {
            if (paused) return;
            double elapsed = clock.Elapsed.TotalSeconds;
            double dt = Math.Max(0, Math.Min(0.05, elapsed - lastFrame));
            lastFrame = elapsed;
            if (!alreadyPolled) signals.Poll();
            var cursor = new System.Drawing.Point((int)signals.MouseX, (int)signals.MouseY);
            if (!pointerScreenKnown || !pointerScreen.Contains(cursor))
            {
                pointerScreen = Forms.Screen.FromPoint(cursor).Bounds;
                pointerScreenKnown = true;
            }
            double pointerX = pointerScreen.Width > 0 ? (signals.MouseX - pointerScreen.Left) * 2 / pointerScreen.Width - 1 : 0;
            double pointerY = pointerScreen.Height > 0 ? (signals.MouseY - pointerScreen.Top) * 2 / pointerScreen.Height - 1 : 0;
            bool calibrating=rangeWindow!=null;
            character.IdleMouseMotion=!calibrating;
            // The calibration preview holds still; otherwise the character acts, and can
            // tell a pointer on its head (for petting) from one elsewhere.
            character.Scenes=!calibrating;
            character.PointerOnArt=calibrating ? new Point(double.NaN,double.NaN) : PointerOnCharacter();
            double audioLevel=lipSyncTest ? CharacterView.LipSyncTestLevel(elapsed-lipSyncTestStarted) : signals.AudioLevel;
            if(calibrating)
            {
                Point preview=rangeWindow.PreviewPointer;
                pointerX=preview.X;pointerY=preview.Y;
            }
            // Moving to a setting must not move the desktop hand independently of the preview.
            character.Tick(elapsed, dt, calibrating?false:signals.KeyPressed, calibrating?false:signals.MousePressed,
                pointerX, pointerY, calibrating?0:audioLevel, calibrating?false:demo, calibrating?false:reducedMotion,
                signals.KeyEmphatic, signals.KeyRepeated);
            // The first key or click after a quiet spell switches back to full rate in this frame.
            // Pointer moves count once the hand moves quickly (NeedsFullFrameRate): a slow drift
            // of the pointer looks the same at the idle rate.
            if (calibrating || demo || lipSyncTest || signals.KeyPressed || signals.MousePressed ||
                character.NeedsFullFrameRate)
                lastActive = elapsed;
            SetFullRate(elapsed - lastActive < QuietBeforeIdle);
            if (elapsed - lastStatusUpdate > .5)
            {
                lastStatusUpdate = elapsed;
                UpdateTrayText();
            }
        }

        // Where the pointer is on the character's artwork (NaN while that cannot be known).
        private Point PointerOnCharacter()
        {
            try
            {
                if (PresentationSource.FromVisual(character) == null) return new Point(double.NaN, double.NaN);
                return character.PointFromScreen(new Point(signals.MouseX, signals.MouseY));
            }
            catch (InvalidOperationException)
            {
                return new Point(double.NaN, double.NaN);
            }
        }

        // Full rate follows the display through CompositionTarget.Rendering; the idle rate uses a
        // timer instead, because skipping Rendering callbacks does not give a steady rate (WPF
        // slows the callbacks down itself when a frame draws nothing).
        private void SetFullRate(bool full)
        {
            if (full)
            {
                if (idleTimer.IsEnabled) idleTimer.Stop();
                if (!renderingHooked) { CompositionTarget.Rendering += OnRendering; renderingHooked = true; }
            }
            else
            {
                if (renderingHooked) { CompositionTarget.Rendering -= OnRendering; renderingHooked = false; }
                if (!idleTimer.IsEnabled) idleTimer.Start();
            }
        }

        private void StopTicking()
        {
            idleTimer.Stop();
            if (renderingHooked) { CompositionTarget.Rendering -= OnRendering; renderingHooked = false; }
        }

        private void UpdateTrayText()
        {
            if (tray == null) return;
            string text = "隅っこキャラ / " + AudioDescription();
            tray.Text = text.Length > 63 ? text.Substring(0, 63) : text;
        }

        private void SetSize(int newSize)
        {
            size = newSize;
            Width = size;
            Height = size * 1071.0 / 1469.0;
            ClampPosition();
            SaveSettings();
        }

        private void ClampPosition()
        {
            Rect work = SystemParameters.WorkArea;
            if (!double.IsNaN(Left) && !double.IsNaN(Top))
            {
                // Keep a saved secondary-monitor position; convert screen pixels to WPF DIPs.
                var dpi = VisualTreeHelper.GetDpi(this);
                var screen = Forms.Screen.FromRectangle(new System.Drawing.Rectangle(
                    (int)(Left * dpi.DpiScaleX), (int)(Top * dpi.DpiScaleY),
                    (int)(Width * dpi.DpiScaleX), (int)(Height * dpi.DpiScaleY)));
                var area = screen.WorkingArea;
                work = new Rect(area.Left / dpi.DpiScaleX, area.Top / dpi.DpiScaleY,
                    area.Width / dpi.DpiScaleX, area.Height / dpi.DpiScaleY);
            }
            if (double.IsNaN(Left) || double.IsNaN(Top))
            {
                Left = work.Right - Width - 24;
                Top = work.Bottom - Height - 12;
            }
            Left = Math.Max(work.Left, Math.Min(Left, work.Right - Width));
            Top = Math.Max(work.Top, Math.Min(Top, work.Bottom - Height));
        }

        // Back to the lower-right corner of the screen the pointer is on, for when the companion
        // has been lost off-screen or under something.
        private void ReturnToScreen()
        {
            SetPaused(false);
            var dpi = VisualTreeHelper.GetDpi(this);
            var area = Forms.Screen.FromPoint(Forms.Cursor.Position).WorkingArea;
            Left = area.Right / dpi.DpiScaleX - Width - 24;
            Top = area.Bottom / dpi.DpiScaleY - Height - 12;
            ClampPosition();
            Show();
            Activate();
            SaveSettings();
        }

        private void OnDisplaySettingsChanged(object sender, EventArgs e)
        {
            // Raised on another thread; a removed monitor must not leave the companion off-screen.
            Dispatcher.BeginInvoke(new Action(delegate
            {
                pointerScreenKnown = false;
                ClampPosition();
            }));
        }

        private void SetClickThrough(bool enabled)
        {
            clickThrough = enabled;
            IntPtr handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                int style = GetWindowLong(handle, GwlExstyle);
                int changed = enabled ? style | WsExTransparent : style & ~WsExTransparent;
                if (style != changed)
                {
                    SetWindowLong(handle, GwlExstyle, changed);
                    SetWindowPos(handle, IntPtr.Zero, 0, 0, 0, 0,
                        SwpNoMove | SwpNoSize | SwpNoZOrder | SwpFrameChanged);
                }
            }
            SaveSettings();
        }

        // Hidden for a meeting or a screen share: nothing is drawn, polled or recorded until it
        // comes back. The clock stops too, so blinks and head tilts due meanwhile do not all fire
        // at once on return, and a key or button held at that moment is not taken as a new press.
        internal void SetPaused(bool value)
        {
            if (paused == value) return;
            if (value && (rangeWindow != null || audioWindow != null || tuningWindow != null)) return;
            paused = value;
            if (paused)
            {
                StopTicking();
                clock.Stop();
                Hide();
            }
            else
            {
                clock.Start();
                lastFrame = lastActive = clock.Elapsed.TotalSeconds;
                signals.ResetInputState();
                Show();
                SetFullRate(true);
                character.Greet();
            }
            ApplyAudio(false);
            UpdateTrayText();
        }

        internal bool Paused { get { return paused; } }

        internal sealed class MenuEntry
        {
            public string Label, ToolTip;
            public bool Enabled = true, Separator;
            public bool? Checked;
            public Action Action;
            public List<MenuEntry> Children;
        }

        private static MenuEntry Item(string label, Action action)
        {
            return new MenuEntry { Label = label, Action = action };
        }

        private static MenuEntry Check(string label, bool on, Action action)
        {
            return new MenuEntry { Label = label, Checked = on, Action = action };
        }

        private static MenuEntry Info(string label, string toolTip)
        {
            return new MenuEntry { Label = label, ToolTip = toolTip, Enabled = false };
        }

        private static MenuEntry Line()
        {
            return new MenuEntry { Separator = true };
        }

        private static MenuEntry Sub(string label, params MenuEntry[] children)
        {
            return new MenuEntry { Label = label, Children = new List<MenuEntry>(children) };
        }

        // One description for both the right-click menu and the notification-area menu, so
        // every setting can be changed from either (the tray is the only way while the
        // companion ignores clicks or is hidden).
        internal List<MenuEntry> MenuEntries()
        {
            var items = new List<MenuEntry>();
            if (!settingsStore.CanSave)
            {
                items.Add(Info("設定ファイルを読み込めないため、変更を保存していません", settingsStore.LastError));
                items.Add(Item("設定を読み込み直す", ReloadSettingsWithReport));
                items.Add(Item("現在の設定で上書き保存", delegate { OverwriteSettings(); }));
                items.Add(Line());
            }
            else if (settingsStore.SaveFailed)
            {
                items.Add(Info("設定を保存できませんでした", settingsStore.LastError));
                items.Add(Item("もう一度保存する", SaveWithPrompt));
                items.Add(Line());
            }
            items.Add(Info(AudioDescription(), null));
            items.Add(Sub("音声",
                Check("オフ", !lipSyncTest && selectedAudio == AudioSource.Off, delegate { SelectAudio(AudioSource.Off); }),
                Check("マイク・仮想入力", !lipSyncTest && selectedAudio == AudioSource.Microphone, delegate { SelectAudio(AudioSource.Microphone); }),
                Check("PC 再生", !lipSyncTest && selectedAudio == AudioSource.Playback, delegate { SelectAudio(AudioSource.Playback); }),
                Line(),
                Item("オーディオデバイス設定…", ShowAudioDevices),
                Check("口パクテスト（音声不要）", lipSyncTest, delegate { SetLipSyncTest(!lipSyncTest); })));
            items.Add(Sub("動き",
                Sub("キーボードの動きの大きさ",
                    Check("控えめ", motionStrength == .4, delegate { SetStrength(.4); }),
                    Check("大きく（標準）", motionStrength == 1, delegate { SetStrength(1); }),
                    Check("とても大きく", motionStrength == 1.4, delegate { SetStrength(1.4); })),
                Check("動きを抑える", reducedMotion, delegate { SetReducedMotion(!reducedMotion); }),
                Item("マウスの移動範囲…", ShowMouseRange),
                Item("動きの細かい調整…", ShowTuning),
                Line(),
                Check("デモ", demo, delegate { SetDemo(!demo); })));
            MenuEntry through = Check(hotkeyRegistered ? "クリック透過 (Ctrl+Alt+C)" : "クリック透過（Ctrl+Alt+C は使用不可）",
                clickThrough, delegate { SetClickThrough(!clickThrough); });
            if (!hotkeyRegistered)
                through.ToolTip = "Ctrl+Alt+C をショートカットとして登録できませんでした（ほかのアプリが使っている可能性があります）。" +
                    "透過の解除は、このメニューか通知領域のアイコンのダブルクリックでできます。";
            items.Add(Sub("表示",
                Check("小 320", size == 320, delegate { SetSize(320); }),
                Check("中 440", size == 440, delegate { SetSize(440); }),
                Check("大 560", size == 560, delegate { SetSize(560); }),
                Line(),
                through,
                Item("画面内に戻す", ReturnToScreen)));
            var choices = new List<MenuEntry>();
            bool busy = rangeWindow != null || audioWindow != null || tuningWindow != null;
            foreach (CharacterEntry entry in CharacterCatalog.Find(projectRoot))
            {
                string id = entry.Id;
                MenuEntry choice = Check(entry.Name, id == characterId, delegate { ChooseCharacter(id); });
                if (busy && id != characterId)
                {
                    choice.Enabled = false;
                    choice.ToolTip = "開いている設定の画面を閉じてから切り替えてください。";
                }
                choices.Add(choice);
            }
            choices.Add(Line());
            choices.Add(Item("キャラクターのフォルダを開く", OpenCharacterFolder));
            items.Add(new MenuEntry { Label = "キャラクター", Children = choices });
            items.Add(Line());
            MenuEntry hide = Item(paused ? "表示する" : "一時的に隠す", delegate { SetPaused(!paused); });
            if (!paused) hide.ToolTip = "隠している間は、描画・キーやマウスの監視・音声の取得を止めます。通知領域のアイコンから戻せます。";
            if (!paused && (rangeWindow != null || audioWindow != null || tuningWindow != null)) hide.Enabled = false;
            items.Add(hide);
            items.Add(Item("終了", Close));
            return items;
        }

        private ContextMenu BuildWindowMenu()
        {
            var menu = new ContextMenu();
            AddWpfItems(menu.Items, MenuEntries());
            return menu;
        }

        private static void AddWpfItems(ItemCollection items, List<MenuEntry> entries)
        {
            foreach (MenuEntry entry in entries)
            {
                if (entry.Separator) { items.Add(new Separator()); continue; }
                // Underscores would otherwise mark access keys (device names may contain them).
                var item = new MenuItem { Header = entry.Label.Replace("_", "__"), IsEnabled = entry.Enabled };
                if (entry.Checked.HasValue) { item.IsCheckable = true; item.IsChecked = entry.Checked.Value; }
                if (entry.ToolTip != null) { item.ToolTip = entry.ToolTip; ToolTipService.SetShowOnDisabled(item, true); }
                if (entry.Children != null) AddWpfItems(item.Items, entry.Children);
                else if (entry.Action != null)
                {
                    Action action = entry.Action;
                    item.Click += delegate { action(); };
                }
                items.Add(item);
            }
        }

        private void AddTrayItems(Forms.ToolStripItemCollection items, List<MenuEntry> entries)
        {
            foreach (MenuEntry entry in entries)
            {
                if (entry.Separator) { items.Add(new Forms.ToolStripSeparator()); continue; }
                var item = new Forms.ToolStripMenuItem(entry.Label.Replace("&", "&&")) { Enabled = entry.Enabled, ToolTipText = entry.ToolTip };
                if (entry.Checked.HasValue) item.Checked = entry.Checked.Value;
                if (entry.Children != null)
                {
                    AddTrayItems(item.DropDownItems, entry.Children);
                    item.DropDown.ShowItemToolTips = true;
                }
                else if (entry.Action != null)
                {
                    // Run after the menu has closed (some actions open a modal dialog).
                    Action action = entry.Action;
                    item.Click += delegate { Dispatcher.BeginInvoke(action); };
                }
                items.Add(item);
            }
        }

        // ---- Choosing the character ----

        internal string CharacterId { get { return characterId; } }
        internal CharacterView Character { get { return character; } }
        internal string TuningFile { get { return tuningFile; } }

        // The mouse range saved for the character on screen, or its own built-in one.
        private MouseMotionRange StoredRange()
        {
            MouseMotionRange stored;
            if (characterId.Length == 0) stored = firstCharacterRange;
            else otherRanges.TryGetValue(characterId, out stored);
            return stored ?? character.Profile.MouseRange;
        }

        // Each character keeps its own tuning beside the settings file.
        private void LoadTuning()
        {
            tuningFile = Path.Combine(settingsDirectory, characterId.Length == 0 ? "tuning.txt" : "tuning-" + characterId + ".txt");
            string tuningError;
            tuningUnread = !character.Tuning.Load(tuningFile, out tuningError);
        }

        private void ChooseCharacter(string id)
        {
            if (!SwitchCharacter(id))
            {
                Message("キャラクターを切り替えられませんでした。\n\n画像 5 枚（base.png、keyboard-arm.png、mouse-arm.png、head.png、" +
                    "face-expression.png）がそのフォルダにそろっているか確かめてください。", MessageBoxButton.OK);
                return;
            }
            if (character.Profile.Problems.Count > 0)
                Message("キャラクターの設定（" + CharacterProfile.FileName + "）に読めないところがありました。その項目は見本の値のまま表示しています。\n\n" +
                    string.Join("\n", character.Profile.Problems), MessageBoxButton.OK);
        }

        // False when the character cannot be shown (nothing changes then), or while a
        // settings window that works on the current one is open.
        internal bool SwitchCharacter(string id)
        {
            if (id == characterId) return true;
            if (rangeWindow != null || audioWindow != null || tuningWindow != null) return false;
            // The range of the one being left is kept for when it comes back.
            if (characterId.Length == 0) firstCharacterRange = mouseRange;
            else otherRanges[characterId] = mouseRange;
            if (!ShowCharacter(id)) return false;
            SaveSettings();
            return true;
        }

        private bool ShowCharacter(string id)
        {
            CharacterEntry chosen = null;
            foreach (CharacterEntry entry in CharacterCatalog.Find(projectRoot)) if (entry.Id == id) chosen = entry;
            if (chosen == null) return false;
            CharacterView next;
            try { next = new CharacterView(chosen.Directory); }
            catch (Exception) { return false; }
            characterId = chosen.Id;
            assetsDirectory = chosen.Directory;
            character = next;
            viewbox.Child = next;
            mouseRange = StoredRange();
            ApplyToCharacter();
            LoadTuning();
            if (tray != null)
            {
                IntPtr old = trayIconHandle;
                trayIconHandle = IntPtr.Zero;
                tray.Icon = CreateTrayIcon() ?? System.Drawing.SystemIcons.Application;
                if (old != IntPtr.Zero) DestroyIcon(old);
            }
            if (!paused && clock.IsRunning)
            {
                // The new character starts in step with the clock, and says hello.
                lastActive = clock.Elapsed.TotalSeconds;
                SetFullRate(true);
                Frame(false);
                character.Greet();
            }
            return true;
        }

        private void OpenCharacterFolder()
        {
            try
            {
                string folder = Path.Combine(projectRoot, CharacterCatalog.FolderName);
                Directory.CreateDirectory(folder);
                Process.Start("explorer.exe", "\"" + folder + "\"");
            }
            catch (Exception e)
            {
                Message("フォルダを開けませんでした。\n\n" + e.Message, MessageBoxButton.OK);
            }
        }

        internal void SelectAudio(AudioSource value)
        {
            lipSyncTest=false;
            demo=false;
            selectedAudio = value;
            // Choosing a source again restarts it, e.g. after the Windows default device changed.
            ApplyAudio(true);
        }

        // The audio actually needed right now: none while hidden, nor during the mouth test or
        // the demo (both drive the mouth with synthetic levels). The user's choice is kept and
        // comes back with them.
        internal static AudioSource AudioToAcquire(AudioSource selected, bool hidden, bool mouthTest, bool demoRunning)
        {
            return hidden || mouthTest || demoRunning ? AudioSource.Off : selected;
        }

        internal AudioSource AcquiredAudio { get { return acquiredAudio; } }

        private void ApplyAudio(bool restart)
        {
            AudioSource wanted = AudioToAcquire(selectedAudio, paused, lipSyncTest, demo);
            string device = wanted == AudioSource.Microphone ? inputDeviceId : (wanted == AudioSource.Playback ? outputDeviceId : "");
            if (!restart && wanted == acquiredAudio && device == acquiredDevice) return;
            acquiredAudio = wanted;
            acquiredDevice = device;
            signals.SetAudioSource(wanted, device);
        }

        private void ShowAudioDevices()
        {
            if(audioWindow!=null) { audioWindow.Activate(); return; }
            audioWindow=new AudioDeviceWindow(selectedAudio,inputDeviceId,inputDeviceName,outputDeviceId,outputDeviceName,
                lipSyncGain,lipSyncFloor);
            // While the companion is hidden the dialog stands on its own instead of revealing it.
            if (paused) audioWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            else audioWindow.Owner=this;
            audioWindow.Topmost=true;
            try
            {
                if(audioWindow.ShowDialog()!=true)return;
                inputDeviceId=audioWindow.InputId; inputDeviceName=audioWindow.InputName;
                outputDeviceId=audioWindow.OutputId; outputDeviceName=audioWindow.OutputName;
                lipSyncGain=audioWindow.LipSyncGain; lipSyncFloor=audioWindow.LipSyncFloor;
                ApplyToCharacter();
                SelectAudio(audioWindow.SelectedSource);
            }
            finally { audioWindow=null; }
            SaveWithPrompt();
        }

        internal void SetLipSyncTest(bool enabled)
        {
            if(lipSyncTest==enabled)return;
            lipSyncTest=enabled;
            if(enabled)
            {
                demo=false;
                lipSyncTestStarted=clock.Elapsed.TotalSeconds;
            }
            // The selected source is kept for restoration; the test itself acquires no audio.
            ApplyAudio(false);
        }

        internal void SetDemo(bool enabled)
        {
            demo=enabled;
            if(tuningWindow!=null)tuningWindow.ShowDemo(enabled);
            // Ending the mouth test for the demo must not start the real audio the demo ignores.
            if(enabled)lipSyncTest=false;
            ApplyAudio(false);
        }

        private void SetStrength(double strength)
        {
            motionStrength = strength;
            character.MotionStrength = strength;
            SaveSettings();
        }

        private void SetReducedMotion(bool enabled)
        {
            reducedMotion = enabled;
            SaveSettings();
        }

        // The tuning panel stays open beside the companion (it is not a dialog), so values can
        // be judged while typing and clicking as usual.
        private void ShowTuning()
        {
            if (tuningWindow != null) { tuningWindow.Activate(); return; }
            SetPaused(false);
            if (tuningUnread)
            {
                // Never save over tuned values this run could not read.
                string error;
                tuningUnread = !character.Tuning.Load(tuningFile, out error);
                character.TuningChanged();
                if (tuningUnread)
                {
                    Message("保存してある動きの調整（tuning.txt）を読み込めませんでした。\n\n" + error +
                        "\n\nほかのアプリが使っていないか確かめてから、もう一度お試しください。", MessageBoxButton.OK);
                    return;
                }
            }
            tuningWindow = NewTuningWindow();
            tuningWindow.Owner = this;
            tuningWindow.Topmost = true;
            tuningWindow.Closed += delegate { tuningWindow = null; };
            tuningWindow.Show();
        }

        internal TuningWindow NewTuningWindow()
        {
            return new TuningWindow(character.Tuning, character.TuningChanged, SaveTuning, demo, SetDemo);
        }

        // Null when saved, otherwise why not.
        internal string SaveTuning()
        {
            if (tuningUnread) return "保存してある調整を読み込めていません。";
            string error;
            return character.Tuning.Save(tuningFile, out error) ? null : error;
        }

        internal MotionTuning Tuning { get { return character.Tuning; } }

        private void ShowMouseRange()
        {
            if(rangeWindow!=null) { rangeWindow.Activate(); return; }
            // The desktop character is part of the calibration preview.
            SetPaused(false);
            BeginMouseRangeEdit();
            MouseMotionRange accepted=null;
            bool saved=true;
            try
            {
                rangeWindow=new MouseRangeWindow(assetsDirectory,mouseRange,PreviewMouseRange);
                rangeWindow.Owner=this;
                rangeWindow.Topmost=true;
                if(rangeWindow.ShowDialog()==true)accepted=rangeWindow.SelectedRange;
            }
            finally
            {
                rangeWindow=null;
                saved=EndMouseRangeEdit(accepted);
            }
            if(accepted!=null && !saved)SaveWithPrompt();
        }

        // While the range dialog is open its preview drives the character, but only a
        // confirmed range is ever saved; cancelling restores the range from before.
        internal void BeginMouseRangeEdit()
        {
            rangeBeforeEditing=mouseRange;
        }

        internal void PreviewMouseRange(MouseMotionRange changed)
        {
            mouseRange=changed;
            character.MouseRange=changed;
        }

        // accepted is null when the dialog was cancelled. Returns whether the settings were saved.
        internal bool EndMouseRangeEdit(MouseMotionRange accepted)
        {
            mouseRange=accepted ?? rangeBeforeEditing ?? mouseRange;
            character.MouseRange=mouseRange;
            rangeBeforeEditing=null;
            return SaveSettings();
        }

        private string AudioDescription()
        {
            if (paused) return "一時的に非表示（動きと音声は停止中）";
            string status = signals.AudioStatus ?? "";
            if (status.IndexOf("cleanup pending", StringComparison.OrdinalIgnoreCase) >= 0)
                return "マイク停止処理中（機器の応答待ち）";
            if(lipSyncTest)return rangeWindow==null?"口パクテスト中（音声不要）":"口パクテスト一時停止（マウス調整中）";
            if(demo)return selectedAudio==AudioSource.Off?"デモ中":"デモ中（音声の取得は停止中）";
            if (selectedAudio == AudioSource.Off) return "口パク: オフ";
            bool input=selectedAudio==AudioSource.Microphone;
            string name=input?inputDeviceName:outputDeviceName;
            string id=input?inputDeviceId:outputDeviceId;
            string label=(input?"入力: ":"PC 再生: ")+(string.IsNullOrEmpty(id)?"Windowsの既定":
                (string.IsNullOrEmpty(name)?"選択したデバイス":name));
            if (rangeWindow != null) return label + "（マウス調整中は口パク停止）";
            if (status.IndexOf("ready", StringComparison.OrdinalIgnoreCase) >= 0)
                return label + " 接続中 / 音量 " + (signals.AudioLevel * 100).ToString("0") + "%";
            return label + " を取得できません（接続・機器の設定を確認）";
        }

        private void CreateTray()
        {
            tray = new Forms.NotifyIcon();
            tray.Icon = CreateTrayIcon() ?? System.Drawing.SystemIcons.Application;
            tray.Text = "隅っこキャラ";
            var menu = new Forms.ContextMenuStrip { ShowItemToolTips = true };
            menu.Opening += delegate
            {
                var old = new List<Forms.ToolStripItem>();
                foreach (Forms.ToolStripItem item in menu.Items) old.Add(item);
                menu.Items.Clear();
                foreach (Forms.ToolStripItem item in old) item.Dispose();
                AddTrayItems(menu.Items, MenuEntries());
            };
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += delegate
            {
                Dispatcher.BeginInvoke(new Action(delegate { SetPaused(false); SetClickThrough(false); Show(); Activate(); }));
            };
            tray.Visible = true;
            UpdateTrayText();
        }

        // The character's own head, so it can be told apart from the other icons.
        private System.Drawing.Icon CreateTrayIcon()
        {
            try
            {
                int edge = Math.Max(16, Forms.SystemInformation.SmallIconSize.Width);
                using (var art = new System.Drawing.Bitmap(Path.Combine(assetsDirectory, "head.png")))
                using (var bitmap = new System.Drawing.Bitmap(edge, edge, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                {
                    // The part of head.png that holds the whole head.
                    double[] square = character.Profile.Numbers("trayIcon");
                    double scale = art.Width / CharacterView.ArtWidth;
                    var crop = new System.Drawing.Rectangle((int)(square[0] * scale), (int)(square[1] * scale),
                        (int)(square[2] * scale), (int)(square[2] * scale));
                    using (var g = System.Drawing.Graphics.FromImage(bitmap))
                    {
                        g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                        g.DrawImage(art, new System.Drawing.Rectangle(0, 0, edge, edge), crop, System.Drawing.GraphicsUnit.Pixel);
                    }
                    trayIconHandle = bitmap.GetHicon();
                    return System.Drawing.Icon.FromHandle(trayIconHandle);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void ShowBalloon(string title, string text)
        {
            if (tray == null) return;
            tray.ShowBalloonTip(8000, title, text, Forms.ToolTipIcon.Warning);
        }

        private IntPtr HandleMessage(IntPtr hwnd, int msg, IntPtr wparam, IntPtr lparam, ref bool handled)
        {
            if (msg == WmHotkey && wparam.ToInt32() == HotkeyId)
            {
                SetClickThrough(!clickThrough);
                handled = true;
            }
            return IntPtr.Zero;
        }

        // live: also apply to the running window, character and audio (a re-read while running).
        private void ApplySettings(CompanionSettings saved, bool live)
        {
            size = saved.Size;
            if (!double.IsNaN(saved.Left)) Left = saved.Left;
            if (!double.IsNaN(saved.Top)) Top = saved.Top;
            clickThrough = saved.ClickThrough;
            reducedMotion = saved.ReducedMotion;
            motionStrength = saved.MotionStrength;
            wantedCharacter = saved.Character ?? "";
            firstCharacterRange = saved.MouseRange;
            otherRanges.Clear();
            foreach (KeyValuePair<string, MouseMotionRange> own in saved.CharacterRanges) otherRanges[own.Key] = own.Value;
            // (Before the first character exists, the constructor picks the range up.)
            if (character != null)
            {
                // A range being previewed stays on screen; the saved one is what Cancel returns to.
                if (rangeBeforeEditing != null) rangeBeforeEditing = StoredRange();
                else mouseRange = StoredRange();
            }
            inputDeviceId = saved.InputDeviceId;
            inputDeviceName = saved.InputDeviceName;
            outputDeviceId = saved.OutputDeviceId;
            outputDeviceName = saved.OutputDeviceName;
            lipSyncGain = saved.LipSyncGain;
            lipSyncFloor = saved.LipSyncFloor;
            if (!live) return;
            if (wantedCharacter != characterId && rangeWindow == null && audioWindow == null && tuningWindow == null)
                ShowCharacter(wantedCharacter);
            ApplyToCharacter();
            SetSize(size);
            SetClickThrough(clickThrough);
            ApplyAudio(false);
        }

        private void ApplyToCharacter()
        {
            character.MotionStrength = motionStrength;
            character.MouseRange = mouseRange;
            character.LipSyncGain = lipSyncGain;
            character.LipSyncFloor = lipSyncFloor;
        }

        // Saving is skipped (and reported as done) until the window has a position to keep.
        internal bool SaveSettings()
        {
            if (double.IsNaN(Left) || double.IsNaN(Top)) return true;
            bool failedBefore = settingsStore.SaveFailed;
            bool saved = settingsStore.Save(CurrentSettings());
            // Say so once when saving starts failing, instead of only at the next menu.
            if (!saved && settingsStore.CanSave && !failedBefore && !promptingSave)
                ShowBalloon("設定を保存できませんでした", "メニューの「もう一度保存する」で再試行できます。");
            return saved;
        }

        // After a dialog's "save", a failure is reported right away with a chance to retry.
        private void SaveWithPrompt()
        {
            promptingSave = true;
            try
            {
                while (!SaveSettings())
                {
                    if (!settingsStore.CanSave)
                    {
                        Message("設定ファイルを読み込めなかったため、変更は保存していません（今回の起動中は有効です）。\n\n" +
                            "メニューの先頭の「設定を読み込み直す」または「現在の設定で上書き保存」を選んでください。",
                            MessageBoxButton.OK);
                        return;
                    }
                    if (Message("設定を保存できませんでした。\n\n" + settingsStore.LastError +
                        "\n\n今回の起動中は変更が有効です。もう一度保存しますか？", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                        return;
                }
            }
            finally { promptingSave = false; }
        }

        private MessageBoxResult Message(string text, MessageBoxButton buttons)
        {
            return IsVisible
                ? MessageBox.Show(this, text, "隅っこキャラ", buttons, MessageBoxImage.Warning)
                : MessageBox.Show(text, "隅っこキャラ", buttons, MessageBoxImage.Warning);
        }

        internal CompanionSettings CurrentSettings()
        {
            // Other actions (e.g. a tray hotkey) must not persist an unconfirmed preview.
            MouseMotionRange shown = rangeBeforeEditing ?? mouseRange;
            var ranges = new Dictionary<string, MouseMotionRange>(otherRanges, StringComparer.Ordinal);
            if (characterId.Length > 0) ranges[characterId] = shown;
            return new CompanionSettings
            {
                Size = size, Left = Left, Top = Top,
                ClickThrough = clickThrough, ReducedMotion = reducedMotion, MotionStrength = motionStrength,
                MouseRange = characterId.Length == 0 ? shown :
                    (firstCharacterRange ?? CharacterProfile.Load(Path.Combine(projectRoot, "assets")).MouseRange),
                Character = characterId, CharacterRanges = ranges,
                InputDeviceId = inputDeviceId, InputDeviceName = inputDeviceName,
                OutputDeviceId = outputDeviceId, OutputDeviceName = outputDeviceName,
                LipSyncGain = lipSyncGain, LipSyncFloor = lipSyncFloor
            };
        }

        // The user chose to replace a settings file this run could not read.
        internal bool OverwriteSettings()
        {
            settingsStore.AllowOverwrite();
            return SaveSettings();
        }

        // Reads settings.txt again after it could not be read (e.g. it was locked at start-up)
        // and applies what it holds. False while it still cannot be read.
        internal bool ReloadSettings()
        {
            CompanionSettings saved = settingsStore.Load();
            if (settingsStore.LoadResult == SettingsLoadResult.Failed) return false;
            if (saved != null) ApplySettings(saved, true);
            return true;
        }

        private void ReloadSettingsWithReport()
        {
            if (!ReloadSettings())
                Message("設定ファイルをまだ読み込めません。\n\n" + settingsStore.LastError +
                    "\n\nほかのアプリが使っていないか確かめてから、もう一度お試しください。", MessageBoxButton.OK);
        }

        private void OnClosed(object sender, EventArgs e)
        {
            StopTicking();
            Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
            if (source != null) source.RemoveHook(HandleMessage);
            if (hotkeyRegistered) UnregisterHotKey(new WindowInteropHelper(this).Handle, HotkeyId);
            if (tray != null)
            {
                tray.Visible = false;
                tray.ContextMenuStrip.Dispose();
                tray.Dispose();
            }
            if (trayIconHandle != IntPtr.Zero) DestroyIcon(trayIconHandle);
            signals.Dispose();
            SaveSettings();
        }

        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr hwnd, int id, int modifiers, int key);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)] private static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)] private static extern int SetWindowLong(IntPtr hwnd, int index, int value);
        [DllImport("user32.dll", SetLastError = true)] private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int width, int height, int flags);
        [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr icon);
    }
}
