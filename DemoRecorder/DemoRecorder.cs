using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;

namespace DemoRecorder;
    
public class DemoRecorder : BasePlugin
{
    public override string ModuleName => "Demo Recorder";
    public override string ModuleAuthor => "Jon-Mailes Graeffe <mail@jonni.it>";
    public override string ModuleVersion => "1.0.0";

    private string? _demoName = null;
    
    public override void Load(bool hotReload)
    {
        Directory.SetCurrentDirectory(Server.GameDirectory);
        Directory.CreateDirectory("csgo/addons/counterstrikesharp/data/demos");
        
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
        RegisterListener<Listeners.OnMapStart>(OnMapStart);
    }

    private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        if (_demoName == null) return HookResult.Continue;
        Server.ExecuteCommand($"tv_record \"addons/counterstrikesharp/data/demos/{_demoName}\"");
        _demoName = null;
        return HookResult.Continue;
    }

    private void OnMapStart(string mapName)
    {
        _demoName = DateTime.Now.ToString("dd_MM_yyyy_HH_mm") + "-" + mapName + ".dem";
    }
}
