using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using Microsoft.Win32;
using Ellipse = System.Windows.Shapes.Ellipse;

namespace ERSeamless
{
    public class MainUi
    {
        public readonly Window Window;

        readonly State state;
        readonly List<Action> loaders = new List<Action>();
        readonly List<UIElement> lockWhenBusy = new List<UIElement>();
        Dictionary<string, string> values = ModConfig.Defaults();
        string gameDir;
        Release latest;
        bool updateCheckFailed, busy;
        TaskCompletionSource<bool> dialog;
        TextBox passwordBox;

        readonly Grid Root, PanelInner, Overlay;
        readonly Image Bg, PanelBg;
        readonly TextBlock Title, Subtitle, Tagline, Caption, LblGame, LangCode, GameText, ModText, EacText, SteamText, StatusText, DlgTitle, DlgText;
        readonly Ellipse GameDot, ModDot, EacDot, SteamDot;
        readonly Button MainBtn, UpdateBtn, BrowseBtn, LangBtn, MinBtn, CloseBtn, DlgOk, DlgCancel;
        readonly ProgressBar Progress;
        readonly RadioButton TabSession, TabDiff, TabAdv;
        readonly StackPanel PageSession, PageDiff, PageAdv, LangList;
        readonly Popup LangPopup;

        const double LangMenuWidth = 170;

        public MainUi(string gameOverride)
        {
            using (var s = Res("MainWindow.xaml")) Window = (Window)XamlReader.Load(s);
            Root = F<Grid>("Root"); PanelInner = F<Grid>("PanelInner"); Overlay = F<Grid>("Overlay");
            Bg = F<Image>("Bg"); PanelBg = F<Image>("PanelBg");
            Title = F<TextBlock>("Title"); Subtitle = F<TextBlock>("Subtitle"); Tagline = F<TextBlock>("Tagline"); Caption = F<TextBlock>("Caption");
            LblGame = F<TextBlock>("LblGame"); LangCode = F<TextBlock>("LangCode");
            GameText = F<TextBlock>("GameText"); ModText = F<TextBlock>("ModText"); EacText = F<TextBlock>("EacText");
            SteamText = F<TextBlock>("SteamText"); StatusText = F<TextBlock>("StatusText");
            DlgTitle = F<TextBlock>("DlgTitle"); DlgText = F<TextBlock>("DlgText");
            GameDot = F<Ellipse>("GameDot"); ModDot = F<Ellipse>("ModDot"); EacDot = F<Ellipse>("EacDot"); SteamDot = F<Ellipse>("SteamDot");
            MainBtn = F<Button>("MainBtn"); UpdateBtn = F<Button>("UpdateBtn"); BrowseBtn = F<Button>("BrowseBtn"); LangBtn = F<Button>("LangBtn");
            MinBtn = F<Button>("MinBtn"); CloseBtn = F<Button>("CloseBtn"); DlgOk = F<Button>("DlgOk"); DlgCancel = F<Button>("DlgCancel");
            Progress = F<ProgressBar>("Progress");
            TabSession = F<RadioButton>("TabSession"); TabDiff = F<RadioButton>("TabDiff"); TabAdv = F<RadioButton>("TabAdv");
            PageSession = F<StackPanel>("PageSession"); PageDiff = F<StackPanel>("PageDiff"); PageAdv = F<StackPanel>("PageAdv");
            LangList = F<StackPanel>("LangList"); LangPopup = F<Popup>("LangPopup");

            state = State.Load();
            L.Set(state.Get("language")); // rien d'enregistré : langue de Windows
            InitChrome();
            InitFont();
            InitBackground();
            Title.Text = Spaced("ELDEN RING", " ");
            Subtitle.Text = Spaced("SEAMLESS CO-OP", " ");
            Caption.Text = "Seamless Co-op Launcher · v" + Program.Version;
            Wire();
            ApplyLanguage();

            gameDir = gameOverride ?? state.Get("game_dir");
            if (!Game.IsValid(gameDir))
            {
                gameDir = Game.Detect();
                if (gameDir != null) { state.Set("game_dir", gameDir); state.Save(); }
            }
            LoadSettings();
            RefreshStatus();

            Window.Loaded += async (s, e) =>
            {
                LayoutGlass();
                await CheckUpdates();
            };
        }

        // ───────────────────────── fenêtre ─────────────────────────

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        void InitChrome()
        {
            WindowChrome.SetWindowChrome(Window, new WindowChrome
            {
                CaptionHeight = 34,
                ResizeBorderThickness = new Thickness(0),
                GlassFrameThickness = new Thickness(0, 0, 0, 1),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false
            });
            WindowChrome.SetIsHitTestVisibleInChrome(LangBtn, true);
            WindowChrome.SetIsHitTestVisibleInChrome(MinBtn, true);
            WindowChrome.SetIsHitTestVisibleInChrome(CloseBtn, true);
            Window.SourceInitialized += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(Window).Handle;
                int round = 2, dark = 1; // coins arrondis Windows 11 + bordure sombre
                DwmSetWindowAttribute(hwnd, 33, ref round, 4);
                DwmSetWindowAttribute(hwnd, 20, ref dark, 4);
            };
        }

        void InitFont()
        {
            var dir = Path.Combine(Paths.AppData, "fonts");
            var file = Path.Combine(dir, "Cinzel.ttf");
            try
            {
                using (var s = Res("Cinzel.ttf"))
                    if (!File.Exists(file) || new FileInfo(file).Length != s.Length)
                    {
                        Directory.CreateDirectory(dir);
                        using (var f = File.Create(file)) s.CopyTo(f);
                    }
            }
            catch (Exception ex) { Log.Write("font: " + ex.Message); }
            if (File.Exists(file))
                Window.Resources["Serif"] = new FontFamily(new Uri(dir + "\\"), "./#Cinzel, Palatino Linotype");
        }

        void InitBackground()
        {
            // une image "launcher-background.jpg/png" posée à côté de l'exe remplace le fond intégré
            BitmapImage bmp = null;
            foreach (var name in new[] { "launcher-background.jpg", "launcher-background.png" })
            {
                var custom = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
                if (!File.Exists(custom)) continue;
                try { bmp = LoadBitmap(File.OpenRead(custom)); break; }
                catch (Exception ex) { Log.Write("background: " + ex.Message); }
            }
            if (bmp == null) bmp = LoadBitmap(Res("background.jpg"));
            Bg.Source = bmp;
            PanelBg.Source = bmp;
        }

        static BitmapImage LoadBitmap(Stream stream)
        {
            using (stream)
            {
                var b = new BitmapImage();
                b.BeginInit();
                b.CacheOption = BitmapCacheOption.OnLoad;
                b.StreamSource = stream;
                b.EndInit();
                b.Freeze();
                return b;
            }
        }

        /// <summary>Effet verre dépoli : copie floutée du fond, calée pixel pour pixel derrière le panneau.</summary>
        void LayoutGlass()
        {
            var p = PanelInner.TranslatePoint(new Point(0, 0), Root);
            PanelBg.Width = Root.ActualWidth;
            PanelBg.Height = Root.ActualHeight;
            Canvas.SetLeft(PanelBg, -p.X);
            Canvas.SetTop(PanelBg, -p.Y);
            PanelInner.Clip = new RectangleGeometry(new Rect(0, 0, PanelInner.ActualWidth, PanelInner.ActualHeight), 9, 9);
        }

        void Wire()
        {
            MinBtn.Click += (s, e) => Window.WindowState = WindowState.Minimized;
            CloseBtn.Click += (s, e) => Window.Close();
            LangBtn.Click += (s, e) => OpenLanguageMenu();
            RoutedEventHandler tab = (s, e) =>
            {
                PageSession.Visibility = TabSession.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                PageDiff.Visibility = TabDiff.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                PageAdv.Visibility = TabAdv.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
            };
            TabSession.Checked += tab;
            TabDiff.Checked += tab;
            TabAdv.Checked += tab;

            BrowseBtn.Click += (s, e) => Browse();
            MainBtn.Click += async (s, e) => await OnMain();
            UpdateBtn.Click += async (s, e) => await Install();

            DlgOk.Click += (s, e) => CloseDialog(true);
            DlgCancel.Click += (s, e) => CloseDialog(false);
            Window.PreviewKeyDown += (s, e) =>
            {
                if (Overlay.Visibility != Visibility.Visible) return;
                if (e.Key == Key.Escape) { CloseDialog(false); e.Handled = true; }
                else if (e.Key == Key.Enter) { CloseDialog(true); e.Handled = true; }
            };
            Window.Activated += (s, e) => { if (!busy) RefreshStatus(); };
            Window.Closing += (s, e) =>
            {
                try { SaveSettings(); }
                catch (Exception ex) { Log.Write("save on close: " + ex.Message); }
            };
        }

        // ───────────────────────── langue ─────────────────────────

        /// <summary>Applique la langue courante : textes fixes + reconstruction des onglets (les valeurs saisies sont conservées).</summary>
        void ApplyLanguage()
        {
            LangCode.Text = L.Code.ToUpperInvariant();
            LangBtn.ToolTip = L.T("lang_tip");
            Tagline.Text = L.T("tagline");
            LblGame.Text = L.T("lbl_game");
            TabSession.Content = L.T("tab_session");
            TabDiff.Content = L.T("tab_diff");
            TabAdv.Content = L.T("tab_adv");
            EacText.ToolTip = L.T("eac_tip");
            StatusText.Text = "";

            loaders.Clear();
            lockWhenBusy.Clear();
            lockWhenBusy.AddRange(new UIElement[] { MainBtn, UpdateBtn, BrowseBtn, LangBtn });
            foreach (var page in new[] { PageSession, PageDiff, PageAdv }) page.Children.Clear();
            BuildPages();
            ReloadControls();
        }

        void OpenLanguageMenu()
        {
            if (busy || Overlay.Visibility == Visibility.Visible) return;
            LangList.Children.Clear();
            for (int i = 0; i < L.Codes.Length; i++)
            {
                var code = L.Codes[i];
                bool current = code == L.Code;
                var row = new DockPanel();
                var check = new TextBlock { Text = current ? "✓" : "", Foreground = B("Gold") };
                DockPanel.SetDock(check, Dock.Right);
                row.Children.Add(check);
                row.Children.Add(new TextBlock { Text = L.Names[i], Foreground = current ? B("Gold") : B("Text") });
                var item = new Button { Style = (Style)Window.Resources["MenuBtn"], Content = row };
                item.Click += (s, e) =>
                {
                    LangPopup.IsOpen = false;
                    if (code == L.Code) return;
                    L.Set(code);
                    state.Set("language", code);
                    state.Save();
                    ApplyLanguage();
                    RefreshStatus();
                };
                LangList.Children.Add(item);
            }
            LangPopup.HorizontalOffset = LangBtn.ActualWidth - LangMenuWidth; // menu aligné à droite du bouton
            LangPopup.IsOpen = true;
        }

        // ───────────────────────── panneau de réglages ─────────────────────────

        void BuildPages()
        {
            var s = PageSession;
            passwordBox = AddText(s, "cooppassword", L.T("pwd_label"), L.T("pwd_hint"), L.T("pwd_placeholder"), 64, null);
            AddToggle(s, "allow_invaders", L.T("invaders"), L.T("invaders_hint"));
            AddToggle(s, "death_debuffs", L.T("debuffs"), L.T("debuffs_hint"));
            AddToggle(s, "allow_summons", L.T("summons"), L.T("summons_hint"));
            AddToggle(s, "always_spectate_on_death", L.T("spectate"), L.T("spectate_hint"));
            AddChoice(s, "overhead_player_display", L.T("overhead"), L.T("overhead_items").Split('|'));
            AddToggle(s, "skip_splash_screens", L.T("splash"), null);
            AddSlider(s, "default_boot_master_volume", L.T("volume"), 0, 10, 1, v => v == 0 ? L.T("volume_mute") : v + " / 10");

            var d = PageDiff;
            d.Children.Add(Hint(L.T("diff_hint"), new Thickness(0, 0, 0, 16)));
            Func<int, string> pct = v => L.F("pct", v);
            AddSlider(d, "enemy_health_scaling", L.T("enemy_hp"), 0, 200, 5, pct);
            AddSlider(d, "enemy_damage_scaling", L.T("enemy_dmg"), 0, 200, 5, pct);
            AddSlider(d, "enemy_posture_scaling", L.T("enemy_posture"), 0, 200, 5, pct);
            AddSlider(d, "boss_health_scaling", L.T("boss_hp"), 0, 300, 5, pct);
            AddSlider(d, "boss_damage_scaling", L.T("boss_dmg"), 0, 200, 5, pct);
            AddSlider(d, "boss_posture_scaling", L.T("boss_posture"), 0, 200, 5, pct);
            d.Children.Add(ActionButton(L.T("diff_reset"), new Thickness(0, 6, 0, 0), () =>
            {
                foreach (var k in ModConfig.ScalingKeys) values[k] = ModConfig.Default(k);
                ReloadControls();
            }));

            var a = PageAdv;
            AddText(a, "save_file_extension", L.T("ext_label"), L.T("ext_hint"), "co2", 120, ModConfig.SanitizeExtension);
            AddToggle(a, "append_steam_id_to_players", L.T("steamid"), L.T("steamid_hint"));
            AddStateToggle(a, "close_on_launch", true, L.T("close_on_launch"), null);
            AddStateToggle(a, "backup_on_launch", true, L.T("backup"), L.T("backup_hint"));
            a.Children.Add(ActionButton(L.T("btn_import"), new Thickness(0, 4, 0, 8), async () => await ImportSave()));
            var grid = new UniformGrid { Columns = 2 };
            grid.Children.Add(ActionButton(L.T("btn_game_folder"), new Thickness(0, 0, 4, 8), () => OpenFolder(gameDir)));
            grid.Children.Add(ActionButton(L.T("btn_saves_folder"), new Thickness(4, 0, 0, 8), () => OpenFolder(Paths.SavesRoot)));
            grid.Children.Add(ActionButton(L.T("btn_reinstall"), new Thickness(0, 0, 4, 0), async () => await Install()));
            grid.Children.Add(ActionButton(L.T("btn_uninstall"), new Thickness(4, 0, 0, 0), async () => await Uninstall()));
            a.Children.Add(grid);
        }

        TextBlock Label(string text)
        {
            return new TextBlock { Text = text, FontSize = 13.5, Foreground = B("Text") };
        }

        TextBlock Hint(string text, Thickness margin)
        {
            return new TextBlock { Text = text, FontSize = 11.5, Foreground = B("Muted"), TextWrapping = TextWrapping.Wrap, Margin = margin };
        }

        Button ActionButton(string text, Thickness margin, Action click)
        {
            var b = new Button { Style = (Style)Window.Resources["Action"], Content = text, Margin = margin };
            b.Click += (s, e) => click();
            lockWhenBusy.Add(b);
            return b;
        }

        TextBox AddText(Panel host, string key, string label, string hint, string placeholder, int maxLength, Func<string, string> sanitize)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            sp.Children.Add(Label(label));
            var g = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            var tb = new TextBox { Style = (Style)Window.Resources["Input"], MaxLength = maxLength };
            var ph = new TextBlock
            {
                Text = placeholder, FontSize = 14, Foreground = B("Muted"), Opacity = 0.7, IsHitTestVisible = false,
                Margin = new Thickness(13, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center
            };
            g.Children.Add(tb);
            g.Children.Add(ph);
            sp.Children.Add(g);
            if (hint != null) sp.Children.Add(Hint(hint, new Thickness(0, 5, 0, 0)));
            tb.TextChanged += (s, e) =>
            {
                ph.Visibility = tb.Text.Length == 0 ? Visibility.Visible : Visibility.Hidden;
                values[key] = sanitize == null ? tb.Text : sanitize(tb.Text);
            };
            loaders.Add(() => tb.Text = Get(key));
            host.Children.Add(sp);
            return tb;
        }

        CheckBox MakeToggle(string label, string hint)
        {
            var sp = new StackPanel();
            sp.Children.Add(Label(label));
            if (hint != null) sp.Children.Add(Hint(hint, new Thickness(0, 2, 0, 0)));
            return new CheckBox { Style = (Style)Window.Resources["Toggle"], Content = sp, Margin = new Thickness(0, 0, 0, hint == null ? 13 : 11) };
        }

        void AddToggle(Panel host, string key, string label, string hint)
        {
            var cb = MakeToggle(label, hint);
            RoutedEventHandler changed = (s, e) => values[key] = cb.IsChecked == true ? "1" : "0";
            cb.Checked += changed;
            cb.Unchecked += changed;
            loaders.Add(() => cb.IsChecked = Get(key) == "1");
            host.Children.Add(cb);
        }

        void AddStateToggle(Panel host, string key, bool def, string label, string hint)
        {
            var cb = MakeToggle(label, hint);
            cb.IsChecked = state.GetBool(key, def);
            RoutedEventHandler changed = (s, e) => { state.SetBool(key, cb.IsChecked == true); state.Save(); };
            cb.Checked += changed;
            cb.Unchecked += changed;
            host.Children.Add(cb);
        }

        void AddChoice(Panel host, string key, string label, string[] items)
        {
            var g = new Grid { Margin = new Thickness(0, 1, 0, 14) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var lbl = Label(label);
            lbl.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(lbl);
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            Grid.SetColumn(sp, 1);
            var prev = new Button { Style = (Style)Window.Resources["Arrow"], Content = "‹" };
            var txt = new TextBlock { Width = 96, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = B("Gold") };
            var next = new Button { Style = (Style)Window.Resources["Arrow"], Content = "›" };
            sp.Children.Add(prev);
            sp.Children.Add(txt);
            sp.Children.Add(next);
            g.Children.Add(sp);
            Func<int> current = () =>
            {
                int v;
                return int.TryParse(Get(key), out v) && v >= 0 && v < items.Length ? v : 0;
            };
            Action<int> select = v =>
            {
                v = (v + items.Length) % items.Length;
                values[key] = v.ToString();
                txt.Text = items[v];
            };
            prev.Click += (s, e) => select(current() - 1);
            next.Click += (s, e) => select(current() + 1);
            loaders.Add(() => txt.Text = items[current()]);
            host.Children.Add(g);
        }

        void AddSlider(Panel host, string key, string label, int min, int max, int step, Func<int, string> format)
        {
            var g = new Grid { Margin = new Thickness(0, 0, 0, 11) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var val = new TextBlock { Foreground = B("Gold"), FontSize = 13 };
            Grid.SetColumn(val, 1);
            var sl = new Slider
            {
                Style = (Style)Window.Resources["GoldSlider"], Minimum = min, Maximum = max,
                TickFrequency = step, SmallChange = step, LargeChange = step, Margin = new Thickness(0, 4, 0, 0)
            };
            Grid.SetRow(sl, 1);
            Grid.SetColumnSpan(sl, 2);
            g.Children.Add(Label(label));
            g.Children.Add(val);
            g.Children.Add(sl);
            sl.ValueChanged += (s, e) =>
            {
                int v = (int)Math.Round(sl.Value);
                val.Text = format(v);
                values[key] = v.ToString();
            };
            sl.PreviewMouseWheel += (s, e) =>
            {
                sl.Value = Math.Max(sl.Minimum, Math.Min(sl.Maximum, sl.Value + (e.Delta > 0 ? step : -step)));
                e.Handled = true;
            };
            loaders.Add(() =>
            {
                int v;
                if (!int.TryParse(Get(key), out v)) int.TryParse(ModConfig.Default(key), out v);
                if (v > sl.Maximum) sl.Maximum = v; // valeur réglée à la main au-delà de la plage
                sl.Value = v;
                val.Text = format(v);
            });
            host.Children.Add(g);
        }

        // ───────────────────────── réglages ─────────────────────────

        string Get(string key)
        {
            string v;
            return values.TryGetValue(key, out v) ? v : ModConfig.Default(key);
        }

        string Ext
        {
            get
            {
                var e = ModConfig.SanitizeExtension(Get("save_file_extension"));
                return e.Length == 0 ? "co2" : e;
            }
        }

        void LoadSettings()
        {
            var v = ModConfig.Defaults();
            var src = Mod.IsInstalled(gameDir) ? Ini.Load(Mod.IniPath(gameDir)) : null;
            if (src == null) src = Ini.Load(Paths.SettingsCopy);
            if (src != null)
                foreach (var kv in src.Values()) v[kv.Key] = kv.Value;
            values = v;
            ReloadControls();
        }

        void ReloadControls()
        {
            foreach (var load in loaders) load();
        }

        /// <summary>Écrit les réglages dans le ersc_settings.ini du jeu (si le mod est installé) et dans une copie locale.</summary>
        void SaveSettings()
        {
            values["save_file_extension"] = Ext;
            try
            {
                Directory.CreateDirectory(Paths.AppData);
                var copy = Ini.Load(Paths.SettingsCopy) ?? new Ini(null);
                ModConfig.Apply(copy, values);
                copy.Save(Paths.SettingsCopy);
            }
            catch (Exception ex) { Log.Write("settings copy: " + ex.Message); }
            if (!Mod.IsInstalled(gameDir)) return;
            var path = Mod.IniPath(gameDir);
            var ini = Ini.Load(path) ?? new Ini(null);
            ModConfig.Apply(ini, values);
            ini.Save(path);
        }

        // ───────────────────────── état ─────────────────────────

        void RefreshStatus()
        {
            bool gameOk = Game.IsValid(gameDir);
            bool installed = Mod.IsInstalled(gameDir);
            string ver = installed ? Mod.InstalledVersion(gameDir) : null;
            bool updatable = installed && latest != null && !string.Equals(ver, latest.Tag, StringComparison.OrdinalIgnoreCase);

            Row(GameDot, GameText, gameOk ? "Ok" : "Bad", gameOk ? ShortPath(gameDir) : L.T("not_found"));
            GameText.ToolTip = gameOk ? gameDir : L.T("game_tip_missing");
            BrowseBtn.Content = L.T(gameOk ? "browse_change" : "browse_locate");

            if (!gameOk) Row(ModDot, ModText, "Off", "—");
            else if (!installed) Row(ModDot, ModText, "Bad", L.T("mod_missing") + (latest != null ? L.F("mod_available", latest.Tag) : ""));
            else if (updatable) Row(ModDot, ModText, "Gold", L.F("mod_update", ver ?? L.T("version_unknown"), latest.Tag));
            else Row(ModDot, ModText, "Ok", (ver ?? L.T("mod_installed")) + (latest != null ? L.T("mod_uptodate") : updateCheckFailed ? L.T("mod_unchecked") : ""));
            ModText.ToolTip = latest == null ? null
                : L.F("latest_tip", latest.Tag) + (latest.Summary.Length > 0 ? " — " + latest.Summary : "");

            if (installed) Row(EacDot, EacText, "Ok", L.T("eac_off"));
            else Row(EacDot, EacText, "Off", L.T("eac_on"));

            if (Steam.Ready()) Row(SteamDot, SteamText, "Ok", L.T("steam_ok"));
            else if (Steam.Exe() != null) Row(SteamDot, SteamText, "Gold", L.T(Steam.IsRunning() ? "steam_waiting" : "steam_autostart"));
            else Row(SteamDot, SteamText, "Bad", L.T("not_found"));

            MainBtn.Content = L.T(!gameOk ? "btn_locate_game" : !installed ? "btn_install" : "btn_play");
            UpdateBtn.Visibility = updatable ? Visibility.Visible : Visibility.Collapsed;
            if (updatable)
            {
                UpdateBtn.Content = L.F("btn_update", latest.Tag);
                UpdateBtn.ToolTip = latest.Summary.Length > 0 ? latest.Summary : null;
            }
        }

        void Row(Ellipse dot, TextBlock text, string brush, string value)
        {
            dot.Fill = B(brush);
            text.Text = value;
        }

        /// <summary>progress : NaN = barre masquée, négatif = indéterminé, sinon pourcentage.</summary>
        void Status(string text, double progress)
        {
            StatusText.Text = text;
            Progress.Visibility = double.IsNaN(progress) ? Visibility.Hidden : Visibility.Visible;
            Progress.IsIndeterminate = progress < 0;
            if (progress >= 0) Progress.Value = progress;
        }

        void SetBusy(bool value)
        {
            busy = value;
            foreach (var e in lockWhenBusy) e.IsEnabled = !value;
            if (!value) Progress.Visibility = Visibility.Hidden;
        }

        async Task CheckUpdates()
        {
            try
            {
                latest = await Mod.FetchLatest();
                updateCheckFailed = false;
            }
            catch (Exception ex)
            {
                updateCheckFailed = true;
                Log.Write("update check: " + ex.Message);
            }
            if (!busy) RefreshStatus();
        }

        // ───────────────────────── actions ─────────────────────────

        async Task OnMain()
        {
            if (busy) return;
            if (!Game.IsValid(gameDir)) Browse();
            else if (!Mod.IsInstalled(gameDir)) await Install();
            else await Play();
        }

        void Browse()
        {
            var dlg = new OpenFileDialog
            {
                Title = L.T("browse_title"),
                Filter = "Elden Ring (eldenring.exe)|eldenring.exe",
                FileName = "eldenring.exe"
            };
            if (Game.IsValid(gameDir)) dlg.InitialDirectory = gameDir;
            if (dlg.ShowDialog(Window) != true) return;
            var dir = Path.GetDirectoryName(dlg.FileName);
            if (!Game.IsValid(dir)) return;
            try { SaveSettings(); } catch (Exception ex) { Log.Write("save before browse: " + ex.Message); }
            gameDir = dir;
            state.Set("game_dir", dir);
            state.Save();
            LoadSettings();
            RefreshStatus();
        }

        async Task<bool> Install()
        {
            if (busy) return false;
            if (!Game.IsValid(gameDir)) { Browse(); return false; }
            if (Game.IsRunning())
            {
                await Alert(L.T("game_running_title"), L.T("game_running_install"));
                return false;
            }

            SetBusy(true);
            Exception error = null;
            string message = null;
            try
            {
                if (!Fs.CanWrite(gameDir))
                {
                    if (!await Ask(L.T("access_title"), L.T("access_text"), L.T("access_ok"), L.T("cancel")))
                    {
                        Status(L.T("install_cancelled"), double.NaN);
                        return false;
                    }
                    Status(L.T("requesting_admin"), -1);
                    var dir = gameDir;
                    if (!await Task.Run(() => Fs.RequestWriteAccess(dir)) || !Fs.CanWrite(dir))
                        throw new UserError(L.T("access_failed"));
                }

                Status(L.T("checking_latest"), -1);
                var rel = latest = await Mod.FetchLatest();
                updateCheckFailed = false;
                var label = L.F("downloading", rel.Tag);
                Status(label, 0);
                var zip = await Mod.Download(rel, p => Status(label + "  " + p + " %", p));

                Status(L.T("installing"), -1);
                var vals = new Dictionary<string, string>(values, StringComparer.OrdinalIgnoreCase);
                vals["save_file_extension"] = Ext;
                var game = gameDir;
                await Task.Run(() => Mod.Install(zip, game, vals, rel.Tag));
                try { File.Delete(zip); } catch { }
                Log.Write("installed " + rel.Tag + " in " + game);
                message = L.F("installed_msg", rel.Tag);

                // première installation : on reprend la progression vanilla si aucune sauvegarde Seamless n'existe
                if (!state.GetBool("save_import_done", false))
                {
                    try
                    {
                        if (Saves.ImportVanilla(Ext, false) > 0) message += "\n\n" + L.T("import_auto_done");
                    }
                    catch (Exception ex)
                    {
                        Log.Write("auto import: " + ex);
                        message += "\n\n" + L.F("import_auto_failed", ex.Message);
                    }
                    state.SetBool("save_import_done", true);
                    state.Save();
                }
                Status(L.F("installed_status", rel.Tag), double.NaN);
            }
            catch (Exception ex) { error = ex; }
            finally
            {
                SetBusy(false);
                RefreshStatus();
            }

            if (error != null)
            {
                Log.Write("install: " + error);
                Status(L.T("install_failed_status"), double.NaN);
                await Alert(L.T("install_failed_title"), Friendly(error));
                return false;
            }
            bool needPassword = string.IsNullOrWhiteSpace(Get("cooppassword"));
            if (needPassword) message += "\n\n" + L.T("need_password");
            await Alert(L.T("install_done_title"), message);
            if (needPassword) FocusPassword();
            return true;
        }

        async Task Play()
        {
            if (busy) return;
            if (Game.IsRunning())
            {
                await Alert(L.T("already_running_title"), L.T("already_running_text"));
                return;
            }
            if (string.IsNullOrWhiteSpace(Get("cooppassword")) &&
                !await Ask(L.T("no_password_title"), L.T("no_password_text"), L.T("launch_anyway"), L.T("choose_password")))
            {
                FocusPassword();
                return;
            }

            SetBusy(true);
            Exception error = null;
            bool started = false;
            try
            {
                SaveSettings();

                if (!Steam.Ready())
                {
                    var exe = Steam.Exe();
                    if (exe == null) throw new UserError(L.T("steam_missing"));
                    if (!Steam.IsRunning())
                        using (Process.Start(exe, "-silent")) { }
                    Status(L.T("starting_steam"), -1);
                    var sw = Stopwatch.StartNew();
                    while (!Steam.Ready())
                    {
                        if (sw.Elapsed.TotalSeconds > 120) throw new UserError(L.T("steam_timeout"));
                        await Task.Delay(500);
                    }
                    await Task.Delay(3000); // laisse Steam finir son initialisation
                }

                if (state.GetBool("backup_on_launch", true))
                {
                    Status(L.T("backing_up"), -1);
                    var ext = Ext;
                    try { await Task.Run(() => Saves.BackupAll(ext)); }
                    catch (Exception ex) { Log.Write("backup: " + ex.Message); }
                }

                // complète les traductions du mod si elles manquent (installation faite avant cette version du launcher, mise à jour hors launcher…)
                try { Mod.WriteLocales(gameDir, null, false); }
                catch (Exception ex) { Log.Write("locales: " + ex.Message); }

                Status(L.T("launching"), -1);
                using (Process.Start(new ProcessStartInfo(Mod.LauncherExe(gameDir)) { WorkingDirectory = gameDir, UseShellExecute = true })) { }
                var wait = Stopwatch.StartNew();
                while (!Game.IsRunning() && wait.Elapsed.TotalSeconds < 60) await Task.Delay(500);
                if (!Game.IsRunning()) throw new UserError(L.T("launch_not_detected"));
                started = true;
                Status(L.T("launched"), double.NaN);
                Log.Write("game started");
            }
            catch (Exception ex) { error = ex; }
            finally
            {
                SetBusy(false);
                RefreshStatus();
            }

            if (error != null)
            {
                Log.Write("play: " + error);
                Status(L.T("launch_failed") + ".", double.NaN);
                await Alert(L.T("launch_failed"), Friendly(error));
                return;
            }
            if (started && state.GetBool("close_on_launch", true))
            {
                await Task.Delay(1200);
                Window.Close();
            }
        }

        async Task ImportSave()
        {
            if (busy) return;
            if (!Saves.HasVanilla())
            {
                await Alert(L.T("no_vanilla_title"), L.F("no_vanilla_text", Paths.SavesRoot));
                return;
            }
            if (Game.IsRunning())
            {
                await Alert(L.T("game_running_title"), L.T("game_running_import"));
                return;
            }
            var ext = Ext;
            var text = Saves.HasMod(ext) ? L.F("import_replace", ext, Saves.BackupDir) : L.F("import_copy", ext);
            if (!await Ask(L.T("import_title"), text, L.T("import_ok"), L.T("cancel"))) return;

            string err = null;
            try
            {
                Saves.ImportVanilla(ext, true);
                Status(L.T("import_done"), double.NaN);
            }
            catch (Exception ex) { err = Friendly(ex); }
            if (err != null) await Alert(L.T("import_failed_title"), err);
        }

        async Task Uninstall()
        {
            if (busy) return;
            if (!Mod.IsInstalled(gameDir))
            {
                await Alert(L.T("nothing_title"), L.T("nothing_text"));
                return;
            }
            if (Game.IsRunning())
            {
                await Alert(L.T("game_running_title"), L.T("game_running_uninstall"));
                return;
            }
            if (!await Ask(L.T("uninstall_title"), L.T("uninstall_text"), L.T("uninstall_ok"), L.T("cancel"))) return;
            string err = null;
            try
            {
                SaveSettings();
                Mod.Uninstall(gameDir);
                Status(L.T("uninstalled"), double.NaN);
            }
            catch (Exception ex) { err = Friendly(ex); }
            RefreshStatus();
            if (err != null) await Alert(L.T("uninstall_failed_title"), err);
        }

        void OpenFolder(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            using (Process.Start("explorer.exe", "\"" + dir + "\"")) { }
        }

        void FocusPassword()
        {
            TabSession.IsChecked = true;
            passwordBox.Focus();
            Keyboard.Focus(passwordBox);
        }

        // ───────────────────────── boîte de dialogue ─────────────────────────

        Task<bool> Ask(string title, string text, string ok, string cancel)
        {
            if (dialog != null) dialog.TrySetResult(false);
            DlgTitle.Text = title;
            DlgText.Text = text;
            DlgOk.Content = ok;
            DlgCancel.Content = cancel;
            DlgCancel.Visibility = cancel == null ? Visibility.Collapsed : Visibility.Visible;
            Overlay.Visibility = Visibility.Visible;
            dialog = new TaskCompletionSource<bool>();
            return dialog.Task;
        }

        Task Alert(string title, string text)
        {
            return Ask(title, text, L.T("ok"), null);
        }

        void CloseDialog(bool result)
        {
            Overlay.Visibility = Visibility.Collapsed;
            var d = dialog;
            dialog = null;
            if (d != null) d.TrySetResult(result);
        }

        // ───────────────────────── utilitaires ─────────────────────────

        static string Friendly(Exception ex)
        {
            var agg = ex as AggregateException;
            if (agg != null && agg.InnerException != null) ex = agg.InnerException;
            if (ex is UserError) return ex.Message;
            var web = ex as WebException;
            if (web != null)
            {
                var http = web.Response as HttpWebResponse;
                if (http != null && (int)http.StatusCode == 403) return L.T("err_ratelimit");
                return L.T("err_network") + "\n\n(" + ex.Message + ")";
            }
            if (ex is UnauthorizedAccessException) return L.T("err_access") + "\n\n(" + ex.Message + ")";
            if (ex is IOException) return L.T("err_locked") + "\n\n(" + ex.Message + ")";
            return ex.Message;
        }

        static string ShortPath(string p)
        {
            var parts = p.TrimEnd('\\').Split('\\');
            return parts.Length <= 3 ? p : parts[0] + "\\…\\" + parts[parts.Length - 2] + "\\" + parts[parts.Length - 1];
        }

        static string Spaced(string text, string gap)
        {
            return string.Join(gap, text.Select(c => c.ToString()));
        }

        static Stream Res(string name)
        {
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(name);
        }

        T F<T>(string name) where T : class
        {
            return (T)Window.FindName(name);
        }

        Brush B(string key)
        {
            return (Brush)Window.Resources[key];
        }
    }
}
