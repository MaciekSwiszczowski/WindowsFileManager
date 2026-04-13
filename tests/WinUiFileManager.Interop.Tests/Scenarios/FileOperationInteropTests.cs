using TUnit.Core;
using WinUiFileManager.Interop.Adapters;
using WinUiFileManager.Interop.Tests.Fixtures;

namespace WinUiFileManager.Interop.Tests.Scenarios;

public sealed class FileOperationInteropTests
{
    [Test]
    public async Task Test_CopyFile_CopiesSuccessfully()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var source = fixture.CreateFile("original.txt", sizeInBytes: 256);
        var dest = Path.Combine(fixture.RootPath, "copied.txt");
        var sut = new FileOperationInterop();

        // Act
        var result = sut.CopyFile(source, dest, overwrite: false);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(File.Exists(dest)).IsTrue();
        await Assert.That(new FileInfo(dest).Length).IsEqualTo(256L);
    }

    [Test]
    public async Task Test_DeleteFile_DeletesSuccessfully()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var filePath = fixture.CreateFile("disposable.txt");
        var sut = new FileOperationInterop();

        // Act
        var result = sut.DeleteFile(filePath);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(File.Exists(filePath)).IsFalse();
    }

    [Test]
    public async Task Test_CreateDirectory_CreatesSuccessfully()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var newDir = Path.Combine(fixture.RootPath, "created_dir");
        var sut = new FileOperationInterop();

        // Act
        var result = sut.CreateDirectory(newDir);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(Directory.Exists(newDir)).IsTrue();
    }

    [Test]
    public async Task Test_MoveFile_MovesSuccessfully()
    {
        // Arrange
        using var fixture = new NtfsTempDirectoryFixture();
        var source = fixture.CreateFile("to_move.txt", sizeInBytes: 128);
        var dest = Path.Combine(fixture.RootPath, "moved.txt");
        var sut = new FileOperationInterop();

        // Act
        var result = sut.MoveFile(source, dest, overwrite: false);

        // Assert
        await Assert.That(result.Success).IsTrue();
        await Assert.That(File.Exists(source)).IsFalse();
        await Assert.That(File.Exists(dest)).IsTrue();
        await Assert.That(new FileInfo(dest).Length).IsEqualTo(128L);
    }
}
