// Banc d'essai : logique du launcher dans un bac à sable + captures de l'interface dans les 3 langues.
// Usage : Harness.exe <dossier_test> <dossier_src>   (télécharge la dernière release du mod depuis GitHub)
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Net;
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

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "  PASS  " : "  FAIL  ") + what);
        if (!ok) failures++;
    }

    [STAThread]
    static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        string src = Path.GetFullPath(args[1]);
        if (Directory.Exists(root)) Directory.Delete(root, true);
        Paths.AppData = Path.Combine(root, "appdata");
        Paths.SavesRoot = Path.Combine(root, "saves");
        string game = Path.Combine(root, "ELDEN RING", "Game");
        Directory.CreateDirectory(game);
        File.WriteAllText(Path.Combine(game, "eldenring.exe"), "");
        File.WriteAllText(Path.Combine(game, "start_protected_game.exe"), "vanilla");
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

        Console.WriteLine("== Traductions");
        var placeholders = new Regex(@"\{\d\}");
        int missing = 0, mismatch = 0;
        foreach (var key in L.Keys)
        {
            var counts = new List<int>();
            foreach (var code in L.Codes)
            {
                L.Set(code);
                string s = null;
                try { s = L.T(key); } catch { }
                if (string.IsNullOrWhiteSpace(s)) { missing++; Console.WriteLine("    manquant : " + key + " [" + code + "]"); }
                else counts.Add(placeholders.Matches(s).Count);
            }
            if (counts.Distinct().Count() > 1) { mismatch++; Console.WriteLine("    paramètres différents : " + key); }
        }
        Check(missing == 0, L.Keys.Count() + " clés présentes dans les 3 langues");
        Check(mismatch == 0, "mêmes paramètres {0}/{1} dans chaque langue");

        // toutes les clés utilisées dans le code existent (hors clés d'INI / de préférences sur les mêmes lignes)
        var notKeys = new HashSet<string>(ModConfig.Defaults().Keys) { "game_dir", "language", "backup_on_launch", "save_import_done" };
        var used = new HashSet<string>();
        foreach (var file in Directory.GetFiles(src, "*.cs").Where(f => !f.EndsWith("Lang.cs")))
            foreach (var line in File.ReadAllLines(file).Where(l => l.Contains("L.T(") || l.Contains("L.F(")))
                foreach (Match m in Regex.Matches(line, "\"([a-z]+(?:_[a-z]+)*)\""))
                    if (!notKeys.Contains(m.Groups[1].Value)) used.Add(m.Groups[1].Value);
        var unknown = used.Where(k => !L.Keys.Contains(k)).ToList();
        Check(unknown.Count == 0, used.Count + " clés utilisées dans le code, toutes traduites" + (unknown.Count > 0 ? " — inconnues : " + string.Join(", ", unknown) : ""));
        L.Set("fr"); Check(L.T("btn_play") == "JOUER", "fr : JOUER");
        L.Set("en"); Check(L.T("btn_play") == "PLAY", "en : PLAY");
        L.Set("es"); Check(L.T("btn_play") == "JUGAR", "es : JUGAR");
        L.Set("de"); Check(L.Codes.Contains(L.Code), "langue non gérée → langue de Windows ou anglais (" + L.Code + ")");
        L.Set("fr");

        Console.WriteLine("== GitHub (en ligne)");
        var rel = Mod.FetchLatest().Result;
        Console.WriteLine("  dernière : " + rel.Tag + " | " + rel.Summary);
        int last = -1;
        string zip = Mod.Download(rel, p => last = p).Result;
        Check(File.Exists(zip) && last == 100 && rel.Sha256 != null, "téléchargement + vérification SHA-256 (" + new FileInfo(zip).Length / 1024 + " Ko)");

        Console.WriteLine("== Installation / mise à jour");
        var user = ModConfig.Defaults();
        user["cooppassword"] = "tarnished42";
        user["allow_invaders"] = "0";
        user["boss_health_scaling"] = "150";
        Mod.Install(zip, game, user, rel.Tag);
        var iv = Ini.Load(Mod.IniPath(game)).Values();
        Check(Mod.IsInstalled(game) && Mod.InstalledVersion(game) == rel.Tag, "mod installé, version enregistrée");
        Check(File.ReadAllText(Path.Combine(game, "start_protected_game.exe")) == "vanilla", "start_protected_game.exe (EAC) intact");
        Check(iv["cooppassword"] == "tarnished42" && iv["allow_invaders"] == "0" && iv["boss_health_scaling"] == "150", "réglages écrits");
        File.WriteAllText(Mod.IniPath(game), File.ReadAllText(Mod.IniPath(game)).Replace("mod_language_override =", "mod_language_override = custom_fr"));
        Mod.Install(zip, game, user, "v0.0.1-test");
        Check(Ini.Load(Mod.IniPath(game)).Values()["mod_language_override"] == "custom_fr", "mise à jour : clés non gérées préservées");

        Console.WriteLine("== Traductions du mod (locale)");
        var loc = Path.Combine(game, "SeamlessCoop", "locale");
        Func<string, Dictionary<string, object>> json = n =>
        {
            var p = Path.Combine(loc, n + ".json");
            return File.Exists(p) ? (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(File.ReadAllText(p, Encoding.UTF8)) : null;
        };
        var en = json("english");
        foreach (var lang in Mod.Locales)
        {
            var d = json(lang);
            Check(d != null && d.Keys.SequenceEqual(en.Keys), lang + ".json créé, mêmes " + en.Count + " clés que english.json");
            Check(d != null && d.All(kv => Regex.Matches((string)kv.Value, @"\{\}").Count == Regex.Matches((string)en[kv.Key], @"\{\}").Count), lang + " : mêmes paramètres {} que l'anglais");
        }
        Check((string)json("french")["OPTIONSELECT_LEAVEWORLD"] == "Quitter le groupe" && (string)json("spanish")["YKNX3_CONNECTING"] == "Conectando..."
              && (string)json("latam")["OPTIONSELECT_BOSSRUSH"] == "JEFE", "textes traduits (fr / es / latam)");
        Check(new FileInfo(Path.Combine(loc, "french.json")).Length > 0 && File.ReadAllBytes(Path.Combine(loc, "french.json"))[0] == (byte)'{', "UTF-8 sans BOM");

        // version future du mod : une clé nouvelle + un texte anglais modifié
        var enPath = Path.Combine(loc, "english.json");
        File.WriteAllText(enPath, File.ReadAllText(enPath).Replace("\"Leave party\"", "\"Leave the party\"").Replace("\"CREDITS\"", "\"BRAND_NEW\": \"Brand new\",\n\t\"CREDITS\""));
        File.Delete(Path.Combine(loc, "french.json"));
        Check(Mod.WriteLocales(game, null, false) == 1, "au lancement : seule la traduction manquante est recréée");
        var fr = json("french");
        Check((string)fr["BRAND_NEW"] == "Brand new" && (string)fr["OPTIONSELECT_LEAVEWORLD"] == "Leave the party"
              && (string)fr["OPTIONSELECT_JOINWORLD"] == "Rejoindre un monde en tant que vagabond", "texte nouveau ou modifié → anglais, le reste reste traduit");

        // traduction officielle livrée par le mod : jamais écrasée
        var zip2 = Path.Combine(root, "mod_with_french.zip");
        File.Copy(zip, zip2);
        using (var z = ZipFile.Open(zip2, ZipArchiveMode.Update))
        using (var w = new StreamWriter(z.CreateEntry("SeamlessCoop/locale/french.json").Open())) w.Write("{\"OFFICIAL\": \"oui\"}");
        Mod.Install(zip2, game, user, "v9.9.9-test");
        Check(json("french").ContainsKey("OFFICIAL") && json("spanish").Count == json("english").Count, "french.json officiel conservé, les autres régénérés");
        File.Delete(zip2);
        Mod.Install(zip, game, user, rel.Tag);

        Console.WriteLine("== Sauvegardes");
        var prof = Path.Combine(Paths.SavesRoot, "76561198000000001");
        Directory.CreateDirectory(prof);
        File.WriteAllText(Path.Combine(prof, "ER0000.sl2"), "vanilla-save");
        Check(Saves.ImportVanilla("co2", false) == 1, "import .sl2 -> .co2");
        File.WriteAllText(Path.Combine(prof, "ER0000.co2"), "coop-progress");
        Check(Saves.ImportVanilla("co2", false) == 0, "import auto n'écrase jamais une save Seamless");

        Console.WriteLine("== Interface");
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

        // changement de langue à chaud depuis le menu
        var ui = Show(Ui(game, "fr"));
        Check((string)Find<Button>(ui, "MainBtn").Content == "JOUER", "démarrage en français");
        Click(Find<Button>(ui, "LangBtn"));
        Pump(0.5);
        var items = Find<StackPanel>(ui, "LangList").Children;
        Check(items.Count == 3 && Find<Popup>(ui, "LangPopup").IsOpen, "menu des langues ouvert (3 choix)");
        Render((FrameworkElement)Find<Popup>(ui, "LangPopup").Child, Path.Combine(root, "ui_lang_menu.png"));
        Click((Button)items[2]);
        Pump(0.5);
        Check((string)Find<Button>(ui, "MainBtn").Content == "JUGAR" && Find<TextBlock>(ui, "LangCode").Text == "ES", "bascule en espagnol sans redémarrer");
        Check(State.Load().Get("language") == "es", "choix de langue mémorisé");
        var pwd = FindAll<TextBox>(Find<StackPanel>(ui, "PageSession")).FirstOrDefault();
        Check(pwd != null && pwd.Text == "tarnished42", "réglages saisis conservés après le changement de langue");
        ui.Close();

        Snapshot(Ui(game, "fr"), Path.Combine(root, "ui_fr_session.png"), null);
        Snapshot(Ui(game, "en"), Path.Combine(root, "ui_en_session.png"), null);
        Snapshot(Ui(game, "en"), Path.Combine(root, "ui_en_diff.png"), "TabDiff");
        Snapshot(Ui(game, "es"), Path.Combine(root, "ui_es_session.png"), null);
        Snapshot(Ui(game, "es"), Path.Combine(root, "ui_es_adv.png"), "TabAdv");
        File.Delete(Mod.LauncherExe(game));
        Snapshot(Ui(game, "es"), Path.Combine(root, "ui_es_not_installed.png"), null);

        File.Delete(zip);
        Console.WriteLine(failures == 0 ? "\nTOUS LES TESTS PASSENT" : "\n" + failures + " ÉCHEC(S)");
        return failures;
    }

    static MainUi Ui(string game, string lang)
    {
        var st = State.Load();
        st.Set("language", lang);
        st.Save();
        return new MainUi(game);
    }

    static Window Show(MainUi ui)
    {
        var w = ui.Window;
        w.WindowStartupLocation = WindowStartupLocation.Manual;
        w.Left = -4000; w.Top = -4000;
        w.ShowActivated = false; w.ShowInTaskbar = false;
        w.Show();
        Pump(3);
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

    static void Render(FrameworkElement e, string png)
    {
        var rtb = new RenderTargetBitmap((int)Math.Ceiling(e.ActualWidth), (int)Math.Ceiling(e.ActualHeight), 96, 96, PixelFormats.Pbgra32);
        rtb.Render(e);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using (var f = File.Create(png)) enc.Save(f);
        Console.WriteLine("  capture : " + Path.GetFileName(png));
    }

    static void Snapshot(MainUi ui, string png, string tab)
    {
        var w = Show(ui);
        if (tab != null) { Find<RadioButton>(w, tab).IsChecked = true; Pump(0.3); }
        Render(Find<FrameworkElement>(w, "Root"), png);
        w.Close();
    }
}
