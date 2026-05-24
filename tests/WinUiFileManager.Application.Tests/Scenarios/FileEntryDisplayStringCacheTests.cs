using WinUiFileManager.Presentation.Services;

namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class FileEntryDisplayStringCacheTests
{
    [Test]
    public async Task Test_TableAttributes_UseCompactShortcuts()
    {
        var cache = FileEntryDisplayStringCache.Shared;

        var result = cache.GetTableAttributes(FileAttributes.Hidden | FileAttributes.ReadOnly);

        await Assert.That(result).IsEqualTo("F H R");
    }

    [Test]
    public async Task Test_InspectorAttributes_UseFullFrameworkText()
    {
        var cache = FileEntryDisplayStringCache.Shared;
        var attributes = FileAttributes.Directory | FileAttributes.Hidden;

        var result = cache.GetInspectorAttributes(attributes);

        await Assert.That(result).IsEqualTo(attributes.ToString());
    }

    [Test]
    public async Task Test_Extensions_CacheOnlyShortLetterExtensions()
    {
        var cache = FileEntryDisplayStringCache.Shared;

        var shortExtension = cache.GetExtension(new string(['.', 't', 'x', 't']));
        var sameShortExtension = cache.GetExtension(new string(['.', 't', 'x', 't']));
        var longExtension = cache.GetExtension(new string(['.', 'c', 'o', 'n', 'f', 'i', 'g']));
        var sameLongExtension = cache.GetExtension(new string(['.', 'c', 'o', 'n', 'f', 'i', 'g']));
        var numericExtension = cache.GetExtension(new string(['.', '1', '2', '3']));
        var sameNumericExtension = cache.GetExtension(new string(['.', '1', '2', '3']));

        await Assert.That(ReferenceEquals(shortExtension, sameShortExtension)).IsTrue();
        await Assert.That(ReferenceEquals(longExtension, sameLongExtension)).IsFalse();
        await Assert.That(ReferenceEquals(numericExtension, sameNumericExtension)).IsFalse();
    }
}
