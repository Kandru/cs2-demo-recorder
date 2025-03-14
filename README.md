# CounterstrikeSharp - Automatic SourceTV Demo Recorder

[![UpdateManager Compatible](https://img.shields.io/badge/CS2-UpdateManager-darkgreen)](https://github.com/Kandru/cs2-update-manager/)
[![GitHub release](https://img.shields.io/github/release/Kandru/cs2-demo-recorder?include_prereleases=&sort=semver&color=blue)](https://github.com/Kandru/cs2-demo-recorder/releases/)
[![License](https://img.shields.io/badge/License-GPLv3-blue)](#license)
[![issues - cs2-map-modifier](https://img.shields.io/github/issues/Kandru/cs2-demo-recorder)](https://github.com/Kandru/cs2-demo-recorder/issues)
[![](https://www.paypalobjects.com/en_US/i/btn/btn_donateCC_LG.gif)](https://www.paypal.com/donate/?hosted_button_id=C2AVYKGVP9TRG)

This tool automatically start a recording whenever someone is on your server. It makes sure to stop the recording before the level is being changed. CounterStrike 2 does currently crash if the HLTV recording is not stopped before the map is changed regardless of what caused a map change. This tool will make sure to stop the recording first and afterwards change the map.

## Features

- check server state on plugin hot-reload
- enables the HLTV before starting to record
- stops recording when last player left the server
- starts recording when first player joins the server
- disables transmit of HLTV entity to players to avoid one possible way to detect recording

## Installation

1. Download and extract the latest release from the [GitHub releases page](https://github.com/Kandru/cs2-demo-recorder/releases/).
2. Move the "DemoRecorder" folder to the `/addons/counterstrikesharp/configs/plugins/` directory.
3. Restart the server.

Updating is even easier: simply overwrite all plugin files and they will be reloaded automatically. To automate updates please use our [CS2 Update Manager](https://github.com/Kandru/cs2-update-manager/).


## Configuration

This plugin automatically creates a readable JSON configuration file. This configuration file can be found in `/addons/counterstrikesharp/configs/plugins/DemoRecorder/DemoRecorder.json`.

```json
{
  "enabled": true,
  "demo_folder": "",
  "changelevel_delay": 3,
  "minimum_players_for_recording": 1,
  "transmit_hltv_entity": false,
  "hltv_name": "visit Counterstrike.Party",
  "ConfigVersion": 1
}
```

Config will be read on map start to update all parameters without the need to reload the plugin.

### enabled

Disabled this plugin without removing it.

### demo_folder

Folder where to save recorded demos to. A default absolute path will be generated if this value is empty or does not exist. Please allow the plugin to generate this path first and change it afterwards.

### changelevel_delay

Minimum delay before map will be changed when using a map-change command. Should be at least one second to allow SourceTV to stop recording properly.

### transmit_hltv_entity

An experimental feature to not send the HLTV entity to players to avoid detection whether a demo is being recorded right now. Does not complete hide the fact that a SourceTV is currently active but may helps to avoid detection of whether we currently record or not.

### hltv_name

Change the name of the SourceTV. Simply changes "tv_name".

## Compile Yourself

Clone the project:

```bash
git clone https://github.com/Kandru/cs2-demo-recorder.git
```

Go to the project directory

```bash
  cd cs2-demo-recorder
```

Install dependencies

```bash
  dotnet restore
```

Build debug files (to use on a development game server)

```bash
  dotnet build
```

Build release files (to use on a production game server)

```bash
  dotnet publish
```

## FAQ

TODO

## License

Released under [GPLv3](/LICENSE) by [@Kandru](https://github.com/Kandru).

## Authors

- [@derkalle4](https://www.github.com/derkalle4)
- [@jmgraeffe](https://www.github.com/jmgraeffe)
