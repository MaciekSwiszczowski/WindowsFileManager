namespace WinUiFileManager.Application.Messaging;

/// <summary>
/// Identifies a pane/scope (e.g. left vs right pane) so that messages can be routed to the
/// correct recipient. Pane-scoped behaviors must filter on this through the messenger wrapper's
/// identity-aware registration methods rather than registering globally (see AGENTS.md §4).
/// </summary>
/// <remarks>
/// Equality is value-based (the record compares <see cref="Value"/>).
/// <para>
/// <b>Foot-gun:</b> the implicit conversions to/from <see cref="string"/> make
/// <see cref="Identity"/> and a bare string interchangeable at call sites. That is convenient,
/// but it silently boxes raw literals into identities and erases identities into strings, which
/// defeats the type's purpose and makes it easy to compare against ad-hoc <c>"Left"</c>/<c>"Right"</c>
/// literals. Prefer the shared pane <see cref="Identity"/> constants over string literals.
/// </para>
/// </remarks>
public sealed record Identity
{
    /// <param name="value">The scope key. Must not be null/empty/whitespace.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is null, empty, or whitespace.</exception>
    public Identity(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        Value = value;
    }

    /// <summary>The underlying scope key string.</summary>
    public string Value { get; }

    /// <summary>Implicitly unwraps to the underlying <see cref="Value"/>. See the foot-gun note on the type.</summary>
    public static implicit operator string(Identity identity) => identity.Value;

    /// <summary>Implicitly wraps a raw string as an <see cref="Identity"/>. See the foot-gun note on the type.</summary>
    public static implicit operator Identity(string value) => new(value);

    public override string ToString() => Value;
}
