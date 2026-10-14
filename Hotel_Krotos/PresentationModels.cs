namespace LosKrotos.Desktop;

public sealed class RoomTile
{
    public required Room Room { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public required string Accent { get; init; }
    public required string Background { get; init; }
    public required string FloorLabel { get; init; }
    public required string Detail { get; init; }
}

public sealed class ScheduleCell
{
    public string Label { get; init; } = "";
    public string Background { get; init; } = "#F2F6F2";
    public string Foreground { get; init; } = "#5B7364";
}

public sealed class ScheduleRow
{
    public string RoomNumber { get; init; } = "";
    public List<ScheduleCell> Cells { get; init; } = [];
}
