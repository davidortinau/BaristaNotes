using BaristaNotes.Services;
using Microsoft.Maui.AI.Attributes;

namespace BaristaNotes.Services.AI;

/// <summary>
/// Source-generated AI tool context aggregating tool methods from
/// VoiceCommandService and NavigationTools. The generator emits a
/// static <c>Default</c> singleton and a <c>Tools</c> collection at
/// compile time — no reflection on the invocation path.
/// </summary>
[AIToolSource(typeof(VoiceCommandService))]
[AIToolSource(typeof(NavigationTools))]
[AIToolSource(typeof(ProfileContextTools))]
public partial class VoiceTools : AIToolContext
{
}
