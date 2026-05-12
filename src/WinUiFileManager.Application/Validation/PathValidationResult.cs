namespace WinUiFileManager.Application.Validation;

public sealed record PathValidationResult
{
    public bool IsValid { get; }
    public string? ErrorMessage { get; }

    private PathValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static PathValidationResult Valid() => new(true, null);

    public static PathValidationResult Invalid(string message) => new(false, message);
}
