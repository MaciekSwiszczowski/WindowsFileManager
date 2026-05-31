namespace WinUiFileManager.Application.Dialogs;

/// <summary>
/// Declarative description of a single dialog button passed in a
/// <see cref="ShowDialogMessage"/>. The Presentation layer renders one button per configuration.
/// </summary>
/// <param name="Role">Semantic role used to map to the dialog's primary/secondary/close slots and to report the result.</param>
/// <param name="Text">The button caption.</param>
/// <param name="IsDefault">Whether this button is the default (activated by Enter).</param>
public sealed record DialogButtonConfiguration(DialogButtonRole Role, string Text, bool IsDefault = false);
