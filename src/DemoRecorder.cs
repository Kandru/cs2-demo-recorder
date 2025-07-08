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
            if (!Config.TransmitHLTV)
            {
                RegisterListener<Listeners.CheckTransmit>(EventCheckTransmit);
            }

            if (hotReload)
            {
                // check if it is during a round, not end match
                if ((int)GameRules.Get("GamePhase")! <= 3)
                {
                    // allow recording
                    _isRecordingForbidden = false;
                    // start recording if players are connected
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
            RemoveListener<Listeners.CheckTransmit>(EventCheckTransmit);
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
            if (EnoughPlayersConnected() && CheckForWarmup())
            {
                StartRecording();
            }
            return HookResult.Continue;
        }

        private HookResult OnPlayerConnectFull(EventPlayerConnectFull @event, GameEventInfo info)
        {
            Server.NextFrame(() =>
            {
                DebugPrint($"Player connected");
                if (EnoughPlayersConnected() && CheckForWarmup())
                {
                    StartRecording();
                }
            });
            return HookResult.Continue;
        }

        private HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
        {
            Server.NextFrame(() =>
            {
                DebugPrint($"Player disconnected");
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
            EnableHLTV();
            SetHLTVName();
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

        private void EventCheckTransmit(CCheckTransmitInfoList infoList)
        {
            // remove listener if disabled to save resources
            if (!Config.Enabled)
            {
                return;
            }
            // worker
            foreach ((CCheckTransmitInfo info, CCSPlayerController? player) in infoList)
            {
                if (player == null
                    || !player.IsValid
                    || player.IsBot
                    || player.IsHLTV)
                {
                    continue;
                }
                // iterate through all players and remove HLTV from transmit list
                foreach (CCSPlayerController? hltv in Utilities.GetPlayers().Where(static player => player.IsHLTV))
                {
                    info.TransmitEntities.Remove(hltv);
                }
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
            string demoName = DateTime.Now.ToString("yyyy_MM_dd_HH_mm") + "-" + Server.MapName.ToLower(System.Globalization.CultureInfo.CurrentCulture) + ".dem";
            Server.ExecuteCommand($"tv_record \"{Config.DemoFolder}/{demoName}\" -instance 1");
            DebugPrint($"Recording started: {demoName}");
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
            Server.ExecuteCommand($"tv_enable true");
            Server.ExecuteCommand($"tv_record_immediate 1");
        }

        private void DisableHLTV()
        {
            DebugPrint("Disabling HLTV");
            Server.ExecuteCommand($"tv_enable false");
        }

        private void SetHLTVName()
        {
            DebugPrint("Setting HLTV name");
            if (Config.HLTVName != "")
            {
                Server.ExecuteCommand($"tv_name \"{Config.HLTVName}\"");
            }
        }
    }
}
