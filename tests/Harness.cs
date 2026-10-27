// Banc d'essai : logique multi-mods dans un bac à sable + captures de l'interface.
// Usage : Harness.exe <dossier_test> <dossier_src> <eldenring.exe réel (lien physique, pour la version)>
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ERSeamless;

static class Harness
{
    static int failures;
    static string root;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    static extern bool CreateHardLink(string newFile, string existingFile, IntPtr security);

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + what);
        if (!ok) failures++;
    }

    static void Throws(Action a, string expectedKey, string what)
    {
        try { a(); Check(false, what + " (aucune erreur)"); }
        catch (UserError e) { Check(e.Message == L.T(expectedKey), what); }
        catch (Exception e) { Check(false, what + " (" + e.GetType().Name + ": " + e.Message + ")"); }
    }

    static string Zip(string name, params string[] entries) // entries : "chemin=contenu"
    {
        var path = Path.Combine(root, "zips", name);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        if (File.Exists(path)) File.Delete(path);
        using (var z = ZipFile.Open(path, ZipArchiveMode.Create))
            foreach (var e in entries)
            {
                var i = e.IndexOf('=');
                using (var w = new StreamWriter(z.CreateEntry(e.Substring(0, i)).Open())) w.Write(e.Substring(i + 1));
            }
        return path;
    }

    [STAThread]
    static int Main(string[] args)
    {
        root = Path.GetFullPath(args[0]);
        string src = Path.GetFullPath(args[1]);
        if (Directory.Exists(root)) Directory.Delete(root, true);
        Paths.AppData = Path.Combine(root, "appdata");
        Paths.SavesRoot = Path.Combine(root, "saves");
        string game = Path.Combine(root, "ELDEN RING", "Game");
        Directory.CreateDirectory(game);
        if (!CreateHardLink(Path.Combine(game, "eldenring.exe"), args[2], IntPtr.Zero)) File.Copy(args[2], Path.Combine(game, "eldenring.exe"));
        File.WriteAllText(Path.Combine(game, "start_protected_game.exe"), "vanilla");
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
        L.Set("fr");
        var gv = Game.Version(game);
        Console.WriteLine("  version du jeu : " + gv);

        Console.WriteLine("== Traductions");
        var placeholders = new Regex(@"\{\d\}");
        int missing = 0, mismatch = 0;
        foreach (var key in L.Keys)
        {
            var counts = new List<int>();
            foreach (var code in L.Codes)
            {
                L.Set(code);
                var txt = L.T(key);
                if (string.IsNullOrWhiteSpace(txt)) { missing++; Console.WriteLine("    manquant : " + key + " [" + code + "]"); }
                else counts.Add(placeholders.Matches(txt).Count);
            }
            if (counts.Distinct().Count() > 1) { mismatch++; Console.WriteLine("    paramètres différents : " + key); }
        }
        L.Set("fr");
        Check(missing == 0, L.Keys.Count() + " textes présents dans les 3 langues");
        Check(mismatch == 0, "mêmes paramètres {0}/{1} dans chaque langue");
        var notKeys = new HashSet<string> { "game_dir", "language", "backup_on_launch", "save_import_done", "game_version", "legacy_declined", "import_message",
                                            "muted", "warn", "bad", "me3", "classic", "github", "nexus", "manual", "seamless" };
        var used = new HashSet<string>();
        foreach (var file in Directory.GetFiles(src, "*.cs").Where(f => !f.EndsWith("Lang.cs")))
            foreach (var line in File.ReadAllLines(file).Where(l => l.Contains("L.T(") || l.Contains("L.F(")))
                foreach (Match m in Regex.Matches(line, "\"([a-z]+(?:_[a-z0-9]+)*)\""))
                    if (!notKeys.Contains(m.Groups[1].Value)) used.Add(m.Groups[1].Value);
        var unknown = used.Where(k => !L.Keys.Contains(k)).ToList();
        Check(unknown.Count == 0, used.Count + " clés utilisées dans le code, toutes traduites" + (unknown.Count > 0 ? " — inconnues : " + string.Join(", ", unknown) : ""));

        Console.WriteLine("== Catalogue");
        var cat = Catalog.Embedded();
        var seam = cat.Find(Seamless.Id);
        Check(cat.Mods.Count == 5 && seam != null && seam.Core, "catalogue embarqué : 5 mods, Seamless en tête");
        Check(seam.Settings != null && seam.Settings.Tabs.Count == 3 && seam.Settings.Items.Count() == 16, "réglages Seamless : 3 onglets, 16 clés");
        Check(CatalogMod.VersionMatch("2.7.*", "2.7.2") && !CatalogMod.VersionMatch("2.7.*", "2.8.0") && CatalogMod.VersionMatch("*", "1.0.0")
              && CatalogMod.VersionMatch("2.7.2", "2.7.2") && !CatalogMod.VersionMatch("2.7", "2.7.2"), "comparaison de versions");
        Check(seam.Supports(gv) && !cat.Find("unlock-the-fps").Supports(gv), "Seamless compatible " + gv + ", mods Techiew non vérifiés");
        var newer = Catalog.Parse("{\"schema\":1,\"updated\":\"2099-01-01\",\"mods\":[]}");
        Check(Catalog.Newest(cat, newer) == newer && Catalog.Newest(cat, Catalog.Parse("{\"schema\":1,\"updated\":\"2000-01-01\",\"mods\":[]}")) == cat, "le catalogue le plus récent gagne");
        Check(Catalog.Parse("{\"schema\":99,\"mods\":[]}") == null, "schéma inconnu ignoré");
        var remote = Catalog.FetchRemote().Result;
        Console.WriteLine("  catalogue distant : " + (remote == null ? "absent (normal tant que la branche n'est pas fusionnée)" : remote.Updated));

        Console.WriteLine("== Réglages : reprise de l'ancienne installation");
        Directory.CreateDirectory(Path.Combine(game, "SeamlessCoop"));
        File.WriteAllText(Path.Combine(game, "SeamlessCoop", "ersc_settings.ini"), "[PASSWORD]\r\ncooppassword = ancien\r\n[SCALING]\r\nboss_health_scaling = 150\r\n");
        File.WriteAllText(Path.Combine(game, "ersc_launcher.exe"), "v1");
        var vals = ModSettings.Load(Seamless.Id, null, seam.Settings, Seamless.LegacyIni(game));
        Check(vals["cooppassword"] == "ancien" && vals["boss_health_scaling"] == "150" && vals["allow_invaders"] == "1", "valeurs v1.0 reprises, défauts du schéma pour le reste");

        Console.WriteLine("== Installation depuis GitHub");
        var store = ModStore.Load();
        var s = Installer.InstallCatalogMod(seam, store, null).Result;
        Check(File.Exists(Path.Combine(s.Dir, "SeamlessCoop", "ersc.dll")) && store.Mods[0] == s && s.Tag.StartsWith("v"), "Seamless " + s.Tag + " installé dans le dossier du launcher");
        Check(!File.Exists(Path.Combine(game, "SeamlessCoop", "ersc.dll")), "rien d'installé dans le dossier du jeu");
        Check(File.Exists(Path.Combine(s.Dir, "SeamlessCoop", "locale", "french.json")) && File.Exists(Path.Combine(s.Dir, "SeamlessCoop", "locale", "latam.json")), "traductions du mod générées");
        Check(Seamless.LegacyPresent(game, s), "ancienne installation détectée");
        vals["cooppassword"] = "tarnished42";
        ModSettings.Save(Seamless.Id, s, seam.Settings, vals);
        Check(Ini.Load(Path.Combine(s.Dir, "SeamlessCoop", "ersc_settings.ini")).Values()["cooppassword"] == "tarnished42" && File.Exists(ModSettings.CopyPath(Seamless.Id)), "réglages écrits dans le mod + copie de secours");
        File.WriteAllText(Path.Combine(s.Dir, "SeamlessCoop", "ersc_settings.ini"), File.ReadAllText(Path.Combine(s.Dir, "SeamlessCoop", "ersc_settings.ini")).Replace("mod_language_override =", "mod_language_override = custom"));
        s = Installer.InstallCatalogMod(seam, store, null).Result;
        var after = Ini.Load(Path.Combine(s.Dir, "SeamlessCoop", "ersc_settings.ini")).Values();
        Check(after["cooppassword"] == "tarnished42" && after["boss_health_scaling"] == "150" && after["mod_language_override"] == "custom", "réinstallation : réglages et clés inconnues conservés");
        Check(store.Mods.Count == 1, "pas de doublon après réinstallation");

        var fps = Installer.InstallCatalogMod(cat.Find("unlock-the-fps"), store, null).Result;
        Check(File.Exists(Path.Combine(fps.Dir, "mods", "UnlockTheFps.dll")) && fps.Deploy.Count == 1 && fps.AssetSha.Equals("116a523d858ab76ed38ca8a918c1f1e393d646f4d632ea2f692a614524b86a51", StringComparison.OrdinalIgnoreCase), "Unlock the FPS installé (empreinte épinglée vérifiée)");
        var bad = cat.Find("adjust-the-fov");
        bad.Source.Sha256 = new string('0', 64);
        try { Installer.InstallCatalogMod(bad, store, null).Wait(); Check(false, "empreinte fausse refusée"); }
        catch (AggregateException e) { Check(e.InnerException is UserError && e.InnerException.Message == L.T("err_sha") && store.Get("adjust-the-fov") == null, "empreinte SHA-256 fausse : installation refusée"); }

        Console.WriteLine("== me3");
        var rel = Me3.Latest().Result;
        Me3.Install(rel, null).Wait();
        Check(Me3.IsInstalled && Me3.InstalledVersion() == rel.Tag, "me3 " + rel.Tag + " installé");

        Console.WriteLine("== Import de mods");
        ModAnalysis an;
        var tex = Installer.Import(Zip("Malenia HD-1234-1-0-1700000000.zip", "Malenia HD/chr/c2120.texbnd.dcx=x", "Malenia HD/readme.txt=lisez-moi"), cat, store, out an);
        Check(tex.Packages.SequenceEqual(new[] { "Malenia HD" }) && tex.Natives.Count == 0 && tex.Name == "Malenia HD" && tex.Nexus == 1234, "texture : dossier de fichiers du jeu détecté, nom et id Nexus lus");
        var eml = Installer.Import(Zip("Foo.zip", "dinput8.dll=x", "mods/Foo.dll=x", "mods/Foo/config.ini=[foo]\nenabled = 1\nspeed = 2.5"), cat, store, out an);
        Check(eml.Natives.SequenceEqual(new[] { "mods/Foo.dll" }) && an.LoaderIgnored && eml.Deploy.Count == 1 && eml.Deploy[0].Value == "mods/Foo" && eml.SettingsFile == "mods/Foo/config.ini",
              "DLL façon Elden Mod Loader : chargeur ignoré, config copiée dans le jeu");
        var reg = Installer.Import(Zip("Rebalance.zip", "regulation.bin=x", "msg/engus/item.msgbnd.dcx=x"), cat, store, out an);
        Check(reg.Packages.SequenceEqual(new[] { "" }), "regulation.bin à la racine : tout le mod est un pack de fichiers");
        var known = Installer.Import(Zip("Unlock the framerate-216-1-0-1652300000.zip", "mods/UnlockTheFps.dll=x", "mods/UnlockTheFps/config.ini=[unlockthefps]\nlimit = 300"), cat, store, out an);
        Check(known.Id == "unlock-the-fps" && known.FromCatalog && known.Source == "nexus", "archive Nexus reconnue comme mod du catalogue");
        Throws(() => Installer.Import(Path.Combine(root, "mod.7z"), cat, store, out an), "err_archive_format", ".7z refusé avec explication");
        Throws(() => Installer.Import(Zip("vide.zip", "readme.txt=rien"), cat, store, out an), "err_import_empty", "archive sans mod refusée");
        var schema = SettingsSchema.FromIni("mods/Foo/config.ini", "; vitesse du jeu\n[foo]\nenabled = 1\nspeed = 2.5");
        Check(schema != null && schema.Items.First().Type == "toggle" && schema.Items.Last().Type == "text" && Json.Loc(schema.Items.Last().Hint) == null
              && Json.Loc(SettingsSchema.FromIni("x.ini", "[a]\n; aide\nk = 1").Items.First().Hint) == "aide", "réglages déduits d'un .ini quelconque");

        Console.WriteLine("== Profil me3");
        var profile = File.ReadAllText(Me3.WriteProfile(store, "test"));
        int iErsc = profile.IndexOf("ersc.dll"), iFps = profile.IndexOf("UnlockTheFps.dll");
        Check(iErsc > 0 && iFps > iErsc && profile.Contains("[[packages]]") && profile.Contains("Malenia HD") && profile.Contains("\\\\"), "natives dans l'ordre, packs listés, chemins échappés");
        eml.Enabled = false;
        Check(!File.ReadAllText(Me3.WriteProfile(store, "test")).Contains("Foo.dll"), "mod désactivé absent du profil");
        eml.Enabled = true;

        Console.WriteLine("== Fichiers copiés dans le jeu");
        var cfg = Path.Combine(game, "mods", "UnlockTheFps", "config.ini");
        Directory.CreateDirectory(Path.GetDirectoryName(cfg));
        File.WriteAllText(cfg, "fichier de l'utilisateur");
        var w = Installer.SyncDeploy(store, game, false);
        Check(w.Count == 1 && File.ReadAllText(cfg) == "fichier de l'utilisateur", "fichier existant non posé par le launcher : jamais écrasé");
        File.Delete(cfg);
        Installer.SyncDeploy(store, game, false);
        Check(File.Exists(cfg) && File.Exists(Path.Combine(game, "mods", "Foo", "config.ini")) && known.Deployed.Count == 1, "config des mods actifs copiée et suivie");
        known.Enabled = false; eml.Enabled = false;
        Installer.SyncDeploy(store, game, false);
        Check(!File.Exists(cfg) && !Directory.Exists(Path.Combine(game, "mods")), "mods désactivés : fichiers et dossiers vides retirés");
        Installer.SyncDeploy(store, game, true);
        Check(File.ReadAllText(Path.Combine(game, "ersc_launcher.exe")) != "v1" && File.Exists(Path.Combine(game, "SeamlessCoop", "ersc.dll")), "mode « Seamless seul » : Seamless copié dans le jeu");
        Installer.SyncDeploy(store, game, false);
        Check(!File.Exists(Path.Combine(game, "ersc_launcher.exe")) && !File.Exists(Path.Combine(game, "SeamlessCoop", "ersc.dll")) && File.Exists(Path.Combine(game, "eldenring.exe")), "retour à me3 : le jeu redevient vanilla");
        Check(File.ReadAllText(Path.Combine(game, "start_protected_game.exe")) == "vanilla", "start_protected_game.exe (EAC) jamais touché");

        Console.WriteLine("== Désinstallation");
        eml.Enabled = true;
        Installer.SyncDeploy(store, game, false);
        Installer.Uninstall(store, eml.Id, game);
        Check(store.Get(eml.Id) == null && !Directory.Exists(eml.Dir) && !File.Exists(Path.Combine(game, "mods", "Foo", "config.ini")), "mod et fichiers posés dans le jeu supprimés");
        Check(ModStore.Load().Mods.Select(m => m.Id).SequenceEqual(store.Mods.Select(m => m.Id)), "mods.json relu à l'identique");

        Console.WriteLine("== Nexus (en ligne)");
        var found = Nexus.Search("fps", 5).Result;
        Check(found.Count > 0 && found.All(n => n.Id > 0 && n.Name.Length > 0), "recherche « fps » : " + found.Count + " résultats");

        Console.WriteLine("== Interface");
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        known.Enabled = true; // mod non vérifié actif…
        store.Save();
        var st = State.Load();
        st.Set("game_version", "2.6.0"); // …et le jeu vient de changer de version
        st.Set("language", "fr");
        st.SetBool("legacy_declined", true);
        st.Save();
        var ui = new MainUi(game);
        var win = Show(ui, 6);
        Check(Find<Grid>(win, "Overlay").Visibility == Visibility.Visible && !ModStore.Load().Get("unlock-the-fps").Enabled && ModStore.Load().Get(Seamless.Id).Enabled,
              "après un patch : mod non vérifié désactivé, Seamless (compatible) gardé, message affiché");
        Render(Find<FrameworkElement>(win, "Root"), "ui_patch_dialog.png");
        Click(Find<Button>(win, "DlgOk"));
        Pump(0.5);
        Check((string)Find<Button>(win, "MainBtn").Content == "JOUER", "bouton JOUER (me3 + Seamless installés)");
        Render(Find<FrameworkElement>(win, "Root"), "ui_play.png");
        Find<RadioButton>(win, "NavMods").IsChecked = true;
        Pump(0.8);
        Render(Find<FrameworkElement>(win, "Root"), "ui_mods_catalog.png");
        Find<RadioButton>(win, "SubInstalled").IsChecked = true;
        Pump(0.5);
        Render(Find<FrameworkElement>(win, "Root"), "ui_mods_installed.png");
        var settingsBtn = FindAll<Button>(Find<StackPanel>(win, "InstalledList")).FirstOrDefault(b => (b.Content as string) == "Réglages" && FindAncestorText(b).Contains("Unlock"));
        if (settingsBtn != null) { Click(settingsBtn); Pump(0.5); }
        Check(Find<Grid>(win, "Sheet").Visibility == Visibility.Visible, "fenêtre de réglages d'un mod générée depuis le catalogue");
        Render(Find<FrameworkElement>(win, "Root"), "ui_mod_settings.png");
        Click(Find<Button>(win, "SheetClose"));
        OnUi(win, () => Find<RadioButton>(win, "SubDiscover").IsChecked = true); // comme un vrai clic : dans la boucle de messages
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (Find<StackPanel>(win, "DiscoverList").Children.Count < 5 && waited.Elapsed.TotalSeconds < 30) Pump(0.5);
        Console.WriteLine("  résultats Nexus affichés après " + waited.Elapsed.TotalSeconds.ToString("0.0") + " s");
        Pump(3); // vignettes
        Check(Find<StackPanel>(win, "DiscoverList").Children.Count >= 5, "onglet Découvrir : mods Nexus populaires affichés");
        Render(Find<FrameworkElement>(win, "Root"), "ui_mods_discover.png");
        win.Close();

        st = State.Load(); st.Set("language", "es"); st.Save();
        win = Show(new MainUi(game), 4);
        Find<RadioButton>(win, "NavMods").IsChecked = true;
        Find<RadioButton>(win, "SubInstalled").IsChecked = true;
        Pump(0.8);
        Render(Find<FrameworkElement>(win, "Root"), "ui_es_installed.png");
        Find<RadioButton>(win, "NavPlay").IsChecked = true;
        Pump(0.5);
        var tabs = Find<UniformGrid>(win, "PanelTabs").Children.OfType<RadioButton>().ToList();
        tabs.Last().IsChecked = true;
        Pump(0.4);
        Render(Find<FrameworkElement>(win, "Root"), "ui_es_advanced.png");
        win.Close();

        Console.WriteLine(failures == 0 ? "\nTOUS LES TESTS PASSENT" : "\n" + failures + " ÉCHEC(S)");
        return failures;
    }

    static string FindAncestorText(DependencyObject d)
    {
        for (var p = VisualTreeHelper.GetParent(d); p != null; p = VisualTreeHelper.GetParent(p))
            if (p is Border && ((Border)p).Child is Grid)
                return string.Join(" ", FindAll<TextBlock>(p).Select(t => t.Text));
        return "";
    }

    static Window Show(MainUi ui, double seconds)
    {
        var w = ui.Window;
        w.WindowStartupLocation = WindowStartupLocation.Manual;
        w.Left = -4000; w.Top = -4000;
        w.ShowActivated = false; w.ShowInTaskbar = false;
        w.Show();
        Pump(seconds);
        return w;
    }

    static void Pump(double seconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
        timer.Tick += (s, e) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    static void Click(Button b) { b.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); }

    /// <summary>Exécute une action dans la boucle de messages de la fenêtre (contexte de synchronisation WPF actif).</summary>
    static void OnUi(Window w, Action a)
    {
        w.Dispatcher.BeginInvoke(a);
        Pump(0.2);
    }

    static T Find<T>(Window w, string name) where T : class { return (T)w.FindName(name); }

    static IEnumerable<T> FindAll<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var c = VisualTreeHelper.GetChild(parent, i);
            if (c is T) yield return (T)c;
            foreach (var d in FindAll<T>(c)) yield return d;
        }
    }

    static void Render(FrameworkElement e, string name)
    {
        var rtb = new RenderTargetBitmap((int)Math.Ceiling(e.ActualWidth), (int)Math.Ceiling(e.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        rtb.Render(e);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using (var f = File.Create(Path.Combine(root, name))) enc.Save(f);
        Console.WriteLine("  capture : " + name);
    }
}
