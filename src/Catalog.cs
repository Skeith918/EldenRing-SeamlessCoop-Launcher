using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace ERSeamless
{
    /// <summary>D'où vient un mod : release GitHub (installable et mis à jour automatiquement), page Nexus ou import manuel.</summary>
    public class ModSource
    {
        public string Type = "manual"; // github | nexus | manual
        public string Repo, Tag, Asset, Sha256;

        public bool IsGitHub { get { return Type == "github" && !string.IsNullOrEmpty(Repo); } }

        public static ModSource From(Dictionary<string, object> d)
        {
            if (d == null) return new ModSource();
            return new ModSource
            {
                Type = Json.Str(d, "type", "manual"),
                Repo = Json.Str(d, "repo"),
                Tag = Json.Str(d, "tag"),
                Asset = Json.Str(d, "asset", @"\.zip$"),
                Sha256 = Json.Str(d, "sha256")
            };
        }
    }

    /// <summary>Un contrôle : toggle (0/1), slider, choice (index), text, ou note (simple texte d'aide).</summary>
    public class SettingItem
    {
        public string Key, Section, Type, Default, Sanitize;
        public object Label, Hint, Placeholder, Zero, Choices, Format;
        public int Min, Max = 100, Step = 1, MaxLength = 120;
    }

    public class SettingTab
    {
        public object Title;
        public bool Reset; // bouton « valeurs par défaut » en bas de l'onglet
        public List<SettingItem> Items = new List<SettingItem>();
    }

    /// <summary>Réglages d'un mod : fichier (relatif au dossier du mod), format et onglets de contrôles.</summary>
    public class SettingsSchema
    {
        public string File, Format = "ini";
        public List<SettingTab> Tabs = new List<SettingTab>();

        public IEnumerable<SettingItem> Items { get { return Tabs.SelectMany(t => t.Items).Where(i => i.Type != "note" && !string.IsNullOrEmpty(i.Key)); } }

        public Dictionary<string, string> Defaults()
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var it in Items) d[it.Key] = it.Default ?? "";
            return d;
        }

        public static SettingsSchema From(Dictionary<string, object> d)
        {
            if (d == null) return null;
            var s = new SettingsSchema { File = Json.Str(d, "file"), Format = Json.Str(d, "format", "ini") };
            foreach (var t in Json.Objs(d, "tabs"))
            {
                var tab = new SettingTab { Title = Json.Get(t, "title"), Reset = Json.Bool(t, "reset", false) };
                foreach (var i in Json.Objs(t, "items"))
                    tab.Items.Add(new SettingItem
                    {
                        Key = Json.Str(i, "key"),
                        Section = Json.Str(i, "section", "GENERAL"),
                        Type = Json.Str(i, "type", "text"),
                        Default = Json.Str(i, "default", ""),
                        Sanitize = Json.Str(i, "sanitize"),
                        Format = Json.Get(i, "format"),
                        Label = Json.Get(i, "label"),
                        Hint = Json.Get(i, "hint"),
                        Placeholder = Json.Get(i, "placeholder"),
                        Zero = Json.Get(i, "zero"),
                        Choices = Json.Get(i, "choices"),
                        Min = Json.Int(i, "min", 0),
                        Max = Json.Int(i, "max", 100),
                        Step = Json.Int(i, "step", 1),
                        MaxLength = Json.Int(i, "maxLength", 120)
                    });
                if (tab.Items.Count > 0) s.Tabs.Add(tab);
            }
            return string.IsNullOrEmpty(s.File) || !s.Items.Any() ? null : s;
        }

        /// <summary>Réglages déduits d'un .ini quelconque : une section = un onglet, 0/1/true/false = interrupteur, le reste = texte.</summary>
        public static SettingsSchema FromIni(string relFile, string iniText)
        {
            var s = new SettingsSchema { File = relFile };
            SettingTab tab = null;
            string section = "GENERAL";
            var comments = new List<string>();
            foreach (var raw in (iniText ?? "").Replace("\r\n", "\n").Split('\n'))
            {
                var t = raw.Trim();
                if (t.Length == 0) { comments.Clear(); continue; }
                if (t.StartsWith(";") || t.StartsWith("#")) { comments.Add(t.TrimStart(';', '#').Trim()); continue; }
                if (t.StartsWith("[") && t.EndsWith("]"))
                {
                    section = t.Substring(1, t.Length - 2).Trim();
                    tab = null;
                    comments.Clear();
                    continue;
                }
                int eq = t.IndexOf('=');
                if (eq <= 0) continue;
                if (tab == null) { tab = new SettingTab { Title = section }; s.Tabs.Add(tab); }
                var key = t.Substring(0, eq).Trim();
                var val = t.Substring(eq + 1).Trim();
                var low = val.ToLowerInvariant();
                tab.Items.Add(new SettingItem
                {
                    Key = key,
                    Section = section,
                    Type = low == "0" || low == "1" || low == "true" || low == "false" ? "toggle" : "text",
                    Default = val,
                    Label = key,
                    Hint = comments.Count > 0 ? string.Join(" ", comments) : null
                });
                comments.Clear();
            }
            return s.Tabs.Count == 0 ? null : s;
        }
    }

    public class CatalogMod
    {
        public string Id, Name, Author, Category, Version, Homepage, Special;
        public object Description;
        public int Nexus;
        public ModSource Source = new ModSource();
        public string[] Games = new string[0], Natives = new string[0], Packages = new string[0], Conflicts = new string[0], Requires = new string[0];
        public List<KeyValuePair<string, string>> Deploy = new List<KeyValuePair<string, string>>();
        public bool Gameplay, Core;
        public SettingsSchema Settings;

        public bool Supports(string gameVersion)
        {
            return gameVersion != null && Games.Any(p => VersionMatch(p, gameVersion));
        }

        /// <summary>« 2.7.2 », « 2.7.* » ou « * ».</summary>
        public static bool VersionMatch(string pattern, string version)
        {
            if (string.IsNullOrEmpty(pattern) || version == null) return false;
            var p = pattern.Trim().Split('.');
            var v = version.Split('.');
            for (int i = 0; i < p.Length; i++)
            {
                if (p[i] == "*") return true;
                if (i >= v.Length || p[i] != v[i]) return false;
            }
            return p.Length == v.Length;
        }

        public string NexusPage
        {
            get { return Nexus > 0 ? "https://www.nexusmods.com/eldenring/mods/" + Nexus : Homepage; }
        }

        public static CatalogMod From(Dictionary<string, object> d)
        {
            var m = new CatalogMod
            {
                Id = Json.Str(d, "id"),
                Name = Json.Str(d, "name"),
                Author = Json.Str(d, "author", ""),
                Category = Json.Str(d, "category", "other"),
                Version = Json.Str(d, "version"),
                Homepage = Json.Str(d, "homepage"),
                Special = Json.Str(d, "special"),
                Description = Json.Get(d, "description"),
                Nexus = Json.Int(d, "nexus", 0),
                Source = ModSource.From(Json.Obj(d, "source")),
                Games = Json.Strs(d, "games"),
                Natives = Json.Strs(d, "natives"),
                Packages = Json.Strs(d, "packages"),
                Conflicts = Json.Strs(d, "conflicts"),
                Requires = Json.Strs(d, "requires"),
                Gameplay = Json.Bool(d, "gameplay", false),
                Core = Json.Bool(d, "core", false),
                Settings = SettingsSchema.From(Json.Obj(d, "settings"))
            };
            foreach (var x in Json.List(d, "deploy"))
            {
                var s = x as string;
                var o = x as Dictionary<string, object>;
                if (s != null) m.Deploy.Add(new KeyValuePair<string, string>(s, s));
                else if (o != null) m.Deploy.Add(new KeyValuePair<string, string>(Json.Str(o, "from"), Json.Str(o, "to", Json.Str(o, "from"))));
            }
            return string.IsNullOrEmpty(m.Id) || string.IsNullOrEmpty(m.Name) ? null : m;
        }
    }

    /// <summary>
    /// Catalogue des mods vérifiés : catalog.json du dépôt GitHub, avec une copie embarquée dans l'exe
    /// et une copie en cache. On garde toujours le plus récent (champ « updated »).
    /// </summary>
    public class Catalog
    {
        public const int SupportedSchema = 1;
        public const string RemoteUrl = "https://raw.githubusercontent.com/Skeith918/EldenRing-SeamlessCoop-Launcher/main/catalog.json";

        public int Schema;
        public string Updated = "";
        public List<CatalogMod> Mods = new List<CatalogMod>();

        public CatalogMod Find(string id)
        {
            return Mods.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public CatalogMod FindNexus(int nexusId)
        {
            return nexusId <= 0 ? null : Mods.FirstOrDefault(m => m.Nexus == nexusId);
        }

        public static Catalog Parse(string json)
        {
            var d = Json.ParseObject(json);
            if (d == null) return null;
            var c = new Catalog { Schema = Json.Int(d, "schema", 0), Updated = Json.Str(d, "updated", "") };
            if (c.Schema < 1 || c.Schema > SupportedSchema) return null;
            foreach (var m in Json.Objs(d, "mods"))
            {
                var cm = CatalogMod.From(m);
                if (cm != null && c.Find(cm.Id) == null) c.Mods.Add(cm);
            }
            return c;
        }

        static Catalog TryParse(string json, string origin)
        {
            try { return Parse(json); }
            catch (Exception ex) { Log.Write("catalog " + origin + ": " + ex.Message); return null; }
        }

        public static Catalog Embedded()
        {
            using (var s = Assembly.GetExecutingAssembly().GetManifestResourceStream("catalog.json"))
            using (var r = new StreamReader(s, Encoding.UTF8))
                return Parse(r.ReadToEnd());
        }

        /// <summary>Catalogue disponible sans réseau : cache s'il est plus récent que celui de l'exe.</summary>
        public static Catalog LoadLocal()
        {
            var embedded = Embedded();
            Catalog cached = null;
            try { if (File.Exists(Paths.CatalogCache)) cached = TryParse(File.ReadAllText(Paths.CatalogCache, Encoding.UTF8), "cache"); }
            catch { }
            return Newest(embedded, cached);
        }

        public static Catalog Newest(Catalog a, Catalog b)
        {
            if (a == null) return b;
            if (b == null) return a;
            return string.CompareOrdinal(b.Updated, a.Updated) > 0 ? b : a;
        }

        /// <summary>Télécharge le catalogue du dépôt ; null en cas d'échec (le launcher garde alors sa copie locale).</summary>
        public static async Task<Catalog> FetchRemote()
        {
            try
            {
                string json;
                using (var wc = new WebClient { Encoding = Encoding.UTF8 })
                {
                    wc.Headers[HttpRequestHeader.UserAgent] = "ERSeamlessLauncher/" + Program.Version;
                    json = await wc.DownloadStringTaskAsync(RemoteUrl + "?t=" + DateTime.UtcNow.Ticks);
                }
                var c = TryParse(json, "remote");
                if (c != null)
                {
                    Directory.CreateDirectory(Paths.AppData);
                    File.WriteAllText(Paths.CatalogCache, json, new UTF8Encoding(false));
                }
                return c;
            }
            catch (Exception ex)
            {
                Log.Write("catalog fetch: " + ex.Message);
                return null;
            }
        }
    }
}
