namespace WinUiFileManager.Infrastructure.Persistence;

internal sealed record WindowPlacementDto
{
    public int X { get; init; }

    public int Y { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }

    public bool IsMaximized { get; init; }
}
