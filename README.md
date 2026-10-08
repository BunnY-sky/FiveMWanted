# FiveM Wanted Lighting

 Please Install OpenRGB and The Zip File below!!!
 
Download: https://github.com/BunnY-sky/FiveMWanted/actions/runs/37712598547/artifacts/11522760735

OpenRGB: https://openrgb.org/



External C# / .NET WinForms application for controlling RGB lighting based on the FiveM/GTA V wanted level.

## Features

- External Windows application — no FiveM resource, Lua or NUI required.
- Razer Chroma via the Razer Chroma SDK HTTP interface.
- Logitech G support via Logitech LED SDK DLL.
- ROCCAT/OpenRGB support via OpenRGB.
- Optional blinking for 1–2 and 3–4 stars.
- 5-star alternating colors.
- Blink interval configurable from 40 ms to 5000 ms.
- Process selection for the FiveM/GTA process.

## Project structure

```text
FiveMWantedLighting/
├── External/
│   ├── FiveMWantedLighting.csproj
│   └── Program.cs
├── build.ps1
├── README.md
├── LICENSE
└── .gitignore
```

## Requirements

- Windows 10/11
- .NET 8 SDK
- FiveM/GTA V
- Razer Chroma SDK for Razer devices, Logitech G HUB/LED SDK for Logitech devices, or OpenRGB for OpenRGB-supported devices.

## Build

Open PowerShell in the repository folder and run:

```powershell
Set-ExecutionPolicy -Scope Process Bypass
.\build.ps1
```

The published application is created in the build output directory specified by the script.

## Notes

This project is an external application. It does not install a FiveM resource and does not require Lua or NUI.

OpenRGB and third-party SDKs remain subject to their respective licenses.
