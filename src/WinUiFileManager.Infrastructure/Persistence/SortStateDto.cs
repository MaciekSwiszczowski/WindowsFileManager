namespace WinUiFileManager.Infrastructure.Persistence;

internal sealed record SortStateDto
{
    public string Column { get; init; } = "Name";

    public bool Ascending { get; init; } = true;
}
