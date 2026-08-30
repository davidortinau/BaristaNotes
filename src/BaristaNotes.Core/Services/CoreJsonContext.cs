using System.Text.Json.Serialization;
using BaristaNotes.Core.Services.Grind;

namespace BaristaNotes.Core.Services;

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(List<int>))]
[JsonSerializable(typeof(List<GrindAnchor>))]
internal partial class CoreJsonContext : JsonSerializerContext;
