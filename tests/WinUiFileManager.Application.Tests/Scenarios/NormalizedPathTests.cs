namespace WinUiFileManager.Application.Tests.Scenarios;

public sealed class NormalizedPathTests
{
    [Test]
    public async Task Test_Equality_ComparesNormalizedPathsIgnoringCase()
    {
        var left = NormalizedPath.FromUserInput(@"C:\Temp");
        var right = NormalizedPath.FromUserInput(@"c:\temp");

        await Assert.That(left == right).IsTrue();
    }

    [Test]
    public async Task Test_Equality_ComparesNormalizedPathWithDisplayString()
    {
        var path = NormalizedPath.FromUserInput(@"C:\Temp");

        await Assert.That(path == @"c:\temp").IsTrue();
        await Assert.That(@"c:\temp" == path).IsTrue();
    }
}
