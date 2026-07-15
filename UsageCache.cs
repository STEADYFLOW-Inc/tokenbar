using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace ClaudeTokenMeter
{
    /// <summary>
    /// Persists the last successful API usage snapshot to disk so it survives
    /// restarts and can be preferred over the local JSONL estimate during
    /// transient API outages (e.g. 429 back-off).
    /// </summary>
    internal static class UsageCache
    {
        private static string CachePath
        {
            get
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "usage_cache.json");
            }
        }

        /// <summary>
        /// Serializes the API-sourced fields of <paramref name="r"/> to disk.
        /// All exceptions are swallowed.
        /// </summary>
        public static void Save(UsageResult r)
        {
            try
            {
                if (r == null)
                    return;

                Dictionary<string, object> map = new Dictionary<string, object>();
                map["sessionPct"] = r.SessionPct;
                map["sessionResetUtcTicks"] = TicksOrNull(r.SessionResetUtc);
                map["weeklyPct"] = r.WeeklyPct;
                map["weeklyResetUtcTicks"] = TicksOrNull(r.WeeklyResetUtc);
                map["hasWeeklyScoped"] = r.HasWeeklyScoped;
                map["weeklyScopedPct"] = r.WeeklyScopedPct;
                map["weeklyScopedModel"] = r.WeeklyScopedModel;
                map["fetchedAtUtcTicks"] = r.FetchedAtUtc.Ticks;

                List<object> scoped = new List<object>();
                if (r.ScopedLimits != null)
                {
                    foreach (ScopedLimit sl in r.ScopedLimits)
                    {
                        if (sl == null)
                            continue;
                        Dictionary<string, object> entry = new Dictionary<string, object>();
                        entry["model"] = sl.Model;
                        entry["pct"] = sl.Pct;
                        scoped.Add(entry);
                    }
                }
                map["scopedLimits"] = scoped;

                JavaScriptSerializer ser = new JavaScriptSerializer();
                ser.MaxJsonLength = int.MaxValue;
                string json = ser.Serialize(map);
                File.WriteAllText(CachePath, json);
            }
            catch (Exception)
            {
                // swallow
            }
        }

        /// <summary>
        /// Loads the cached API snapshot into <paramref name="result"/>, marking it
        /// as a stale API result. Returns true on success, false on any failure.
        /// </summary>
        public static bool TryLoad(UsageResult result)
        {
            try
            {
                if (result == null)
                    return false;

                string path = CachePath;
                if (!File.Exists(path))
                    return false;

                string json = File.ReadAllText(path);
                JavaScriptSerializer ser = new JavaScriptSerializer();
                ser.MaxJsonLength = int.MaxValue;

                Dictionary<string, object> map = ser.DeserializeObject(json) as Dictionary<string, object>;
                if (map == null)
                    return false;

                result.SessionPct = GetDouble(map, "sessionPct");
                result.SessionResetUtc = GetDateTimeFromTicks(map, "sessionResetUtcTicks");
                result.WeeklyPct = GetDouble(map, "weeklyPct");
                result.WeeklyResetUtc = GetDateTimeFromTicks(map, "weeklyResetUtcTicks");
                result.HasWeeklyScoped = GetBool(map, "hasWeeklyScoped");
                result.WeeklyScopedPct = GetDouble(map, "weeklyScopedPct");
                result.WeeklyScopedModel = GetString(map, "weeklyScopedModel");

                long fetchedTicks = GetLong(map, "fetchedAtUtcTicks");
                if (fetchedTicks > 0)
                    result.FetchedAtUtc = new DateTime(fetchedTicks, DateTimeKind.Utc);

                object scopedObj;
                if (map.TryGetValue("scopedLimits", out scopedObj))
                {
                    object[] scoped = scopedObj as object[];
                    if (scoped != null)
                    {
                        List<ScopedLimit> list = new List<ScopedLimit>();
                        foreach (object item in scoped)
                        {
                            Dictionary<string, object> entry = item as Dictionary<string, object>;
                            if (entry == null)
                                continue;
                            ScopedLimit sl = new ScopedLimit();
                            sl.Model = GetString(entry, "model");
                            sl.Pct = GetDouble(entry, "pct");
                            list.Add(sl);
                        }
                        if (list.Count > 0)
                            result.ScopedLimits = list;
                    }
                }

                result.FromApi = true;
                result.Stale = true;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // --- Helpers ---

        private static object TicksOrNull(DateTime? dt)
        {
            if (dt == null)
                return null;
            return dt.Value.Ticks;
        }

        private static DateTime? GetDateTimeFromTicks(Dictionary<string, object> map, string key)
        {
            object val;
            if (map == null || !map.TryGetValue(key, out val) || val == null)
                return null;
            try
            {
                long ticks = Convert.ToInt64(val);
                if (ticks <= 0)
                    return null;
                return new DateTime(ticks, DateTimeKind.Utc);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static long GetLong(Dictionary<string, object> map, string key)
        {
            object val;
            if (map == null || !map.TryGetValue(key, out val) || val == null)
                return 0L;
            try
            {
                return Convert.ToInt64(val);
            }
            catch (Exception)
            {
                return 0L;
            }
        }

        private static double GetDouble(Dictionary<string, object> map, string key)
        {
            object val;
            if (map == null || !map.TryGetValue(key, out val) || val == null)
                return 0.0;
            try
            {
                return Convert.ToDouble(val);
            }
            catch (Exception)
            {
                return 0.0;
            }
        }

        private static bool GetBool(Dictionary<string, object> map, string key)
        {
            object val;
            if (map == null || !map.TryGetValue(key, out val) || val == null)
                return false;
            try
            {
                return Convert.ToBoolean(val);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string GetString(Dictionary<string, object> map, string key)
        {
            object val;
            if (map == null || !map.TryGetValue(key, out val) || val == null)
                return null;
            return val as string;
        }
    }
}
