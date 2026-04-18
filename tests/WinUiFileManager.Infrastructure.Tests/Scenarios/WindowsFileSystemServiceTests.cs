using Microsoft.Extensions.Logging.Abstractions;
using TUnit.Core;
using WinUiFileManager.Domain.Enums;
using WinUiFileManager.Domain.ValueObjects;
using WinUiFileManager.Infrastructure.FileSystem;
using WinUiFileManager.Infrastructure.Services;
using WinUiFileManager.Infrastructure.Tests.Fixtures;
using WinUiFileManager.Interop.Adapters;

namespace WinUiFileManager.Infrastructure.Tests.Scenarios;

public sealed class WindowsFileSystemServiceTests
{
    [Test]
    public async Task Test_EnumerateDirectory_ReturnsFilesAndDirectories()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        fixture.CreateFile("file1.txt");
        fixture.CreateFile("file2.log");
        fixture.CreateDirectory("subdir");
        var sut = CreateService();
        var path = NormalizedPath.FromUserInput(fixture.RootPath);

        // Act
        var entries = await sut.EnumerateDirectoryAsync(path, CancellationToken.None);

        // Assert
        await Assert.That(entries.Count).IsEqualTo(3);
        await Assert.That(entries.Any(e => e.Name == "file1.txt")).IsTrue();
        await Assert.That(entries.Any(e => e.Name == "file2.log")).IsTrue();
        await Assert.That(entries.Any(e => e.Name == "subdir")).IsTrue();
    }

    [Test]
    public async Task Test_EnumerateDirectory_ReturnsCorrectMetadata()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        fixture.CreateFile("data.bin", sizeInBytes: 1024);
        var sut = CreateService();
        var path = NormalizedPath.FromUserInput(fixture.RootPath);

        // Act
        var entries = await sut.EnumerateDirectoryAsync(path, CancellationToken.None);

        // Assert
        var file = entries.Single(e => e.Name == "data.bin");
        await Assert.That(file.Kind).IsEqualTo(ItemKind.File);
        await Assert.That(file.Size).IsEqualTo(1024L);
        await Assert.That(file.Extension).IsEqualTo(".bin");
        await Assert.That(file.LastWriteTimeUtc).IsGreaterThan(DateTime.MinValue);
        await Assert.That(file.CreationTimeUtc).IsGreaterThan(DateTime.MinValue);
    }

    [Test]
    public async Task Test_EnumerateDirectory_EmptyDirectory()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var sut = CreateService();
        var path = NormalizedPath.FromUserInput(fixture.RootPath);

        // Act
        var entries = await sut.EnumerateDirectoryAsync(path, CancellationToken.None);

        // Assert
        await Assert.That(entries.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Test_GetEntry_ReturnsFileEntry()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var filePath = fixture.CreateFile("sample.txt", sizeInBytes: 512);
        var sut = CreateService();
        var path = NormalizedPath.FromUserInput(filePath);

        // Act
        var entry = await sut.GetEntryAsync(path, CancellationToken.None);

        // Assert
        await Assert.That(entry).IsNotNull();
        await Assert.That(entry!.Name).IsEqualTo("sample.txt");
        await Assert.That(entry.Kind).IsEqualTo(ItemKind.File);
        await Assert.That(entry.Size).IsEqualTo(512L);
    }

    [Test]
    public async Task Test_GetEntry_ReturnsDirectoryEntry()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var dirPath = fixture.CreateDirectory("mydir");
        var sut = CreateService();
        var path = NormalizedPath.FromUserInput(dirPath);

        // Act
        var entry = await sut.GetEntryAsync(path, CancellationToken.None);

        // Assert
        await Assert.That(entry).IsNotNull();
        await Assert.That(entry!.Name).IsEqualTo("mydir");
        await Assert.That(entry.Kind).IsEqualTo(ItemKind.Directory);
    }

    [Test]
    public async Task Test_DirectoryExists_ReturnsTrueForExisting()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var sut = CreateService();
        var path = NormalizedPath.FromUserInput(fixture.RootPath);

        // Act
        var exists = await sut.DirectoryExistsAsync(path, CancellationToken.None);

        // Assert
        await Assert.That(exists).IsTrue();
    }

    [Test]
    public async Task Test_DirectoryExists_ReturnsFalseForMissing()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var sut = CreateService();
        var path = NormalizedPath.FromUserInput(Path.Combine(fixture.RootPath, "nonexistent"));

        // Act
        var exists = await sut.DirectoryExistsAsync(path, CancellationToken.None);

        // Assert
        await Assert.That(exists).IsFalse();
    }

    private static WindowsFileSystemService CreateService()
    {
        return new WindowsFileSystemService(
            new WindowsPathNormalizationService(),
            NullLogger<WindowsFileSystemService>.Instance);
    }
}
