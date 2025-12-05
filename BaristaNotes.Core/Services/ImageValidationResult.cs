namespace BaristaNotes.Core.Services;

public enum ImageValidationResult
{
    Valid,
    TooLarge,
    DimensionsTooLarge,
    DimensionsTooSmall,
    InvalidFormat,
    ProcessingFailed
}
