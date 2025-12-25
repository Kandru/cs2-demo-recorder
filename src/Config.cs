using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Extensions;
using System.Text.Json.Serialization;

namespace DemoRecorder
{
    public class PluginConfig : BasePluginConfig
    {
        // enable plugin
        [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
        // debug mode
        [JsonPropertyName("debug")] public bool Debug { get; set; } = false;
        // folder for demo files
        [JsonPropertyName("demo_folder")] public string DemoFolder { get; set; } = "";
        // delay before changelevel to allow recording to stop properly without crash
        [JsonPropertyName("changelevel_delay")] public float ChangelevelDelay { get; set; } = 3f;
        // minimum players to start recording
        [JsonPropertyName("minimum_players_for_recording")] public int MinimumPlayers { get; set; } = 1;
        // whether or not to start recording during warmup
        [JsonPropertyName("disable_recording_during_warmup")] public bool DisableRecordingDuringWarmup { get; set; } = false;
        // whether or not to transmit HLTV to players
        [JsonPropertyName("transmit_hltv_entity")] public bool TransmitHLTV { get; set; } = false;
        // name of the hltv
        [JsonPropertyName("hltv_name")] public string HLTVName { get; set; } = "visit Counterstrike.Party";
    }

    public partial class DemoRecorder : IPluginConfig<PluginConfig>
    {
        public required PluginConfig Config { get; set; }

        public void OnConfigParsed(PluginConfig config)
        {
            Config = config;
            // create directory for demos
            if (Config.DemoFolder == "")
            {
                Config.DemoFolder = Server.GameDirectory + "/csgo/addons/counterstrikesharp/data/demos/";
            }
            // update config file with latest plugin changes
            Config.Update();
            // create demo directory if not exists
            if (!Directory.Exists(Config.DemoFolder))
            {
                _ = Directory.CreateDirectory(Config.DemoFolder);
            }
            Console.WriteLine(Localizer["core.config"]);
        }
    }
}