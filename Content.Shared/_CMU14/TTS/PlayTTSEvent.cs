using Robust.Shared.Serialization;

namespace Content.Shared._CMU14.TTS;

[Serializable, NetSerializable]
public sealed class PlayTTSEvent : EntityEventArgs
{
    public readonly byte[] Data;
    public readonly NetEntity? Source;
    public readonly float VolumeModifier;
    public readonly float MaxDistance;

    public PlayTTSEvent(byte[] data, NetEntity? source, float volumeModifier, float maxDistance)
    {
        Data = data;
        Source = source;
        VolumeModifier = volumeModifier;
        MaxDistance = maxDistance;
    }
}
