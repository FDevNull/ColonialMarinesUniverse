using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Content.Server.Chat.Systems;
using Content.Server.Radio;
using Content.Shared._CMU14.TTS;
using Content.Shared.CCVar;
using Content.Shared.Humanoid;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CMU14.TTS;

public sealed partial class TTSSystem : EntitySystem
{
    private const float SayRange = ChatSystem.VoiceRange;
    private const float WhisperRange = ChatSystem.WhisperClearRange;
    private const float WhisperVolume = 0.55f;
    private const float RadioVolume = 0.8f;
    private const int MaxCacheEntries = 64;

    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private ILogManager _logManager = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    private readonly Dictionary<string, byte[]> _cache = new();
    private readonly Queue<string> _cacheOrder = new();
    private readonly Dictionary<string, Task<byte[]?>> _inFlight = new();
    private readonly object _cacheLock = new();
    private NTTSClient _client = default!;
    private ISawmill _sawmill = default!;
    private bool _enabled;

    public override void Initialize()
    {
        base.Initialize();

        _client = new NTTSClient(_cfg, _logManager);
        _sawmill = _logManager.GetSawmill("tts");
        Subs.CVar(_cfg, CCVars.TTSEnabled, value => _enabled = value, true);
        SubscribeLocalEvent<TTSComponent, EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<ActorComponent, HeadsetRadioReceiveRelayEvent>(OnRadioReceived);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _client.Dispose();
        lock (_cacheLock)
        {
            _cache.Clear();
            _cacheOrder.Clear();
            _inFlight.Clear();
        }
    }

    private void OnRadioReceived(Entity<ActorComponent> listener, ref HeadsetRadioReceiveRelayEvent args)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(_cfg.GetCVar(CCVars.TTSApiToken)))
            return;

        var source = args.RelayedEvent.MessageSource;
        if (!TryComp(source, out TTSComponent? component))
            return;

        var maxChars = Math.Clamp(_cfg.GetCVar(CCVars.TTSMaxMessageChars), 1, 1000);
        var message = args.RelayedEvent.ChatMsg.Message.Message;
        if (message.Length > maxChars)
            return;

        var voice = GetOrAssignVoice(source, component);
        if (voice == null)
            return;

        _ = PlayRadio(listener.Comp.PlayerSession, voice, message);
    }

    private async Task PlayRadio(ICommonSession listener, TTSVoicePrototype voice, string rawMessage)
    {
        try
        {
            var message = Sanitize(rawMessage);
            if (message.Length == 0)
                return;

            var sound = await GetOrGenerate(voice, message);
            if (sound == null)
                return;

            RaiseNetworkEvent(new PlayTTSEvent(sound, null, RadioVolume, 0f), listener);
        }
        catch (Exception e)
        {
            _sawmill.Error($"Radio TTS handling failed: {e.Message}");
        }
    }

    private async void OnEntitySpoke(EntityUid uid, TTSComponent component, EntitySpokeEvent args)
    {
        try
        {
            if (!_enabled || string.IsNullOrWhiteSpace(_cfg.GetCVar(CCVars.TTSApiToken)))
                return;

            var maxChars = Math.Clamp(_cfg.GetCVar(CCVars.TTSMaxMessageChars), 1, 1000);
            if (args.Message.Length > maxChars)
                return;

            var message = Sanitize(args.Message);
            if (message.Length == 0)
                return;

            var voice = GetOrAssignVoice(uid, component);
            if (voice == null)
                return;

            var whispering = args.ObfuscatedMessage != null;
            var range = whispering ? WhisperRange : SayRange;
            var volume = whispering ? WhisperVolume : 1f;
            var recipients = Filter.Empty().AddInRange(
                _transform.GetMapCoordinates(uid),
                range,
                entMan: EntityManager);

            if (recipients.Count == 0)
                return;

            var source = GetNetEntity(uid);
            var sound = await GetOrGenerate(voice, message);
            if (sound == null)
                return;

            RaiseNetworkEvent(new PlayTTSEvent(sound, source, volume, range), recipients);
        }
        catch (Exception e)
        {
            _sawmill.Error($"TTS speech handling failed: {e.Message}");
        }
    }

    private TTSVoicePrototype? GetOrAssignVoice(EntityUid uid, TTSComponent component)
    {
        if (component.VoicePrototypeId is { } voiceId &&
            _prototypes.TryIndex(voiceId, out TTSVoicePrototype? selected))
            return selected;

        var sex = TryComp(uid, out HumanoidAppearanceComponent? humanoid)
            ? humanoid.Sex
            : Sex.Unsexed;

        var candidates = _prototypes.EnumeratePrototypes<TTSVoicePrototype>()
            .Where(voice => !voice.Abstract && voice.RoundStart && voice.Sex == sex)
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = _prototypes.EnumeratePrototypes<TTSVoicePrototype>()
                .Where(voice => !voice.Abstract && voice.RoundStart)
                .ToList();
        }

        if (candidates.Count == 0)
            return null;

        selected = _random.Pick(candidates);
        component.VoicePrototypeId = selected.ID;
        return selected;
    }

    private async Task<byte[]?> GetOrGenerate(TTSVoicePrototype voice, string message)
    {
        var key = $"{voice.Speaker}\n{message}";
        Task<byte[]?> request;
        lock (_cacheLock)
        {
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            if (!_inFlight.TryGetValue(key, out request!))
            {
                request = _client.Synthesize(voice.Speaker, message);
                _inFlight[key] = request;
            }
        }

        byte[]? sound;
        try
        {
            sound = await request;
        }
        catch
        {
            lock (_cacheLock)
                _inFlight.Remove(key);
            throw;
        }

        if (sound == null)
        {
            lock (_cacheLock)
                _inFlight.Remove(key);
            return null;
        }

        lock (_cacheLock)
        {
            _inFlight.Remove(key);
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            if (_cache.Count >= MaxCacheEntries)
            {
                var oldest = _cacheOrder.Dequeue();
                _cache.Remove(oldest);
            }

            _cache[key] = sound;
            _cacheOrder.Enqueue(key);
        }

        return sound;
    }

    private static string Sanitize(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character) || char.IsWhiteSpace(character) ||
                ".,!?…:;'\"()-—".Contains(character))
                builder.Append(character);
        }

        var sanitized = string.Join(" ", builder
            .ToString()
            .Split((char[]?) null, StringSplitOptions.RemoveEmptyEntries));

        if (sanitized.Length > 0 && char.IsLetterOrDigit(sanitized[^1]))
            sanitized += ".";

        return sanitized;
    }
}
