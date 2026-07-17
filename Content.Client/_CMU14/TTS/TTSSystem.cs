using Content.Shared._CMU14.TTS;
using Content.Shared.CCVar;
using Robust.Client.Audio;
using Robust.Client.ResourceManagement;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client._CMU14.TTS;

/// <summary>
/// Loads synthesized OGG data into an in-memory content root and plays it from
/// the speaking entity, following the approach used by Sunrise Station.
/// </summary>
public sealed partial class TTSSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IResourceManager _resourceManager = default!;
    [Dependency] private IResourceCache _resourceCache = default!;
    [Dependency] private IDependencyCollection _dependencies = default!;
    [Dependency] private AudioSystem _audio = default!;

    private static readonly MemoryContentRoot ContentRoot = new();
    private static readonly ResPath Prefix = ResPath.Root / "TTS";
    private static readonly AudioResource EmptyAudioResource = new();

    private ISawmill _sawmill = default!;
    private bool _enabled;
    private float _volume;
    private int _fileIndex;

    public override void Initialize()
    {
        base.Initialize();

        _sawmill = Logger.GetSawmill("tts");
        _resourceManager.AddRoot(Prefix, ContentRoot);
        Subs.CVar(_cfg, CCVars.TTSClientEnabled, value => _enabled = value, true);
        Subs.CVar(_cfg, CCVars.TTSVolume, value => _volume = Math.Clamp(value, 0f, 1f), true);
        SubscribeNetworkEvent<PlayTTSEvent>(OnPlayTTS);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        ContentRoot.Clear();
    }

    private void OnPlayTTS(PlayTTSEvent ev)
    {
        if (!_enabled || _volume <= 0f || ev.Data.Length == 0)
            return;

        var filePath = new ResPath($"{_fileIndex++}.ogg");
        var fullPath = Prefix / filePath;

        try
        {
            ContentRoot.AddOrUpdateFile(filePath, ev.Data);
            var resource = new AudioResource();
            resource.Load(_dependencies, fullPath);
            _resourceCache.CacheResource(fullPath, resource);

            var audioParams = AudioParams.Default
                .WithVolume(SharedAudioSystem.GainToVolume(_volume * ev.VolumeModifier))
                .WithMaxDistance(ev.MaxDistance);

            if (ev.Source is { } netSource)
            {
                var source = GetEntity(netSource);
                if (source.Id == 0)
                    return;

                _audio.PlayEntity(resource.AudioStream, source, null, audioParams);
            }
            else
            {
                _audio.PlayGlobal(resource.AudioStream, null, audioParams);
            }
        }
        catch (Exception e)
        {
            _sawmill.Error($"Failed to play TTS audio: {e.Message}");
        }
        finally
        {
            ContentRoot.RemoveFile(filePath);
            _resourceCache.CacheResource(fullPath, EmptyAudioResource);
        }
    }
}
