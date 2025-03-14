using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Extensions;
using System.Text.Json.Serialization;

namespace DemoRecorder;

public class PluginConfig : BasePluginConfig
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
    [JsonPropertyName("changelevel_delay")] public float ChangelevelDelay { get; set; } = 3f;
    [JsonPropertyName("transmit_hltv_entity")] public bool TransmitHLTV { get; set; } = false;
    [JsonPropertyName("hltv_name")] public string HLTVName { get; set; } = "visit Counterstrike.Party";
}

public partial class DemoRecorder : BasePlugin, IPluginConfig<PluginConfig>
{
    public override string ModuleName => "Demo Recorder";
    public override string ModuleAuthor => "Jon-Mailes Graeffe <mail@jonni.it> / Kalle <kalle@kandru.de>";

    public required PluginConfig Config { get; set; }
    private string _demoFolder = "";
    private bool _isRecording = false;
    private bool _isRecordingForbidden = true;

    public void OnConfigParsed(PluginConfig config)
    {
        Config = config;
        Config.Update();
        // create directory for demos
        _demoFolder = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Config.GetConfigPath()) ?? "./demos/", "../../../data/demos/"));
        // create directory if not exists
        if (!Directory.Exists(_demoFolder))
            Directory.CreateDirectory(_demoFolder);
    }

    public override void Load(bool hotReload)
    {
        // register listener
        AddCommandListener("changelevel", CommandListener_Changelevel, HookMode.Pre);
        AddCommandListener("ds_workshop_changelevel", CommandListener_Changelevel, HookMode.Pre);
        AddCommandListener("map", CommandListener_Changelevel, HookMode.Pre);
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
            if (GetGameRules()?.GamePhase <= 3)
            {
                // allow recording
                _isRecordingForbidden = false;
                // start recording if players are connected
                if (PlayersConnected())
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
        if (_isRecording && commandInfo.ArgCount >= 2)
        {
            _isRecordingForbidden = true;
            StopRecording();
            string command = commandInfo.GetArg(0);
            string map = commandInfo.GetArg(1);
            AddTimer(Config.ChangelevelDelay, () => Server.ExecuteCommand($"{command} {map}"));
            return HookResult.Stop;
        }
        return HookResult.Continue;
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        _isRecordingForbidden = false;
        if (PlayersConnected())
            StartRecording();
        return HookResult.Continue;
    }

    private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
    {
        StartRecording();
        return HookResult.Continue;
    }

    private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
    {
        if (!PlayersConnected())
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
        // remove listener if no players to save resources
        if (!Config.Enabled) return;
        // worker
        foreach ((CCheckTransmitInfo info, CCSPlayerController? player) in infoList)
        {
            if (player == null) continue;
            foreach (var hltv in Utilities.GetPlayers().Where(player => player.IsHLTV))
                info.TransmitEntities.Remove(hltv);
        }
    }

    private bool PlayersConnected()
    {
        return Utilities.GetPlayers().Count(player => !player.IsBot && !player.IsHLTV) > 0;
    }

    private void StartRecording()
    {
        if (!Config.Enabled || _isRecording || _isRecordingForbidden) return;
        _isRecording = true;
        string demoName = DateTime.Now.ToString("yyyy_MM_dd_HH_mm") + "-" + Server.MapName.ToLower() + ".dem";
        Server.ExecuteCommand($"tv_enable 1");
        Server.ExecuteCommand($"tv_record_immediate 1");
        Server.ExecuteCommand($"tv_record \"{_demoFolder}/{demoName}\"");
    }

    private void StopRecording()
    {
        if (!Config.Enabled || !_isRecording) return;
        _isRecording = false;
        Server.ExecuteCommand("tv_stoprecord");
    }

    private CCSGameRules? GetGameRules()
    {
        return Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules").First().GameRules;
    }
}
