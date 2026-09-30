namespace BaristaNotes.Core.Services.DTOs;

/// <summary>
/// Default leaves a field unchanged. Set(null) explicitly clears a nullable field.
/// </summary>
public readonly record struct FieldUpdate<T>(bool IsSpecified, T Value)
{
    public static FieldUpdate<T> Set(T value) => new(true, value);

    public static implicit operator FieldUpdate<T>(T value) => Set(value);
}
