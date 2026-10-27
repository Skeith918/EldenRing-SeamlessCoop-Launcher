using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ERSeamless
{
    /// <summary>Incompatibilité avérée entre deux mods installés.</summary>
    public class ModConflict
    {
        public string With;   // identifiant de l'autre mod
        public string Kind;   // declared | files | deploy
        public object Reason; // explication du catalogue (declared)
        public List<string> Files = new List<string>();
    }

    /// <summary>
    /// Incompatibilités réelles, jamais supposées (même catégorie ou même thème ne suffisent pas) :
    /// - déclarées par le catalogue après vérification (mêmes octets du jeu modifiés, avis de l'auteur…) ;
    /// - mêmes fichiers du jeu remplacés avec un contenu différent : un seul des deux serait chargé ;
    /// - même fichier copié dans le dossier du jeu.
    /// Deux mods incompatibles ne peuvent pas être activés ensemble.
    /// </summary>
    public static class Conflicts
    {
        class Footprint
        {
            public string Stamp;
            public Dictionary<string, string> Game = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // chemin vu par le jeu → fichier du mod
            public Dictionary<string, string> Deploy = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // chemin dans Game\ → fichier du mod
        }

        static readonly Dictionary<string, Footprint> footprints = new Dictionary<string, Footprint>(StringComparer.OrdinalIgnoreCase);
        static readonly Dictionary<string, string> hashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>Fichiers que le mod fournit au jeu ; recalculés seulement après une (ré)installation.</summary>
        static Footprint Of(InstalledMod m)
        {
            var stamp = string.Join("|", m.InstalledAt, m.AssetSha, string.Join(",", m.Packages), string.Join(",", m.Deploy.Select(kv => kv.Key + ">" + kv.Value)));
            Footprint f;
            lock (footprints) if (footprints.TryGetValue(m.Id, out f) && f.Stamp == stamp) return f;
            f = new Footprint { Stamp = stamp };
            try
            {
                foreach (var p in m.Packages)
                {
                    var root = Path.Combine(m.Dir, Files.Native(p));
                    if (!Directory.Exists(root)) continue;
                    foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                    {
                        var rel = Files.Rel(root, file);
                        if (IsGameFile(rel) && !f.Game.ContainsKey(rel)) f.Game[rel] = file;
                    }
                }
                foreach (var kv in m.Deploy)
                {
                    var src = Path.Combine(m.Dir, Files.Native(kv.Key));
                    if (Directory.Exists(src))
                        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories)) f.Deploy[kv.Value.TrimEnd('/') + "/" + Files.Rel(src, file)] = file;
                    else if (File.Exists(src)) f.Deploy[kv.Value] = src;
                }
            }
            catch (Exception ex) { Log.Write("footprint " + m.Id + ": " + ex.Message); }
            lock (footprints) footprints[m.Id] = f;
            return f;
        }

        /// <summary>Fichier réellement chargé par le jeu (regulation.bin ou données d'un dossier du jeu), pas un readme ni une image d'aperçu.</summary>
        static bool IsGameFile(string rel)
        {
            int i = rel.IndexOf('/');
            if (i < 0) return rel.Equals("regulation.bin", StringComparison.OrdinalIgnoreCase);
            return Installer.GameDirs.Contains(rel.Substring(0, i).ToLowerInvariant());
        }

        static bool SameContent(string a, string b)
        {
            var fa = new FileInfo(a);
            var fb = new FileInfo(b);
            return fa.Exists && fb.Exists && fa.Length == fb.Length && Hash(fa).Equals(Hash(fb), StringComparison.OrdinalIgnoreCase);
        }

        static string Hash(FileInfo f)
        {
            var key = f.FullName + "|" + f.Length + "|" + f.LastWriteTimeUtc.Ticks;
            string h;
            lock (hashes) if (hashes.TryGetValue(key, out h)) return h;
            h = Files.Sha256(f.FullName);
            lock (hashes) hashes[key] = h;
            return h;
        }

        /// <summary>Chemins fournis par les deux mods avec un contenu différent (un fichier identique ne gêne pas).</summary>
        static List<string> Overlap(Dictionary<string, string> a, Dictionary<string, string> b)
        {
            if (a.Count > b.Count) { var t = a; a = b; b = t; }
            return a.Where(kv => b.ContainsKey(kv.Key) && !SameContent(kv.Value, b[kv.Key])).Select(kv => kv.Key).OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToList();
        }

        /// <summary>Incompatibilité déclarée par le catalogue entre deux mods, dans un sens ou dans l'autre.</summary>
        public static CatalogConflict Declared(Catalog catalog, string a, string b)
        {
            if (catalog == null) return null;
            var ca = catalog.Find(a);
            var cb = catalog.Find(b);
            return (ca == null ? null : ca.Conflicts.FirstOrDefault(c => c.Id.Equals(b, StringComparison.OrdinalIgnoreCase)))
                ?? (cb == null ? null : cb.Conflicts.FirstOrDefault(c => c.Id.Equals(a, StringComparison.OrdinalIgnoreCase)));
        }

        /// <summary>Mods du catalogue déclarés incompatibles avec cm (déclaration de cm ou de l'autre mod).</summary>
        public static List<CatalogConflict> DeclaredFor(Catalog catalog, CatalogMod cm)
        {
            var list = cm.Conflicts.ToList();
            foreach (var other in catalog.Mods.Where(o => o != cm))
                foreach (var c in other.Conflicts)
                    if (c.Id.Equals(cm.Id, StringComparison.OrdinalIgnoreCase) && !list.Any(x => x.Id.Equals(other.Id, StringComparison.OrdinalIgnoreCase)))
                        list.Add(new CatalogConflict { Id = other.Id, Reason = c.Reason });
            return list;
        }

        static ModConflict Between(InstalledMod a, InstalledMod b, Catalog catalog)
        {
            var declared = Declared(catalog, a.Id, b.Id);
            if (declared != null) return new ModConflict { Kind = "declared", Reason = declared.Reason };
            Footprint fa = Of(a), fb = Of(b);
            var files = Overlap(fa.Game, fb.Game);
            if (files.Count > 0) return new ModConflict { Kind = "files", Files = files };
            files = Overlap(fa.Deploy, fb.Deploy);
            if (files.Count > 0) return new ModConflict { Kind = "deploy", Files = files };
            return null;
        }

        /// <summary>Incompatibilités de chaque mod installé, activé ou non (la relation est symétrique).</summary>
        public static Dictionary<string, List<ModConflict>> Compute(ModStore store, Catalog catalog)
        {
            var mods = store.Mods.ToList();
            var map = mods.ToDictionary(m => m.Id, m => new List<ModConflict>(), StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < mods.Count; i++)
                for (int j = i + 1; j < mods.Count; j++)
                {
                    var c = Between(mods[i], mods[j], catalog);
                    if (c == null) continue;
                    map[mods[i].Id].Add(new ModConflict { With = mods[j].Id, Kind = c.Kind, Reason = c.Reason, Files = c.Files });
                    map[mods[j].Id].Add(new ModConflict { With = mods[i].Id, Kind = c.Kind, Reason = c.Reason, Files = c.Files });
                }
            return map;
        }

        /// <summary>Incompatibilités de m avec des mods actuellement activés : tant qu'il y en a, m ne peut pas être activé.</summary>
        public static List<ModConflict> Blockers(Dictionary<string, List<ModConflict>> map, ModStore store, InstalledMod m)
        {
            List<ModConflict> list;
            if (!map.TryGetValue(m.Id, out list)) return new List<ModConflict>();
            return list.Where(c =>
            {
                var other = store.Get(c.With);
                return other != null && other != m && other.Enabled;
            }).ToList();
        }

        /// <summary>
        /// Garantit qu'aucun couple incompatible n'est activé : dans l'ordre de chargement, un mod incompatible avec un mod
        /// déjà retenu est désactivé. Renvoie les mods désactivés et la raison.
        /// </summary>
        public static List<KeyValuePair<InstalledMod, ModConflict>> Resolve(ModStore store, Catalog catalog)
        {
            var map = Compute(store, catalog);
            var kept = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var off = new List<KeyValuePair<InstalledMod, ModConflict>>();
            foreach (var m in store.Mods.Where(x => x.Enabled).ToList())
            {
                var c = map[m.Id].FirstOrDefault(x => kept.Contains(x.With));
                if (c == null) { kept.Add(m.Id); continue; }
                m.Enabled = false;
                off.Add(new KeyValuePair<InstalledMod, ModConflict>(m, c));
                Log.Write("conflict: " + m.Id + " disabled (" + c.Kind + " with " + c.With + ")");
            }
            if (off.Count > 0) store.Save();
            return off;
        }
    }
}
