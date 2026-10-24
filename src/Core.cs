using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace ERSeamless
{
    /// <summary>Erreur dont le message est destiné tel quel à l'utilisateur.</summary>
    public class UserError : Exception
    {
        public UserError(string message) : base(message) { }
    }

    public static class Paths
    {
        // modifiables pour les tests
        public static string AppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ERSeamlessLauncher");
        public static string SavesRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EldenRing");

        public static string StateFile { get { return Path.Combine(AppData, "launcher.ini"); } }
        public static string SettingsCopy { get { return Path.Combine(AppData, "ersc_settings.ini"); } }
        public static string LogFile { get { return Path.Combine(AppData, "launcher.log"); } }
    }

    public static class Log
    {
        public static void Write(string message)
        {
            try
            {
                Directory.CreateDirectory(Paths.AppData);
                var fi = new FileInfo(Paths.LogFile);
                if (fi.Exists && fi.Length > 256 * 1024) fi.Delete();
                File.AppendAllText(Paths.LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
            }
            catch { }
        }
    }

    /// <summary>Préférences propres au launcher (clé=valeur).</summary>
    public class State
    {
        readonly Dictionary<string, string> d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public static State Load()
        {
            var s = new State();
            try
            {
                if (File.Exists(Paths.StateFile))
                    foreach (var line in File.ReadAllLines(Paths.StateFile))
                    {
                        int i = line.IndexOf('=');
                        if (i > 0) s.d[line.Substring(0, i).Trim()] = line.Substring(i + 1).Trim();
                    }
            }
            catch (Exception ex) { Log.Write("state load: " + ex.Message); }
            return s;
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Paths.AppData);
                File.WriteAllLines(Paths.StateFile, d.Select(kv => kv.Key + "=" + kv.Value));
            }
            catch (Exception ex) { Log.Write("state save: " + ex.Message); }
        }

        public string Get(string key) { string v; return d.TryGetValue(key, out v) ? v : null; }
        public void Set(string key, string value) { if (value == null) d.Remove(key); else d[key] = value; }
        public bool GetBool(string key, bool def) { var v = Get(key); return v == null ? def : v == "1"; }
        public void SetBool(string key, bool value) { d[key] = value ? "1" : "0"; }
    }

    /// <summary>INI qui conserve commentaires et mise en forme d'origine.</summary>
    public class Ini
    {
        static readonly Regex KeyRx = new Regex(@"^\s*([A-Za-z0-9_]+)\s*=(.*)$");
        readonly List<string> lines;

        public Ini(string text)
        {
            lines = string.IsNullOrEmpty(text) ? new List<string>() : text.Replace("\r\n", "\n").Split('\n').ToList();
        }

        public static Ini Load(string path)
        {
            try { return File.Exists(path) ? new Ini(File.ReadAllText(path)) : null; }
            catch { return null; }
        }

        public string Text { get { return string.Join("\r\n", lines); } }

        public void Save(string path)
        {
            File.WriteAllText(path, Text, new UTF8Encoding(false));
        }

        static bool IsComment(string t) { return t.StartsWith(";") || t.StartsWith("#"); }
        static string SectionName(string t) { return t.StartsWith("[") && t.EndsWith("]") ? t.Substring(1, t.Length - 2).Trim() : null; }
        static bool Eq(string a, string b) { return string.Equals(a, b, StringComparison.OrdinalIgnoreCase); }

        public Dictionary<string, string> Values()
        {
            var r = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in lines)
            {
                if (IsComment(l.Trim())) continue;
                var m = KeyRx.Match(l);
                if (m.Success) r[m.Groups[1].Value] = m.Groups[2].Value.Trim();
            }
            return r;
        }

        public bool Has(string key) { return Values().ContainsKey(key); }

        public void Set(string section, string key, string value)
        {
            int header = -1, last = -1;
            string cur = null;
            for (int i = 0; i < lines.Count; i++)
            {
                var t = lines[i].Trim();
                var s = SectionName(t);
                if (s != null)
                {
                    cur = s;
                    if (Eq(s, section)) { header = i; last = i; }
                    continue;
                }
                if (cur != null && Eq(cur, section) && t.Length > 0) last = i;
                if (IsComment(t)) continue;
                var m = KeyRx.Match(lines[i]);
                if (m.Success && Eq(m.Groups[1].Value, key))
                {
                    lines[i] = m.Groups[1].Value + " = " + value;
                    return;
                }
            }
            string entry = key + " = " + value;
            if (header >= 0) { lines.Insert(last + 1, entry); return; }
            if (lines.Count > 0 && lines[lines.Count - 1].Trim().Length > 0) lines.Add("");
            lines.Add("[" + section + "]");
            lines.Add(entry);
        }
    }

    /// <summary>Clés connues de SeamlessCoop\ersc_settings.ini (v2.x) et leurs valeurs par défaut.</summary>
    public static class ModConfig
    {
        static readonly string[][] Schema =
        {
            new[] { "GAMEPLAY", "allow_invaders", "1" },
            new[] { "GAMEPLAY", "death_debuffs", "1" },
            new[] { "GAMEPLAY", "allow_summons", "1" },
            new[] { "GAMEPLAY", "overhead_player_display", "0" },
            new[] { "GAMEPLAY", "skip_splash_screens", "0" },
            new[] { "GAMEPLAY", "append_steam_id_to_players", "0" },
            new[] { "GAMEPLAY", "always_spectate_on_death", "0" },
            new[] { "GAMEPLAY", "default_boot_master_volume", "5" },
            new[] { "SCALING", "enemy_health_scaling", "35" },
            new[] { "SCALING", "enemy_damage_scaling", "0" },
            new[] { "SCALING", "enemy_posture_scaling", "15" },
            new[] { "SCALING", "boss_health_scaling", "100" },
            new[] { "SCALING", "boss_damage_scaling", "0" },
            new[] { "SCALING", "boss_posture_scaling", "20" },
            new[] { "PASSWORD", "cooppassword", "" },
            new[] { "SAVE", "save_file_extension", "co2" },
        };

        public static readonly string[] ScalingKeys = Schema.Where(e => e[0] == "SCALING").Select(e => e[1]).ToArray();

        public static Dictionary<string, string> Defaults()
        {
            return Schema.ToDictionary(e => e[1], e => e[2], StringComparer.OrdinalIgnoreCase);
        }

        public static string Default(string key)
        {
            var e = Schema.FirstOrDefault(x => x[1] == key);
            return e == null ? "" : e[2];
        }

        public static void Apply(Ini ini, IDictionary<string, string> values)
        {
            foreach (var e in Schema)
            {
                string v;
                if (values.TryGetValue(e[1], out v)) ini.Set(e[0], e[1], v);
            }
        }

        public static string SanitizeExtension(string s)
        {
            var ext = new string((s ?? "").Where(char.IsLetterOrDigit).ToArray());
            return ext.Length > 120 ? ext.Substring(0, 120) : ext;
        }
    }

    public static class Steam
    {
        public static string InstallDir()
        {
            string p = null;
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                if (k != null) p = k.GetValue("SteamPath") as string;
            if (p == null)
                using (var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32))
                using (var k = b.OpenSubKey(@"SOFTWARE\Valve\Steam"))
                    if (k != null) p = k.GetValue("InstallPath") as string;
            if (p == null) return null;
            p = p.Replace('/', '\\');
            return Directory.Exists(p) ? p : null;
        }

        public static string Exe()
        {
            var d = InstallDir();
            if (d == null) return null;
            var exe = Path.Combine(d, "steam.exe");
            return File.Exists(exe) ? exe : null;
        }

        public static bool IsRunning() { return Proc.Running("steam"); }

        public static bool IsLoggedIn()
        {
            using (var k = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam\ActiveProcess"))
            {
                if (k == null) return false;
                var v = k.GetValue("ActiveUser");
                return v is int && (int)v != 0;
            }
        }

        public static bool Ready() { return IsRunning() && IsLoggedIn(); }

        public static List<string> Libraries()
        {
            var result = new List<string>();
            var steam = InstallDir();
            if (steam == null) return result;
            result.Add(steam);
            try
            {
                var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(vdf)) return result;
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                {
                    var p = m.Groups[1].Value.Replace(@"\\", @"\");
                    if (!result.Any(x => string.Equals(x.TrimEnd('\\'), p.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase)))
                        result.Add(p);
                }
            }
            catch (Exception ex) { Log.Write("libraryfolders: " + ex.Message); }
            return result;
        }
    }

    public static class Game
    {
        public const string AppId = "1245620";

        public static bool IsValid(string dir)
        {
            return !string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "eldenring.exe"));
        }

        public static bool IsRunning() { return Proc.Running("eldenring"); }

        /// <summary>Cherche le dossier ELDEN RING\Game dans les bibliothèques Steam puis dans la clé de désinstallation.</summary>
        public static string Detect()
        {
            foreach (var lib in Steam.Libraries())
            {
                string installdir = "ELDEN RING";
                try
                {
                    var acf = Path.Combine(lib, "steamapps", "appmanifest_" + AppId + ".acf");
                    if (File.Exists(acf))
                    {
                        var m = Regex.Match(File.ReadAllText(acf), "\"installdir\"\\s+\"([^\"]+)\"");
                        if (m.Success) installdir = m.Groups[1].Value;
                    }
                }
                catch { }
                var dir = Path.Combine(lib, "steamapps", "common", installdir, "Game");
                if (IsValid(dir)) return dir;
            }
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                using (var b = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, view))
                using (var k = b.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App " + AppId))
                {
                    var loc = k == null ? null : k.GetValue("InstallLocation") as string;
                    if (loc != null && IsValid(Path.Combine(loc, "Game"))) return Path.Combine(loc, "Game");
                }
            }
            return null;
        }
    }

    public class Release
    {
        public string Tag, ZipUrl, Sha256, Notes, Page;
        public long Size;

        public string Summary
        {
            get
            {
                var first = (Notes ?? "").Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
                return first ?? "";
            }
        }
    }

    public static class Mod
    {
        public const string Repo = "yuiamoroll/EldenRingSeamlessCoopRelease";
        public const string VersionFile = "launcher_version.txt";

        public static string Dir(string game) { return Path.Combine(game, "SeamlessCoop"); }
        public static string IniPath(string game) { return Path.Combine(Dir(game), "ersc_settings.ini"); }
        public static string LauncherExe(string game) { return Path.Combine(game, "ersc_launcher.exe"); }

        public static bool IsInstalled(string game)
        {
            return Game.IsValid(game) && File.Exists(LauncherExe(game)) && File.Exists(Path.Combine(Dir(game), "ersc.dll"));
        }

        public static string InstalledVersion(string game)
        {
            try
            {
                var f = Path.Combine(Dir(game), VersionFile);
                return File.Exists(f) ? File.ReadAllText(f).Trim() : null;
            }
            catch { return null; }
        }

        static WebClient Client()
        {
            var wc = new WebClient { Encoding = Encoding.UTF8 };
            wc.Headers[HttpRequestHeader.UserAgent] = "ERSeamlessLauncher/" + Program.Version;
            return wc;
        }

        static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            return d.TryGetValue(key, out v) && v != null ? v.ToString() : null;
        }

        public static async Task<Release> FetchLatest()
        {
            string json;
            using (var wc = Client())
            {
                wc.Headers[HttpRequestHeader.Accept] = "application/vnd.github+json";
                json = await wc.DownloadStringTaskAsync("https://api.github.com/repos/" + Repo + "/releases/latest");
            }
            var root = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(json);
            var r = new Release { Tag = Str(root, "tag_name"), Page = Str(root, "html_url"), Notes = Str(root, "body") };
            object assetsObj;
            var assets = root.TryGetValue("assets", out assetsObj) ? assetsObj as object[] : null;
            if (assets != null)
                foreach (Dictionary<string, object> a in assets)
                {
                    if (!(Str(a, "name") ?? "").EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) continue;
                    r.ZipUrl = Str(a, "browser_download_url");
                    long size;
                    if (long.TryParse(Str(a, "size"), out size)) r.Size = size;
                    var digest = Str(a, "digest");
                    if (digest != null && digest.StartsWith("sha256:")) r.Sha256 = digest.Substring(7);
                    break;
                }
            if (string.IsNullOrEmpty(r.Tag) || r.ZipUrl == null)
                throw new UserError(L.T("err_no_zip"));
            var host = new Uri(r.ZipUrl).Host;
            if (!host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
                throw new UserError(L.F("err_bad_host", host));
            return r;
        }

        public static async Task<string> Download(Release r, Action<int> progress)
        {
            string path = Path.Combine(Path.GetTempPath(), "ersc_" + Regex.Replace(r.Tag, @"[^\w\.\-]", "_") + ".zip");
            using (var wc = Client())
            {
                wc.DownloadProgressChanged += (s, e) => progress(e.ProgressPercentage);
                await wc.DownloadFileTaskAsync(new Uri(r.ZipUrl), path);
            }
            if (r.Sha256 != null)
            {
                var hash = await Task.Run(() => Sha256(path));
                if (!hash.Equals(r.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(path);
                    throw new UserError(L.T("err_sha"));
                }
            }
            return path;
        }

        public static string Sha256(string path)
        {
            using (var sha = SHA256.Create())
            using (var f = File.OpenRead(path))
                return BitConverter.ToString(sha.ComputeHash(f)).Replace("-", "");
        }

        /// <summary>
        /// Extrait l'archive dans le dossier Game. Le ersc_settings.ini fourni sert de modèle :
        /// les anciennes valeurs puis celles du launcher y sont réappliquées, les nouvelles clés gardent leur défaut.
        /// </summary>
        public static void Install(string zipPath, string game, IDictionary<string, string> userValues, string tag)
        {
            string root = Path.GetFullPath(game).TrimEnd('\\') + "\\";
            string iniPath = Path.GetFullPath(IniPath(game));
            Ini old = Ini.Load(iniPath);
            string template = null;
            var shipped = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // traductions fournies par le mod lui-même

            using (var zip = ZipFile.OpenRead(zipPath))
            {
                var launcher = zip.Entries.FirstOrDefault(e => e.Name.Equals("ersc_launcher.exe", StringComparison.OrdinalIgnoreCase));
                if (launcher == null) throw new UserError(L.T("err_archive"));
                string prefix = launcher.FullName.Substring(0, launcher.FullName.Length - launcher.Name.Length);

                foreach (var e in zip.Entries)
                {
                    if (!e.FullName.StartsWith(prefix, StringComparison.Ordinal)) continue;
                    string rel = e.FullName.Substring(prefix.Length).Replace('/', '\\');
                    if (rel.Length == 0) continue;
                    string dest = Path.GetFullPath(Path.Combine(root, rel));
                    if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                    if (e.Name.Length == 0) { Directory.CreateDirectory(dest); continue; }
                    if (rel.StartsWith(@"SeamlessCoop\locale\", StringComparison.OrdinalIgnoreCase) && rel.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                        shipped.Add(Path.GetFileNameWithoutExtension(rel));
                    if (dest.Equals(iniPath, StringComparison.OrdinalIgnoreCase))
                    {
                        using (var sr = new StreamReader(e.Open())) template = sr.ReadToEnd();
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));
                    e.ExtractToFile(dest, true);
                }
            }

            var ini = new Ini(template ?? (old != null ? old.Text : null));
            if (old != null)
                foreach (var kv in old.Values())
                    if (ini.Has(kv.Key)) ini.Set("GAMEPLAY", kv.Key, kv.Value);
            ModConfig.Apply(ini, userValues);
            Directory.CreateDirectory(Path.GetDirectoryName(iniPath));
            ini.Save(iniPath);
            File.WriteAllText(Path.Combine(Dir(game), VersionFile), tag);
            try { WriteLocales(game, shipped, true); }
            catch (Exception ex) { Log.Write("locales: " + ex.Message); }

            if (!IsInstalled(game))
                throw new UserError(L.T("err_antivirus"));
        }

        /// <summary>Traductions du mod fournies par le launcher : depuis la v1.9, le mod n'embarque plus que l'anglais.</summary>
        public static readonly string[] Locales = { "french", "spanish", "latam" };

        /// <summary>
        /// Écrit SeamlessCoop\locale\&lt;langue&gt;.json à partir du english.json installé. Une traduction n'est reprise
        /// que si le texte anglais est celui pour lequel elle a été faite ; sinon le texte anglais est gardé.
        /// </summary>
        public static int WriteLocales(string game, ICollection<string> skip, bool overwrite)
        {
            var dir = Path.Combine(Dir(game), "locale");
            var englishPath = Path.Combine(dir, "english.json");
            if (!File.Exists(englishPath)) return 0;
            var current = (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(File.ReadAllText(englishPath, Encoding.UTF8));
            var reference = ResourceJson("locale.english.json");
            int written = 0;
            foreach (var lang in Locales)
            {
                var path = Path.Combine(dir, lang + ".json");
                if ((skip != null && skip.Contains(lang)) || (!overwrite && File.Exists(path))) continue;
                var translation = ResourceJson("locale." + lang + ".json");
                var sb = new StringBuilder("{\n");
                foreach (var kv in current)
                {
                    var en = kv.Value as string ?? "";
                    object refEn, tr;
                    var value = reference.TryGetValue(kv.Key, out refEn) && (refEn as string) == en && translation.TryGetValue(kv.Key, out tr)
                        ? (string)tr : en;
                    if (sb.Length > 2) sb.Append(",\n");
                    sb.Append("\t\"").Append(JsonEscape(kv.Key)).Append("\": \"").Append(JsonEscape(value)).Append('"');
                }
                sb.Append("\n}\n");
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                written++;
            }
            return written;
        }

        static Dictionary<string, object> ResourceJson(string name)
        {
            using (var s = typeof(Mod).Assembly.GetManifestResourceStream(name))
            using (var r = new StreamReader(s, Encoding.UTF8))
                return (Dictionary<string, object>)new JavaScriptSerializer().DeserializeObject(r.ReadToEnd());
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

        public static void Uninstall(string game)
        {
            if (File.Exists(LauncherExe(game))) File.Delete(LauncherExe(game));
            if (Directory.Exists(Dir(game))) Directory.Delete(Dir(game), true);
        }
    }

    /// <summary>Sauvegardes : %APPDATA%\EldenRing\&lt;SteamID64&gt;\ER0000.sl2 (vanilla) / ER0000.&lt;ext&gt; (Seamless).</summary>
    public static class Saves
    {
        public const string BackupDir = "ERSC_Backups";
        const int KeepAuto = 10;

        public static List<string> Profiles()
        {
            var r = new List<string>();
            try
            {
                if (Directory.Exists(Paths.SavesRoot))
                    foreach (var d in Directory.GetDirectories(Paths.SavesRoot))
                    {
                        var n = Path.GetFileName(d);
                        if (n.Length >= 5 && n.All(char.IsDigit)) r.Add(d);
                    }
            }
            catch (Exception ex) { Log.Write("saves: " + ex.Message); }
            return r;
        }

        static string Vanilla(string dir) { return Path.Combine(dir, "ER0000.sl2"); }
        static string ModSave(string dir, string ext) { return Path.Combine(dir, "ER0000." + ext); }

        public static bool HasVanilla() { return Profiles().Any(d => File.Exists(Vanilla(d))); }
        public static bool HasMod(string ext) { return Profiles().Any(d => File.Exists(ModSave(d, ext))); }

        /// <summary>Copie .sl2 → .ext. Sans overwrite, les profils qui ont déjà une sauvegarde Seamless sont ignorés.</summary>
        public static int ImportVanilla(string ext, bool overwrite)
        {
            int n = 0;
            foreach (var d in Profiles())
            {
                string src = Vanilla(d), dst = ModSave(d, ext);
                if (!File.Exists(src)) continue;
                if (File.Exists(dst))
                {
                    if (!overwrite) continue;
                    Backup(d, ext, "avant-import");
                }
                File.Copy(src, dst, true);
                n++;
            }
            return n;
        }

        public static int BackupAll(string ext)
        {
            int n = 0;
            foreach (var d in Profiles())
            {
                if (!File.Exists(ModSave(d, ext))) continue;
                Backup(d, ext, "auto");
                foreach (var old in new DirectoryInfo(Path.Combine(d, BackupDir)).GetFiles("ER0000_auto_*." + ext)
                                                                                 .OrderByDescending(f => f.Name).Skip(KeepAuto))
                    try { old.Delete(); } catch { }
                n++;
            }
            return n;
        }

        static void Backup(string dir, string ext, string tag)
        {
            var bd = Path.Combine(dir, BackupDir);
            Directory.CreateDirectory(bd);
            File.Copy(ModSave(dir, ext), Path.Combine(bd, "ER0000_" + tag + "_" + DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "." + ext), true);
        }
    }

    public static class Fs
    {
        public static bool CanWrite(string dir)
        {
            try
            {
                var p = Path.Combine(dir, ".ersc_write_test");
                File.WriteAllText(p, "");
                File.Delete(p);
                return true;
            }
            catch (UnauthorizedAccessException) { return false; }
            catch (IOException) { return false; }
        }

        /// <summary>Relance l'exe en administrateur (UAC) juste pour donner à l'utilisateur courant le droit d'écrire dans le dossier du jeu.</summary>
        public static bool RequestWriteAccess(string dir)
        {
            var psi = new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,
                                           "--grant \"" + dir.TrimEnd('\\') + "\" " + WindowsIdentity.GetCurrent().User.Value)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            try
            {
                using (var p = Process.Start(psi))
                {
                    p.WaitForExit();
                    return p.ExitCode == 0;
                }
            }
            catch (System.ComponentModel.Win32Exception) { return false; } // UAC refusé
        }

        public static int Grant(string dir, string sid)
        {
            try
            {
                if (!Game.IsValid(dir)) return 2;
                var di = new DirectoryInfo(dir);
                var acl = di.GetAccessControl();
                acl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid), FileSystemRights.Modify,
                    InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
                di.SetAccessControl(acl);
                return 0;
            }
            catch (Exception ex) { Log.Write("grant: " + ex); return 1; }
        }
    }

    public static class Proc
    {
        public static bool Running(string name)
        {
            var ps = Process.GetProcessesByName(name);
            foreach (var p in ps) p.Dispose();
            return ps.Length > 0;
        }

        public static void Open(string target)
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
    }
}
