using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Win32;
using Ellipse = System.Windows.Shapes.Ellipse;
using Rectangle = System.Windows.Shapes.Rectangle;

namespace ERSeamless
{
    /// <summary>Valeurs en cours d'édition des réglages d'un mod.</summary>
    class Form
    {
        public string ModId;
        public SettingsSchema Schema;
        public Dictionary<string, string> Values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public readonly List<Action> Loaders = new List<Action>();

        public string Get(string key)
        {
            string v;
            return Values.TryGetValue(key, out v) ? v : "";
        }

        public void Reload()
        {
            foreach (var load in Loaders) load();
        }
    }

    public class MainUi
    {
        public readonly Window Window;

        readonly State state;
        ModStore store;
        Catalog catalog;
        string gameDir, gameVersion;
        GitHub.Release me3Latest;
        List<KeyValuePair<InstalledMod, CatalogMod>> pendingUpdates = new List<KeyValuePair<InstalledMod, CatalogMod>>();
        Dictionary<string, List<ModConflict>> conflicts = new Dictionary<string, List<ModConflict>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<int, string> nexusVersions = new Dictionary<int, string>();
        List<NexusMod> discoverResults; // dernière recherche Nexus réussie (null si elle a échoué)
        DownloadWatch downloads;
        bool busy, discoverLoaded, suppressToggle;
        TaskCompletionSource<bool> dialog;
        Form seamlessForm, sheetForm;
        InstalledMod sheetMod;
        TextBox passwordBox;
        readonly List<KeyValuePair<Grid, Image>> glass = new List<KeyValuePair<Grid, Image>>();

        readonly Grid Root, PanelInner, ModsInner, Overlay, Sheet, PlayPage, ModsPage, PanelPages, SheetPages;
        readonly Image Bg;
        readonly Rectangle PlayShade, ModsShade;
        readonly TextBlock Title, Subtitle, Tagline, Caption, LblGame, LblMods, LblEngine, LangCode, GameText, ModsText, EngineText, SteamText,
                           StatusText, DlgTitle, DlgText, ModsInfo, SearchPh, DiscoverHint, SheetTitle;
        readonly Ellipse GameDot, ModsDot, EngineDot, SteamDot;
        readonly Button MainBtn, UpdateBtn, BrowseBtn, ManageBtn, LangBtn, MinBtn, CloseBtn, DlgOk, DlgCancel, ImportZipBtn, ImportDirBtn, CheckUpdatesBtn, SearchBtn, SheetClose;
        readonly ProgressBar Progress;
        readonly RadioButton NavPlay, NavMods, SubCatalog, SubInstalled, SubDiscover;
        readonly UniformGrid PanelTabs, SheetTabs;
        readonly StackPanel CatalogList, InstalledList, DiscoverList, LangList;
        readonly ScrollViewer CatalogScroll, InstalledScroll;
        readonly DockPanel DiscoverPage;
        readonly TextBox SearchBox;
        readonly Popup LangPopup;

        const double LangMenuWidth = 170;

        public MainUi(string gameOverride)
        {
            using (var s = Res("MainWindow.xaml")) Window = (Window)XamlReader.Load(s);
            Root = F<Grid>("Root"); PanelInner = F<Grid>("PanelInner"); ModsInner = F<Grid>("ModsInner"); Overlay = F<Grid>("Overlay");
            Sheet = F<Grid>("Sheet"); PlayPage = F<Grid>("PlayPage"); ModsPage = F<Grid>("ModsPage"); PanelPages = F<Grid>("PanelPages");
            SheetPages = F<Grid>("SheetPages"); Bg = F<Image>("Bg"); PlayShade = F<Rectangle>("PlayShade"); ModsShade = F<Rectangle>("ModsShade");
            Title = F<TextBlock>("Title"); Subtitle = F<TextBlock>("Subtitle"); Tagline = F<TextBlock>("Tagline"); Caption = F<TextBlock>("Caption");
            LblGame = F<TextBlock>("LblGame"); LblMods = F<TextBlock>("LblMods"); LblEngine = F<TextBlock>("LblEngine"); LangCode = F<TextBlock>("LangCode");
            GameText = F<TextBlock>("GameText"); ModsText = F<TextBlock>("ModsText"); EngineText = F<TextBlock>("EngineText"); SteamText = F<TextBlock>("SteamText");
            StatusText = F<TextBlock>("StatusText"); DlgTitle = F<TextBlock>("DlgTitle"); DlgText = F<TextBlock>("DlgText"); ModsInfo = F<TextBlock>("ModsInfo");
            SearchPh = F<TextBlock>("SearchPh"); DiscoverHint = F<TextBlock>("DiscoverHint"); SheetTitle = F<TextBlock>("SheetTitle");
            GameDot = F<Ellipse>("GameDot"); ModsDot = F<Ellipse>("ModsDot"); EngineDot = F<Ellipse>("EngineDot"); SteamDot = F<Ellipse>("SteamDot");
            MainBtn = F<Button>("MainBtn"); UpdateBtn = F<Button>("UpdateBtn"); BrowseBtn = F<Button>("BrowseBtn"); ManageBtn = F<Button>("ManageBtn");
            LangBtn = F<Button>("LangBtn"); MinBtn = F<Button>("MinBtn"); CloseBtn = F<Button>("CloseBtn"); DlgOk = F<Button>("DlgOk"); DlgCancel = F<Button>("DlgCancel");
            ImportZipBtn = F<Button>("ImportZipBtn"); ImportDirBtn = F<Button>("ImportDirBtn"); CheckUpdatesBtn = F<Button>("CheckUpdatesBtn"); SearchBtn = F<Button>("SearchBtn"); SheetClose = F<Button>("SheetClose");
            Progress = F<ProgressBar>("Progress");
            NavPlay = F<RadioButton>("NavPlay"); NavMods = F<RadioButton>("NavMods");
            SubCatalog = F<RadioButton>("SubCatalog"); SubInstalled = F<RadioButton>("SubInstalled"); SubDiscover = F<RadioButton>("SubDiscover");
            PanelTabs = F<UniformGrid>("PanelTabs"); SheetTabs = F<UniformGrid>("SheetTabs");
            CatalogList = F<StackPanel>("CatalogList"); InstalledList = F<StackPanel>("InstalledList"); DiscoverList = F<StackPanel>("DiscoverList"); LangList = F<StackPanel>("LangList");
            CatalogScroll = F<ScrollViewer>("CatalogScroll"); InstalledScroll = F<ScrollViewer>("InstalledScroll"); DiscoverPage = F<DockPanel>("DiscoverPage");
            SearchBox = F<TextBox>("SearchBox"); LangPopup = F<Popup>("LangPopup");

            state = State.Load();
            L.Set(state.Get("language")); // rien d'enregistré : langue de Windows
            store = ModStore.Load();
            catalog = Catalog.LoadLocal().WithUserMods();
            InitChrome();
            InitFont();
            InitBackground();
            FitToScreen();
            AddGlass(PanelInner);
            AddGlass(ModsInner);
            Title.Text = Spaced("ELDEN RING", " ");
            Subtitle.Text = Spaced("SEAMLESS CO-OP", " ");
            Caption.Text = "Seamless Co-op Launcher · v" + Program.Version;
            Wire();

            gameDir = gameOverride ?? state.Get("game_dir");
            if (!Game.IsValid(gameDir))
            {
                gameDir = Game.Detect();
                if (gameDir != null) { state.Set("game_dir", gameDir); state.Save(); }
            }
            gameVersion = Game.Version(gameDir);
            ApplyLanguage();
            RefreshStatus();

            Window.Loaded += async (s, e) =>
            {
                LayoutGlass();
                await Startup();
            };
        }

        string Engine { get { return state.Get("engine") == "classic" ? "classic" : "me3"; } }

        // ───────────────────────── fenêtre ─────────────────────────

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        void InitChrome()
        {
            WindowChrome.SetWindowChrome(Window, new WindowChrome
            {
                CaptionHeight = 40,
                ResizeBorderThickness = new Thickness(0),
                GlassFrameThickness = new Thickness(0, 0, 0, 1),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false
            });
            foreach (var b in new UIElement[] { LangBtn, MinBtn, CloseBtn, NavPlay, NavMods })
                WindowChrome.SetIsHitTestVisibleInChrome(b, true);
            Window.SourceInitialized += (s, e) =>
            {
                var hwnd = new WindowInteropHelper(Window).Handle;
                int round = 2, dark = 1; // coins arrondis Windows 11 + bordure sombre
                DwmSetWindowAttribute(hwnd, 33, ref round, 4);
                DwmSetWindowAttribute(hwnd, 20, ref dark, 4);
            };
        }

        /// <summary>La fenêtre est dessinée en 1280×720 ; sur un petit écran, le tout est réduit proportionnellement.</summary>
        void FitToScreen()
        {
            var wa = SystemParameters.WorkArea;
            var k = Math.Min(1.0, Math.Min((wa.Width - 24) / 1280.0, (wa.Height - 24) / 720.0));
            Window.Width = Math.Floor(1280 * k);
            Window.Height = Math.Floor(720 * k);
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
        void AddGlass(Grid inner)
        {
            var canvas = new Canvas { ClipToBounds = true };
            var img = new Image { Stretch = Stretch.UniformToFill, Source = Bg.Source, Effect = new BlurEffect { Radius = 26, RenderingBias = RenderingBias.Performance } };
            canvas.Children.Add(img);
            inner.Children.Insert(0, new Border { Background = Br("#B80E0D0B") });
            inner.Children.Insert(0, canvas);
            glass.Add(new KeyValuePair<Grid, Image>(inner, img));
        }

        void LayoutGlass()
        {
            foreach (var g in glass)
            {
                if (!g.Key.IsVisible || g.Key.ActualWidth == 0) continue;
                var p = g.Key.TranslatePoint(new Point(0, 0), Root);
                g.Value.Width = Root.ActualWidth;
                g.Value.Height = Root.ActualHeight;
                Canvas.SetLeft(g.Value, -p.X);
                Canvas.SetTop(g.Value, -p.Y);
                g.Key.Clip = new RectangleGeometry(new Rect(0, 0, g.Key.ActualWidth, g.Key.ActualHeight), 9, 9);
            }
        }

        void Wire()
        {
            MinBtn.Click += (s, e) => Window.WindowState = WindowState.Minimized;
            CloseBtn.Click += (s, e) => Window.Close();
            LangBtn.Click += (s, e) => OpenLanguageMenu();
            NavPlay.Checked += (s, e) => ShowPage(false);
            NavMods.Checked += (s, e) => ShowPage(true);
            ManageBtn.Click += (s, e) => NavMods.IsChecked = true;

            RoutedEventHandler sub = (s, e) =>
            {
                CatalogScroll.Visibility = SubCatalog.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                InstalledScroll.Visibility = SubInstalled.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                DiscoverPage.Visibility = SubDiscover.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                if (SubDiscover.IsChecked == true && !discoverLoaded) { discoverLoaded = true; var t = Discover(""); }
            };
            SubCatalog.Checked += sub;
            SubInstalled.Checked += sub;
            SubDiscover.Checked += sub;

            BrowseBtn.Click += (s, e) => Browse();
            MainBtn.Click += async (s, e) => await OnMain();
            UpdateBtn.Click += async (s, e) => await ApplyUpdates();
            ImportZipBtn.Click += async (s, e) => await ImportZip();
            ImportDirBtn.Click += async (s, e) => await ImportFolder();
            CheckUpdatesBtn.Click += async (s, e) => await CheckForUpdates();
            SearchBtn.Click += async (s, e) => await Discover(SearchBox.Text);
            SearchBox.TextChanged += (s, e) => SearchPh.Visibility = SearchBox.Text.Length == 0 ? Visibility.Visible : Visibility.Hidden;
            SearchBox.KeyDown += async (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; await Discover(SearchBox.Text); } };
            SheetClose.Click += (s, e) => CloseSheet();

            DlgOk.Click += (s, e) => CloseDialog(true);
            DlgCancel.Click += (s, e) => CloseDialog(false);
            Window.PreviewKeyDown += (s, e) =>
            {
                if (Overlay.Visibility == Visibility.Visible)
                {
                    if (e.Key == Key.Escape) { CloseDialog(false); e.Handled = true; }
                    else if (e.Key == Key.Enter) { CloseDialog(true); e.Handled = true; }
                }
                else if (Sheet.Visibility == Visibility.Visible && e.Key == Key.Escape) { CloseSheet(); e.Handled = true; }
            };
            Window.Activated += (s, e) => { if (!busy) RefreshStatus(); };
            Window.Closing += (s, e) =>
            {
                try
                {
                    SaveSeamless();
                    if (sheetForm != null) ModSettings.Save(sheetForm.ModId, sheetMod, sheetForm.Schema, sheetForm.Values);
                }
                catch (Exception ex) { Log.Write("save on close: " + ex.Message); }
                if (downloads != null) downloads.Dispose();
            };
        }

        void ShowPage(bool mods)
        {
            PlayPage.Visibility = mods ? Visibility.Collapsed : Visibility.Visible;
            PlayShade.Visibility = mods ? Visibility.Collapsed : Visibility.Visible;
            ModsPage.Visibility = mods ? Visibility.Visible : Visibility.Collapsed;
            ModsShade.Visibility = mods ? Visibility.Visible : Visibility.Collapsed;
            if (mods) RebuildModLists();
            Window.Dispatcher.BeginInvoke(new Action(LayoutGlass), DispatcherPriority.Loaded);
        }

        // ───────────────────────── démarrage ─────────────────────────

        async Task Startup()
        {
            // lancés tout de suite : ils avancent pendant les éventuelles fenêtres de patch et de nettoyage
            var remoteCheck = Catalog.FetchRemote();
            var me3Check = Me3.Latest();
            await CheckGameVersion();
            await OfferLegacyCleanup();

            var remote = await remoteCheck;
            if (remote != null && Catalog.Newest(catalog, remote) == remote && remote.Updated != catalog.Updated)
            {
                SaveSeamless();
                catalog = remote.WithUserMods();
                BuildSeamlessPanel();
                RebuildModLists();
            }
            var updatesCheck = Installer.FindUpdates(catalog, store);
            var nexusCheck = Nexus.LatestVersions(catalog.Mods.Select(c => c.Nexus));
            await EnforceConflicts(null);

            try { me3Latest = await me3Check; }
            catch (Exception ex) { Log.Write("me3 check: " + ex.Message); }
            pendingUpdates = await updatesCheck;
            try { nexusVersions = await nexusCheck; }
            catch (Exception ex) { Log.Write("nexus versions: " + ex.Message); }
            if (!busy) { RebuildModLists(); RefreshStatus(); }
            if (state.GetBool("auto_update", true) && UpdateCount() > 0 && !Game.IsRunning()) await ApplyUpdates();
            await NotifyNexusAhead();
        }

        List<KeyValuePair<InstalledMod, string>> NexusAhead()
        {
            return Installer.NexusAhead(catalog, store, nexusVersions);
        }

        string NexusNewer(InstalledMod m)
        {
            return NexusAhead().Where(a => a.Key == m).Select(a => a.Value).FirstOrDefault();
        }

        static string Ver(string v)
        {
            return string.IsNullOrEmpty(v) || !char.IsDigit(v[0]) ? v : "v" + v;
        }

        /// <summary>
        /// Une mise à jour est sortie sur Nexus : à télécharger soi-même (mod Nexus), ou pas encore sur GitHub d'où le launcher
        /// l'installe. Une fois par version, sauf si force (vérification demandée par le joueur).
        /// </summary>
        async Task NotifyNexusAhead(bool force = false)
        {
            foreach (var a in NexusAhead())
            {
                var key = "nexus_notified_" + a.Key.Id;
                if ((!force && state.Get(key) == a.Value) || pendingUpdates.Any(u => u.Key == a.Key)) continue;
                state.Set(key, a.Value);
                state.Save();
                var cm = catalog.Find(a.Key.Id);
                var text = cm.Source.IsGitHub ? "nexus_ahead_text" : "nexus_only_text";
                if (await Ask(L.F("nexus_ahead_title", a.Key.Name), L.F(text, a.Key.Name, Ver(a.Value), Ver(a.Key.Version)), L.T("nexus_ahead_open"), L.T("later")))
                    OpenUrl(cm.NexusPage);
            }
        }

        /// <summary>Infobulle d'une version plus récente sur Nexus : elle arrivera par GitHub, ou il faut la télécharger.</summary>
        static string NexusTip(CatalogMod cm)
        {
            return L.T(cm != null && cm.Source.IsGitHub ? "nexus_newer_tip" : "nexus_only_tip");
        }

        bool Me3Outdated()
        {
            return Me3.IsInstalled && me3Latest != null && Me3.InstalledVersion() != me3Latest.Tag;
        }

        int UpdateCount()
        {
            return pendingUpdates.Count + (Me3Outdated() ? 1 : 0);
        }

        /// <summary>Après un patch du jeu, les mods non vérifiés pour la nouvelle version sont désactivés (anti-plantage).</summary>
        async Task CheckGameVersion()
        {
            if (gameVersion == null) return;
            var last = state.Get("game_version");
            if (last != null && last != gameVersion)
            {
                var off = store.Mods.Where(m => m.Enabled && !Compatible(m)).ToList();
                foreach (var m in off) m.Enabled = false;
                if (off.Count > 0)
                {
                    store.Save();
                    RefreshStatus();
                    await Alert(L.F("patch_title", gameVersion), L.F("patch_text", last, gameVersion, string.Join(", ", off.Select(m => m.Name))));
                }
            }
            state.Set("game_version", gameVersion);
            state.Save();
        }

        /// <summary>Le launcher v1.0 avait installé Seamless dans le dossier du jeu : proposé au nettoyage une fois Seamless migré.</summary>
        async Task OfferLegacyCleanup()
        {
            var s = store.Get(Seamless.Id);
            if (Engine != "me3" || s == null || !Seamless.LegacyPresent(gameDir, s) || state.GetBool("legacy_declined", false) || Game.IsRunning()) return;
            if (!await Ask(L.T("legacy_title"), L.T("legacy_text"), L.T("legacy_ok"), L.T("legacy_keep")))
            {
                state.SetBool("legacy_declined", true);
                state.Save();
                return;
            }
            if (!await EnsureWritable()) return;
            string err = null;
            try
            {
                Seamless.RemoveLegacy(gameDir);
                Status(L.T("legacy_done"), double.NaN);
            }
            catch (Exception ex) { err = Friendly(ex); }
            if (err != null) await Alert(L.T("legacy_title"), err);
        }

        // ───────────────────────── langue ─────────────────────────

        void ApplyLanguage()
        {
            LangCode.Text = L.Code.ToUpperInvariant();
            LangBtn.ToolTip = L.T("lang_tip");
            Tagline.Text = L.T("tagline");
            LblGame.Text = L.T("lbl_game");
            LblMods.Text = L.T("lbl_mods");
            LblEngine.Text = L.T("lbl_engine");
            NavPlay.Content = L.T("nav_play");
            NavMods.Content = L.T("nav_mods");
            ManageBtn.Content = L.T("manage");
            ImportZipBtn.Content = L.T("import_zip");
            ImportDirBtn.Content = L.T("import_dir");
            CheckUpdatesBtn.Content = L.T("btn_check_updates");
            SearchBtn.Content = L.T("search_btn");
            SearchPh.Text = L.T("search_placeholder");
            DiscoverHint.Text = L.T("discover_hint");
            SheetClose.Content = L.T("close");
            EngineText.ToolTip = L.T("engine_tip");
            StatusText.Text = "";
            BuildSeamlessPanel();
            RebuildModLists();
            if (sheetForm != null)
            {
                SheetTitle.Text = L.F("settings_of", sheetMod.Name);
                BuildForm(sheetForm, SheetTabs, SheetPages, null);
            }
            if (discoverResults != null) ShowDiscover(); // mêmes résultats, nouvelle langue : pas de nouvelle requête
            else if (discoverLoaded) { var t = Discover(SearchBox.Text); }
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
                var item = new Button { Style = St("MenuBtn"), Content = row };
                item.Click += (s, e) =>
                {
                    LangPopup.IsOpen = false;
                    if (code == L.Code) return;
                    SaveSeamless();
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

        // ───────────────────────── réglages (générés depuis le catalogue) ─────────────────────────

        void BuildSeamlessPanel()
        {
            var cm = catalog.Find(Seamless.Id);
            PanelTabs.Children.Clear();
            PanelPages.Children.Clear();
            passwordBox = null;
            if (cm == null || cm.Settings == null) { seamlessForm = null; return; }
            seamlessForm = new Form
            {
                ModId = Seamless.Id,
                Schema = cm.Settings,
                Values = ModSettings.Load(Seamless.Id, store.Get(Seamless.Id), cm.Settings, Seamless.LegacyIni(gameDir), Paths.LegacySettingsCopy)
            };
            BuildForm(seamlessForm, PanelTabs, PanelPages, AppendLauncherOptions);
        }

        void SaveSeamless()
        {
            if (seamlessForm == null) return;
            try { ModSettings.Save(Seamless.Id, store.Get(Seamless.Id), seamlessForm.Schema, seamlessForm.Values); }
            catch (Exception ex) { Log.Write("save seamless: " + ex.Message); }
        }

        string SeamlessExt
        {
            get
            {
                if (seamlessForm == null) return "co2";
                var it = seamlessForm.Schema.Items.FirstOrDefault(i => i.Key == "save_file_extension");
                return it == null ? "co2" : ModSettings.Sanitize(it, seamlessForm.Get("save_file_extension"));
            }
        }

        /// <summary>Un onglet par onglet du schéma ; extraLast complète le dernier (options du launcher pour Seamless).</summary>
        void BuildForm(Form f, UniformGrid tabsHost, Grid pagesHost, Action<StackPanel> extraLast)
        {
            tabsHost.Children.Clear();
            pagesHost.Children.Clear();
            f.Loaders.Clear();
            var group = "tabs_" + f.ModId + "_" + tabsHost.GetHashCode();
            var pages = new List<StackPanel>();
            for (int i = 0; i < f.Schema.Tabs.Count; i++)
            {
                var tab = f.Schema.Tabs[i];
                var page = new StackPanel { Visibility = i == 0 ? Visibility.Visible : Visibility.Collapsed };
                foreach (var it in tab.Items) AddItem(f, page, it);
                if (tab.Reset)
                    page.Children.Add(ActionButton(L.T("reset_defaults"), new Thickness(0, 6, 0, 0), () =>
                    {
                        foreach (var it in tab.Items.Where(x => !string.IsNullOrEmpty(x.Key))) f.Values[it.Key] = it.Default ?? "";
                        f.Reload();
                    }));
                if (i == f.Schema.Tabs.Count - 1 && extraLast != null) extraLast(page);
                pages.Add(page);
                pagesHost.Children.Add(page);
                var rb = new RadioButton { Style = St("Tab"), GroupName = group, Content = Json.Loc(tab.Title), IsChecked = i == 0, Padding = new Thickness(0, 0, 0, 3) };
                rb.Checked += (s, e) => { foreach (var p in pages) p.Visibility = p == page ? Visibility.Visible : Visibility.Collapsed; };
                tabsHost.Children.Add(rb);
            }
            tabsHost.Columns = Math.Max(1, f.Schema.Tabs.Count);
            f.Reload();
        }

        void AddItem(Form f, Panel host, SettingItem it)
        {
            switch (it.Type)
            {
                case "note": host.Children.Add(Hint(Json.Loc(it.Label), new Thickness(0, 0, 0, 16))); break;
                case "toggle": AddToggle(f, host, it); break;
                case "slider": AddSlider(f, host, it); break;
                case "choice": AddChoice(f, host, it); break;
                default:
                    var tb = AddText(f, host, it);
                    if (f.ModId == Seamless.Id && it.Key == "cooppassword") passwordBox = tb;
                    break;
            }
        }

        /// <summary>Partie « LAUNCHER » ajoutée à l'onglet Avancé de Seamless.</summary>
        void AppendLauncherOptions(StackPanel page)
        {
            page.Children.Add(new Rectangle { Height = 1, Fill = Br("#33D4B26A"), Margin = new Thickness(0, 4, 0, 12) });
            page.Children.Add(new TextBlock { Text = L.T("section_launcher"), FontFamily = (FontFamily)Window.Resources["Serif"], FontWeight = FontWeights.Bold, FontSize = 12, Foreground = B("GoldDim"), Margin = new Thickness(0, 0, 0, 10) });
            AddStateChoice(page, "engine", L.T("engine"), new[] { L.T("engine_me3"), L.T("engine_classic") }, new[] { "me3", "classic" }, "me3", RefreshStatus);
            AddStateToggle(page, "auto_update", true, L.T("auto_update"), L.T("auto_update_hint"));
            AddStateToggle(page, "close_on_launch", true, L.T("close_on_launch"), null);
            AddStateToggle(page, "backup_on_launch", true, L.T("backup"), L.T("backup_hint"));
            page.Children.Add(ActionButton(L.T("btn_import"), new Thickness(0, 4, 0, 8), async () => await ImportSave()));
            var grid = new UniformGrid { Columns = 2 };
            grid.Children.Add(ActionButton(L.T("btn_game_folder"), new Thickness(0, 0, 4, 0), () => OpenFolder(gameDir)));
            grid.Children.Add(ActionButton(L.T("btn_saves_folder"), new Thickness(4, 0, 0, 0), () => OpenFolder(Paths.SavesRoot)));
            page.Children.Add(grid);
        }

        TextBlock Label(string text)
        {
            return new TextBlock { Text = text, FontSize = 13.5, Foreground = B("Text"), TextWrapping = TextWrapping.Wrap };
        }

        TextBlock Hint(string text, Thickness margin)
        {
            return new TextBlock { Text = text, FontSize = 11.5, Foreground = B("Muted"), TextWrapping = TextWrapping.Wrap, Margin = margin };
        }

        Button ActionButton(string text, Thickness margin, Action click)
        {
            var b = new Button { Style = St("Action"), Content = text, Margin = margin };
            b.Click += (s, e) => click();
            return b;
        }

        TextBox AddText(Form f, Panel host, SettingItem it)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
            sp.Children.Add(Label(Json.Loc(it.Label) ?? it.Key));
            var g = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            var tb = new TextBox { Style = St("Input"), MaxLength = Math.Max(1, it.MaxLength) };
            var ph = new TextBlock
            {
                Text = Json.Loc(it.Placeholder) ?? "", FontSize = 14, Foreground = B("Muted"), Opacity = 0.7, IsHitTestVisible = false,
                Margin = new Thickness(13, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center
            };
            g.Children.Add(tb);
            g.Children.Add(ph);
            sp.Children.Add(g);
            var hint = Json.Loc(it.Hint);
            if (hint != null) sp.Children.Add(Hint(hint, new Thickness(0, 5, 0, 0)));
            tb.TextChanged += (s, e) =>
            {
                ph.Visibility = tb.Text.Length == 0 ? Visibility.Visible : Visibility.Hidden;
                f.Values[it.Key] = tb.Text;
            };
            f.Loaders.Add(() => tb.Text = f.Get(it.Key));
            host.Children.Add(sp);
            return tb;
        }

        CheckBox MakeToggle(string label, string hint)
        {
            var sp = new StackPanel();
            sp.Children.Add(Label(label));
            if (hint != null) sp.Children.Add(Hint(hint, new Thickness(0, 2, 0, 0)));
            return new CheckBox { Style = St("Toggle"), Content = sp, Margin = new Thickness(0, 0, 0, hint == null ? 13 : 11) };
        }

        static bool IsOn(string v)
        {
            return v == "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
        }

        void AddToggle(Form f, Panel host, SettingItem it)
        {
            var cb = MakeToggle(Json.Loc(it.Label) ?? it.Key, Json.Loc(it.Hint));
            RoutedEventHandler changed = (s, e) =>
            {
                var words = f.Get(it.Key).Length > 1; // true/false plutôt que 1/0
                f.Values[it.Key] = cb.IsChecked == true ? (words ? "true" : "1") : (words ? "false" : "0");
            };
            cb.Checked += changed;
            cb.Unchecked += changed;
            f.Loaders.Add(() => cb.IsChecked = IsOn(f.Get(it.Key)));
            host.Children.Add(cb);
        }

        void AddStateToggle(Panel host, string key, bool def, string label, string hint)
        {
            var cb = MakeToggle(label, hint);
            cb.IsChecked = state.GetBool(key, def);
            RoutedEventHandler changed = (s, e) => { state.SetBool(key, cb.IsChecked == true); state.Save(); RefreshStatus(); };
            cb.Checked += changed;
            cb.Unchecked += changed;
            host.Children.Add(cb);
        }

        StackPanel ChoiceRow(Panel host, string label, string hint, out TextBlock txt, out Button prev, out Button next)
        {
            var outer = new StackPanel { Margin = new Thickness(0, 1, 0, 14) };
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var lbl = Label(label);
            lbl.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(lbl);
            var sp = new StackPanel { Orientation = Orientation.Horizontal };
            Grid.SetColumn(sp, 1);
            prev = new Button { Style = St("Arrow"), Content = "‹" };
            txt = new TextBlock { Width = 112, TextAlignment = TextAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Foreground = B("Gold"), TextTrimming = TextTrimming.CharacterEllipsis };
            next = new Button { Style = St("Arrow"), Content = "›" };
            sp.Children.Add(prev);
            sp.Children.Add(txt);
            sp.Children.Add(next);
            g.Children.Add(sp);
            outer.Children.Add(g);
            if (hint != null) outer.Children.Add(Hint(hint, new Thickness(0, 4, 0, 0)));
            host.Children.Add(outer);
            return outer;
        }

        void AddChoice(Form f, Panel host, SettingItem it)
        {
            var items = (it.Choices as System.Collections.IEnumerable ?? new object[0]).Cast<object>().Select(o => Json.Loc(o) ?? "").ToArray();
            if (items.Length == 0) return;
            TextBlock txt; Button prev, next;
            ChoiceRow(host, Json.Loc(it.Label) ?? it.Key, Json.Loc(it.Hint), out txt, out prev, out next);
            bool named = it.Values.Length == items.Length; // le fichier attend un mot (« modded_switch ») plutôt qu'un index
            Func<int> current = () =>
            {
                int v;
                if (named) return Math.Max(0, Array.FindIndex(it.Values, x => x.Equals(f.Get(it.Key), StringComparison.OrdinalIgnoreCase)));
                return int.TryParse(f.Get(it.Key), out v) && v >= 0 && v < items.Length ? v : 0;
            };
            Action<int> select = v =>
            {
                v = (v + items.Length) % items.Length;
                f.Values[it.Key] = named ? it.Values[v] : v.ToString(CultureInfo.InvariantCulture);
                txt.Text = items[v];
            };
            prev.Click += (s, e) => select(current() - 1);
            next.Click += (s, e) => select(current() + 1);
            f.Loaders.Add(() => txt.Text = items[current()]);
        }

        void AddStateChoice(Panel host, string key, string label, string[] labels, string[] values, string def, Action changed)
        {
            TextBlock txt; Button prev, next;
            ChoiceRow(host, label, null, out txt, out prev, out next);
            Func<int> current = () => Math.Max(0, Array.IndexOf(values, state.Get(key) ?? def));
            Action<int> select = v =>
            {
                v = (v + values.Length) % values.Length;
                state.Set(key, values[v]);
                state.Save();
                txt.Text = labels[v];
                if (changed != null) changed();
            };
            prev.Click += (s, e) => select(current() - 1);
            next.Click += (s, e) => select(current() + 1);
            txt.Text = labels[current()];
        }

        void AddSlider(Form f, Panel host, SettingItem it)
        {
            var g = new Grid { Margin = new Thickness(0, 0, 0, 11) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var val = new TextBlock { Foreground = B("Gold"), FontSize = 13 };
            Grid.SetColumn(val, 1);
            int step = Math.Max(1, it.Step);
            var sl = new Slider
            {
                Style = St("GoldSlider"), Minimum = it.Min, Maximum = Math.Max(it.Min + step, it.Max),
                TickFrequency = step, SmallChange = step, LargeChange = step, Margin = new Thickness(0, 4, 0, 0)
            };
            Grid.SetRow(sl, 1);
            Grid.SetColumnSpan(sl, 2);
            var hint = Json.Loc(it.Hint);
            var head = new StackPanel();
            head.Children.Add(Label(Json.Loc(it.Label) ?? it.Key));
            if (hint != null) head.Children.Add(Hint(hint, new Thickness(0, 2, 0, 0)));
            g.Children.Add(head);
            g.Children.Add(val);
            g.Children.Add(sl);
            var format = Json.Loc(it.Format) ?? "{0}";
            var zero = Json.Loc(it.Zero);
            Func<int, string> show = v => v == 0 && zero != null ? zero : string.Format(format, v);
            sl.ValueChanged += (s, e) =>
            {
                int v = (int)Math.Round(sl.Value);
                val.Text = show(v);
                f.Values[it.Key] = v.ToString(CultureInfo.InvariantCulture);
            };
            sl.PreviewMouseWheel += (s, e) =>
            {
                sl.Value = Math.Max(sl.Minimum, Math.Min(sl.Maximum, sl.Value + (e.Delta > 0 ? step : -step)));
                e.Handled = true;
            };
            f.Loaders.Add(() =>
            {
                double d;
                if (!double.TryParse(f.Get(it.Key), NumberStyles.Float, CultureInfo.InvariantCulture, out d))
                    double.TryParse(it.Default, NumberStyles.Float, CultureInfo.InvariantCulture, out d);
                int v = (int)Math.Round(d);
                if (v > sl.Maximum) sl.Maximum = v; // valeur réglée à la main au-delà de la plage
                if (v < sl.Minimum) sl.Minimum = v;
                sl.Value = v;
                val.Text = show(v);
            });
            host.Children.Add(g);
        }

        // ───────────────────────── fenêtre de réglages d'un mod ─────────────────────────

        SettingsSchema SchemaFor(InstalledMod m)
        {
            var cm = catalog.Find(m.Id);
            if (cm != null && cm.Settings != null) return cm.Settings;
            var p = ModSettings.FilePath(m, m.SettingsFile);
            try { return p != null && File.Exists(p) ? SettingsSchema.FromIni(m.SettingsFile, File.ReadAllText(p)) : null; }
            catch { return null; }
        }

        void OpenSheet(InstalledMod m)
        {
            if (m.Id == Seamless.Id) { NavPlay.IsChecked = true; return; } // Seamless se règle sur la page Jouer
            var schema = SchemaFor(m);
            if (schema == null) return;
            sheetMod = m;
            sheetForm = new Form { ModId = m.Id, Schema = schema, Values = ModSettings.Load(m.Id, m, schema) };
            SheetTitle.Text = L.F("settings_of", m.Name);
            BuildForm(sheetForm, SheetTabs, SheetPages, null);
            Sheet.Visibility = Visibility.Visible;
        }

        void CloseSheet()
        {
            if (sheetForm != null)
            {
                try { ModSettings.Save(sheetForm.ModId, sheetMod, sheetForm.Schema, sheetForm.Values); }
                catch (Exception ex) { Log.Write("save settings: " + ex.Message); }
            }
            sheetForm = null;
            sheetMod = null;
            Sheet.Visibility = Visibility.Collapsed;
        }

        // ───────────────────────── page Mods ─────────────────────────

        bool Compatible(InstalledMod m)
        {
            if (gameVersion != null && m.TestedOn.Contains(gameVersion)) return true;
            var cm = catalog.Find(m.Id);
            return cm != null && cm.Supports(gameVersion);
        }

        void RebuildModLists()
        {
            if (ModsPage.Visibility != Visibility.Visible) return; // ShowPage reconstruit la page à son ouverture
            conflicts = Conflicts.Compute(store, catalog);
            var gv = gameVersion ?? "?";
            ModsInfo.Text = gameVersion != null ? L.F("mods_info", gameVersion) : "";
            SubCatalog.Content = L.T("sub_catalog");
            SubInstalled.Content = L.F("sub_installed", store.Mods.Count);
            SubDiscover.Content = L.T("sub_discover");

            CatalogList.Children.Clear();
            var ok = catalog.Mods.Where(cm => cm.Supports(gameVersion) || (store.Get(cm.Id) != null && Compatible(store.Get(cm.Id)))).ToList();
            var others = catalog.Mods.Except(ok).ToList();
            CatalogList.Children.Add(Section(L.F("cat_compatible", gv), null));
            if (ok.Count == 0) CatalogList.Children.Add(Hint(L.T("cat_empty"), new Thickness(0, 0, 0, 14)));
            foreach (var cm in ok) CatalogList.Children.Add(CatalogCard(cm));
            if (others.Count > 0)
            {
                CatalogList.Children.Add(Section(L.F("cat_unverified", gv), L.T("cat_unverified_hint")));
                foreach (var cm in others) CatalogList.Children.Add(CatalogCard(cm));
            }

            InstalledList.Children.Clear();
            InstalledList.Children.Add(Hint(store.Mods.Count == 0 ? L.T("installed_empty") : L.T("load_order_hint"), new Thickness(0, 0, 0, 12)));
            for (int i = 0; i < store.Mods.Count; i++) InstalledList.Children.Add(InstalledCard(store.Mods[i], i));
        }

        FrameworkElement Section(string title, string hint)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 4, 0, 10) };
            sp.Children.Add(new TextBlock { Text = title, FontFamily = (FontFamily)Window.Resources["Serif"], FontWeight = FontWeights.Bold, FontSize = 13, Foreground = B("Gold") });
            if (hint != null) sp.Children.Add(Hint(hint, new Thickness(0, 3, 0, 0)));
            return sp;
        }

        Border Card(UIElement content)
        {
            return new Border
            {
                Background = Br("#40000000"), BorderBrush = Br("#26D4B26A"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6), Padding = new Thickness(16, 12, 16, 12), Margin = new Thickness(0, 0, 0, 8), Child = content
            };
        }

        Border Badge(string text, string kind)
        {
            string bg = "#22FFFFFF", fg = "#A39C8C";
            if (kind == "ok") { bg = "#2A8BC48A"; fg = "#8BC48A"; }
            else if (kind == "warn") { bg = "#33D4B26A"; fg = "#D4B26A"; }
            else if (kind == "bad") { bg = "#33DE7A66"; fg = "#DE7A66"; }
            return new Border
            {
                CornerRadius = new CornerRadius(3), Padding = new Thickness(6, 1, 6, 2), Margin = new Thickness(8, 0, 0, 0), Background = Br(bg),
                VerticalAlignment = VerticalAlignment.Center, Child = new TextBlock { Text = text, FontSize = 11, Foreground = Br(fg) }
            };
        }

        WrapPanel Head(string name, params Border[] badges)
        {
            var w = new WrapPanel();
            w.Children.Add(new TextBlock { Text = name, FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = B("Text"), VerticalAlignment = VerticalAlignment.Center });
            foreach (var b in badges.Where(b => b != null)) w.Children.Add(b);
            return w;
        }

        string CategoryName(string c)
        {
            var k = "category_" + (c ?? "other");
            var t = L.T(k);
            return t == k ? L.T("category_other") : t;
        }

        /// <summary>Version plus récente publiée sur Nexus que celle installée.</summary>
        Border NexusBadge(string version, CatalogMod cm)
        {
            if (version == null) return null;
            var b = Badge(L.F("badge_nexus_newer", Ver(version)), "warn");
            b.ToolTip = new TextBlock { Text = NexusTip(cm), TextWrapping = TextWrapping.Wrap, MaxWidth = 420 };
            return b;
        }

        Border CompatBadge(CatalogMod cm, InstalledMod m)
        {
            if (cm != null && cm.Supports(gameVersion)) return Badge(L.F("badge_compatible", gameVersion), "ok");
            if (m != null && gameVersion != null && m.TestedOn.Contains(gameVersion)) return Badge(L.T("badge_tested"), "ok");
            return Badge(L.T("badge_unverified"), "warn");
        }

        // ───────────────────────── incompatibilités ─────────────────────────

        string ModName(string id)
        {
            var m = store.Get(id);
            if (m != null) return m.Name;
            var cm = catalog.Find(id);
            return cm != null ? cm.Name : id;
        }

        string ConflictReason(ModConflict c)
        {
            if (c.Kind == "files") return L.F("conflict_files", FileList(c.Files));
            if (c.Kind == "deploy") return L.F("conflict_deploy", FileList(c.Files));
            return Json.Loc(c.Reason) ?? L.T("conflict_declared");
        }

        static string FileList(List<string> files)
        {
            return string.Join(", ", files.Take(3)) + (files.Count > 3 ? " (+" + (files.Count - 3) + ")" : "");
        }

        string ConflictNames(IEnumerable<string> ids)
        {
            return string.Join(", ", ids.Select(ModName));
        }

        /// <summary>« Autre mod : raison », une ligne par incompatibilité.</summary>
        string ConflictDetails(IEnumerable<ModConflict> list)
        {
            return string.Join("\n", list.Select(c => L.F("conflict_line", ModName(c.With), ConflictReason(c))));
        }

        Border ConflictBadge(string text, string kind, string tip)
        {
            var b = Badge(text, kind);
            b.ToolTip = new TextBlock { Text = tip, TextWrapping = TextWrapping.Wrap, MaxWidth = 420 };
            return b;
        }

        /// <summary>Après un ajout, une mise à jour ou au démarrage : désactive les mods incompatibles avec un mod chargé avant eux et l'explique.</summary>
        async Task EnforceConflicts(string addedId)
        {
            var off = Conflicts.Resolve(store, catalog);
            if (off.Count == 0) return;
            RebuildModLists();
            RefreshStatus();
            var added = off.FirstOrDefault(o => o.Key.Id.Equals(addedId ?? "", StringComparison.OrdinalIgnoreCase));
            if (added.Key != null && off.Count == 1)
                await Alert(L.T("conflict_title"), L.F("install_blocked_text", added.Key.Name, ModName(added.Value.With), ConflictReason(added.Value)));
            else
                await Alert(L.T("conflict_title"), ResolvedText(off));
        }

        string ResolvedText(List<KeyValuePair<InstalledMod, ModConflict>> off)
        {
            return L.F("resolved_text", string.Join("\n", off.Select(o => "• " + L.F("resolved_line", o.Key.Name, ModName(o.Value.With), ConflictReason(o.Value)))));
        }

        Grid TwoColumns(UIElement left, UIElement right)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.Children.Add(left);
            if (right != null) { Grid.SetColumn(right, 1); g.Children.Add(right); }
            return g;
        }

        FrameworkElement CatalogCard(CatalogMod cm)
        {
            var m = store.Get(cm.Id);
            var info = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
            var gameplay = cm.Gameplay ? Badge(L.T("badge_gameplay"), "muted") : null;
            if (gameplay != null) gameplay.ToolTip = L.T("gameplay_tip");
            // incompatibilités déclarées : en rouge si l'autre mod est activé (celui-ci s'installera alors désactivé)
            var declared = Conflicts.DeclaredFor(catalog, cm);
            var active = declared.Where(c => { var o = store.Get(c.Id); return o != null && o.Enabled && o != m; }).ToList();
            var tip = string.Join("\n", declared.Select(c => L.F("conflict_line", ModName(c.Id), Json.Loc(c.Reason) ?? L.T("conflict_declared"))));
            Border incompatible = null;
            if (m == null && active.Count > 0) incompatible = ConflictBadge(L.F("badge_incompatible", ConflictNames(active.Select(c => c.Id))), "bad", tip);
            else if (declared.Count > 0) incompatible = ConflictBadge(L.F("badge_incompatible", ConflictNames(declared.Select(c => c.Id))), "muted", tip);
            var newer = m == null ? null : NexusNewer(m);
            var kind = cm.User ? Badge(L.T("badge_my_catalog"), "muted") : Badge(CategoryName(cm.Category), "muted");
            info.Children.Add(Head(cm.Name, kind, CompatBadge(cm, m), gameplay, incompatible, NexusBadge(newer, cm)));
            var sub = L.F("by_author", cm.Author) + (cm.Version != null ? " · " + cm.Version : "") + " · " + L.T(cm.Source.IsGitHub ? "source_github" : "source_nexus");
            info.Children.Add(new TextBlock { Text = sub, FontSize = 12, Foreground = B("Muted"), Margin = new Thickness(0, 2, 0, 0) });
            var desc = Json.Loc(cm.Description);
            if (desc != null) info.Children.Add(new TextBlock { Text = desc, FontSize = 12.5, Foreground = B("Text"), Opacity = 0.85, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) });
            if (m == null && active.Count > 0)
                info.Children.Add(new TextBlock { Text = L.F("catalog_will_be_disabled", ConflictNames(active.Select(c => c.Id))), FontSize = 12, Foreground = B("Bad"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 5, 0, 0) });

            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (m != null)
            {
                if (pendingUpdates.Any(u => u.Key == m))
                    actions.Children.Add(ActionButton(L.T("btn_update_mod"), new Thickness(0, 0, 8, 0), async () => await ApplyUpdates()));
                actions.Children.Add(Badge(m.Enabled ? L.T("badge_installed") : L.T("badge_disabled"), m.Enabled ? "ok" : "muted"));
            }
            else if (cm.Source.IsGitHub)
            {
                var b = new Button { Style = St("Ghost"), Content = L.T("btn_install_mod"), Height = 34, Padding = new Thickness(18, 0, 18, 0), FontSize = 13 };
                b.Click += async (s, e) => await InstallMod(cm);
                actions.Children.Add(b);
            }
            if (cm.NexusPage != null && (m == null || !cm.Source.IsGitHub || newer != null))
            {
                var nb = ActionButton(L.T(cm.Nexus > 0 ? "btn_nexus_page" : "btn_mod_page"), new Thickness(8, 0, 0, 0), () => OpenUrl(cm.NexusPage));
                if (!cm.Source.IsGitHub && m == null) nb.ToolTip = L.T("nexus_manual_tip");
                actions.Children.Add(nb);
            }
            if (cm.User) actions.Children.Add(ActionButton(L.T("btn_remove_my_catalog"), new Thickness(8, 0, 0, 0), () => RemoveFromMyCatalog(cm)));
            return Card(TwoColumns(info, actions));
        }

        FrameworkElement InstalledCard(InstalledMod m, int index)
        {
            var cm = catalog.Find(m.Id);
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            List<ModConflict> partners;
            if (!conflicts.TryGetValue(m.Id, out partners)) partners = new List<ModConflict>();
            var blockers = m.Enabled ? new List<ModConflict>() : Conflicts.Blockers(conflicts, store, m);

            var cb = new CheckBox { Style = St("Toggle"), IsChecked = m.Enabled, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 4, 0) };
            cb.Checked += async (s, e) => await SetEnabled(m, true, cb);
            cb.Unchecked += async (s, e) => await SetEnabled(m, false, cb);
            if (blockers.Count > 0) // un mod incompatible est activé : interrupteur verrouillé
            {
                cb.IsEnabled = false;
                cb.Opacity = 0.45;
                ToolTipService.SetShowOnDisabled(cb, true);
                cb.ToolTip = L.F("blocked_tip", ConflictNames(blockers.Select(c => c.With)));
            }
            g.Children.Add(cb);

            var order = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 16, 0) };
            var chevrons = new FontFamily("Segoe MDL2 Assets");
            var up = new Button { Style = St("Arrow"), Content = "", FontFamily = chevrons, Width = 22, Height = 22, FontSize = 9, Padding = new Thickness(0), IsEnabled = index > 0, ToolTip = L.T("move_up"), Margin = new Thickness(0, 0, 0, 4) };
            var down = new Button { Style = St("Arrow"), Content = "", FontFamily = chevrons, Width = 22, Height = 22, FontSize = 9, IsEnabled = index < store.Mods.Count - 1, ToolTip = L.T("move_down") };
            up.Click += (s, e) => MoveMod(m, -1);
            down.Click += (s, e) => MoveMod(m, 1);
            order.Children.Add(up);
            order.Children.Add(down);
            Grid.SetColumn(order, 1);
            g.Children.Add(order);

            var info = new StackPanel { Margin = new Thickness(0, 0, 16, 0), Opacity = m.Enabled ? 1 : 0.6 };
            var src = m.Source == "github" ? "source_github" : m.Source == "nexus" ? "source_nexus" : "source_manual";
            Border incompatible = null;
            if (blockers.Count > 0) incompatible = ConflictBadge(L.F("badge_blocked_by", ConflictNames(blockers.Select(c => c.With))), "bad", ConflictDetails(blockers));
            else if (partners.Count > 0) incompatible = ConflictBadge(L.F("badge_incompatible", ConflictNames(partners.Select(c => c.With))), "muted", ConflictDetails(partners));
            var newer = NexusNewer(m);
            info.Children.Add(Head(m.Name, m.Version.Length > 0 ? Badge(m.Version, "muted") : null, CompatBadge(cm, m), Badge(L.T(src), "muted"), incompatible, NexusBadge(newer, cm)));
            var details = (m.Author.Length > 0 ? L.F("by_author", m.Author) + " · " : "") + Contents(m);
            info.Children.Add(new TextBlock { Text = details, FontSize = 12, Foreground = B("Muted"), Margin = new Thickness(0, 3, 0, 0), TextWrapping = TextWrapping.Wrap });
            if (blockers.Count > 0)
                info.Children.Add(new TextBlock { Text = ConflictDetails(blockers), FontSize = 12, Foreground = B("Bad"), Margin = new Thickness(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap });
            Grid.SetColumn(info, 2);
            g.Children.Add(info);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (newer != null && cm != null)
            {
                var nb = ActionButton(L.T("btn_nexus_page"), new Thickness(0, 0, 8, 0), () => OpenUrl(cm.NexusPage));
                nb.ToolTip = NexusTip(cm);
                actions.Children.Add(nb);
            }
            if (m.Id == Seamless.Id || SchemaFor(m) != null)
                actions.Children.Add(ActionButton(L.T("btn_settings"), new Thickness(0, 0, 8, 0), () => OpenSheet(m)));
            if (!Compatible(m) && gameVersion != null)
                actions.Children.Add(ActionButton(L.T("btn_mark_tested"), new Thickness(0, 0, 8, 0), () => MarkTested(m)));
            // seulement si le launcher sait le retélécharger : un mod importé depuis Nexus pourrait revenir à une version GitHub plus ancienne
            if (cm != null && cm.Source.IsGitHub && m.Source == "github")
                actions.Children.Add(ActionButton(L.T("btn_reinstall_mod"), new Thickness(0, 0, 8, 0), async () => await ReinstallMod(m, cm)));
            actions.Children.Add(ActionButton(L.T("btn_uninstall_mod"), new Thickness(0), async () => await UninstallMod(m)));
            Grid.SetColumn(actions, 3);
            g.Children.Add(actions);
            return Card(g);
        }

        /// <summary>« 2 DLL · 1 pack · fichiers copiés dans le jeu ».</summary>
        static string Contents(InstalledMod m)
        {
            var parts = new List<string>();
            if (m.Natives.Count > 0) parts.Add(L.F("n_dlls", m.Natives.Count));
            if (m.Packages.Count > 0) parts.Add(L.F("n_packages", m.Packages.Count));
            if (m.Deploy.Count > 0) parts.Add(L.T("has_deploy"));
            return string.Join(" · ", parts);
        }

        /// <summary>Remet l'interrupteur sur value sans relancer SetEnabled.</summary>
        void Revert(CheckBox cb, bool value)
        {
            suppressToggle = true;
            cb.IsChecked = value;
            suppressToggle = false;
        }

        async Task SetEnabled(InstalledMod m, bool enable, CheckBox cb)
        {
            if (suppressToggle || m.Enabled == enable) return;
            if (busy) { Revert(cb, m.Enabled); return; }
            // conflicts a été calculé en même temps que cette carte : la liste des mods installés n'a pas changé depuis
            var blockers = enable ? Conflicts.Blockers(conflicts, store, m) : new List<ModConflict>();
            if (blockers.Count > 0)
            {
                Revert(cb, false);
                await Alert(L.T("conflict_title"), L.F("enable_blocked_text", m.Name, ConflictNames(blockers.Select(c => c.With)), ConflictDetails(blockers)));
                return;
            }
            if (enable && !Compatible(m) &&
                !await Ask(L.T("enable_unverified_title"), L.F("enable_unverified_text", m.Name, gameVersion ?? "?"), L.T("enable_anyway"), L.T("cancel")))
            {
                Revert(cb, false);
                return;
            }
            m.Enabled = enable;
            store.Save();
            RebuildModLists();
            RefreshStatus();
        }

        void MoveMod(InstalledMod m, int delta)
        {
            if (busy || !store.Move(m.Id, delta)) return;
            store.Save();
            RebuildModLists();
        }

        void MarkTested(InstalledMod m)
        {
            if (gameVersion == null || m.TestedOn.Contains(gameVersion)) return;
            m.TestedOn.Add(gameVersion);
            store.Save();
            Status(L.F("mark_tested_done", m.Name, gameVersion), double.NaN);
            RebuildModLists();
            RefreshStatus();
        }

        /// <summary>Vrai, après l'avoir expliqué, si le jeu tourne : on ne touche pas aux mods pendant une partie.</summary>
        async Task<bool> GameBlocksInstall()
        {
            if (!Game.IsRunning()) return false;
            await Alert(L.T("game_running_title"), L.T("game_running_install"));
            return true;
        }

        /// <summary>Affiche label avec la barre à 0 % et renvoie le suivi de progression du téléchargement (« label  42 % »).</summary>
        Action<int> Downloading(string label)
        {
            Status(label, 0);
            return p => Status(label + "  " + p + " %", p);
        }

        async Task InstallMod(CatalogMod cm)
        {
            if (await GameBlocksInstall()) return;
            var ok = await Run(L.T("install_failed_title"), async () =>
            {
                await Installer.InstallCatalogMod(cm, store, Downloading(L.F("installing_mod", cm.Name)));
                pendingUpdates.RemoveAll(u => u.Key.Id == cm.Id);
                Status(L.F("installed_mod", cm.Name), double.NaN);
            });
            if (cm.Id == Seamless.Id && ok) AfterSeamlessInstalled();
            RebuildModLists();
            RefreshStatus();
            if (!ok) return;
            await EnforceConflicts(cm.Id);
            var m = store.Get(cm.Id);
            if (m != null && m.Enabled && !cm.Supports(gameVersion))
                await Alert(L.F("installed_mod", cm.Name), L.F("installed_unverified", cm.Name, gameVersion ?? "?"));
        }

        /// <summary>Retélécharge le mod et remplace ses fichiers (abîmés, ou en partie supprimés par l'antivirus) ; réglages conservés.</summary>
        async Task ReinstallMod(InstalledMod m, CatalogMod cm)
        {
            if (!await Ask(L.F("reinstall_mod_title", m.Name), L.T("reinstall_mod_text"), L.T("btn_reinstall_mod"), L.T("cancel"))) return;
            await InstallMod(cm);
        }

        /// <summary>Vérification à la demande : relit les releases GitHub (le cache de la session est vidé) et la version Nexus.</summary>
        async Task CheckForUpdates()
        {
            GitHub.ClearCache();
            var ok = await Run(L.T("check_updates_failed"), async () =>
            {
                Status(L.T("checking_updates"), -1);
                var me3Check = Me3.Latest();
                var updatesCheck = Installer.FindUpdates(catalog, store);
                var nexusCheck = Nexus.LatestVersions(catalog.Mods.Select(c => c.Nexus));
                me3Latest = await me3Check; // une erreur réseau ici est affichée : pas de « tout est à jour » hors ligne
                pendingUpdates = await updatesCheck;
                try { nexusVersions = await nexusCheck; }
                catch (Exception ex) { Log.Write("nexus versions: " + ex.Message); }
                Status("", double.NaN);
            });
            RebuildModLists();
            RefreshStatus();
            if (!ok) return;
            var names = pendingUpdates.Select(u => u.Key.Name).ToList();
            if (Me3Outdated()) names.Insert(0, "me3 " + me3Latest.Tag);
            if (names.Count == 0 && NexusAhead().Count == 0) await Alert(L.T("updates_title"), L.T("updates_done"));
            else if (names.Count > 0 && await Ask(L.T("updates_title"), L.F("updates_found", string.Join("\n", names.Select(n => "• " + n))), L.T("btn_update_mod"), L.T("later")))
                await ApplyUpdates();
            await NotifyNexusAhead(true); // versions à prendre sur Nexus, même déjà signalées
        }

        async Task UninstallMod(InstalledMod m)
        {
            if (await GameBlocksInstall()) return;
            if (!await Ask(L.F("uninstall_mod_title", m.Name), L.T("uninstall_mod_text"), L.T("btn_uninstall_mod"), L.T("cancel"))) return;
            if (m.Deployed.Count > 0 && !await EnsureWritable()) return;
            if (m.Id == Seamless.Id) SaveSeamless();
            await Run(L.T("install_failed_title"), () => Task.Run(() => Installer.Uninstall(store, m.Id, gameDir)));
            if (m.Id == Seamless.Id) BuildSeamlessPanel();
            RebuildModLists();
            RefreshStatus();
        }

        async Task ImportZip()
        {
            var dlg = new OpenFileDialog { Title = L.T("import_zip_title"), Filter = L.T("filter_zip") + "|*.zip" };
            if (dlg.ShowDialog(Window) == true) await ImportPath(dlg.FileName);
        }

        async Task ImportFolder()
        {
            using (var dlg = new System.Windows.Forms.FolderBrowserDialog { Description = L.T("import_dir_title"), ShowNewFolderButton = false })
                if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK) await ImportPath(dlg.SelectedPath);
        }

        async Task ImportPath(string path)
        {
            if (await GameBlocksInstall()) return;
            InstalledMod m = null;
            ModAnalysis an = null;
            var ok = await Run(L.T("import_mod_failed"), async () =>
            {
                Status(L.T("importing"), -1);
                m = await Task.Run(() => Installer.Import(path, catalog, store, out an));
                Status(L.F("installed_mod", m.Name), double.NaN);
            });
            if (ok)
            {
                pendingUpdates.RemoveAll(u => u.Key.Id == m.Id); // la version importée peut être plus récente que GitHub
                if (m.Id == Seamless.Id) AfterSeamlessInstalled();
            }
            RebuildModLists();
            RefreshStatus();
            if (!ok) return;
            SubInstalled.IsChecked = true;
            var msg = L.F("import_mod_done", m.Name, Contents(m));
            if (m.FromCatalog) msg += "\n\n" + L.F("import_catalog_note", m.Name);
            else msg += "\n\n" + L.T("import_unverified_note");
            if (an != null && an.LoaderIgnored) msg += "\n\n" + L.T("import_loader_ignored");
            await Alert(L.T("import_mod_done_title"), msg);
            await EnforceConflicts(m.Id);
        }

        async Task Discover(string text)
        {
            DiscoverList.Children.Clear();
            DiscoverList.Children.Add(Hint(L.T("searching"), new Thickness(0)));
            List<NexusMod> results = null;
            string err = null;
            try { results = await Nexus.Search(text, 30); }
            catch (Exception ex) { err = ex.Message; Log.Write("nexus: " + ex.Message); }
            discoverResults = results;
            DiscoverList.Children.Clear();
            if (err != null) { DiscoverList.Children.Add(Hint(L.F("search_failed", err), new Thickness(0))); return; }
            ShowDiscover();
        }

        void ShowDiscover()
        {
            DiscoverList.Children.Clear();
            if (discoverResults.Count == 0) { DiscoverList.Children.Add(Hint(L.T("search_none"), new Thickness(0))); return; }
            foreach (var n in discoverResults) DiscoverList.Children.Add(DiscoverCard(n));
        }

        FrameworkElement DiscoverCard(NexusMod n)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var thumb = new Border { Width = 128, Height = 72, Background = Br("#33000000"), Margin = new Thickness(0, 0, 16, 0), CornerRadius = new CornerRadius(4), VerticalAlignment = VerticalAlignment.Top };
            if (!string.IsNullOrEmpty(n.Picture))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(n.Picture);
                    bmp.DecodePixelWidth = 256;
                    bmp.EndInit();
                    thumb.Child = new Image { Source = bmp, Stretch = Stretch.UniformToFill };
                    thumb.ClipToBounds = true;
                }
                catch { }
            }
            g.Children.Add(thumb);

            var entry = catalog.FindNexus(n.Id);
            var info = new StackPanel { Margin = new Thickness(0, 0, 16, 0) };
            info.Children.Add(Head(n.Name, entry == null ? Badge(L.T("badge_unverified"), "warn") : entry.User ? Badge(L.T("badge_my_catalog"), "muted") : Badge(L.T("badge_in_catalog"), "ok")));
            DateTime updated;
            var date = DateTime.TryParse(n.Updated, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out updated) ? updated.ToString("d", L.Culture) : "";
            var sub = L.F("by_author", n.Author) + " · ♥ " + n.Endorsements.ToString("N0", L.Culture) + (date.Length > 0 ? " · " + L.F("updated_on", date) : "") + (n.Version.Length > 0 ? " · " + n.Version : "");
            info.Children.Add(new TextBlock { Text = sub, FontSize = 12, Foreground = B("Muted"), Margin = new Thickness(0, 2, 0, 0) });
            info.Children.Add(new TextBlock { Text = n.Summary, FontSize = 12.5, Opacity = 0.85, TextWrapping = TextWrapping.Wrap, MaxHeight = 36, TextTrimming = TextTrimming.WordEllipsis, Margin = new Thickness(0, 5, 0, 0) });
            Grid.SetColumn(info, 1);
            g.Children.Add(info);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            if (entry == null)
            {
                var add = ActionButton(L.T("btn_add_my_catalog"), new Thickness(0, 0, 8, 0), () => AddToMyCatalog(n));
                add.ToolTip = new TextBlock { Text = L.T("add_my_catalog_tip"), TextWrapping = TextWrapping.Wrap, MaxWidth = 420 };
                actions.Children.Add(add);
            }
            var open = ActionButton(L.T("btn_nexus_page"), new Thickness(0), () => OpenUrl(n.Page));
            open.ToolTip = L.T("nexus_manual_tip");
            actions.Children.Add(open);
            Grid.SetColumn(actions, 2);
            g.Children.Add(actions);
            return Card(g);
        }

        void AddToMyCatalog(NexusMod n)
        {
            try { catalog.AddUserMod(n, store); }
            catch (Exception ex) { Log.Write("my catalog add: " + ex.Message); Status(Friendly(ex), double.NaN); return; }
            if (n.Version.Length > 0) nexusVersions[n.Id] = n.Version; // ses mises à jour Nexus sont suivies sans attendre le prochain démarrage
            Status(L.F("my_catalog_added", n.Name), double.NaN);
            ShowDiscover();
            RebuildModLists();
            RefreshStatus();
        }

        void RemoveFromMyCatalog(CatalogMod cm)
        {
            try { catalog.RemoveUserMod(cm); }
            catch (Exception ex) { Log.Write("my catalog remove: " + ex.Message); Status(Friendly(ex), double.NaN); return; }
            if (discoverResults != null) ShowDiscover();
            RebuildModLists();
            RefreshStatus();
        }

        // ───────────────────────── état ─────────────────────────

        bool NeedsBase()
        {
            return store.Mods.Count == 0 || (Engine == "me3" && !Me3.IsInstalled);
        }

        void RefreshStatus()
        {
            bool gameOk = Game.IsValid(gameDir);
            Row(GameDot, GameText, gameOk ? "Ok" : "Bad", gameOk ? ShortPath(gameDir) + (gameVersion != null ? "  ·  v" + gameVersion : "") : L.T("not_found"));
            GameText.ToolTip = gameOk ? gameDir : L.T("game_tip_missing");
            BrowseBtn.Content = L.T(gameOk ? "browse_change" : "browse_locate");

            var seamless = store.Get(Seamless.Id);
            var enabled = store.Mods.Where(m => m.Enabled).ToList();
            var unverified = enabled.Where(m => !Compatible(m)).Select(m => m.Name).ToList();
            var ahead = NexusAhead();
            if (!gameOk) Row(ModsDot, ModsText, "Off", "—");
            else if (store.Mods.Count == 0) Row(ModsDot, ModsText, "Bad", L.T("mods_none"));
            else
            {
                var newer = seamless == null ? null : NexusNewer(seamless);
                var text = seamless != null && seamless.Enabled
                    ? "Seamless Co-op " + seamless.Version + (newer != null ? " · " + L.F("badge_nexus_newer", Ver(newer)) : "")
                    : L.T("solo_mode");
                int others = enabled.Count(m => m.Id != Seamless.Id);
                if (others > 0) text += L.F("plus_mods", others);
                if (pendingUpdates.Count > 0) text += L.F("n_updates", pendingUpdates.Count);
                Row(ModsDot, ModsText, unverified.Count > 0 || pendingUpdates.Count > 0 || ahead.Count > 0 ? "Gold" : "Ok", text);
            }
            var tips = new List<string>();
            if (unverified.Count > 0) tips.Add(L.F("unverified_tip", gameVersion ?? "?", string.Join(", ", unverified)));
            if (ahead.Count > 0) tips.Add(NexusTip(catalog.Find(ahead[0].Key.Id)));
            ModsText.ToolTip = tips.Count > 0 ? string.Join("\n\n", tips) : null;

            if (Engine == "classic")
                Row(EngineDot, EngineText, seamless != null ? "Ok" : "Off", L.T("engine_classic_status"));
            else if (Me3.IsInstalled)
                Row(EngineDot, EngineText, Me3Outdated() ? "Gold" : "Ok",
                    L.F("engine_me3_status", Me3.InstalledVersion() ?? "") + (Me3Outdated() ? L.F("engine_me3_update", me3Latest.Tag) : ""));
            else Row(EngineDot, EngineText, "Gold", L.T("engine_missing"));

            bool steamRunning = Steam.IsRunning(); // une seule énumération des processus : appelé à chaque retour sur la fenêtre
            if (steamRunning && Steam.IsLoggedIn()) Row(SteamDot, SteamText, "Ok", L.T("steam_ok"));
            else if (Steam.Exe() != null) Row(SteamDot, SteamText, "Gold", L.T(steamRunning ? "steam_waiting" : "steam_autostart"));
            else Row(SteamDot, SteamText, "Bad", L.T("not_found"));

            MainBtn.Content = L.T(!gameOk ? "btn_locate_game" : NeedsBase() ? "btn_install" : "btn_play");
            int n = UpdateCount();
            UpdateBtn.Visibility = n > 0 && gameOk ? Visibility.Visible : Visibility.Collapsed;
            UpdateBtn.Content = L.F("btn_update_n", n);
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
            foreach (var e in new UIElement[] { MainBtn, UpdateBtn, BrowseBtn, ManageBtn, LangBtn, ModsInner, PanelPages, Sheet })
                e.IsEnabled = !value;
            if (!value) Progress.Visibility = Visibility.Hidden;
        }

        /// <summary>Exécute une opération longue avec l'interface verrouillée ; affiche l'erreur éventuelle.</summary>
        async Task<bool> Run(string failTitle, Func<Task> work)
        {
            if (busy) return false;
            SetBusy(true);
            Exception error = null;
            try { await work(); }
            catch (Exception ex) { error = ex; }
            finally { SetBusy(false); }
            if (error == null) return true;
            Log.Write(failTitle + ": " + error);
            Status(failTitle + ".", double.NaN);
            await Alert(failTitle, Friendly(error));
            return false;
        }

        // ───────────────────────── actions ─────────────────────────

        async Task OnMain()
        {
            if (busy) return;
            if (!Game.IsValid(gameDir)) Browse();
            else if (NeedsBase()) await EnsureBase(false);
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
            SaveSeamless();
            gameDir = dir;
            gameVersion = Game.Version(dir);
            state.Set("game_dir", dir);
            state.Save();
            BuildSeamlessPanel();
            RebuildModLists();
            RefreshStatus();
        }

        /// <summary>Le dossier du jeu doit être modifiable pour y copier certains fichiers : sinon, demande unique des droits admin.</summary>
        async Task<bool> EnsureWritable()
        {
            if (Fs.CanWrite(gameDir)) return true;
            if (!await Ask(L.T("access_title"), L.T("access_text"), L.T("access_ok"), L.T("cancel"))) return false;
            Status(L.T("requesting_admin"), -1);
            var dir = gameDir;
            bool ok = await Task.Run(() => Fs.RequestWriteAccess(dir)) && Fs.CanWrite(dir);
            Status("", double.NaN);
            if (!ok) await Alert(L.T("access_title"), L.T("access_failed"));
            return ok;
        }

        /// <summary>Installe ce qui manque pour jouer : me3 et, au tout premier lancement, Seamless Co-op.</summary>
        async Task<bool> EnsureBase(bool forPlay)
        {
            if (await GameBlocksInstall()) return false;
            var seamlessEntry = catalog.Find(Seamless.Id);
            bool needMe3 = Engine == "me3" && !Me3.IsInstalled;
            bool needSeamless = store.Mods.Count == 0 && seamlessEntry != null;
            bool seamlessOnNexus = needSeamless && !seamlessEntry.Source.IsGitHub; // à télécharger soi-même, le launcher l'importe ensuite
            if (seamlessOnNexus) needSeamless = false;
            if (!needMe3 && !needSeamless && !seamlessOnNexus) return true;
            SaveSeamless();
            bool ok = true;
            if (needMe3 || needSeamless)
                ok = await Run(L.T("install_failed_title"), async () =>
                {
                    if (needMe3)
                    {
                        var rel = me3Latest ?? await Me3.Latest();
                        me3Latest = rel;
                        await Me3.Install(rel, Downloading(L.F("downloading", "me3 " + rel.Tag)));
                    }
                    if (needSeamless)
                        await Installer.InstallCatalogMod(seamlessEntry, store, Downloading(L.F("downloading", "Seamless Co-op")));
                    Status(L.T("base_installed"), double.NaN);
                });
            if (needSeamless && ok) AfterSeamlessInstalled();
            RebuildModLists();
            RefreshStatus();
            if (!ok) return false;
            if (needSeamless) await OfferLegacyCleanup();
            if (seamlessOnNexus)
            {
                if (await Ask(L.T("seamless_nexus_title"), L.T("seamless_nexus_text"), L.T("nexus_ahead_open"), L.T("later")))
                    OpenUrl(seamlessEntry.NexusPage);
                return false; // rien à lancer tant que Seamless n'est pas importé
            }
            if (!forPlay)
            {
                var msg = L.T("base_installed");
                var imported = state.Get("import_message");
                if (imported != null) { msg += "\n\n" + imported; state.Set("import_message", null); state.Save(); }
                bool needPassword = seamlessForm != null && string.IsNullOrWhiteSpace(seamlessForm.Get("cooppassword"));
                if (needPassword) msg += "\n\n" + L.T("need_password");
                await Alert(L.T("install_done_title"), msg);
                if (needPassword) FocusPassword();
            }
            return true;
        }

        /// <summary>Après la (ré)installation de Seamless : réglages migrés écrits dans le mod, progression vanilla reprise la 1re fois.</summary>
        void AfterSeamlessInstalled()
        {
            SaveSeamless();
            BuildSeamlessPanel();
            if (state.GetBool("save_import_done", false)) return;
            try
            {
                if (Saves.ImportVanilla(SeamlessExt, false) > 0) state.Set("import_message", L.T("import_auto_done"));
            }
            catch (Exception ex)
            {
                Log.Write("auto import: " + ex);
                state.Set("import_message", L.F("import_auto_failed", ex.Message));
            }
            state.SetBool("save_import_done", true);
            state.Save();
        }

        async Task ApplyUpdates()
        {
            if (await GameBlocksInstall()) return;
            bool seamlessUpdated = pendingUpdates.Any(u => u.Key.Id == Seamless.Id);
            if (seamlessUpdated) SaveSeamless();
            await Run(L.T("update_failed_title"), async () =>
            {
                if (Me3Outdated()) await Me3.Install(me3Latest, Downloading(L.F("updating", "me3 " + me3Latest.Tag)));
                foreach (var u in pendingUpdates.ToList())
                {
                    await Installer.InstallCatalogMod(u.Value, store, Downloading(L.F("updating", u.Key.Name)));
                    pendingUpdates.Remove(u);
                }
                Status(L.T("updates_done"), double.NaN);
            });
            if (seamlessUpdated) BuildSeamlessPanel();
            RebuildModLists();
            RefreshStatus();
            await EnforceConflicts(null); // une nouvelle version peut remplacer d'autres fichiers du jeu
        }

        async Task Play()
        {
            if (busy) return;
            if (Game.IsRunning())
            {
                await Alert(L.T("already_running_title"), L.T("already_running_text"));
                return;
            }
            if (!await EnsureBase(true)) return;
            var resolved = Conflicts.Resolve(store, catalog); // filet de sécurité : jamais deux mods incompatibles au lancement
            if (resolved.Count > 0)
            {
                RebuildModLists();
                RefreshStatus();
                await Alert(L.T("conflict_title"), ResolvedText(resolved));
                return;
            }
            var seamless = store.Get(Seamless.Id);
            bool coop = seamless != null && seamless.Enabled;
            bool classic = Engine == "classic";
            if (classic && !coop) { await Alert(L.T("classic_title"), L.T("classic_needs_seamless")); return; }
            if (classic && store.Mods.Any(m => m.Enabled && m.Id != Seamless.Id) &&
                !await Ask(L.T("classic_title"), L.T("classic_ignores_text"), L.T("launch_anyway"), L.T("cancel"))) return;
            if (coop && string.IsNullOrWhiteSpace(seamlessForm == null ? "" : seamlessForm.Get("cooppassword")) &&
                !await Ask(L.T("no_password_title"), L.T("no_password_text"), L.T("launch_anyway"), L.T("choose_password")))
            {
                FocusPassword();
                return;
            }
            SaveSeamless();
            bool touchesGame = classic || store.Mods.Any(m => (m.Enabled && m.Deploy.Count > 0) || m.Deployed.Count > 0);
            if (touchesGame && !await EnsureWritable()) return;

            SetBusy(true);
            Exception error = null;
            bool started = false;
            try
            {
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
                    var ext = coop ? SeamlessExt : "sl2";
                    try { await Task.Run(() => Saves.BackupAll(ext)); }
                    catch (Exception ex) { Log.Write("backup: " + ex.Message); }
                }

                // traductions du mod, fichiers à poser dans le jeu, profil me3
                if (seamless != null)
                {
                    try { Seamless.WriteLocales(Seamless.Folder(seamless), null, false); }
                    catch (Exception ex) { Log.Write("locales: " + ex.Message); }
                }
                var warnings = await Task.Run(() => Installer.SyncDeploy(store, gameDir, classic));
                foreach (var w in warnings) Log.Write("deploy: " + w);

                Status(L.T("launching"), -1);
                if (classic)
                    using (Process.Start(new ProcessStartInfo(Path.Combine(gameDir, "ersc_launcher.exe")) { WorkingDirectory = gameDir, UseShellExecute = true })) { }
                else
                {
                    var profile = Me3.WriteProfile(store, "default");
                    Me3.Launch(profile, gameDir);
                }
                var wait = Stopwatch.StartNew();
                while (!Game.IsRunning() && wait.Elapsed.TotalSeconds < 120) await Task.Delay(500);
                if (!Game.IsRunning()) throw new UserError(L.T(classic ? "launch_not_detected" : "launch_not_detected_me3"));
                started = true;
                Status(warnings.Count > 0 ? L.F("deploy_warn", warnings.Count) : L.T("launched"), double.NaN);
                Log.Write("game started (" + Engine + ", " + store.Mods.Count(m => m.Enabled) + " mods)");
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
            var ext = SeamlessExt;
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

        void OpenFolder(string dir)
        {
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return;
            using (Process.Start("explorer.exe", "\"" + dir + "\"")) { }
        }

        void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url) || !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return;
            try { using (Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })) { } }
            catch (Exception ex) { Log.Write("open url: " + ex.Message); }
            if (new Uri(url).Host.EndsWith("nexusmods.com", StringComparison.OrdinalIgnoreCase)) WatchDownloads();
        }

        /// <summary>Après l'ouverture d'une page Nexus : pendant 15 min, un .zip qui arrive dans Téléchargements est proposé à l'import.</summary>
        void WatchDownloads()
        {
            try
            {
                if (downloads == null)
                    downloads = new DownloadWatch(Paths.Downloads, path => Window.Dispatcher.BeginInvoke(new Action(async () => await OfferDownloaded(path))));
                downloads.Until = DateTime.Now.AddMinutes(15);
            }
            catch (Exception ex) { Log.Write("downloads watch: " + ex.Message); } // dossier introuvable : l'import manuel reste possible
        }

        async Task OfferDownloaded(string path)
        {
            while (busy || Overlay.Visibility == Visibility.Visible) await Task.Delay(500); // Ask fermerait la fenêtre déjà ouverte
            if (Window.WindowState == WindowState.Minimized) Window.WindowState = WindowState.Normal;
            Window.Activate(); // le joueur est dans son navigateur
            if (await Ask(L.T("downloaded_title"), L.F("downloaded_text", Path.GetFileName(path)), L.T("import_ok"), L.T("ignore")))
                await ImportPath(path);
        }

        void FocusPassword()
        {
            NavPlay.IsChecked = true;
            if (PanelTabs.Children.Count > 0) ((RadioButton)PanelTabs.Children[0]).IsChecked = true;
            if (passwordBox == null) return;
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

        Style St(string key)
        {
            return (Style)Window.Resources[key];
        }

        static readonly Dictionary<string, Brush> brushes = new Dictionary<string, Brush>();

        static Brush Br(string hex)
        {
            Brush b;
            if (!brushes.TryGetValue(hex, out b))
            {
                b = (Brush)new BrushConverter().ConvertFromString(hex);
                b.Freeze();
                brushes[hex] = b;
            }
            return b;
        }
    }
}
