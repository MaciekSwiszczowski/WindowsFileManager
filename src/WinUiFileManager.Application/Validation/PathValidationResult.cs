namespace WinUiFileManager.Application.Validation;

/// <summary>
/// Outcome of validating a path: either valid, or invalid with a user-facing message. Returned by the
/// path/volume validation services (e.g.
/// <see cref="WinUiFileManager.Application.Abstractions.IPathNormalizationService.Validate"/>,
/// <see cref="WinUiFileManager.Application.Abstractions.INtfsVolumePolicyService.ValidateNtfsPath"/>).
/// Construct via the <see cref="Valid"/>/<see cref="Invalid"/> factories.
/// </summary>
public sealed record PathValidationResult
{
    /// <summary>Whether the path passed validation.</summary>
    public bool IsValid { get; }

    /// <summary>The failure message when <see cref="IsValid"/> is <see langword="false"/>; otherwise <see langword="null"/>.</summary>
    public string? ErrorMessage { get; }

    private PathValidationResult(bool isValid, string? errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    /// <summary>Creates a successful result with no message.</summary>
    public static PathValidationResult Valid() => new(true, null);

    /// <summary>Creates a failed result carrying a user-facing <paramref name="message"/>.</summary>
    public static PathValidationResult Invalid(string message) => new(false, message);
}
