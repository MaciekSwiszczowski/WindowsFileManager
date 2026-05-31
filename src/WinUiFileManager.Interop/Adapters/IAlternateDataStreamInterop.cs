namespace WinUiFileManager.Interop.Adapters;

public interface IAlternateDataStreamInterop
{
    IReadOnlyList<string> EnumerateAlternateDataStreamDisplayLines(string path);
}
