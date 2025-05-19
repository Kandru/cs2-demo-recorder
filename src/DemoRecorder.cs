using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Extensions;
using System.Text.Json.Serialization;
using DemoRecorder.Utils;

namespace DemoRecorder;

public class PluginConfig : BasePluginConfig
{
    // enable plugin
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
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

public partial class DemoRecorder : BasePlugin, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "Demo Recorder";
    public override string ModuleAuthor => "Jon-Mailes Graeffe <mail@jonni.it> / Kalle <kalle@kandru.de>";

    public required PluginConfig Config { get; set; }
    private bool _isRecording = false;
    private bool _isRecordingForbidden = true;

    public void OnConfigParsed(PluginConfig config)
    {
        Config = config;
        // create directory for demos
        if (Config.DemoFolder == "")
            Config.DemoFolder = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(Server.GameDirectory) ?? "./",
                "game/csgo/addons/counterstrikesharp/data/demos/"));
        // update config file with latest plugin changes
        Config.Update();
        // create demo directory if not exists
        if (!Directory.Exists(Config.DemoFolder))
            Directory.CreateDirectory(Config.DemoFolder);
    }

    public override void Load(bool hotReload)
    {
        // register listener
        AddCommandListener("changelevel", CommandListener_Changelevel, HookMode.Pre);
        AddCommandListener("ds_workshop_changelevel", CommandListener_Changelevel, HookMode.Pre);
        AddCommandListener("map", CommandListener_Changelevel, HookMode.Pre);
        AddCommandListener("host_workshop_map", CommandListener_Changelevel, HookMode.Pre);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventCsIntermission>(OnCsIntermission);
        RegisterEventHandler<EventCsWinPanelMatch>(OnCsWinPanelMatch);
        RegisterEventHandler<EventCsMatchEndRestart>(OnCsMatchEndRestart);
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
        RegisterListener<Listeners.OnMapEnd>(OnMapEnd);
        RegisterListener<Listeners.OnServerHibernationUpdate>(OnServerHibernationUpdate);
        if (!Config.TransmitHLTV)
            RegisterListener<Listeners.CheckTransmit>(EventCheckTransmit);
        // set name of HLTV
        if (Config.HLTVName != "")
            Server.ExecuteCommand($"tv_name \"{Config.HLTVName}\"");
        if (hotReload)
        {
            // check if it is during a round, not end match
            if ((int)GameRules.Get("GamePhase")! <= 3)
            {
                // allow recording
                _isRecordingForbidden = false;
                // start recording if players are connected
                if (EnoughPlayersConnected() && CheckForWarmup())
                    StartRecording();
            }
        }
    }

    public void Unload()
    {
        // stop recording
        StopRecording();
        // deregister listener
        RemoveCommandListener("changelevel", CommandListener_Changelevel, HookMode.Pre);
        RemoveCommandListener("ds_workshop_changelevel", CommandListener_Changelevel, HookMode.Pre);
        RemoveCommandListener("map", CommandListener_Changelevel, HookMode.Pre);
        RemoveCommandListener("host_workshop_map", CommandListener_Changelevel, HookMode.Pre);
        DeregisterEventHandler<EventRoundStart>(OnRoundStart);
        DeregisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        DeregisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        DeregisterEventHandler<EventCsIntermission>(OnCsIntermission);
        DeregisterEventHandler<EventCsWinPanelMatch>(OnCsWinPanelMatch);
        DeregisterEventHandler<EventCsMatchEndRestart>(OnCsMatchEndRestart);
        RemoveListener<Listeners.OnMapStart>(OnMapStart);
        RemoveListener<Listeners.OnMapEnd>(OnMapEnd);
        RemoveListener<Listeners.OnServerHibernationUpdate>(OnServerHibernationUpdate);
        RemoveListener<Listeners.CheckTransmit>(EventCheckTransmit);
    }

    private HookResult CommandListener_Changelevel(CCSPlayerController? player, CommandInfo commandInfo)
    {
        // intercept if recording is active and changelevel is called
        if (_isRecording && commandInfo.ArgCount >= 2)
        {
            _isRecordingForbidden = true;
            StopRecording();
            string command = commandInfo.GetArg(0);
            string map = commandInfo.GetArg(1);
            // delay changelevel
            AddTimer(Config.ChangelevelDelay, () => Server.ExecuteCommand($"{command} {map}"));
            // stop further event execution
            return HookResult.Stop;
        }
        return HookResult.Continue;
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        _isRecordingForbidden = false;
        if (EnoughPlayersConnected() && CheckForWarmup())
            StartRecording();
        return HookResult.Continue;
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        if (EnoughPlayersConnected() && CheckForWarmup())
            StartRecording();
        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (!EnoughPlayersConnected())
            StopRecording();
        return HookResult.Continue;
    }

    private HookResult OnCsIntermission(EventCsIntermission @event, GameEventInfo info)
    {
        _isRecordingForbidden = true;
        StopRecording();
        return HookResult.Continue;
    }

    private HookResult OnCsWinPanelMatch(EventCsWinPanelMatch @event, GameEventInfo info)
    {
        _isRecordingForbidden = true;
        StopRecording();
        return HookResult.Continue;
    }

    private HookResult OnCsMatchEndRestart(EventCsMatchEndRestart @event, GameEventInfo info)
    {
        _isRecordingForbidden = true;
        StopRecording();
        return HookResult.Continue;
    }

    private void OnMapStart(string mapName)
    {
        Config.Reload();
        _isRecordingForbidden = false;
    }

    private void OnMapEnd()
    {
        _isRecordingForbidden = true;
    }

    private void OnServerHibernationUpdate(bool isHibernating)
    {
        if (isHibernating)
        {
            _isRecordingForbidden = true;
            StopRecording();
        }
        else
        {
            _isRecordingForbidden = false;
        }
    }

    private void EventCheckTransmit(CCheckTransmitInfoList infoList)
    {
        // remove listener if disabled to save resources
        if (!Config.Enabled) return;
        // worker
        foreach ((CCheckTransmitInfo info, CCSPlayerController? player) in infoList)
        {
            if (player == null
                || !player.IsValid
                || player.IsBot
                || player.IsHLTV) continue;
            // iterate through all players and remove HLTV from transmit list
            foreach (var hltv in Utilities.GetPlayers().Where(player => player.IsHLTV))
                info.TransmitEntities.Remove(hltv);
        }
    }

    private bool EnoughPlayersConnected()
    {
        return Utilities.GetPlayers().Count(player => !player.IsBot && !player.IsHLTV) >= Config.MinimumPlayers;
    }

    private bool CheckForWarmup()
    {
        if (Config.DisableRecordingDuringWarmup
            && (bool)GameRules.Get("WarmupPeriod")! == true)
        {
            return false;
        }
        else
        {
            return true;
        }
    }

    private void StartRecording()
    {
        if (!Config.Enabled || _isRecording || _isRecordingForbidden) return;
        _isRecording = true;
        string demoName = DateTime.Now.ToString("yyyy_MM_dd_HH_mm") + "-" + Server.MapName.ToLower() + ".dem";
        Server.ExecuteCommand($"tv_enable 1");
        Server.ExecuteCommand($"tv_record_immediate 1");
        Server.ExecuteCommand($"tv_record \"{Config.DemoFolder}/{demoName}\"");
    }

    private void StopRecording()
    {
        if (!Config.Enabled || !_isRecording) return;
        _isRecording = false;
        Server.ExecuteCommand("tv_stoprecord");
    }
}
