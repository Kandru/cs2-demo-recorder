using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Extensions;
using DemoRecorder.Utils;

namespace DemoRecorder
{
    public partial class DemoRecorder : BasePlugin
    {
        public override string ModuleName => "Demo Recorder";
        public override string ModuleAuthor => "Jon-Mailes Graeffe <mail@jonni.it> / Kalle <kalle@kandru.de>";

        private bool _isRecording;
        private bool _isRecordingForbidden = true;

        public override void Load(bool hotReload)
        {
            Console.WriteLine(Localizer["core.init"]);
            // register listener
            AddCommandListener("changelevel", CommandListener_Changelevel, HookMode.Pre);
            AddCommandListener("ds_workshop_changelevel", CommandListener_Changelevel, HookMode.Pre);
            AddCommandListener("map", CommandListener_Changelevel, HookMode.Pre);
            AddCommandListener("host_workshop_map", CommandListener_Changelevel, HookMode.Pre);
            RegisterEventHandler<EventRoundStart>(OnRoundStart);
            RegisterEventHandler<EventPlayerConnectFull>(OnPlayerConnectFull);
            RegisterEventHandler<EventPlayerDisconnect>(OnPlayerDisconnect);
            RegisterEventHandler<EventCsWinPanelMatch>(OnCsWinPanelMatch);
            RegisterEventHandler<EventBeginNewMatch>(OnBeginNewMatch);
            RegisterEventHandler<EventCsMatchEndRestart>(OnCsMatchEndRestart);
            RegisterListener<Listeners.OnMapStart>(OnMapStart);
            RegisterListener<Listeners.OnMapEnd>(OnMapEnd);
            RegisterListener<Listeners.OnServerHibernationUpdate>(OnServerHibernationUpdate);

            if (hotReload)
            {
                // check if it is during a round, not end match
                if ((int)GameRules.Get("GamePhase")! <= 3)
                {
                    // allow recording
                    _isRecordingForbidden = false;
                    // check if we should start recording
                    if (EnoughPlayersConnected() && CheckForWarmup())
                    {
                        StartRecording();
                    }
                }
            }
        }

        public void Unload()
        {
            Console.WriteLine(Localizer["core.shutdown"]);
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
            DeregisterEventHandler<EventCsWinPanelMatch>(OnCsWinPanelMatch);
            DeregisterEventHandler<EventBeginNewMatch>(OnBeginNewMatch);
            DeregisterEventHandler<EventCsMatchEndRestart>(OnCsMatchEndRestart);
            RemoveListener<Listeners.OnMapStart>(OnMapStart);
            RemoveListener<Listeners.OnMapEnd>(OnMapEnd);
            RemoveListener<Listeners.OnServerHibernationUpdate>(OnServerHibernationUpdate);
        }

        private HookResult CommandListener_Changelevel(CCSPlayerController? player, CommandInfo commandInfo)
        {
            // intercept if recording is active and changelevel is called
            if (_isRecording && commandInfo.ArgCount >= 2)
            {
                DebugPrint($"Intercepted changelevel command: {commandInfo.GetArg(0)} {commandInfo.GetArg(1)}");
                _isRecordingForbidden = true;
                StopRecording();
                string command = commandInfo.GetArg(0);
                string map = commandInfo.GetArg(1);
                // delay changelevel
                _ = AddTimer(Config.ChangelevelDelay, () => Server.ExecuteCommand($"{command} {map}"));
                // stop further event execution
                return HookResult.Stop;
            }
            return HookResult.Continue;
        }

        private HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
        {
            DebugPrint("Round started");
            _isRecordingForbidden = false;
            // check if we should start recording
            if (EnoughPlayersConnected() && CheckForWarmup())
            {
                StartRecording();
            }
            return HookResult.Continue;
        }

        private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;
            if (player == null
                || player.IsBot
                || player.IsHLTV)
            {
                return HookResult.Continue;
            }
            DebugPrint($"Player {player.PlayerName} connected");
            // check if we should start recording
            if (EnoughPlayersConnected() && CheckForWarmup())
            {
                StartRecording();
            }
            return HookResult.Continue;
        }

        private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            CCSPlayerController? player = @event.Userid;
            if (player == null
                || player.IsBot
                || player.IsHLTV)
            {
                return HookResult.Continue;
            }
            DebugPrint($"Player {player.PlayerName} disconnected");
            // delay a frame to not interfere with connection state of player
            Server.NextFrame(() =>
            {
                if (!EnoughPlayersConnected())
                {
                    StopRecording();
                }
            });
            return HookResult.Continue;
        }

        private HookResult OnCsWinPanelMatch(EventCsWinPanelMatch @event, GameEventInfo info)
        {
            DebugPrint("Match ended");
            _isRecordingForbidden = true;
            StopRecording();
            return HookResult.Continue;
        }

        private HookResult OnBeginNewMatch(EventBeginNewMatch @event, GameEventInfo info)
        {
            DebugPrint("New match started");
            _isRecordingForbidden = false;
            // check if we should start recording
            if (EnoughPlayersConnected() && CheckForWarmup())
            {
                StartRecording();
            }
            return HookResult.Continue;
        }

        private HookResult OnCsMatchEndRestart(EventCsMatchEndRestart @event, GameEventInfo info)
        {
            DebugPrint("Match end restart");
            _isRecordingForbidden = true;
            return HookResult.Continue;
        }

        private void OnMapStart(string mapName)
        {
            DebugPrint($"Map started: {mapName}");
            Config.Reload();
            _isRecordingForbidden = false;
        }

        private void OnMapEnd()
        {
            DebugPrint("Map ended");
            _isRecordingForbidden = true;
            DisableHLTV();
        }

        private void OnServerHibernationUpdate(bool isHibernating)
        {
            DebugPrint($"Server hibernation update: Hibernation {(isHibernating ? "started" : "ended")}");
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

        private bool EnoughPlayersConnected()
        {
            int playerCount = Utilities.GetPlayers().Count(static player => !player.IsBot && !player.IsHLTV);
            DebugPrint($"Checking whether player-threshold is matched: {playerCount} >= {Config.MinimumPlayers} ({playerCount >= Config.MinimumPlayers})");
            return playerCount >= Config.MinimumPlayers;
        }

        private bool CheckForWarmup()
        {
            DebugPrint("Checking for warmup period: disabled during warmup: " + (Config.DisableRecordingDuringWarmup ? "yes" : "no") + ", warmup period active: " + ((bool)GameRules.Get("WarmupPeriod")! ? "yes" : "no"));
            return !Config.DisableRecordingDuringWarmup
                || !(bool)GameRules.Get("WarmupPeriod")!;
        }

        private void StartRecording()
        {
            DebugPrint("Starting recording");
            if (!Config.Enabled || _isRecording || _isRecordingForbidden)
            {
                DebugPrint("Recording is already active or forbidden, not starting a new recording.");
                return;
            }
            _isRecording = true;
            // delay setting commands to avoid interference with internal cs2 logic (commands take a tick to be executed)
            Server.NextFrame(() =>
            {
                // enable HLTV before recording (because on map start may not work due to hibernation enabled)
                EnableHLTV();
                // delay demo recording a frame further
                Server.NextFrame(() =>
                {
                    string demoName = DateTime.Now.ToString("yyyy_MM_dd_HH_mm") + "-" + Server.MapName.ToLower(System.Globalization.CultureInfo.CurrentCulture) + ".dem";
                    Server.ExecuteCommand($"tv_record \"{Config.DemoFolder}/{demoName}\" -instance 1".Replace("//", "/"));
                    DebugPrint($"Recording started: {demoName}");
                });
            });
        }

        private void StopRecording()
        {
            DebugPrint("Stopping recording");
            if (!Config.Enabled || !_isRecording)
            {
                DebugPrint("Recording is not active, nothing to stop.");
                return;
            }
            _isRecording = false;
            Server.ExecuteCommand("tv_stoprecord -instance 1");
            DebugPrint("Recording stopped");
        }

        private void EnableHLTV()
        {
            DebugPrint("Enabling HLTV");
            Server.ExecuteCommand($"tv_enable 1");
            Server.ExecuteCommand($"tv_record_immediate 1");
        }

        private void DisableHLTV()
        {
            DebugPrint("Disabling HLTV");
            Server.ExecuteCommand($"tv_enable 0");
        }
    }
}
