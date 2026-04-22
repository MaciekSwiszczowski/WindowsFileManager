using TUnit.Core;
using WinUiFileManager.Interop.Adapters;
using WinUiFileManager.Interop.Tests.Fixtures;

namespace WinUiFileManager.Interop.Tests.Scenarios;

public sealed class FileIdentityInteropTests
{
    [Test]
    public async Task Test_GetFileId_ReturnsFileIdForExistingFile()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var filePath = fixture.CreateFile("identity.txt");
        var sut = CreateSubject();

        var result = sut.GetFileId(filePath);

        await Assert.That(result.Success).IsTrue();
        await Assert.That(result.FileId128).IsNotNull();
        await Assert.That(result.FileId128!.Length).IsEqualTo(16);
    }

    [Test]
    public async Task Test_GetFileId_ReturnsFailureForMissingFile()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var missingPath = Path.Combine(fixture.RootPath, "no_such_file.txt");
        var sut = CreateSubject();

        var result = sut.GetFileId(missingPath);

        await Assert.That(result.Success).IsFalse();
        await Assert.That(result.ErrorMessage).IsNotNull();
    }

    [Test]
    public async Task Test_GetFileId_ReturnsDifferentIdsForDifferentFiles()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var path1 = fixture.CreateFile("file_a.txt");
        var path2 = fixture.CreateFile("file_b.txt");
        var sut = CreateSubject();

        var result1 = sut.GetFileId(path1);
        var result2 = sut.GetFileId(path2);

        await Assert.That(result1.Success).IsTrue();
        await Assert.That(result2.Success).IsTrue();
        await Assert.That(result1.FileId128!.SequenceEqual(result2.FileId128!)).IsFalse();
    }

    [Test]
    public async Task Test_GetLockDiagnostics_ReturnsResultForExistingFile()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var filePath = fixture.CreateFile("in_use_probe.txt");
        var sut = CreateSubject();

        var result = sut.GetLockDiagnostics(filePath);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.LockBy).IsNotNull();
        await Assert.That(result.LockPids).IsNotNull();
        await Assert.That(result.LockServices).IsNotNull();
    }

    [Test]
    public async Task Test_GetLockDiagnostics_ReturnsResultForMissingFile()
    {
        using var fixture = new NtfsTempDirectoryFixture();
        var missingPath = Path.Combine(fixture.RootPath, "missing-in-use-probe.txt");
        var sut = CreateSubject();

        var result = sut.GetLockDiagnostics(missingPath);

        await Assert.That(result).IsNotNull();
        await Assert.That(result.LockBy).IsNotNull();
        await Assert.That(result.LockPids).IsNotNull();
        await Assert.That(result.LockServices).IsNotNull();
    }

    private static FileIdentityInterop CreateSubject()
    {
        return new FileIdentityInterop(new RestartManagerInterop(), new ShellInterop());
    }
}
