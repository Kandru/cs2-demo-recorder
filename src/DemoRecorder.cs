using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Extensions;
using System.Text.Json.Serialization;

namespace DemoRecorder;

public class PluginConfig : BasePluginConfig
{
    [JsonPropertyName("enabled")] public bool Enabled { get; set; } = true;
}

public partial class DemoRecorder : BasePlugin
{
    public override string ModuleName => "Demo Recorder";
    public override string ModuleAuthor => "Jon-Mailes Graeffe <mail@jonni.it> / Kalle <kalle@kandru.de>";

    public required PluginConfig Config { get; set; }
    private string _demoFolder = "";
    private bool _isRecording = false;

    public void OnConfigParsed(PluginConfig config)
    {
        Config = config;
        Config.Update();
        // create directory for demos
        _demoFolder = Path.Combine(Path.GetDirectoryName(Config.GetConfigPath()) ?? "./demos/", "../../../data/demos/");
        // create directory if not exists
        if (!Directory.Exists(_demoFolder))
            Directory.CreateDirectory(_demoFolder);
    }

    public override void Load(bool hotReload)
    {
        // register listener
        AddCommandListener("changelevel", CommandListener_Changelevel, HookMode.Pre);
        AddCommandListener("ds_workshop_changelevel", CommandListener_Changelevel, HookMode.Pre);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        RegisterEventHandler<EventCsIntermission>(OnCsIntermission);
        RegisterEventHandler<EventCsWinPanelMatch>(OnCsWinPanelMatch);
        RegisterEventHandler<EventCsMatchEndRestart>(OnCsMatchEndRestart);
        if (hotReload)
        {
            if (PlayersConnected())
                StartRecording();
        }
    }

    public void Unload()
    {
        // stop recording
        StopRecording();
        // deregister listener
        RemoveCommandListener("changelevel", CommandListener_Changelevel, HookMode.Pre);
        RemoveCommandListener("ds_workshop_changelevel", CommandListener_Changelevel, HookMode.Pre);
        DeregisterEventHandler<EventRoundStart>(OnRoundStart);
        DeregisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
        DeregisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
        DeregisterEventHandler<EventCsIntermission>(OnCsIntermission);
        DeregisterEventHandler<EventCsWinPanelMatch>(OnCsWinPanelMatch);
        DeregisterEventHandler<EventCsMatchEndRestart>(OnCsMatchEndRestart);
    }

    private HookResult CommandListener_Changelevel(CCSPlayerController? player, CommandInfo commandInfo)
    {
        if (_isRecording && commandInfo.ArgCount >= 2)
        {
            StopRecording();
            string command = commandInfo.GetArg(0);
            string map = commandInfo.GetArg(1);
            AddTimer(3.0f, () => Server.ExecuteCommand($"{command} {map}"));
            return HookResult.Stop;
        }

        return HookResult.Continue;
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
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
        StopRecording();
        return HookResult.Continue;
    }

    private HookResult OnCsWinPanelMatch(EventCsWinPanelMatch @event, GameEventInfo info)
    {

        StopRecording();
        return HookResult.Continue;
    }

    private HookResult OnCsMatchEndRestart(EventCsMatchEndRestart @event, GameEventInfo info)
    {
        StopRecording();
        return HookResult.Continue;
    }

    private bool PlayersConnected()
    {
        return Utilities.GetPlayers().Count(player => !player.IsBot && !player.IsHLTV) > 0;
    }

    private void StartRecording()
    {
        if (_isRecording) return;
        _isRecording = true;
        string demoName = DateTime.Now.ToString("yyyy_MM_dd_HH_mm") + "-" + Server.MapName.ToLower() + ".dem";
        Server.ExecuteCommand($"tv_enable 1");
        Server.ExecuteCommand($"tv_record_immediate 1");
        Server.ExecuteCommand($"tv_record \"{_demoFolder}/{demoName}\"");
    }

    private void StopRecording()
    {
        if (!_isRecording) return;
        _isRecording = false;
        Server.ExecuteCommand("tv_stoprecord");
    }
}
