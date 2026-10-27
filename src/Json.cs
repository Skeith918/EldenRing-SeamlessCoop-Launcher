using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;

namespace ERSeamless
{
    /// <summary>Lecture tolérante du JSON renvoyé par JavaScriptSerializer (dictionnaires + tableaux).</summary>
    public static class Json
    {
        static JavaScriptSerializer Serializer()
        {
            return new JavaScriptSerializer { MaxJsonLength = int.MaxValue, RecursionLimit = 64 };
        }

        public static Dictionary<string, object> ParseObject(string text)
        {
            return Serializer().DeserializeObject(text) as Dictionary<string, object>;
        }

        public static string Write(object value)
        {
            return Serializer().Serialize(value);
        }

        public static object Get(Dictionary<string, object> d, string key)
        {
            object v;
            return d != null && d.TryGetValue(key, out v) ? v : null;
        }

        public static string Str(Dictionary<string, object> d, string key, string def = null)
        {
            var v = Get(d, key);
            return v == null ? def : Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static int Int(Dictionary<string, object> d, string key, int def)
        {
            var v = Get(d, key);
            if (v == null) return def;
            try { return Convert.ToInt32(v, CultureInfo.InvariantCulture); }
            catch { return def; }
        }

        public static bool Bool(Dictionary<string, object> d, string key, bool def)
        {
            var v = Get(d, key);
            return v is bool ? (bool)v : def;
        }

        public static Dictionary<string, object> Obj(Dictionary<string, object> d, string key)
        {
            return Get(d, key) as Dictionary<string, object>;
        }

        public static List<object> List(Dictionary<string, object> d, string key)
        {
            var v = Get(d, key);
            if (v == null || v is string || !(v is IEnumerable)) return new List<object>();
            return ((IEnumerable)v).Cast<object>().ToList();
        }

        public static List<Dictionary<string, object>> Objs(Dictionary<string, object> d, string key)
        {
            return List(d, key).OfType<Dictionary<string, object>>().ToList();
        }

        public static string[] Strs(Dictionary<string, object> d, string key)
        {
            return List(d, key).Where(x => x != null).Select(x => Convert.ToString(x, CultureInfo.InvariantCulture)).ToArray();
        }

        /// <summary>Texte localisé : chaîne simple ou { "fr": …, "en": …, "es": … } (repli sur l'anglais).</summary>
        public static string Loc(object v)
        {
            if (v == null) return null;
            var s = v as string;
            if (s != null) return s;
            var d = v as Dictionary<string, object>;
            if (d == null) return Convert.ToString(v, CultureInfo.InvariantCulture);
            object t;
            if (d.TryGetValue(L.Code, out t) && t != null) return t.ToString();
            if (d.TryGetValue("en", out t) && t != null) return t.ToString();
            return d.Values.OfType<string>().FirstOrDefault();
        }
    }
}
