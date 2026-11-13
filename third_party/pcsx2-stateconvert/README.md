# pcsx2-stateconvert

The save-state converter used by PS2 Manager's "Update old save states" feature, from
[PCSX2 SaveState Converter](https://github.com/pearlxcore/PCSX2-SaveState-Converter).

It's built from PCSX2 2.8.2 plus the patches in that repo's `experimental/` folder (see its
`BUILD.md`). It runs as a separate program and is licensed under GPL-3.0-or-later (`LICENSE`),
like PCSX2 itself. zlib and zstd licenses are included alongside.

The C# code in `PS2Manager.Core/SaveStates` (except `StateUpgrader.cs`) is taken from the same project.
