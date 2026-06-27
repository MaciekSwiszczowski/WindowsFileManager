using WinUiFileManager.Diagnostics.Inspector;
using WinUiFileManager.Interop.Adapters;
using WinUiFileManager.Interop.Types;
using Xunit;

namespace WinUiFileManager.Diagnostics.Tests.Scenarios;

public sealed class StorageProviderSyncRootCacheTests
{
    [Fact]
    public void FindForPath_ReturnsRoot_ForChildPath()
    {
        // Arrange
        const string root = @"C:\WinUiFileManagerBenchmarks2\CloudHandlerBenchmarks";
        var cache = new StorageProviderSyncRootCache(
            new FakeSyncRootRegistryReader(new SyncRootRegistration(root, "Provider!sid!account", "Provider", "Provider")));

        // Act
        var match = cache.FindForPath(@"C:\WinUiFileManagerBenchmarks2\CloudHandlerBenchmarks\local\file-000000.bin");

        // Assert
        Assert.NotNull(match);
        Assert.Equal(root, match.Value.Path);
    }

    [Fact]
    public void FindForPath_ReturnsRoot_WhenPathEqualsRoot()
    {
        // Arrange
        const string root = @"C:\Cloud\OneDrive";
        var cache = new StorageProviderSyncRootCache(
            new FakeSyncRootRegistryReader(new SyncRootRegistration(root, "id", "OneDrive", "OneDrive")));

        // Act
        var match = cache.FindForPath(root);

        // Assert
        Assert.NotNull(match);
        Assert.Equal(root, match.Value.Path);
    }

    [Fact]
    public void FindForPath_ReturnsNull_WhenNoRootContainsPath()
    {
        // Arrange
        var cache = new StorageProviderSyncRootCache(
            new FakeSyncRootRegistryReader(new SyncRootRegistration(@"C:\Cloud\OneDrive", "id", "OneDrive", "OneDrive")));

        // Act
        var match = cache.FindForPath(@"C:\Other\file.txt");

        // Assert
        Assert.Null(match);
    }

    [Fact]
    public void FindForPath_DoesNotMatch_SiblingWithSharedPrefix()
    {
        // Arrange
        var cache = new StorageProviderSyncRootCache(
            new FakeSyncRootRegistryReader(new SyncRootRegistration(@"C:\Cloud\One", "id", "One", "One")));

        // Act
        var match = cache.FindForPath(@"C:\Cloud\OneOther\file.txt");

        // Assert
        Assert.Null(match);
    }

    [Fact]
    public void FindForPath_ReturnsDeepestRoot_WhenNested()
    {
        // Arrange
        const string outer = @"C:\Cloud";
        const string inner = @"C:\Cloud\Team\Project";
        var cache = new StorageProviderSyncRootCache(
            new FakeSyncRootRegistryReader(
                new SyncRootRegistration(outer, "outer", "P", "P"),
                new SyncRootRegistration(inner, "inner", "P", "P")));

        // Act
        var match = cache.FindForPath(@"C:\Cloud\Team\Project\sub\file.txt");

        // Assert
        Assert.NotNull(match);
        Assert.Equal(inner, match.Value.Path);
    }

    private sealed class FakeSyncRootRegistryReader : ISyncRootRegistryReader
    {
        private readonly IReadOnlyList<SyncRootRegistration> _registrations;

        public FakeSyncRootRegistryReader(params SyncRootRegistration[] registrations)
        {
            _registrations = registrations;
        }

        public IReadOnlyList<SyncRootRegistration> ReadRegisteredSyncRoots() => _registrations;
    }
}
