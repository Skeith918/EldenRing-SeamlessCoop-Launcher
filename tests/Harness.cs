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
                                            "muted", "warn", "bad", "me3", "classic", "github", "nexus", "manual", "seamless", "files", "deploy", "declared" };
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
        Check(cat.Mods.Count == 11 && seam != null && seam.Core && cat.Mods[0] == seam, "catalogue embarqué : 11 mods, Seamless en tête");
        Check(seam.Settings != null && seam.Settings.Tabs.Count == 3 && seam.Settings.Items.Count() == 16, "réglages Seamless : 3 onglets, 16 clés");
        Check(CatalogMod.VersionMatch("2.7.*", "2.7.2") && !CatalogMod.VersionMatch("2.7.*", "2.8.0") && CatalogMod.VersionMatch("*", "1.0.0")
              && CatalogMod.VersionMatch("2.7.2", "2.7.2") && !CatalogMod.VersionMatch("2.7", "2.7.2"), "comparaison de versions");
        Check(cat.Mods.All(c => c.Supports(gv)), "tous les mods du catalogue vérifiés pour la version " + gv);
        var fix = cat.Find("fix-the-camera");
        var flc = cat.Find("free-lock-on-camera");
        var reason = Json.Loc(Conflicts.Declared(cat, flc.Id, fix.Id).Reason);
        Check(Conflicts.DeclaredFor(cat, fix).Any(c => c.Id == flc.Id) && Conflicts.DeclaredFor(cat, flc).Any(c => c.Id == fix.Id) && reason.Contains("3B62CD"),
              "paire de test (Fix the Camera / Free Lock-On Camera) incompatible dans les deux sens, avec la preuve");
        Check(cat.Mods.All(a => a.Conflicts.All(c => cat.Find(c.Id) != null))
              && cat.Mods.Where(a => a != fix && a != flc).All(a => cat.Mods.All(b => a == b || Conflicts.Declared(cat, a.Id, b.Id) == null)),
              "tous les autres mods du catalogue sont compatibles entre eux");
        Check(cat.Mods.Where(c => c.Source.IsGitHub && c.Source.Tag != null).All(c => c.Source.Sha256 != null), "versions figées : empreinte SHA-256 épinglée");
        var flcMode = flc.Settings.Items.First(i => i.Key == "target_switching_mode");
        Check(flcMode.Type == "choice" && flcMode.Values.Length == 3 && cat.Find("map-for-goblins-expanded").AutoLayout, "choix enregistré par mot, disposition Nexus analysée à l'import");
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

        Console.WriteLine("== Incompatibilités");
        var mFix = Installer.InstallCatalogMod(fix, store, null).Result;
        var mFlc = Installer.InstallCatalogMod(flc, store, null).Result;
        Check(File.Exists(Path.Combine(mFix.Dir, "mods", "CameraFix.dll")) && File.Exists(Path.Combine(mFlc.Dir, "mods", "FreeLockOnCamera", "config.ini")),
              "Fix the Camera et Free Lock-On Camera " + mFlc.Tag + " installés depuis GitHub");
        var map = Conflicts.Compute(store, cat);
        Check(map[flc.Id].Any(c => c.With == fix.Id && c.Kind == "declared") && map[fix.Id].Any(c => c.With == flc.Id), "incompatibilité détectée entre les deux mods installés");
        var off = Conflicts.Resolve(store, cat);
        Check(off.Count == 1 && off[0].Key == mFlc && !mFlc.Enabled && mFix.Enabled, "activés ensemble : le dernier dans l'ordre de chargement est désactivé");
        Check(Conflicts.Blockers(map, store, mFlc).Select(c => c.With).SequenceEqual(new[] { fix.Id }), "Free Lock-On Camera bloqué tant que Fix the Camera est activé");
        mFix.Enabled = false;
        Check(Conflicts.Blockers(map, store, mFlc).Count == 0, "Fix the Camera désactivé : Free Lock-On Camera redevient activable");
        mFlc.Enabled = true;
        Check(Conflicts.Blockers(map, store, mFix).Count == 1 && Conflicts.Resolve(store, cat).Count == 0, "…et c'est alors Fix the Camera qui est bloqué");
        mFlc.Enabled = false; mFix.Enabled = true; store.Save();
        Check(!File.ReadAllText(Me3.WriteProfile(store, "test")).Contains("FreeLockOnCamera.dll"), "profil me3 : jamais les deux mods à la fois");

        var alt = Installer.Import(Zip("Malenia Alt.zip", "Malenia Alt/chr/c2120.texbnd.dcx=y", "Malenia Alt/readme.txt=autre"), cat, store, out an);
        var copy = Installer.Import(Zip("Malenia Copy.zip", "chr/c2120.texbnd.dcx=x", "readme.txt=encore autre"), cat, store, out an);
        map = Conflicts.Compute(store, cat);
        var tc = map[alt.Id].FirstOrDefault(c => c.With == tex.Id);
        Check(tc != null && tc.Kind == "files" && tc.Files.SequenceEqual(new[] { "chr/c2120.texbnd.dcx" }), "deux textures de Malenia différentes : incompatibles (même fichier du jeu)");
        Check(!map[copy.Id].Any(c => c.With == tex.Id), "même fichier au contenu identique, readme différent : compatibles");
        off = Conflicts.Resolve(store, cat);
        Check(off.Count == 1 && off[0].Key == alt && copy.Enabled && tex.Enabled, "la texture concurrente est désactivée, la copie identique reste active");
        var bar = Installer.Import(Zip("Bar.zip", "mods/Bar.dll=a", "mods/Bar/config.ini=[bar]\nx = 1"), cat, store, out an);
        var fork = Installer.Import(Zip("Bar Fork.zip", "mods/Bar.dll=b", "mods/Bar/config.ini=[bar]\nx = 2"), cat, store, out an);
        Check(Conflicts.Compute(store, cat)[fork.Id].Any(c => c.With == bar.Id && c.Kind == "deploy"), "même fichier copié dans le jeu (mods/Bar/config.ini) : incompatibles");
        var mfg = Installer.Import(Zip("Map for Goblins Expanded v1.4.2.zip", "regulation.bin=z", "full_version_to_customize__regulation.bin=zz",
                                       "menu/02_120_worldmap.gfx=m", "script/talk/m00_00_00_00.talkesdbnd.dcx=t"), cat, store, out an);
        Check(mfg.Id == "map-for-goblins-expanded" && mfg.FromCatalog && mfg.Packages.SequenceEqual(new[] { "" }) && mfg.Version == "v1.4.2",
              "mod Nexus du catalogue sans disposition décrite : reconnu par son nom, contenu analysé");
        Check(Conflicts.Compute(store, cat)[mfg.Id].Any(c => c.With == reg.Id && c.Files.Contains("regulation.bin")), "deux regulation.bin différents : incompatibles");
        off = Conflicts.Resolve(store, cat);
        Check(off.Count == 2 && off.Any(o => o.Key == fork) && off.Any(o => o.Key == mfg) && reg.Enabled && bar.Enabled && Conflicts.Resolve(store, cat).Count == 0,
              "après résolution : plus aucun couple incompatible activé");

        Console.WriteLine("== Version publiée sur Nexus avant GitHub");
        Check(Versions.Compare("v2.0.2", "2.0.1") > 0 && Versions.Compare("2.0.1", "v2.0.1") == 0 && Versions.Compare("2.0.10", "2.0.9") > 0
              && Versions.Compare("Binaries", "2022-05") == null, "comparaison de numéros de version");
        Check(Versions.FromFileName("Seamless Co-op v2.0.2.zip") == "v2.0.2" && Versions.FromFileName("Seamless.Co-op.v1.9.8-510-1-9-8-1776128433.zip") == "v1.9.8"
              && Versions.FromFileName("Malenia HD.zip") == null, "version lue dans le nom de l'archive (ancien et nouveau nommage Nexus)");
        var live = Nexus.LatestVersions(new[] { 510 }).Result;
        var liveSeamless = live.ContainsKey(510) ? live[510] : null;
        Check(Versions.Compare(liveSeamless, "1.0") > 0, "version de Seamless sur Nexus lue sans compte : " + liveSeamless + " (GitHub : " + s.Tag + ")");
        Check(Installer.NexusAhead(cat, store, new Dictionary<int, string> { { 510, "99.0" } }).Select(a => a.Key.Id).SequenceEqual(new[] { Seamless.Id })
              && Installer.NexusAhead(cat, store, new Dictionary<int, string> { { 510, s.Version }, { 216, "99.0" } }).Count == 0,
              "Nexus en avance sur GitHub : signalé pour Seamless, jamais pour un mod à version figée");
        var pwd = Ini.Load(Path.Combine(s.Dir, "SeamlessCoop", "ersc_settings.ini")).Values()["cooppassword"];
        Func<string, string> seamlessZip = name => Zip(name, "SeamlessCoop/ersc.dll=x", "SeamlessCoop/ersc_settings.ini=[PASSWORD]\ncooppassword = \n",
                                                       "SeamlessCoop/locale/english.json={\"k\":\"v\"}", "ersc_launcher.exe=x");
        var imp = Installer.Import(seamlessZip("Seamless Co-op v99.0.zip"), cat, store, out an);
        Check(imp.Id == Seamless.Id && imp.Version == "v99.0" && imp.Source == "nexus" && store.Mods.Count(x => x.Id == Seamless.Id) == 1,
              "zip Nexus au nouveau nommage (« Seamless Co-op v99.0.zip ») reconnu par son contenu, sans doublon");
        Check(Ini.Load(Path.Combine(imp.Dir, "SeamlessCoop", "ersc_settings.ini")).Values()["cooppassword"] == pwd && File.Exists(Path.Combine(imp.Dir, "SeamlessCoop", "locale", "french.json")),
              "Seamless importé : mot de passe conservé, traductions générées");
        Check(!Installer.FindUpdates(cat, store).Result.Any(u => u.Key.Id == Seamless.Id), "version importée plus récente que GitHub : jamais remplacée par une plus ancienne");
        Installer.Import(seamlessZip("Seamless Co-op v1.0.zip"), cat, store, out an);
        Check(Installer.FindUpdates(cat, store).Result.Any(u => u.Key.Id == Seamless.Id), "version importée plus ancienne : mise à jour GitHub proposée");
        s = Installer.InstallCatalogMod(seam, store, null).Result;
        Check(s.Source == "github" && s.Version == s.Tag && store.Mods[0] == s, "réinstallé depuis GitHub : suivi normal, toujours chargé en premier");

        Console.WriteLine("== Nexus (en ligne)");
        var found = Nexus.Search("fps", 5).Result;
        Check(found.Count > 0 && found.All(n => n.Id > 0 && n.Name.Length > 0), "recherche « fps » : " + found.Count + " résultats");

        Console.WriteLine("== Interface");
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        known.Enabled = true; // mods vérifiés et non vérifiés actifs…
        store.Save();
        var st = State.Load();
        st.Set("game_version", "2.6.0"); // …et le jeu vient de changer de version
        st.Set("language", "fr");
        st.SetBool("legacy_declined", true);
        st.Set("nexus_notified_" + Seamless.Id, liveSeamless); // l'avis « nouvelle version sur Nexus » est testé à part, à la fin
        st.Save();
        var ui = new MainUi(game);
        var win = Show(ui, 6);
        var reloaded = ModStore.Load();
        Check(Find<Grid>(win, "Overlay").Visibility == Visibility.Visible && !reloaded.Get(reg.Id).Enabled && reloaded.Get("unlock-the-fps").Enabled && reloaded.Get(Seamless.Id).Enabled,
              "après un patch : mod non vérifié désactivé, mods vérifiés (Seamless, Unlock the FPS) gardés, message affiché");
        Render(Find<FrameworkElement>(win, "Root"), "ui_patch_dialog.png");
        Click(Find<Button>(win, "DlgOk"));
        Pump(0.5);
        Check((string)Find<Button>(win, "MainBtn").Content == "JOUER", "bouton JOUER (me3 + Seamless installés)");
        Render(Find<FrameworkElement>(win, "Root"), "ui_play.png");
        Find<RadioButton>(win, "NavMods").IsChecked = true;
        Pump(0.8);
        Render(Find<FrameworkElement>(win, "Root"), "ui_mods_catalog.png");
        Check(Find<Grid>(win, "Overlay").Visibility != Visibility.Visible, "démarrage : aucun couple incompatible à résoudre (déjà fait)");
        Find<RadioButton>(win, "SubInstalled").IsChecked = true;
        Pump(0.5);
        Render(Find<FrameworkElement>(win, "Root"), "ui_mods_installed.png");
        var flcCard = "Free Lock-On Camera " + mFlc.Tag; // nom suivi du badge de version : pas la carte de Fix the Camera, qui le cite
        var flcToggle = FindAll<CheckBox>(Find<StackPanel>(win, "InstalledList")).FirstOrDefault(c => FindAncestorText(c).Contains(flcCard));
        Check(flcToggle != null && !flcToggle.IsEnabled && FindAncestorText(flcToggle).Contains("Bloqué par Fix the Camera"),
              "onglet Installés : Free Lock-On Camera verrouillé, « Bloqué par Fix the Camera » + raison");
        var installedScroll = Find<ScrollViewer>(win, "InstalledScroll");
        installedScroll.ScrollToVerticalOffset(flcToggle == null ? 0 : flcToggle.TranslatePoint(new Point(0, 0), Find<StackPanel>(win, "InstalledList")).Y - 120);
        Pump(0.3);
        Render(Find<FrameworkElement>(win, "Root"), "ui_mods_blocked.png");
        var flcSettings = FindAll<Button>(Find<StackPanel>(win, "InstalledList")).FirstOrDefault(b => (b.Content as string) == "Réglages" && FindAncestorText(b).Contains(flcCard));
        if (flcSettings != null) { Click(flcSettings); Pump(0.5); }
        var sheet = Find<Grid>(win, "Sheet");
        Check(sheet.Visibility == Visibility.Visible && FindAll<TextBlock>(sheet).Any(t => t.Text == "Là où je vise"), "réglages de Free Lock-On Camera : choix lu depuis config.ini (modded_switch)");
        Render(Find<FrameworkElement>(win, "Root"), "ui_flc_settings.png");
        var nextChoice = FindAll<Button>(sheet).FirstOrDefault(b => (b.Content as string) == "›");
        if (nextChoice != null) Click(nextChoice);
        Click(Find<Button>(win, "SheetClose"));
        Check(Ini.Load(Path.Combine(mFlc.Dir, "mods", "FreeLockOnCamera", "config.ini")).Values()["target_switching_mode"] == "vanilla_switch", "choix enregistré sous forme de mot (vanilla_switch)");
        installedScroll.ScrollToTop();
        Pump(0.2);
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

        Console.WriteLine("== Installer un mod incompatible depuis le catalogue (interface en espagnol)");
        Installer.Uninstall(ModStore.Load(), flc.Id, game);
        st = State.Load(); st.Set("language", "es"); st.Save();
        win = Show(new MainUi(game), 4);
        Find<RadioButton>(win, "NavMods").IsChecked = true;
        Pump(0.8);
        var flcInstall = FindAll<Button>(Find<StackPanel>(win, "CatalogList")).FirstOrDefault(b => (b.Content as string) == "Instalar" && FindAncestorText(b).Contains("Free Lock-On Camera"));
        Check(flcInstall != null && FindAncestorText(flcInstall).Contains("Incompatible con Fix the Camera, que está activado"), "catálogo: Free Lock-On Camera marcado incompatible, se instalará desactivado");
        if (flcInstall != null)
        {
            Find<ScrollViewer>(win, "CatalogScroll").ScrollToVerticalOffset(flcInstall.TranslatePoint(new Point(0, 0), Find<StackPanel>(win, "CatalogList")).Y - 200);
            Pump(0.3);
            Render(Find<FrameworkElement>(win, "Root"), "ui_es_catalog_conflict.png");
            OnUi(win, () => Click(flcInstall));
            var t = System.Diagnostics.Stopwatch.StartNew();
            while (Find<Grid>(win, "Overlay").Visibility != Visibility.Visible && t.Elapsed.TotalSeconds < 60) Pump(0.5);
        }
        var afterInstall = ModStore.Load().Get(flc.Id);
        Check(Find<Grid>(win, "Overlay").Visibility == Visibility.Visible && Find<TextBlock>(win, "DlgText").Text.Contains("sigue desactivado") && afterInstall != null && !afterInstall.Enabled,
              "instalado pero desactivado, con la explicación");
        Render(Find<FrameworkElement>(win, "Root"), "ui_es_install_blocked.png");
        Click(Find<Button>(win, "DlgOk"));
        Pump(0.3);
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

        if (Versions.Compare(liveSeamless, s.Version) > 0)
        {
            Console.WriteLine("== Avis : Seamless " + liveSeamless + " sur Nexus, pas encore sur GitHub");
            st = State.Load(); st.Set("language", "fr"); st.Set("nexus_notified_" + Seamless.Id, null); st.Save();
            win = Show(new MainUi(game), 2);
            var wait = System.Diagnostics.Stopwatch.StartNew();
            while (Find<TextBlock>(win, "DlgTitle").Text != "Mise à jour de Seamless Co-op" && wait.Elapsed.TotalSeconds < 45) Pump(0.5);
            Check(Find<Grid>(win, "Overlay").Visibility == Visibility.Visible && Find<TextBlock>(win, "DlgText").Text.Contains("v" + liveSeamless),
                  "avis affiché : version Nexus, attendre GitHub ou importer le .zip");
            Render(Find<FrameworkElement>(win, "Root"), "ui_nexus_notice.png");
            Click(Find<Button>(win, "DlgCancel"));
            Pump(0.3);
            Check(Find<TextBlock>(win, "ModsText").Text.Contains("v" + liveSeamless + " sur Nexus") && State.Load().Get("nexus_notified_" + Seamless.Id) == liveSeamless,
                  "ligne MODS : « v" + liveSeamless + " sur Nexus », avis montré une seule fois par version");
            Render(Find<FrameworkElement>(win, "Root"), "ui_nexus_status.png");
            win.Close();
        }
        else Console.WriteLine("  (Seamless déjà à jour sur GitHub : avis Nexus non testé)");

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
