namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class NormalizedPathTests
{
    [Fact]
    public void Equality_ComparesNormalizedPathsIgnoringCase()
    {
        var left = NormalizedPath.FromUserInput(@"C:\Temp");
        var right = NormalizedPath.FromUserInput(@"c:\temp");

        Assert.True(left == right);
    }

    [Fact]
    public void Equality_ComparesNormalizedPathWithDisplayString()
    {
        var path = NormalizedPath.FromUserInput(@"C:\Temp");

        Assert.True(path == @"c:\temp");
        Assert.True(@"c:\temp" == path);
    }
}
