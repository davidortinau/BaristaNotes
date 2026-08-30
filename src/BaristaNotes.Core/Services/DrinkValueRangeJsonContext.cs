using System.Text.Json.Serialization;
using BaristaNotes.Core.Models;

namespace BaristaNotes.Core.Services;

[JsonSourceGenerationOptions(UseStringEnumConverter = true)]
[JsonSerializable(typeof(DrinkValueRangeSettings))]
internal partial class DrinkValueRangeJsonContext : JsonSerializerContext;
