namespace WinUiFileManager.Infrastructure.Persistence;

/// <summary>
/// Serialization-only DTO for a pane's persisted sort state. <see cref="Column"/> is stored as a string (the
/// <c>SortColumn</c> enum name) for schema stability; an unrecognized value is mapped back to the default column in
/// <see cref="JsonSettingsRepository"/>.
/// </summary>
internal sealed record SortStateDto
{
    /// <summary>The sort column name (a <c>SortColumn</c> enum member name); defaults to "Name".</summary>
    public string Column { get; init; } = "Name";

    /// <summary>Whether the sort is ascending; defaults to <see langword="true"/>.</summary>
    public bool Ascending { get; init; } = true;
}
