# FiveM Razer Wanted

Standalone Windows app for Razer Chroma lighting based on the GTA V/FiveM wanted level.

## Features
- Mouse calibration for Wanted HUD area.
- Separate mouse calibration for Minimap area.
- Configuration saved in `config.json`.
- 1–2 stars: blue.
- 3–4 stars: red.
- 5 stars: alternating red/blue keyboard grid.
- Test buttons for blue, red and red/blue.
- Fixed BGR color mapping for the Razer Chroma REST API.
- No FiveM resource/server installation required.

## Requirements
- Windows 10/11.
- Razer Synapse with Chroma support and the Razer Chroma SDK service available.
- Razer Chroma-capable keyboard.
- .NET 8 SDK for building from source.

## Build
```powershell
dotnet restore
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
```
