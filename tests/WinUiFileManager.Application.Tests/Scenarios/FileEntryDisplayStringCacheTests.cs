using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class FileEntryDisplayStringCacheTests
{
    [Fact]
    public void TableAttributes_UseCompactShortcuts()
    {
        var cache = FileEntryDisplayStringCache.Shared;

        var result = cache.GetTableAttributes(FileAttributes.Hidden | FileAttributes.ReadOnly);

        Assert.Equal("F H R", result);
    }

    [Fact]
    public void InspectorAttributes_UseFullFrameworkText()
    {
        var cache = FileEntryDisplayStringCache.Shared;
        var attributes = FileAttributes.Directory | FileAttributes.Hidden;

        var result = cache.GetInspectorAttributes(attributes);

        Assert.Equal(attributes.ToString(), result);
    }

    [Fact]
    public void Extensions_CacheOnlyShortLetterExtensions()
    {
        var cache = FileEntryDisplayStringCache.Shared;

        var shortExtension = cache.GetExtension(new string(['.', 't', 'x', 't']));
        var sameShortExtension = cache.GetExtension(new string(['.', 't', 'x', 't']));
        var longExtension = cache.GetExtension(new string(['.', 'c', 'o', 'n', 'f', 'i', 'g']));
        var sameLongExtension = cache.GetExtension(new string(['.', 'c', 'o', 'n', 'f', 'i', 'g']));
        var numericExtension = cache.GetExtension(new string(['.', '1', '2', '3']));
        var sameNumericExtension = cache.GetExtension(new string(['.', '1', '2', '3']));

        Assert.True(ReferenceEquals(shortExtension, sameShortExtension));
        Assert.False(ReferenceEquals(longExtension, sameLongExtension));
        Assert.False(ReferenceEquals(numericExtension, sameNumericExtension));
    }
}
