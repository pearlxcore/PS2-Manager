<p align="center">
  <img src="PS2Manager.App/Assets/logo.png" width="128" alt="PS2 Manager logo">
</p>

<h1 align="center">PS2 Manager</h1>

A Windows tool for managing PS2 disc images, with support for Open PS2 Loader (OPL) and PCSX2.

## Features

* Scan folders of `.iso` / `.bin` / `.img` files and identify each game from the disc
* Game info from GameDB-PS2, with your own edits on top
* Download covers and OPL art
* Rename and organize files using game info, with preview and undo
* Send games to an OPL USB drive or network share, including CFG and art
* Launch games in PCSX2 and see PCSX2 compatibility ratings
* Find duplicates, calculate CRC32, export the library to Excel or CSV

## Download

Get the latest version from [Releases](https://github.com/pearlxcore/PS2-Manager/releases). Unzip and run `PS2 Manager.exe` (Windows 10/11, 64-bit).

Upgrading from 1.x: use **Tools → Import manifest from the old version** to keep your library and covers.

## Notes

* OPL can't load raw `.bin` CD images; convert them to ISO first.
* Games over 4 GB need an exFAT-formatted OPL drive.
* App data is stored in `%LocalAppData%\PS2Manager`. Set `PS2MANAGER_DATA` to use a different folder.

## Building

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).

```
dotnet build "PS2 Manager.slnx"
dotnet test PS2Manager.Tests
pwsh tools/publish.ps1
```

## Credits

[GameDB-PS2](https://github.com/niemasd/GameDB-PS2) ·
[xlenore/ps2-covers](https://github.com/xlenore/ps2-covers) ·
[OPL art database](https://github.com/Luden02/psx-ps2-opl-art-database) ·
[PCSX2](https://github.com/PCSX2/pcsx2) ·
[DiscUtils](https://github.com/DiscUtils/DiscUtils) ·
[WPF-UI](https://github.com/lepoco/wpfui)

Not affiliated with Sony Interactive Entertainment.
