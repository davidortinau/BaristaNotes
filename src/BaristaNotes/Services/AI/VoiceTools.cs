using BaristaNotes.Services;
using Microsoft.Maui.AI.Attributes;

namespace BaristaNotes.Services.AI;

/// <summary>
/// AI tool context aggregating tool methods from VoiceCommandService and
/// NavigationTools. Its checked-in generated implementation avoids runtime
/// reflection and keeps tool schema creation compatible with NativeAOT.
/// </summary>
[AIToolSource(typeof(VoiceCommandService))]
[AIToolSource(typeof(NavigationTools))]
[AIToolSource(typeof(ProfileContextTools))]
[AIToolSource(typeof(PhotoQueryTools))]
public partial class VoiceTools : AIToolContext
{
}
