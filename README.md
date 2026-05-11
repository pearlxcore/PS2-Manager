# PS2 Manager

A desktop tool for managing a collection of PS2 disc images. Browse, search, rename, and organize your games with metadata pulled from the PS2 game database.

<img width="2560" height="1392" alt="image" src="https://github.com/user-attachments/assets/1d5f496c-37ee-4c6e-889d-6aa1b1297c45" />

## What it does

* Scans folders for PS2 disc images (.iso, .bin, .img) and reads game info directly from each disc
* Handles both DVD and CD format automatically, no need to worry about extensions
* Falls back to the ISO volume label for titles not found in the database
* Looks up game titles, regions, version, and more from the community PS2 database
* Downloads cover art automatically on startup
* Rename files with a template system using {title}, {region}, {gameid}, {disc}, {version}
* Move each game into its own folder
* Flatten subdirectories by moving all files into a single folder
* Fix wrong file extensions automatically during scan
* Delete ISOs and open them in Explorer
* Export your library to Excel or text
* Caches everything in a manifest so startup is fast after the first scan
* Remembers your last rename and move templates

## Requirements

* Windows 10 or later
* [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0)

## Getting started

1. Run the app. On first launch it'll ask to download the game database, click yes.
2. Point it at your PS2 ISO folder.
3. That's it. The grid fills up with your games and covers download in the background.

## Menu overview

**File** — Scan folders, save/load the manifest, export to Excel

**Edit** — Rename ISOs, move them to separate folders, delete, open in Explorer

**Tools** — Cover downloader, update the game database, flatten directory, fix file extensions

## Credits

* Game database from [GameDB-PS2](https://github.com/niemasd/GameDB-PS2)
* Covers from [xlenore/ps2-covers](https://github.com/xlenore/ps2-covers)
* ISO reading powered by DiscUtils

