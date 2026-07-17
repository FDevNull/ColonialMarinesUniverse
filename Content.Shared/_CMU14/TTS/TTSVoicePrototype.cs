using Content.Shared.Humanoid;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._CMU14.TTS;

[Prototype("ttsVoice")]
public sealed partial class TTSVoicePrototype : IPrototype, IInheritingPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<TTSVoicePrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    [DataField]
    public string Name = string.Empty;

    [DataField(required: true)]
    public Sex Sex;

    /// <summary>
    /// Speaker identifier accepted by the NTTS API.
    /// </summary>
    [DataField(required: true)]
    public string Speaker = string.Empty;

    [DataField]
    public bool RoundStart = true;
}
