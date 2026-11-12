namespace PS2Manager.Core.Rename;

public sealed record RenameFormat(string Name, string Template);

/// <summary>
/// The single list of preset formats. The rename dialog, the context menu and the folder
/// organizer all read from here, the same approach PS4PKGTool uses.
/// </summary>
public static class RenameFormats
{
    public static IReadOnlyList<RenameFormat> FilePresets { get; } =
    [
        new("Title", "{TITLE}"),
        new("Title [ID]", "{TITLE} [{GAME_ID}]"),
        new("Title (Region)", "{TITLE} ({REGION})"),
        new("Title (Region) [ID] Disc", "{TITLE} ({REGION}) [{GAME_ID}] {DISC}"),
        new("ID - Title", "{GAME_ID} - {TITLE}"),
        new("OPL legacy (ID.Title)", "{GAME_ID_OPL}.{TITLE}"),
        new("Title (Region) (Version)", "{TITLE} ({REGION}) ({VERSION})"),
    ];

    public static IReadOnlyList<RenameFormat> FolderPresets { get; } =
    [
        new("Title", "{TITLE}"),
        new("Title [ID]", "{TITLE} [{GAME_ID}]"),
        new("Title (Region)", "{TITLE} ({REGION})"),
        new("ID", "{GAME_ID}"),
    ];
}
