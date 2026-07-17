using Robust.Shared.Prototypes;

namespace Content.Shared._CMU14.TTS;

/// <summary>
/// Gives an entity synthesized speech. When no voice is specified, the server
/// chooses a round-start voice matching the humanoid's sex on first use.
/// </summary>
[RegisterComponent]
public sealed partial class TTSComponent : Component
{
    [DataField("voice")]
    public ProtoId<TTSVoicePrototype>? VoicePrototypeId;
}
