using System.Windows.Media.Imaging;

namespace LosKrotos.Desktop;

public sealed class RoomTile
{
    public required Room Room { get; init; }
    public required string Status { get; init; }
    public required string StatusLabel { get; init; }
    public required string Accent { get; init; }
    public required string Background { get; init; }
    public required string Detail { get; init; }
    public required BitmapImage Image { get; init; }
}

public static class RoomArtwork
{
    private static readonly Dictionary<string, BitmapImage> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static BitmapImage ForType(string type)
    {
        var key = type switch
        {
            "Deluxe" => "room-deluxe.png",
            "Suite" or "Familiar" => "room-suite.png",
            _ => "room-standard.png"
        };
        if (!Cache.TryGetValue(key, out var image))
        {
            image = new BitmapImage(new Uri($"pack://application:,,,/Assets/{key}", UriKind.Absolute));
            Cache[key] = image;
        }
        return image;
    }
}

public sealed class ScheduleCell
{
    public string Label { get; init; } = "";
    public string Background { get; init; } = "#132D47";
    public string Foreground { get; init; } = "#9BB2C9";
}

public sealed class ScheduleRow
{
    public string RoomNumber { get; init; } = "";
    public List<ScheduleCell> Cells { get; init; } = [];
}
