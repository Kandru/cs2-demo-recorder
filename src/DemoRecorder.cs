using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace DemoRecorder;
    
public partial class DemoRecorder : BasePlugin
{
    public override string ModuleName => "Demo Recorder";
    public override string ModuleAuthor => "Jon-Mailes Graeffe <mail@jonni.it> / Kalle <kalle@kandru.de>";

    private string _demoFolder = "addons/counterstrikesharp/data/demos";
    private bool _isRecording = false;
    
    public override void Load(bool hotReload)
    {
        // create directory for demos
        Directory.SetCurrentDirectory(Server.GameDirectory);
        Directory.CreateDirectory($"csgo/{_demoFolder}");
        // register listener
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterEventHandler<EventCsIntermission>(OnCsIntermission);
        RegisterEventHandler<EventCsWinPanelMatch>(OnCsWinPanelMatch);
        RegisterEventHandler<EventCsMatchEndRestart>(OnCsMatchEndRestart);
    }

    public void Unload()
    {
        // stop recording
        StopRecording();
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (_isRecording) return HookResult.Continue;
        StartRecording();
        return HookResult.Continue;
    }

    private HookResult OnCsIntermission(EventCsIntermission @event, GameEventInfo info)
    {
        return HookResult.Continue;
    }

    private HookResult OnCsWinPanelMatch(EventCsWinPanelMatch @event, GameEventInfo info)
    {
        if (!_isRecording) return HookResult.Continue;
        StopRecording();
        return HookResult.Continue;
    }

    private HookResult OnCsMatchEndRestart(EventCsMatchEndRestart @event, GameEventInfo info)
    {
        if (!_isRecording) return HookResult.Continue;
        StopRecording();
        return HookResult.Continue;
    }
 
    private void StartRecording()
    {
        if ( _isRecording) return;
        _isRecording = true;
        string demoName = DateTime.Now.ToString("dd_MM_yyyy_HH_mm") + "-" + Server.MapName.ToLower() + ".dem";
        Server.ExecuteCommand($"tv_record \"{_demoFolder}/{demoName}\"");
    }

    private void StopRecording()
    {
        if (!_isRecording) return;
        _isRecording = false;
        Server.ExecuteCommand("tv_stoprecord");
    }
}
