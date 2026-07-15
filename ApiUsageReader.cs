using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Web.Script.Serialization;

namespace ClaudeTokenMeter
{
    internal static class ApiUsageReader
    {
        private const string UsageUrl = "https://api.anthropic.com/api/oauth/usage";

        // Back-off: when set, TryRead returns false immediately until the time elapses.
        private static DateTime blockedUntilUtc = DateTime.MinValue;

        /// <summary>
        /// Attempts to populate <paramref name="result"/> from the Claude OAuth usage API.
        /// Returns true and sets result.FromApi = true on success.
        /// Returns false on any failure; result is left untouched.
        /// </summary>
        public static bool TryRead(Config cfg, UsageResult result)
        {
            if (DateTime.UtcNow < blockedUntilUtc)
                return false;

            try
            {
                // --- 1. Load credentials ---
                string credPath = Path.Combine(cfg.ResolveClaudeDir(), ".credentials.json");
                if (!File.Exists(credPath))
                    return false;

                string credJson = File.ReadAllText(credPath);
                JavaScriptSerializer ser = new JavaScriptSerializer();
                ser.MaxJsonLength = int.MaxValue;

                Dictionary<string, object> credRoot = ser.DeserializeObject(credJson) as Dictionary<string, object>;
                if (credRoot == null)
                    return false;

                string token = ExtractAccessToken(credRoot);
                if (string.IsNullOrEmpty(token))
                    return false;

                // --- 2. HTTPS request ---
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(UsageUrl);
                req.Method = "GET";
                req.Timeout = 10000;
                req.ReadWriteTimeout = 10000;
                req.Headers["Authorization"] = "Bearer " + token;
                req.Headers["anthropic-beta"] = "oauth-2025-04-20";
                req.UserAgent = "TokenBar/1.0";
                req.Accept = "application/json";

                string body;
                try
                {
                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    {
                        if (resp.StatusCode != HttpStatusCode.OK)
                            return false;
                        using (StreamReader sr = new StreamReader(resp.GetResponseStream()))
                            body = sr.ReadToEnd();
                    }
                }
                catch (WebException wex)
                {
                    HttpWebResponse httpResp = wex.Response as HttpWebResponse;
                    if (httpResp != null)
                    {
                        int statusCode = (int)httpResp.StatusCode;
                        int backoffSec;
                        if (statusCode == 429)
                        {
                            // Respect Retry-After if present; clamp to [60, 1800].
                            string retryAfter = httpResp.Headers["Retry-After"];
                            int parsed;
                            if (!string.IsNullOrEmpty(retryAfter) && int.TryParse(retryAfter, out parsed) && parsed > 0)
                                backoffSec = Math.Min(Math.Max(parsed, 60), 1800);
                            else
                                backoffSec = 300;
                        }
                        else if (statusCode == 401 || statusCode == 403)
                        {
                            // Token may be mid-refresh; short backoff.
                            backoffSec = 120;
                        }
                        else
                        {
                            backoffSec = 60;
                        }
                        blockedUntilUtc = DateTime.UtcNow.AddSeconds(backoffSec);
                        httpResp.Close();
                    }
                    else if (wex.Response != null)
                    {
                        blockedUntilUtc = DateTime.UtcNow.AddSeconds(60);
                        wex.Response.Close();
                    }
                    return false;
                }

                // --- 3. Parse response ---
                Dictionary<string, object> root = ser.DeserializeObject(body) as Dictionary<string, object>;
                if (root == null)
                    return false;

                // five_hour is essential; abort if missing
                Dictionary<string, object> fiveHour = GetDict(root, "five_hour");
                if (fiveHour == null)
                    return false;

                result.SessionPct = GetDouble(fiveHour, "utilization");
                result.SessionResetUtc = ParseResetAt(fiveHour);

                // seven_day (weekly_all)
                Dictionary<string, object> sevenDay = GetDict(root, "seven_day");
                if (sevenDay != null)
                {
                    result.WeeklyPct = GetDouble(sevenDay, "utilization");
                    result.WeeklyResetUtc = ParseResetAt(sevenDay);
                }

                // limits array — find weekly_scoped entry
                object limitsObj;
                if (root.TryGetValue("limits", out limitsObj))
                {
                    object[] limits = limitsObj as object[];
                    if (limits != null)
                    {
                        foreach (object item in limits)
                        {
                            Dictionary<string, object> limit = item as Dictionary<string, object>;
                            if (limit == null)
                                continue;

                            object kindObj;
                            if (!limit.TryGetValue("kind", out kindObj))
                                continue;
                            string kind = kindObj as string;
                            if (kind != "weekly_scoped")
                                continue;

                            result.HasWeeklyScoped = true;
                            result.WeeklyScopedPct = GetDouble(limit, "percent");

                            // null-safe navigation: scope -> model -> display_name
                            Dictionary<string, object> scope = GetDict(limit, "scope");
                            if (scope != null)
                            {
                                Dictionary<string, object> model = GetDict(scope, "model");
                                if (model != null)
                                {
                                    object displayNameObj;
                                    if (model.TryGetValue("display_name", out displayNameObj))
                                    {
                                        string displayName = displayNameObj as string;
                                        if (!string.IsNullOrEmpty(displayName))
                                            result.WeeklyScopedModel = displayName;
                                    }
                                }
                            }

                            if (string.IsNullOrEmpty(result.WeeklyScopedModel))
                                result.WeeklyScopedModel = Strings.ScopedModelFallback;

                            break;
                        }
                    }
                }

                result.FromApi = true;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // --- Helpers ---

        private static string ExtractAccessToken(Dictionary<string, object> credRoot)
        {
            object oauthObj;
            if (!credRoot.TryGetValue("claudeAiOauth", out oauthObj))
                return null;
            Dictionary<string, object> oauth = oauthObj as Dictionary<string, object>;
            if (oauth == null)
                return null;

            object tokenObj;
            if (!oauth.TryGetValue("accessToken", out tokenObj))
                return null;
            return tokenObj as string;
        }

        private static Dictionary<string, object> GetDict(Dictionary<string, object> dict, string key)
        {
            if (dict == null)
                return null;
            object val;
            if (!dict.TryGetValue(key, out val))
                return null;
            return val as Dictionary<string, object>;
        }

        private static double GetDouble(Dictionary<string, object> dict, string key)
        {
            if (dict == null)
                return 0.0;
            object val;
            if (!dict.TryGetValue(key, out val) || val == null)
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

        private static DateTime? ParseResetAt(Dictionary<string, object> dict)
        {
            if (dict == null)
                return null;
            object val;
            if (!dict.TryGetValue("resets_at", out val) || val == null)
                return null;
            string s = val as string;
            if (string.IsNullOrEmpty(s))
                return null;
            try
            {
                return DateTime.Parse(
                    s,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
