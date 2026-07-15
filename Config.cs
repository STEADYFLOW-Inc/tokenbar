using System;
using System.IO;
using System.Web.Script.Serialization;

namespace ClaudeTokenMeter
{
    public class Config
    {
        public long tokenLimit = 200000;
        public bool includeCacheRead = false;
        public int refreshSec = 60;
        public bool embed = true;
        public string claudeDir = "";
        public string position = "right";
        public int offsetX = 0;
        public int widgetWidth = 240;

        // Target display: 0 = primary display, otherwise the Windows display
        // number (parsed from DeviceName "\\.\DISPLAYn").
        public int monitor = 0;

        // Visibility toggles
        public bool showTitle = true;
        public bool showValueText = true;
        public bool showResetTime = true;

        // Bar visibility
        public bool showSessionBar = true;
        public bool showWeeklyBar = false;
        public bool showModelBars = false;

        // Per-model bar selection: empty = show all models.
        public string[] selectedModels = new string[0];

        public string ResolveClaudeDir()
        {
            if (!string.IsNullOrEmpty(claudeDir))
                return claudeDir;
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".claude");
        }

        public static string ConfigPath
        {
            get
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
            }
        }

        public static Config Load()
        {
            try
            {
                string path = ConfigPath;
                if (File.Exists(path))
                {
                    string text = File.ReadAllText(path);
                    JavaScriptSerializer ser = new JavaScriptSerializer();
                    Config cfg = ser.Deserialize<Config>(text);
                    if (cfg.tokenLimit <= 0)
                        cfg.tokenLimit = 200000;
                    if (cfg.refreshSec < 5)
                        cfg.refreshSec = 5;
                    if (cfg.widgetWidth < 160)
                        cfg.widgetWidth = 160;
                    if (cfg.widgetWidth > 400)
                        cfg.widgetWidth = 400;
                    if (cfg.position != "left")
                        cfg.position = "right";
                    if (cfg.monitor < 0)
                        cfg.monitor = 0;
                    if (!cfg.showSessionBar && !cfg.showWeeklyBar && !cfg.showModelBars)
                        cfg.showSessionBar = true;
                    if (cfg.selectedModels == null)
                        cfg.selectedModels = new string[0];
                    return cfg;
                }
            }
            catch (Exception)
            {
                // fall through to default
            }

            Config defaultCfg = new Config();
            defaultCfg.Save();
            return defaultCfg;
        }

        public void Save()
        {
            try
            {
                JavaScriptSerializer ser = new JavaScriptSerializer();
                string json = ser.Serialize(this);
                File.WriteAllText(ConfigPath, json);
            }
            catch (Exception)
            {
                // swallow
            }
        }

        public void CopyFrom(Config other)
        {
            if (other == null)
                return;
            tokenLimit = other.tokenLimit;
            includeCacheRead = other.includeCacheRead;
            refreshSec = other.refreshSec;
            embed = other.embed;
            claudeDir = other.claudeDir;
            position = other.position;
            offsetX = other.offsetX;
            widgetWidth = other.widgetWidth;
            monitor = other.monitor;
            showTitle = other.showTitle;
            showValueText = other.showValueText;
            showResetTime = other.showResetTime;
            showSessionBar = other.showSessionBar;
            showWeeklyBar = other.showWeeklyBar;
            showModelBars = other.showModelBars;
            selectedModels = (other.selectedModels != null)
                ? (string[])other.selectedModels.Clone()
                : new string[0];
        }
    }
}
