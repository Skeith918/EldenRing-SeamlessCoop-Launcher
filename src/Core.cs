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
        public static string LegacySettingsCopy { get { return Path.Combine(AppData, "ersc_settings.ini"); } } // launcher v1.0
        public static string LogFile { get { return Path.Combine(AppData, "launcher.log"); } }
        public static string ModsRoot { get { return Path.Combine(AppData, "mods"); } }
        public static string ModsFile { get { return Path.Combine(AppData, "mods.json"); } }
        public static string SettingsDir { get { return Path.Combine(AppData, "settings"); } }
        public static string ProfilesDir { get { return Path.Combine(AppData, "profiles"); } }
        public static string Me3Dir { get { return Path.Combine(AppData, "me3"); } }
        public static string CatalogCache { get { return Path.Combine(AppData, "catalog.json"); } }
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

        /// <summary>Version de eldenring.exe sur 3 chiffres (ex. « 2.7.2 »), ou null.</summary>
        public static string Version(string dir)
        {
            try
            {
                if (!IsValid(dir)) return null;
                var v = FileVersionInfo.GetVersionInfo(Path.Combine(dir, "eldenring.exe"));
                return v.FileMajorPart + "." + v.FileMinorPart + "." + v.FileBuildPart;
            }
            catch { return null; }
        }

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
