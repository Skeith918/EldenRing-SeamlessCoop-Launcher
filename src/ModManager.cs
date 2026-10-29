using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ERSeamless
{
    /// <summary>Un mod installé dans %LOCALAPPDATA%\ERSeamlessLauncher\mods\&lt;id&gt; (le dossier du jeu n'est pas touché).</summary>
    public class InstalledMod
    {
        public string Id, Name, Author = "", Version = "", Source = "manual", Repo, Tag, AssetSha, InstalledAt, SettingsFile;
        public int Nexus;
        public bool Enabled = true, FromCatalog;
        public List<string> Natives = new List<string>(), Packages = new List<string>(), TestedOn = new List<string>(), Deployed = new List<string>();
        public List<KeyValuePair<string, string>> Deploy = new List<KeyValuePair<string, string>>();

        public string Dir { get { return Path.Combine(Paths.ModsRoot, Id); } }

        public Dictionary<string, object> ToJson()
        {
            return new Dictionary<string, object>
            {
                { "id", Id }, { "name", Name }, { "author", Author }, { "version", Version }, { "source", Source },
                { "repo", Repo }, { "tag", Tag }, { "assetSha", AssetSha }, { "installedAt", InstalledAt },
                { "settingsFile", SettingsFile }, { "nexus", Nexus }, { "enabled", Enabled }, { "fromCatalog", FromCatalog },
                { "natives", Natives }, { "packages", Packages }, { "testedOn", TestedOn }, { "deployed", Deployed },
                { "deploy", Deploy.Select(kv => new Dictionary<string, object> { { "from", kv.Key }, { "to", kv.Value } }).ToList() }
            };
        }

        public static InstalledMod FromJson(Dictionary<string, object> d)
        {
            var m = new InstalledMod
            {
                Id = Json.Str(d, "id"), Name = Json.Str(d, "name"), Author = Json.Str(d, "author", ""), Version = Json.Str(d, "version", ""),
                Source = Json.Str(d, "source", "manual"), Repo = Json.Str(d, "repo"), Tag = Json.Str(d, "tag"), AssetSha = Json.Str(d, "assetSha"),
                InstalledAt = Json.Str(d, "installedAt"), SettingsFile = Json.Str(d, "settingsFile"), Nexus = Json.Int(d, "nexus", 0),
                Enabled = Json.Bool(d, "enabled", true), FromCatalog = Json.Bool(d, "fromCatalog", false),
                Natives = Json.Strs(d, "natives").ToList(), Packages = Json.Strs(d, "packages").ToList(),
                TestedOn = Json.Strs(d, "testedOn").ToList(), Deployed = Json.Strs(d, "deployed").ToList()
            };
            foreach (var o in Json.Objs(d, "deploy"))
                m.Deploy.Add(new KeyValuePair<string, string>(Json.Str(o, "from"), Json.Str(o, "to")));
            return string.IsNullOrEmpty(m.Id) ? null : m;
        }
    }

    /// <summary>Liste des mods installés (mods.json) ; l'ordre de la liste est l'ordre de chargement.</summary>
    public class ModStore
    {
        public List<InstalledMod> Mods = new List<InstalledMod>();

        public static ModStore Load()
        {
            var s = new ModStore();
            try
            {
                if (File.Exists(Paths.ModsFile))
                    foreach (var o in Json.Objs(Json.ParseObject(File.ReadAllText(Paths.ModsFile, Encoding.UTF8)), "mods"))
                    {
                        var m = InstalledMod.FromJson(o);
                        if (m != null && s.Get(m.Id) == null) s.Mods.Add(m);
                    }
            }
            catch (Exception ex) { Log.Write("mods.json: " + ex.Message); }
            return s;
        }

        public void Save()
        {
            Directory.CreateDirectory(Paths.AppData);
            var tmp = Paths.ModsFile + ".tmp";
            File.WriteAllText(tmp, Json.Write(new Dictionary<string, object> { { "mods", Mods.Select(m => m.ToJson()).ToList() } }), new UTF8Encoding(false));
            if (File.Exists(Paths.ModsFile)) File.Replace(tmp, Paths.ModsFile, null);
            else File.Move(tmp, Paths.ModsFile);
        }

        public InstalledMod Get(string id)
        {
            return Mods.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public bool Move(string id, int delta)
        {
            var m = Get(id);
            if (m == null) return false;
            int i = Mods.IndexOf(m), j = i + delta;
            if (j < 0 || j >= Mods.Count) return false;
            Mods.RemoveAt(i);
            Mods.Insert(j, m);
            return true;
        }
    }

    public static class Files
    {
        public static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var f = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "");
        }

        /// <summary>Chemin relatif avec des « / ».</summary>
        public static string Rel(string root, string full)
        {
            return full.Substring(root.TrimEnd('\\').Length + 1).Replace('\\', '/');
        }

        public static string Native(string rel)
        {
            return (rel ?? "").Replace('/', '\\');
        }

        public static string TempDir()
        {
            var d = Path.Combine(Paths.AppData, ".tmp", Guid.NewGuid().ToString("N").Substring(0, 12));
            Directory.CreateDirectory(d);
            return d;
        }

        /// <summary>Extrait une archive .zip dans un dossier temporaire (protégé contre les chemins « ../ »).</summary>
        public static string ExtractToTemp(string zipPath)
        {
            var dir = TempDir();
            var root = Path.GetFullPath(dir).TrimEnd('\\') + "\\";
            using (var zip = ZipFile.OpenRead(zipPath))
                foreach (var e in zip.Entries)
                {
                    var dest = Path.GetFullPath(Path.Combine(root, e.FullName.Replace('/', '\\')));
                    if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                    if (e.Name.Length == 0) { Directory.CreateDirectory(dest); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    e.ExtractToFile(dest, true);
                }
            return dir;
        }

        public static void CopyDir(string from, string to)
        {
            Directory.CreateDirectory(to);
            foreach (var f in Directory.GetFiles(from, "*", SearchOption.AllDirectories))
            {
                var dest = Path.Combine(to, f.Substring(from.TrimEnd('\\').Length + 1));
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                File.Copy(f, dest, true);
            }
        }

        public static void DeleteDir(string dir)
        {
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories)) File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(dir, true);
        }

        /// <summary>
        /// Remplace le dossier to par from. L'ancien dossier n'est supprimé qu'une fois le nouveau en place : si le déplacement
        /// échoue (antivirus qui analyse un fichier extrait), l'ancienne version est remise et l'erreur remonte.
        /// </summary>
        public static void Swap(string from, string to)
        {
            var old = to.TrimEnd('\\') + ".old";
            if (Directory.Exists(old)) DeleteDir(old); // reste d'un remplacement précédent
            if (Directory.Exists(to)) Directory.Move(to, old);
            try { Directory.Move(from, to); }
            catch
            {
                try { if (Directory.Exists(old)) Directory.Move(old, to); }
                catch (Exception ex) { Log.Write("swap restore " + to + ": " + ex.Message); }
                throw;
            }
            try { DeleteDir(old); }
            catch (Exception ex) { Log.Write("swap cleanup " + old + ": " + ex.Message); }
        }

        public static void CleanTemp()
        {
            try { DeleteDir(Path.Combine(Paths.AppData, ".tmp")); } catch { }
        }

        /// <summary>Supprime les dossiers parents devenus vides, sans remonter au-delà de root.</summary>
        public static void RemoveEmptyParents(string root, string file)
        {
            var stop = Path.GetFullPath(root).TrimEnd('\\');
            var d = Path.GetDirectoryName(Path.GetFullPath(file));
            while (d != null && d.Length > stop.Length && d.StartsWith(stop, StringComparison.OrdinalIgnoreCase))
            {
                if (Directory.Exists(d) && !Directory.EnumerateFileSystemEntries(d).Any()) Directory.Delete(d);
                else break;
                d = Path.GetDirectoryName(d);
            }
        }

        public static bool Same(string a, string b)
        {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            return fa.Exists && fb.Exists && fa.Length == fb.Length && fa.LastWriteTimeUtc == fb.LastWriteTimeUtc;
        }
    }

    public static class GitHub
    {
        public class Asset
        {
            public string Name, Url, Sha256;
            public long Size;
        }

        public class Release
        {
            public string Tag, Page, Notes;
            public List<Asset> Assets = new List<Asset>();

            public Asset Find(string pattern)
            {
                var rx = new Regex(string.IsNullOrEmpty(pattern) ? @"\.zip$" : pattern, RegexOptions.IgnoreCase);
                return Assets.FirstOrDefault(a => rx.IsMatch(a.Name));
            }

            public string Summary
            {
                get { return (Notes ?? "").Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0) ?? ""; }
            }
        }

        static readonly Dictionary<string, Release> cache = new Dictionary<string, Release>();

        public static void ClearCache()
        {
            lock (cache) cache.Clear();
        }

        /// <summary>Release d'un dépôt (dernière si tag est null). Mise en cache pour la session.</summary>
        public static async Task<Release> GetRelease(string repo, string tag)
        {
            var key = repo + "|" + (tag ?? "latest");
            lock (cache) if (cache.ContainsKey(key)) return cache[key];
            string json;
            using (var wc = Program.Web())
            {
                wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                json = await wc.DownloadStringTaskAsync("https://api.github.com/repos/" + repo + "/releases/" + (tag == null ? "latest" : "tags/" + Uri.EscapeDataString(tag)));
            }
            var root = Json.ParseObject(json);
            var r = new Release { Tag = Json.Str(root, "tag_name"), Page = Json.Str(root, "html_url"), Notes = Json.Str(root, "body", "") };
            foreach (var a in Json.Objs(root, "assets"))
            {
                var digest = Json.Str(a, "digest");
                long size;
                long.TryParse(Json.Str(a, "size", "0"), out size);
                r.Assets.Add(new Asset
                {
                    Name = Json.Str(a, "name", ""),
                    Url = Json.Str(a, "browser_download_url"),
                    Size = size,
                    Sha256 = digest != null && digest.StartsWith("sha256:") ? digest.Substring(7) : null
                });
            }
            if (string.IsNullOrEmpty(r.Tag)) throw new UserError(L.T("err_no_zip"));
            lock (cache) cache[key] = r;
            return r;
        }

        /// <summary>Télécharge un fichier de release et vérifie son SHA-256 (celui épinglé dans le catalogue, sinon celui publié par GitHub).</summary>
        public static async Task<string> Download(Asset asset, string pinnedSha, Action<int> progress)
        {
            if (asset.Url == null || !new Uri(asset.Url).Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
                throw new UserError(L.F("err_bad_host", asset.Url == null ? "?" : new Uri(asset.Url).Host));
            var path = Path.Combine(Files.TempDir(), Regex.Replace(asset.Name, @"[^\w\.\-]", "_"));
            using (var wc = Program.Web())
            {
                if (progress != null) wc.DownloadProgressChanged += (s, e) => progress(e.ProgressPercentage);
                await wc.DownloadFileTaskAsync(new Uri(asset.Url), path);
            }
            var expected = pinnedSha ?? asset.Sha256;
            if (expected != null)
            {
                var sha = await Task.Run(() => Files.Sha256(path));
                if (!sha.Equals(expected, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(path);
                    throw new UserError(L.T("err_sha"));
                }
            }
            return path;
        }
    }

    /// <summary>Numéros de version (« v2.0.1 », « 2.0.2 ») : comparaison et lecture dans un nom de fichier.</summary>
    public static class Versions
    {
        static readonly Regex Number = new Regex(@"^\s*v?(\d+(?:\.\d+)*)\s*$", RegexOptions.IgnoreCase);

        static int[] Parse(string v)
        {
            var m = Number.Match(v ?? "");
            if (!m.Success) return null;
            var parts = new List<int>();
            foreach (var s in m.Groups[1].Value.Split('.'))
            {
                int n;
                if (!int.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out n)) return null;
                parts.Add(n);
            }
            return parts.ToArray();
        }

        /// <summary>Positif si a est plus récente que b ; null si l'une des deux n'est pas un numéro de version (« Binaries », « 2022-05 »).</summary>
        public static int? Compare(string a, string b)
        {
            var x = Parse(a);
            var y = Parse(b);
            if (x == null || y == null) return null;
            for (int i = 0; i < Math.Max(x.Length, y.Length); i++)
            {
                int p = i < x.Length ? x[i] : 0, q = i < y.Length ? y[i] : 0;
                if (p != q) return p.CompareTo(q);
            }
            return 0;
        }

        /// <summary>Version d'une archive : nommage Nexus actuel ou ancien (« Nom-510-2-0-2-1761575432 »), sinon « Seamless Co-op v2.0.2 ».</summary>
        public static string FromFileName(string file)
        {
            var name = Path.GetFileNameWithoutExtension((file ?? "").TrimEnd('\\')) ?? "";
            var nx = Installer.NexusName.Match(name);
            if (nx.Success) return "v" + nx.Groups[3].Value.TrimStart('v', 'V');
            var m = Regex.Match(name, @"-\d+-(\d+(?:-\d+)*)-\d{9,}$");
            if (m.Success) return "v" + m.Groups[1].Value.Replace('-', '.');
            m = Regex.Match(name, @"(?:^|[\s_\.\-])v(\d+(?:\.\d+)+)", RegexOptions.IgnoreCase);
            if (!m.Success) m = Regex.Match(name, @"[\s_\-](\d+(?:\.\d+)+)$");
            return m.Success ? "v" + m.Groups[1].Value : null;
        }
    }

    /// <summary>Résultat de l'analyse d'un mod importé à la main (.zip ou dossier).</summary>
    public class ModAnalysis
    {
        public List<string> Natives = new List<string>(), Packages = new List<string>();
        public List<KeyValuePair<string, string>> Deploy = new List<KeyValuePair<string, string>>();
        public string SettingsFile;
        public bool LoaderIgnored;

        public bool Empty { get { return Natives.Count == 0 && Packages.Count == 0 && Deploy.Count == 0; } }
    }

    public static class Installer
    {
        /// <summary>Dossiers de premier niveau des archives du jeu : leur présence signale un mod de fichiers.</summary>
        public static readonly string[] GameDirs =
        {
            "action", "asset", "chr", "cutscene", "event", "expression", "font", "map", "material", "menu", "movie",
            "msg", "obj", "other", "param", "parts", "script", "sd", "sfx", "shader", "sound"
        };
        static readonly string[] LoaderDlls = { "dinput8.dll", "modengine2.dll", "modengine2_launcher.dll", "me3_mod_host.dll" };
        static readonly string[] ProxyDlls = { "dxgi.dll", "d3d11.dll", "d3d12.dll", "winmm.dll", "version.dll", "xinput1_3.dll", "xinput1_4.dll", "dsound.dll" };
        static readonly string[] GameDlls = { "steam_api64.dll", "oo2core_6_win64.dll", "bink2w64.dll", "amd_ags_x64.dll", "eossdk-win64-shipping.dll" };

        /// <summary>Installe ou met à jour un mod du catalogue hébergé sur GitHub.</summary>
        public static async Task<InstalledMod> InstallCatalogMod(CatalogMod cm, ModStore store, Action<int> progress)
        {
            if (!cm.Source.IsGitHub) throw new UserError(L.F("err_not_downloadable", cm.Name));
            var rel = await GitHub.GetRelease(cm.Source.Repo, cm.Source.Tag);
            var asset = rel.Find(cm.Source.Asset);
            if (asset == null) throw new UserError(L.F("err_no_asset", cm.Name));
            var zip = await GitHub.Download(asset, cm.Source.Sha256, progress);
            try
            {
                var sha = await Task.Run(() => Files.Sha256(zip));
                var existing = store.Get(cm.Id);
                var m = existing ?? new InstalledMod { Id = cm.Id };
                await Task.Run(() =>
                {
                    var dir = Files.ExtractToTemp(zip);
                    Require(dir, cm.Natives.Concat(cm.Deploy.Select(kv => kv.Key)), L.T("err_archive"));
                    Place(dir, m, cm.Settings);
                    if (cm.Natives.Any(p => !File.Exists(Path.Combine(m.Dir, Files.Native(p))))) throw new UserError(L.T("err_antivirus"));
                });
                ApplyCatalog(m, cm);
                m.Source = "github";
                m.Repo = cm.Source.Repo;
                m.Tag = rel.Tag;
                m.Version = cm.Version ?? rel.Tag;
                m.AssetSha = sha;
                m.InstalledAt = DateTime.Now.ToString("s");
                if (cm.Special == "seamless") Seamless.AfterInstall(m);
                if (existing == null)
                {
                    if (cm.Core) store.Mods.Insert(0, m);
                    else store.Mods.Add(m);
                }
                store.Save();
                Log.Write("installed " + m.Id + " " + m.Version);
                return m;
            }
            finally
            {
                try { File.Delete(zip); } catch { }
                Files.CleanTemp();
            }
        }

        /// <summary>Chaque chemin (fichier ou dossier) doit exister dans dir, sinon erreur affichée telle quelle.</summary>
        static void Require(string dir, IEnumerable<string> rels, string error)
        {
            foreach (var p in rels)
                if (!File.Exists(Path.Combine(dir, Files.Native(p))) && !Directory.Exists(Path.Combine(dir, Files.Native(p))))
                    throw new UserError(error);
        }

        /// <summary>Disposition déduite de l'archive ; le fichier de réglages du catalogue reste prioritaire.</summary>
        static void ApplyAnalysis(InstalledMod m, ModAnalysis a)
        {
            m.Natives = a.Natives;
            m.Packages = a.Packages;
            m.Deploy = a.Deploy;
            if (m.SettingsFile == null) m.SettingsFile = a.SettingsFile;
        }

        static void ApplyCatalog(InstalledMod m, CatalogMod cm)
        {
            m.Name = cm.Name;
            m.Author = cm.Author;
            m.Nexus = cm.Nexus;
            m.FromCatalog = true;
            m.Natives = cm.Natives.ToList();
            m.Packages = cm.Packages.ToList();
            m.Deploy = cm.Deploy.ToList();
            m.SettingsFile = cm.Settings != null ? cm.Settings.File : null;
        }

        /// <summary>Remplace le dossier du mod par dir en gardant les réglages de l'utilisateur.</summary>
        static void Place(string dir, InstalledMod m, SettingsSchema schema)
        {
            var settingsRel = schema != null ? schema.File : m.SettingsFile;
            Dictionary<string, string> previous = null;
            if (settingsRel != null)
            {
                var old = Ini.Load(Path.Combine(m.Dir, Files.Native(settingsRel)));
                if (old != null) previous = old.Values();
            }
            Directory.CreateDirectory(Paths.ModsRoot);
            Files.Swap(dir, m.Dir);
            if (settingsRel != null) ModSettings.Restore(m.Id, Path.Combine(m.Dir, Files.Native(settingsRel)), schema, previous);
        }

        /// <summary>Déduit les DLL, dossiers de fichiers de jeu et fichiers à copier dans le jeu d'un mod quelconque.</summary>
        public static ModAnalysis Analyze(string root)
        {
            var a = new ModAnalysis();
            var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories).Select(f => Files.Rel(root, f)).ToList();
            var dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "" };
            foreach (var f in files)
                for (var d = Parent(f); d != null; d = d.Length == 0 ? null : Parent(d)) dirs.Add(d);

            // dossiers de fichiers de jeu (regulation.bin ou chr/, parts/…) : on garde les moins profonds
            var roots = dirs.Where(d => files.Contains(Join(d, "regulation.bin"), StringComparer.OrdinalIgnoreCase)
                                        || GameDirs.Any(g => dirs.Contains(Join(d, g))))
                            .OrderBy(d => d.Length).ToList();
            foreach (var r in roots)
                if (!a.Packages.Any(p => p.Length == 0 || r.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase))) a.Packages.Add(r);

            foreach (var f in files.Where(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)))
            {
                var name = Path.GetFileName(f).ToLowerInvariant();
                if (Parent(f).Split('/').Any(seg => GameDirs.Contains(seg.ToLowerInvariant()))) continue; // DLL rangée dans des données du jeu
                if (LoaderDlls.Contains(name)) { a.LoaderIgnored = true; continue; }
                if (GameDlls.Contains(name)) continue;
                if (ProxyDlls.Contains(name)) { a.Deploy.Add(new KeyValuePair<string, string>(f, name)); continue; }
                a.Natives.Add(f);
                // convention Elden Mod Loader : mods\X.dll lit mods\X\config.ini relatif au dossier du jeu
                var folder = Join(Parent(f), Path.GetFileNameWithoutExtension(f));
                if (Parent(f).EndsWith("mods", StringComparison.OrdinalIgnoreCase) && dirs.Contains(folder))
                    a.Deploy.Add(new KeyValuePair<string, string>(folder, "mods/" + Path.GetFileNameWithoutExtension(f)));
            }

            a.SettingsFile = files.Where(f => f.EndsWith(".ini", StringComparison.OrdinalIgnoreCase)
                                              && !Path.GetFileName(f).Equals("desktop.ini", StringComparison.OrdinalIgnoreCase)
                                              && !a.Packages.Any(p => p.Length > 0 && f.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase)))
                                  .OrderBy(f => f.Count(c => c == '/')).FirstOrDefault();
            return a;
        }

        static string Parent(string rel)
        {
            int i = rel.LastIndexOf('/');
            return i < 0 ? "" : rel.Substring(0, i);
        }

        static string Join(string dir, string name)
        {
            return dir.Length == 0 ? name : dir + "/" + name;
        }

        /// <summary>
        /// Nommage actuel des téléchargements Nexus : « Nom &lt;modId&gt; &lt;version&gt; &lt;date&gt; &lt;code&gt;.zip »,
        /// ex. « Seamless Co-op v2.0.2 510 2.0.2 2026-10-27T14-30Z gn8dtBhTy », avec « (1) » si le navigateur l'a déjà.
        /// </summary>
        internal static readonly Regex NexusName = new Regex(@"^(.+) (\d+) (\S+) \d{4}-\d{2}-\d{2}T[\d\-]+Z \S+?(?: \(\d+\))?$");

        /// <summary>Numéro Nexus du mod : nommage actuel, ou ancien « Nom-&lt;modId&gt;-&lt;version&gt;-&lt;horodatage&gt;.zip ».</summary>
        public static int NexusIdFromFileName(string file)
        {
            var name = Path.GetFileNameWithoutExtension(file) ?? "";
            var m = NexusName.Match(name);
            var digits = m.Success ? m.Groups[2].Value : Regex.Match(name, @"-(\d+)-[\d\-]*?-?\d{9,}$").Groups[1].Value;
            int id;
            return int.TryParse(digits, out id) ? id : 0;
        }

        public static string CleanName(string file)
        {
            var n = Path.GetFileNameWithoutExtension(file.TrimEnd('\\')) ?? "mod";
            var m = NexusName.Match(n);
            n = m.Success ? m.Groups[1].Value : Regex.Replace(n, @"-\d+-[\d\-]*?-?\d{9,}$", "");
            return n.Replace('_', ' ').Trim();
        }

        static string Slug(string name, ModStore store)
        {
            var s = Regex.Replace(name.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            if (s.Length == 0) s = "mod";
            if (s.Length > 40) s = s.Substring(0, 40).Trim('-');
            var id = s;
            for (int i = 2; store.Get(id) != null || id == Seamless.Id; i++) id = s + "-" + i;
            return id;
        }

        static string Squash(string s)
        {
            return Regex.Replace((s ?? "").ToLowerInvariant(), "[^a-z0-9]", "");
        }

        /// <summary>
        /// Entrée du catalogue correspondant à un mod importé : numéro Nexus du nom de fichier (ancien nommage), sinon mêmes DLL
        /// que celles décrites par le catalogue, sinon nom du fichier (mods Nexus dont le catalogue ne décrit pas les fichiers).
        /// </summary>
        public static CatalogMod Recognize(string path, Catalog catalog, ModAnalysis analysis)
        {
            if (catalog == null) return null;
            var cm = catalog.FindNexus(NexusIdFromFileName(path));
            if (cm != null) return cm;
            var dlls = new HashSet<string>(analysis.Natives, StringComparer.OrdinalIgnoreCase);
            cm = catalog.Mods.FirstOrDefault(c => !c.AutoLayout && c.Natives.Length > 0 && dlls.SetEquals(c.Natives));
            if (cm != null) return cm;
            var name = Squash(CleanName(path));
            return catalog.Mods.Where(c => c.AutoLayout && c.Nexus > 0 && Squash(c.Name).Length > 0 && name.StartsWith(Squash(c.Name)))
                               .OrderByDescending(c => c.Name.Length).FirstOrDefault();
        }

        /// <summary>
        /// Importe un mod téléchargé à la main (.zip ou dossier). S'il est reconnu comme un mod du catalogue, la disposition et
        /// les réglages du catalogue sont utilisés ; sinon le contenu est analysé.
        /// </summary>
        public static InstalledMod Import(string path, Catalog catalog, ModStore store, out ModAnalysis analysis)
        {
            bool isDir = Directory.Exists(path);
            if (!isDir && !path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) throw new UserError(L.T("err_archive_format"));
            string dir;
            if (isDir) { dir = Files.TempDir(); Files.CopyDir(path, dir); }
            else dir = Files.ExtractToTemp(path);
            try
            {
                analysis = Analyze(dir);
                var cm = Recognize(path, catalog, analysis);
                InstalledMod m;
                if (cm != null)
                {
                    m = store.Get(cm.Id) ?? new InstalledMod { Id = cm.Id };
                    if (cm.AutoLayout && analysis.Empty) throw new UserError(L.F("err_import_mismatch", cm.Name));
                    Require(dir, cm.Natives.Concat(cm.Packages).Concat(cm.Deploy.Select(kv => kv.Key)), L.F("err_import_mismatch", cm.Name));
                    Place(dir, m, cm.Settings);
                    ApplyCatalog(m, cm);
                    if (cm.AutoLayout) ApplyAnalysis(m, analysis); // métadonnées du catalogue
                    m.Source = "nexus";
                    m.Tag = null;
                    m.Version = Versions.FromFileName(path) ?? cm.Version ?? "";
                    if (cm.Special == "seamless") Seamless.AfterInstall(m);
                }
                else
                {
                    if (analysis.Empty) throw new UserError(L.T("err_import_empty"));
                    var name = CleanName(path);
                    m = new InstalledMod { Id = Slug(name, store), Name = name, Source = "manual", Version = Versions.FromFileName(path) ?? "" };
                    Place(dir, m, null);
                    ApplyAnalysis(m, analysis);
                    m.Nexus = NexusIdFromFileName(path);
                }
                m.AssetSha = isDir ? null : Files.Sha256(path);
                m.InstalledAt = DateTime.Now.ToString("s");
                if (store.Get(m.Id) == null) store.Mods.Add(m);
                store.Save();
                Log.Write("imported " + m.Id + " from " + Path.GetFileName(path));
                return m;
            }
            finally { Files.CleanTemp(); }
        }

        public static void Uninstall(ModStore store, string id, string gameDir)
        {
            var m = store.Get(id);
            if (m == null) return;
            m.Enabled = false;
            if (Game.IsValid(gameDir)) SyncDeploy(store, gameDir, false);
            Files.DeleteDir(m.Dir);
            store.Mods.Remove(m);
            store.Save();
            Log.Write("uninstalled " + id);
        }

        /// <summary>
        /// Aligne les fichiers copiés dans le dossier du jeu sur les mods actifs : ajoute ce qui manque, retire ce qui n'est plus
        /// voulu. Un fichier du jeu qui n'a pas été posé par le launcher n'est jamais écrasé. classicSeamless : Seamless entier
        /// copié dans le jeu pour un lancement par ersc_launcher.exe.
        /// </summary>
        public static List<string> SyncDeploy(ModStore store, string gameDir, bool classicSeamless)
        {
            var warnings = new List<string>();
            var wanted = new Dictionary<string, KeyValuePair<InstalledMod, string>>(StringComparer.OrdinalIgnoreCase);
            Action<InstalledMod, string, string> want = (m, src, rel) => { if (!wanted.ContainsKey(rel)) wanted[rel] = new KeyValuePair<InstalledMod, string>(m, src); };
            foreach (var m in store.Mods.Where(x => x.Enabled))
                foreach (var kv in m.Deploy)
                {
                    var src = Path.Combine(m.Dir, Files.Native(kv.Key));
                    if (Directory.Exists(src))
                        foreach (var f in Directory.GetFiles(src, "*", SearchOption.AllDirectories)) want(m, f, kv.Value.TrimEnd('/') + "/" + Files.Rel(src, f));
                    else if (File.Exists(src)) want(m, src, kv.Value);
                }
            var seamless = store.Get(Seamless.Id);
            if (classicSeamless && seamless != null && seamless.Enabled)
                foreach (var f in Directory.GetFiles(seamless.Dir, "*", SearchOption.AllDirectories)) want(seamless, f, Files.Rel(seamless.Dir, f));

            foreach (var m in store.Mods)
                foreach (var rel in m.Deployed.ToList())
                {
                    KeyValuePair<InstalledMod, string> w;
                    if (wanted.TryGetValue(rel, out w) && w.Key == m) continue;
                    var target = Path.Combine(gameDir, Files.Native(rel));
                    try
                    {
                        if (File.Exists(target)) File.Delete(target);
                        Files.RemoveEmptyParents(gameDir, target);
                        m.Deployed.Remove(rel);
                    }
                    catch (Exception ex) { warnings.Add(rel + " : " + ex.Message); }
                }

            foreach (var kv in wanted)
            {
                var owner = kv.Value.Key;
                var target = Path.Combine(gameDir, Files.Native(kv.Key));
                bool tracked = owner.Deployed.Contains(kv.Key, StringComparer.OrdinalIgnoreCase);
                bool legacySeamless = classicSeamless && owner.Id == Seamless.Id; // fichiers posés par le launcher v1.0
                if (File.Exists(target) && !tracked && !legacySeamless) { warnings.Add(L.F("warn_deploy_exists", kv.Key, owner.Name)); continue; }
                if (!Files.Same(kv.Value.Value, target))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(kv.Value.Value, target, true);
                }
                if (!tracked) owner.Deployed.Add(kv.Key);
            }
            store.Save();
            return warnings;
        }

        /// <summary>
        /// Mods installés dont une nouvelle version existe sur GitHub. Un mod importé depuis Nexus reste suivi : il est mis à jour
        /// dès que GitHub publie une version plus récente que la sienne, jamais vers une plus ancienne.
        /// </summary>
        public static async Task<List<KeyValuePair<InstalledMod, CatalogMod>>> FindUpdates(Catalog catalog, ModStore store)
        {
            var result = new List<KeyValuePair<InstalledMod, CatalogMod>>();
            // une requête par dépôt, toutes en même temps : la boucle ci-dessous lit ensuite le cache (erreurs journalisées par la boucle)
            var sources = store.Mods.Where(m => m.Source == "github" || m.Source == "nexus")
                                    .Select(m => catalog.Find(m.Id)).Where(cm => cm != null && cm.Source.IsGitHub)
                                    .GroupBy(cm => cm.Source.Repo + "|" + cm.Source.Tag).Select(g => g.First().Source);
            await Task.WhenAll(sources.Select(async s => { try { await GitHub.GetRelease(s.Repo, s.Tag); } catch { } }));
            foreach (var m in store.Mods.ToList())
            {
                var cm = catalog.Find(m.Id);
                if (cm == null || !cm.Source.IsGitHub || (m.Source != "github" && m.Source != "nexus")) continue;
                try
                {
                    var rel = await GitHub.GetRelease(cm.Source.Repo, cm.Source.Tag);
                    var asset = rel.Find(cm.Source.Asset);
                    if (asset == null) continue;
                    var sha = cm.Source.Sha256 ?? asset.Sha256;
                    var order = Versions.Compare(rel.Tag, m.Version);
                    bool newer = m.Source == "github"
                        ? rel.Tag != m.Tag || (sha != null && m.AssetSha != null && !sha.Equals(m.AssetSha, StringComparison.OrdinalIgnoreCase))
                        : order.HasValue && order.Value > 0;
                    if (newer && !(order.HasValue && order.Value < 0))
                        result.Add(new KeyValuePair<InstalledMod, CatalogMod>(m, cm));
                }
                catch (Exception ex) { Log.Write("update check " + m.Id + ": " + ex.Message); }
            }
            return result;
        }

        /// <summary>
        /// Mods du catalogue pour lesquels Nexus annonce une version plus récente que celle installée : mods venant de Nexus,
        /// ou mods GitHub dont le dépôt n'est qu'un miroir de Nexus en retard. Jamais pour une version figée (tag).
        /// </summary>
        public static List<KeyValuePair<InstalledMod, string>> NexusAhead(Catalog catalog, ModStore store, IDictionary<int, string> nexusVersions)
        {
            var result = new List<KeyValuePair<InstalledMod, string>>();
            foreach (var m in store.Mods)
            {
                var cm = catalog.Find(m.Id);
                string v;
                if (cm == null || cm.Nexus <= 0 || cm.Source.Tag != null || !nexusVersions.TryGetValue(cm.Nexus, out v)) continue;
                var order = Versions.Compare(v, m.Version);
                if (order.HasValue && order.Value > 0) result.Add(new KeyValuePair<InstalledMod, string>(m, v));
            }
            return result;
        }
    }

    /// <summary>Réglages des mods au format INI, avec une copie dans %LOCALAPPDATA% qui survit aux réinstallations.</summary>
    public static class ModSettings
    {
        public static string CopyPath(string modId)
        {
            return Path.Combine(Paths.SettingsDir, modId + ".ini");
        }

        public static string FilePath(InstalledMod m, string relFile)
        {
            return m == null || relFile == null ? null : Path.Combine(m.Dir, Files.Native(relFile));
        }

        /// <summary>Défauts du schéma, puis fichier du mod, sinon copie sauvegardée, sinon anciens emplacements.</summary>
        public static Dictionary<string, string> Load(string modId, InstalledMod m, SettingsSchema s, params string[] legacyFiles)
        {
            var v = s.Defaults();
            var src = m != null ? Ini.Load(FilePath(m, s.File)) : null;
            if (src == null) src = Ini.Load(CopyPath(modId));
            foreach (var f in legacyFiles)
                if (src == null && f != null) src = Ini.Load(f);
            if (src != null)
                foreach (var kv in src.Values()) v[kv.Key] = kv.Value;
            return v;
        }

        public static void Save(string modId, InstalledMod m, SettingsSchema s, IDictionary<string, string> values)
        {
            Directory.CreateDirectory(Paths.SettingsDir);
            var copy = Ini.Load(CopyPath(modId)) ?? new Ini(null);
            Apply(copy, s, values);
            copy.Save(CopyPath(modId));
            var path = FilePath(m, s.File);
            if (path == null || !Directory.Exists(m.Dir)) return;
            var ini = Ini.Load(path) ?? new Ini(null);
            Apply(ini, s, values);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            ini.Save(path);
        }

        public static void Apply(Ini ini, SettingsSchema s, IDictionary<string, string> values)
        {
            foreach (var it in s.Items)
            {
                string v;
                if (values.TryGetValue(it.Key, out v)) ini.Set(it.Section, it.Key, Sanitize(it, v));
            }
        }

        public static string Sanitize(SettingItem it, string v)
        {
            v = v ?? "";
            if (it.Sanitize == "alnum")
            {
                v = new string(v.Where(char.IsLetterOrDigit).ToArray());
                if (v.Length == 0) v = it.Default ?? "";
            }
            return v.Length > it.MaxLength ? v.Substring(0, it.MaxLength) : v;
        }

        /// <summary>Après installation : réapplique les valeurs précédentes puis celles sauvegardées par le launcher.</summary>
        public static void Restore(string modId, string file, SettingsSchema schema, Dictionary<string, string> previous)
        {
            var ini = Ini.Load(file);
            if (ini == null) return;
            if (previous != null)
                foreach (var kv in previous)
                    if (ini.Has(kv.Key)) ini.Set("GENERAL", kv.Key, kv.Value);
            var copy = Ini.Load(CopyPath(modId));
            if (copy != null)
                foreach (var kv in copy.Values())
                {
                    var it = schema == null ? null : schema.Items.FirstOrDefault(i => i.Key.Equals(kv.Key, StringComparison.OrdinalIgnoreCase));
                    if (it != null || ini.Has(kv.Key)) ini.Set(it != null ? it.Section : "GENERAL", kv.Key, kv.Value);
                }
            ini.Save(file);
        }
    }

    /// <summary>Spécificités de Seamless Co-op : traductions du mod et ancienne installation dans le dossier du jeu.</summary>
    public static class Seamless
    {
        public const string Id = "seamless-coop";
        public static readonly string[] Locales = { "french", "spanish", "latam" };

        public static string Folder(InstalledMod m) { return Path.Combine(m.Dir, "SeamlessCoop"); }

        public static void AfterInstall(InstalledMod m)
        {
            var dir = Path.Combine(Folder(m), "locale");
            var shipped = Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*.json").Select(Path.GetFileNameWithoutExtension).ToList()
                : new List<string>();
            try { WriteLocales(Folder(m), shipped, true); }
            catch (Exception ex) { Log.Write("locales: " + ex.Message); }
        }

        /// <summary>
        /// Écrit locale\&lt;langue&gt;.json à partir du english.json installé : depuis la v1.9, le mod n'embarque plus que l'anglais.
        /// Une langue n'est écrite que si chaque texte anglais est celui pour lequel la traduction a été faite : sinon le fichier
        /// est absent et le mod reste en anglais, plutôt que de mélanger les deux langues.
        /// </summary>
        public static int WriteLocales(string seamlessFolder, ICollection<string> skip, bool overwrite)
        {
            var dir = Path.Combine(seamlessFolder, "locale");
            var englishPath = Path.Combine(dir, "english.json");
            if (!File.Exists(englishPath)) return 0;
            var current = Json.ParseObject(File.ReadAllText(englishPath, Encoding.UTF8));
            var reference = ResourceJson("locale.english.json");
            int written = 0;
            foreach (var lang in Locales)
            {
                var path = Path.Combine(dir, lang + ".json");
                if ((skip != null && skip.Contains(lang, StringComparer.OrdinalIgnoreCase)) || (!overwrite && File.Exists(path))) continue;
                var translation = ResourceJson("locale." + lang + ".json");
                var missing = current.Keys.Count(k => Translated(current, reference, translation, k) == null);
                if (missing > 0)
                {
                    Log.Write("locale " + lang + " not written: " + missing + " texts not translated for this Seamless version");
                    continue;
                }
                var sb = new StringBuilder("{\n");
                foreach (var key in current.Keys)
                {
                    if (sb.Length > 2) sb.Append(",\n");
                    sb.Append("\t\"").Append(JsonEscape(key)).Append("\": \"").Append(JsonEscape(Translated(current, reference, translation, key))).Append('"');
                }
                sb.Append("\n}\n");
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                written++;
            }
            return written;
        }

        /// <summary>Traduction de key, ou null si le texte anglais du mod n'est plus celui pour lequel elle a été faite.</summary>
        static string Translated(Dictionary<string, object> current, Dictionary<string, object> reference, Dictionary<string, object> translation, string key)
        {
            object en, refEn, tr;
            current.TryGetValue(key, out en);
            if (!reference.TryGetValue(key, out refEn) || (refEn as string) != (en as string ?? "")) return null;
            return translation.TryGetValue(key, out tr) ? tr as string : null;
        }

        static Dictionary<string, object> ResourceJson(string name)
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            using (var r = new StreamReader(s, Encoding.UTF8))
                return Json.ParseObject(r.ReadToEnd());
        }

        static string JsonEscape(string s)
        {
            var sb = new StringBuilder(s.Length + 8);
            foreach (var c in s)
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < 0x20) sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            return sb.ToString();
        }

        /// <summary>Le launcher v1.0 installait Seamless directement dans le dossier du jeu.</summary>
        public static bool LegacyPresent(string gameDir, InstalledMod seamless)
        {
            if (!Game.IsValid(gameDir)) return false;
            bool files = File.Exists(Path.Combine(gameDir, "ersc_launcher.exe")) || Directory.Exists(Path.Combine(gameDir, "SeamlessCoop"));
            return files && (seamless == null || seamless.Deployed.Count == 0);
        }

        public static string LegacyIni(string gameDir)
        {
            return gameDir == null ? null : Path.Combine(gameDir, "SeamlessCoop", "ersc_settings.ini");
        }

        public static void RemoveLegacy(string gameDir)
        {
            var exe = Path.Combine(gameDir, "ersc_launcher.exe");
            if (File.Exists(exe)) File.Delete(exe);
            Files.DeleteDir(Path.Combine(gameDir, "SeamlessCoop"));
        }
    }

    /// <summary>me3 (ModEngine 3) : chargeur de mods utilisé pour lancer le jeu avec plusieurs mods, sans EAC ni online officiel.</summary>
    public static class Me3
    {
        public const string Repo = "garyttierney/me3";
        const string AssetPattern = @"^me3-windows-amd64\.zip$";

        public static string Bin { get { return Path.Combine(Paths.Me3Dir, "bin"); } }
        public static string Exe { get { return Path.Combine(Bin, "me3.exe"); } }

        public static bool IsInstalled
        {
            get { return File.Exists(Exe) && File.Exists(Path.Combine(Bin, "me3-launcher.exe")) && File.Exists(Path.Combine(Bin, "me3_mod_host.dll")); }
        }

        public static string InstalledVersion()
        {
            var f = Path.Combine(Paths.Me3Dir, "version.txt");
            try { return File.Exists(f) ? File.ReadAllText(f).Trim() : null; }
            catch { return null; }
        }

        public static Task<GitHub.Release> Latest()
        {
            return GitHub.GetRelease(Repo, null);
        }

        public static async Task Install(GitHub.Release rel, Action<int> progress)
        {
            var asset = rel.Find(AssetPattern);
            if (asset == null) throw new UserError(L.F("err_no_asset", "me3"));
            var zip = await GitHub.Download(asset, null, progress);
            try
            {
                await Task.Run(() =>
                {
                    var dir = Files.ExtractToTemp(zip);
                    Files.Swap(dir, Paths.Me3Dir);
                    File.WriteAllText(Path.Combine(Paths.Me3Dir, "version.txt"), rel.Tag);
                });
            }
            finally
            {
                try { File.Delete(zip); } catch { }
                Files.CleanTemp();
            }
            Log.Write("me3 " + rel.Tag + " installed");
            if (!IsInstalled) throw new UserError(L.T("err_me3_av"));
        }

        static string Toml(string s)
        {
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        /// <summary>Écrit le profil me3 des mods actifs, dans l'ordre de chargement.</summary>
        public static string WriteProfile(ModStore store, string name)
        {
            var sb = new StringBuilder();
            sb.Append("# Généré par Seamless Co-op Launcher : réécrit à chaque lancement\n");
            sb.Append("profileVersion = \"v1\"\n\n[[supports]]\ngame = \"eldenring\"\n");
            var active = store.Mods.Where(m => m.Enabled && Directory.Exists(m.Dir)).ToList();
            foreach (var m in active)
                foreach (var n in m.Natives)
                    sb.Append("\n[[natives]]\npath = ").Append(Toml(Path.Combine(m.Dir, Files.Native(n)))).Append('\n');
            foreach (var m in active)
                for (int i = 0; i < m.Packages.Count; i++)
                {
                    var p = m.Packages[i];
                    sb.Append("\n[[packages]]\nid = ").Append(Toml(m.Id + (m.Packages.Count > 1 ? "-" + (i + 1) : "")))
                      .Append("\npath = ").Append(Toml(p.Length == 0 ? m.Dir : Path.Combine(m.Dir, Files.Native(p)))).Append('\n');
                }
            Directory.CreateDirectory(Paths.ProfilesDir);
            var path = Path.Combine(Paths.ProfilesDir, name + ".me3");
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
            return path;
        }

        public static void Launch(string profile, string gameDir)
        {
            var args = "launch -g eldenring -p \"" + profile + "\" -e \"" + Path.Combine(gameDir, "eldenring.exe") + "\"";
            Log.Write("me3 " + args);
            using (Process.Start(new ProcessStartInfo(Exe, args) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Bin })) { }
        }
    }

    public class NexusMod
    {
        public int Id, Endorsements;
        public string Name, Summary, Author, Version, Updated, Picture;

        public string Page { get { return "https://www.nexusmods.com/eldenring/mods/" + Id; } }
    }

    /// <summary>Recherche publique (sans compte) dans les mods Elden Ring de Nexus Mods, contenu adulte exclu.</summary>
    public static class Nexus
    {
        const string Endpoint = "https://api.nexusmods.com/v2/graphql";

        public static Task<List<NexusMod>> Search(string text, int count)
        {
            return Task.Run(() => SearchBlocking(text, count)); // requête bloquante, hors du thread de l'interface
        }

        /// <summary>Requête GraphQL bloquante ; renvoie la partie « data » de la réponse.</summary>
        static Dictionary<string, object> Query(string query)
        {
            string resp;
            using (var wc = Program.Web())
            {
                wc.Headers[HttpRequestHeader.ContentType] = "application/json";
                resp = wc.UploadString(Endpoint, "POST", Json.Write(new Dictionary<string, object> { { "query", query } }));
            }
            return Json.Obj(Json.ParseObject(resp), "data");
        }

        /// <summary>Version actuelle de chaque mod sur Nexus (numéro Nexus → version), sans compte.</summary>
        public static Task<Dictionary<int, string>> LatestVersions(IEnumerable<int> modIds)
        {
            var ids = modIds.Where(i => i > 0).Distinct().ToList();
            return Task.Run(() =>
            {
                var result = new Dictionary<int, string>();
                if (ids.Count == 0) return result;
                var list = string.Join(", ", ids.Select(i => "{ gameDomain: \"eldenring\", modId: " + i.ToString(CultureInfo.InvariantCulture) + " }"));
                var data = Query("{ legacyModsByDomain(ids: [" + list + "]) { nodes { modId version } } }");
                foreach (var n in Json.Objs(Json.Obj(data, "legacyModsByDomain"), "nodes"))
                    result[Json.Int(n, "modId", 0)] = Json.Str(n, "version", "");
                return result;
            });
        }

        static List<NexusMod> SearchBlocking(string text, int count)
        {
            var filter = "gameDomainName: [{ value: \"eldenring\" }], adultContent: [{ value: false }]";
            if (!string.IsNullOrWhiteSpace(text)) filter += ", name: [{ value: " + Json.Write(text.Trim()) + ", op: WILDCARD }]";
            var query = "{ mods(filter: { " + filter + " }, sort: [{ endorsements: { direction: DESC } }], count: " + count +
                        ") { nodes { modId name summary author version updatedAt endorsements pictureUrl } } }";
            var mods = Json.Obj(Query(query), "mods");
            return Json.Objs(mods, "nodes").Select(n => new NexusMod
            {
                Id = Json.Int(n, "modId", 0),
                Name = Json.Str(n, "name", ""),
                Summary = WebUtilityDecode(Json.Str(n, "summary", "")),
                Author = Json.Str(n, "author", ""),
                Version = Json.Str(n, "version", ""),
                Updated = Json.Str(n, "updatedAt", ""),
                Endorsements = Json.Int(n, "endorsements", 0),
                Picture = Json.Str(n, "pictureUrl")
            }).Where(n => n.Id > 0).ToList();
        }

        static string WebUtilityDecode(string s)
        {
            return WebUtility.HtmlDecode(s ?? "").Replace("<br />", " ").Replace("<br>", " ");
        }
    }
}
