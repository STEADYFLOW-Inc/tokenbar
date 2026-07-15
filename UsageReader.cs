using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Web.Script.Serialization;

namespace ClaudeTokenMeter
{
    public class ScopedLimit
    {
        public string Model;
        public double Pct;
    }

    public class UsageResult
    {
        public long UsedTokens;
        public bool Active;
        public DateTime? BlockStartUtc;
        public DateTime? ResetAtUtc;
        public DateTime? LastActivityUtc;
        public string Error;

        // API-sourced fields (FromApi == true when these are populated)
        public bool FromApi;
        public double SessionPct;           // 5-hour window utilization 0-100
        public DateTime? SessionResetUtc;
        public double WeeklyPct;            // weekly_all utilization 0-100
        public DateTime? WeeklyResetUtc;
        public bool HasWeeklyScoped;
        public double WeeklyScopedPct;      // model-scoped weekly utilization 0-100
        public string WeeklyScopedModel;    // e.g. "Fable"

        // All weekly_scoped entries (null when none); first entry mirrors HasWeeklyScoped/WeeklyScopedPct/WeeklyScopedModel.
        public List<ScopedLimit> ScopedLimits;

        // Timestamp of when this result was produced.
        public DateTime FetchedAtUtc;
        // True when this result is a cached API snapshot being shown during a transient outage.
        public bool Stale;
    }

    public static class UsageReader
    {
        private struct TokenEntry
        {
            public DateTime Ts;
            public long Tokens;
        }

        public static UsageResult Read(Config cfg)
        {
            UsageResult result = new UsageResult();
            result.FetchedAtUtc = DateTime.UtcNow;
            try
            {
                // 1. Try the OAuth usage API first; on success (live) it short-circuits.
                if (ApiUsageReader.TryRead(cfg, result))
                    return result;

                // 2. API unavailable: prefer the last successful API snapshot from disk.
                //    We still run the local JSONL estimation below into the SAME result so
                //    tooltip diagnostics (UsedTokens/Active/BlockStart) remain available;
                //    the cached API fields + FromApi=true + Stale=true take precedence since
                //    they occupy different fields than the local computation.
                UsageCache.TryLoad(result);

                string root = Path.Combine(cfg.ResolveClaudeDir(), "projects");
                if (!Directory.Exists(root))
                {
                    result.Error = "projects フォルダが見つかりません";
                    return result;
                }

                DateTime nowUtc = DateTime.UtcNow;
                DateTime entryCutoff = nowUtc.AddHours(-11);
                DateTime fileCutoff = nowUtc.AddHours(-11.5);

                JavaScriptSerializer ser = new JavaScriptSerializer();
                ser.MaxJsonLength = int.MaxValue;

                HashSet<string> seenIds = new HashSet<string>(StringComparer.Ordinal);
                List<TokenEntry> entries = new List<TokenEntry>();

                IEnumerable<string> files = Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    if (File.GetLastWriteTimeUtc(file) < fileCutoff)
                        continue;

                    CollectFromFile(file, ser, entryCutoff, nowUtc, seenIds, entries, cfg);
                }

                entries.Sort(CompareEntries);
                ComputeCurrentBlock(entries, nowUtc, result);
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
            }
            return result;
        }

        private static int CompareEntries(TokenEntry a, TokenEntry b)
        {
            return a.Ts.CompareTo(b.Ts);
        }

        private static void CollectFromFile(
            string file,
            JavaScriptSerializer ser,
            DateTime entryCutoff,
            DateTime nowUtc,
            HashSet<string> seenIds,
            List<TokenEntry> entries,
            Config cfg)
        {
            try
            {
                using (FileStream fs = new FileStream(file, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete))
                using (StreamReader reader = new StreamReader(fs))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        try
                        {
                            if (line.IndexOf("\"usage\"", StringComparison.Ordinal) < 0)
                                continue;

                            DateTime ts;
                            if (!TryExtractTimestamp(line, out ts))
                                continue;

                            if (ts < entryCutoff || ts > nowUtc.AddMinutes(5))
                                continue;

                            Dictionary<string, object> root = ser.DeserializeObject(line) as Dictionary<string, object>;
                            if (root == null)
                                continue;

                            object msgObj;
                            if (!root.TryGetValue("message", out msgObj))
                                continue;
                            Dictionary<string, object> msg = msgObj as Dictionary<string, object>;
                            if (msg == null)
                                continue;

                            object usageObj;
                            if (!msg.TryGetValue("usage", out usageObj))
                                continue;
                            Dictionary<string, object> usage = usageObj as Dictionary<string, object>;
                            if (usage == null)
                                continue;

                            string msgId = null;
                            object msgIdObj;
                            if (msg.TryGetValue("id", out msgIdObj))
                                msgId = msgIdObj as string;

                            string reqId = null;
                            object reqIdObj;
                            if (root.TryGetValue("requestId", out reqIdObj))
                                reqId = reqIdObj as string;

                            if (msgId != null)
                            {
                                string dedupKey = msgId + ":" + (reqId ?? "");
                                if (seenIds.Contains(dedupKey))
                                    continue;
                                seenIds.Add(dedupKey);
                            }

                            long tokens = GetLong(usage, "input_tokens")
                                        + GetLong(usage, "output_tokens")
                                        + GetLong(usage, "cache_creation_input_tokens");
                            if (cfg.includeCacheRead)
                                tokens += GetLong(usage, "cache_read_input_tokens");

                            if (tokens > 0)
                            {
                                TokenEntry entry = new TokenEntry();
                                entry.Ts = ts;
                                entry.Tokens = tokens;
                                entries.Add(entry);
                            }
                        }
                        catch (Exception)
                        {
                            // skip bad line
                        }
                    }
                }
            }
            catch (Exception)
            {
                // skip file on error
            }
        }

        private static bool TryExtractTimestamp(string line, out DateTime ts)
        {
            ts = default(DateTime);
            string marker = "\"timestamp\":\"";
            int idx = line.IndexOf(marker, StringComparison.Ordinal);
            if (idx < 0)
                return false;

            int start = idx + marker.Length;
            int end = line.IndexOf('"', start);
            if (end < 0)
                return false;

            string raw = line.Substring(start, end - start);
            try
            {
                ts = DateTime.Parse(raw, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static long GetLong(Dictionary<string, object> dict, string key)
        {
            if (dict == null)
                return 0L;
            object val;
            if (!dict.TryGetValue(key, out val) || val == null)
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

        private static DateTime FloorToHour(DateTime dt)
        {
            return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, 0, 0, DateTimeKind.Utc);
        }

        private static void ComputeCurrentBlock(List<TokenEntry> entries, DateTime nowUtc, UsageResult result)
        {
            if (entries.Count == 0)
                return;

            bool blockSet = false;
            DateTime blockStart = default(DateTime);
            DateTime lastTs = default(DateTime);
            long blockTokens = 0;

            TimeSpan fiveHours = TimeSpan.FromHours(5);

            foreach (TokenEntry e in entries)
            {
                bool startNew = false;
                if (!blockSet)
                {
                    startNew = true;
                }
                else if (e.Ts >= blockStart + fiveHours)
                {
                    startNew = true;
                }
                else if ((e.Ts - lastTs) >= fiveHours)
                {
                    startNew = true;
                }

                if (startNew)
                {
                    blockStart = FloorToHour(e.Ts);
                    blockTokens = 0;
                    blockSet = true;
                }

                blockTokens += e.Tokens;
                lastTs = e.Ts;
            }

            DateTime blockEnd = blockStart + fiveHours;
            result.LastActivityUtc = lastTs;

            bool active = (nowUtc < blockEnd) && ((nowUtc - lastTs).TotalHours < 5);
            if (active)
            {
                result.Active = true;
                result.UsedTokens = blockTokens;
                result.BlockStartUtc = blockStart;
                result.ResetAtUtc = blockEnd;
            }
        }
    }
}
