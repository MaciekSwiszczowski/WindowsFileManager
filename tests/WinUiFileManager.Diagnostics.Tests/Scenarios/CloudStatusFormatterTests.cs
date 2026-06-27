using Xunit;
using WinUiFileManager.Diagnostics.Inspector.Handlers;

namespace WinUiFileManager.Diagnostics.Tests.Scenarios;

public sealed class CloudStatusFormatterTests
{
    private const uint PlaceholderStatePartial = 0x00000010;

    [Theory]
    [MemberData(nameof(Cases))]
    public void Format_ReturnsExpectedLabel(FileAttributes attributes, uint placeholderState, string expected)
    {
        // Act
        var status = CloudStatusFormatter.Format(attributes, placeholderState);

        // Assert
        Assert.Equal(expected, status);
    }

    [Fact]
    public void Format_PartiallyOnDisk_IsDehydrated()
    {
        // Arrange
        const uint placeholderState = CloudStatusFormatter.PlaceholderStatePartiallyOnDisk | PlaceholderStatePartial;

        // Act
        var status = CloudStatusFormatter.Format(FileAttributes.ReparsePoint, placeholderState);

        // Assert
        Assert.Equal("Dehydrated", status);
    }

    [Fact]
    public void Format_PartialOnlyWithoutPartiallyOnDisk_IsNotDehydrated()
    {
        // Act
        var status = CloudStatusFormatter.Format(FileAttributes.ReparsePoint, PlaceholderStatePartial);

        // Assert
        Assert.Equal("Hydrated", status);
    }

    public static TheoryData<FileAttributes, uint, string> Cases() => new()
    {
        { (FileAttributes)0, CloudStatusFormatter.PlaceholderStateNone, string.Empty },
        { CloudStatusFormatter.Pinned, CloudStatusFormatter.PlaceholderStateNone, "Pinned" },
        { CloudStatusFormatter.Unpinned, CloudStatusFormatter.PlaceholderStateNone, "Unpinned" },
        { FileAttributes.Offline, CloudStatusFormatter.PlaceholderStateNone, "Dehydrated" },
        { CloudStatusFormatter.RecallOnOpen, CloudStatusFormatter.PlaceholderStateNone, "Dehydrated" },
        { CloudStatusFormatter.RecallOnDataAccess, CloudStatusFormatter.PlaceholderStateNone, "Dehydrated" },
        { CloudStatusFormatter.Pinned | FileAttributes.Offline, CloudStatusFormatter.PlaceholderStateNone, "Pinned, Dehydrated" },
        { FileAttributes.ReparsePoint, CloudStatusFormatter.PlaceholderStatePartiallyOnDisk, "Dehydrated" },
        { FileAttributes.ReparsePoint, CloudStatusFormatter.PlaceholderStateInSync, "Hydrated, Synced" },
        { FileAttributes.ReparsePoint, CloudStatusFormatter.PlaceholderStateNone, "Hydrated" },
    };
}
