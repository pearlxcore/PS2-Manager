# PS2 Manager

A dark-themed Windows app for managing a PS2 disc image collection and the Open PS2 Loader (OPL) device you play it from. Browse with covers, edit game info, download art, rename files from game data, and send games to a USB drive or network share with their CFG and ART files in place.

## Features

**Library**
* Scans folders for `.iso`, `.bin` and `.img` images and reads the game ID, version and boot ELF straight from each disc (DVD 2048-byte and raw CD 2352-byte layouts)
* Titles, genre, developer, publisher, release date and languages from [GameDB-PS2](https://github.com/niemasd/GameDB-PS2); falls back to the disc label or file name for unknown discs
* List view or cover wall, details pane, search and filters (region, CD/DVD, missing cover, edited, not in database, problems, multi-disc)
* Background scanning with progress and cancel; rescans only re-read new or changed files
* Drag folders onto the window to add them

**Game info editor (OPL Manager style)**
* Edit title, genre, developer, publisher, release, region, languages and description
* OPL fields: players, rating, parental rating, video mode, scan mode, aspect ratio, compatibility modes 1–8, CD/DVD placement
* Your edits are stored separately from the database, so they survive database updates; changed fields are marked and can be reset one by one
* Optionally writes `CFG\<GAMEID>.cfg` on the OPL device (unknown keys OPL wrote itself are kept), or loads it back

**Covers and art**
* All OPL art types: front, back, spine, disc icon, logo, background and two screenshots
* Sources: OPL Manager art database archive, xlenore 2D covers, xlenore 3D covers, with a priority order you choose
* Per-game art manager with download, choose file, paste and drag and drop; bulk download for the library or a selection

**Rename and organize** (preview first, then apply, with undo)
* Batch rename with tokens: `{TITLE}` `{GAME_ID}` `{GAME_ID_OPL}` `{REGION}` `{VERSION}` `{DISC}` `{DISC_NUM}` `{MEDIA}` `{DEVELOPER}` `{PUBLISHER}` `{GENRE}` `{YEAR}` `{LANGUAGE}` `{CRC}`
* Presets such as `{TITLE} [{GAME_ID}]` and OPL legacy `{GAME_ID_OPL}.{TITLE}`, plus your own templates
* Move into per-game folders, flatten into one folder, fix `.iso`/`.bin` extensions from the real sector layout
* Name clashes are flagged (skip, or add `(2)`); every operation can be undone, even after a restart

**OPL device**
* Point at a USB drive, SMB share folder or HDD folder; creates the `CD`, `DVD`, `ART`, `CFG`, `VMC`… layout
* Send games with copy or move, optional renaming on the device, CFG and art included; CD games go to `CD\`, DVD games to `DVD\` (detected from the disc's UDF bridge)
* Checks before copying: raw `.bin` images, files over 4 GB on FAT32, free space
* Device list shows which games have CFG and art and flags games in the wrong folder; one click writes all CFG, copies all art, fixes folders, or imports CFG info and art from the device

**Other tools**
* Duplicate finder (identical copies, or several dumps of the same game) with deletion to the Recycle Bin
* CRC32 of whole images (to compare with Redump) and the PCSX2 game CRC
* Export to Excel, CSV or text with the columns you pick
* Import the manifest from PS2 Manager 1.x, reusing its downloaded covers

## Requirements

* Windows 10 or later
* [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

## Getting started

1. Run `PS2 Manager.exe`. On first launch it offers to download the game database; say yes.
2. Click **Add folder** and pick the folder with your disc images (subfolders are included).
3. Covers download in the background. Select a game and press **Edit info** or **Art**, or use **Tools** for batch work.
4. For OPL, open **OPL Device**, choose your drive, select games in the Library and press **Send selected**.

Coming from version 1.x? Use **Tools → Import manifest from the old version** and pick `Manifests\ps2_manifest.json` from the old app folder.

## Data location

Settings, the library cache, your edits, art and logs live in `%LocalAppData%\PS2Manager`. Set the environment variable `PS2MANAGER_DATA` to a folder path to keep them somewhere else (for a portable setup).

## Building

```
dotnet build "PS2 Manager.slnx"
dotnet test PS2Manager.Tests
dotnet publish PS2Manager.App -c Release
```

| Project | Contents |
|---|---|
| `PS2Manager.Core` | Disc reading, database, overrides, art, rename engine, OPL, export (no UI) |
| `PS2Manager.App` | WPF app (WPF-UI, CommunityToolkit.Mvvm) |
| `PS2Manager.Tests` | xUnit tests, including reading generated ISO and raw CD images |

## Credits

* Game database: [GameDB-PS2](https://github.com/niemasd/GameDB-PS2)
* Covers: [xlenore/ps2-covers](https://github.com/xlenore/ps2-covers)
* OPL art: [OPL Manager art database archive](https://github.com/Luden02/psx-ps2-opl-art-database)
* Disc reading: [DiscUtils](https://github.com/DiscUtils/DiscUtils)
* UI: [WPF-UI](https://github.com/lepoco/wpfui)
