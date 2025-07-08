using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Extensions;
using DemoRecorder.Utils;

namespace DemoRecorder
{
    public partial class DemoRecorder
    {
        [ConsoleCommand("demorecorder", "DemoRecorder admin commands")]
        [CommandHelper(whoCanExecute: CommandUsage.SERVER_ONLY, minArgs: 1, usage: "<command>")]
        public void CommandMapVote(CCSPlayerController player, CommandInfo command)
        {
            string subCommand = command.GetArg(1);
            switch (subCommand.ToLower(System.Globalization.CultureInfo.CurrentCulture))
            {
                case "reload":
                    Config.Reload();
                    command.ReplyToCommand(Localizer["admin.reload"]);
                    break;
                case "enable":
                    // enable plug-in
                    Config.Enabled = true;
                    // update config
                    Config.Update();
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
                    command.ReplyToCommand(Localizer["admin.enabled"]);
                    break;
                case "disable":
                    // stop recording
                    StopRecording();
                    // disable plug-in
                    Config.Enabled = false;
                    // update config
                    Config.Update();
                    command.ReplyToCommand(Localizer["admin.disabled"]);
                    break;
                default:
                    command.ReplyToCommand(Localizer["admin.unknown_command"].Value
                        .Replace("{command}", subCommand));
                    break;
            }
        }
    }
}
