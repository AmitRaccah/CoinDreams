#nullable enable

using System;
using System.Collections.Generic;

namespace Game.Infrastructure.CloudFunctions
{
    // Defensive helpers for parsing IDictionary<string, object> responses returned
    // by Firebase callable functions. The SDK upcasts numeric fields to long, so
    // every numeric reader funnels through Convert.* to avoid InvalidCastException
    // and falls back to a typed default on malformed payloads.
    internal static class CallableDataExtensions
    {
        public static string TryGetString(this IDictionary<string, object> data, string key, string fallback = "")
        {
            if (data.TryGetValue(key, out object raw) && raw != null)
            {
                return raw.ToString() ?? fallback;
            }
            return fallback;
        }

        public static int TryGetInt(this IDictionary<string, object> data, string key, int fallback = 0)
        {
            if (!data.TryGetValue(key, out object raw) || raw == null)
            {
                return fallback;
            }
            try { return Convert.ToInt32(raw); }
            catch { return fallback; }
        }

        public static long TryGetLong(this IDictionary<string, object> data, string key, long fallback = 0L)
        {
            if (!data.TryGetValue(key, out object raw) || raw == null)
            {
                return fallback;
            }
            try { return Convert.ToInt64(raw); }
            catch { return fallback; }
        }

        public static bool TryGetBool(this IDictionary<string, object> data, string key, bool fallback = false)
        {
            if (!data.TryGetValue(key, out object raw) || raw == null)
            {
                return fallback;
            }
            try { return Convert.ToBoolean(raw); }
            catch { return fallback; }
        }

        public static int[] TryGetIntArray(this IDictionary<string, object> data, string key)
        {
            if (!data.TryGetValue(key, out object raw) || raw == null)
            {
                return Array.Empty<int>();
            }
            if (raw is IList<object> list)
            {
                int[] arr = new int[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    try { arr[i] = Convert.ToInt32(list[i]); }
                    catch { arr[i] = 0; }
                }
                return arr;
            }
            return Array.Empty<int>();
        }

        public static string[] TryGetStringArray(this IDictionary<string, object> data, string key)
        {
            if (!data.TryGetValue(key, out object raw) || raw == null)
            {
                return Array.Empty<string>();
            }
            if (raw is IList<object> list)
            {
                string[] arr = new string[list.Count];
                for (int i = 0; i < list.Count; i++)
                {
                    arr[i] = list[i]?.ToString() ?? string.Empty;
                }
                return arr;
            }
            return Array.Empty<string>();
        }

        public static IDictionary<string, object>? TryGetDictionary(this IDictionary<string, object> data, string key)
        {
            if (!data.TryGetValue(key, out object raw) || raw == null)
            {
                return null;
            }
            return raw as IDictionary<string, object>;
        }
    }
}
