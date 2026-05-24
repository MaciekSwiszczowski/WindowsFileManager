namespace WinUiFileManager.Application.Messaging;

public sealed record Identity
{
    public Identity(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value;
    }

    public string Value { get; }

    public static implicit operator string(Identity identity) => identity.Value;

    public static implicit operator Identity(string value) => new(value);

    public override string ToString() => Value;
}
