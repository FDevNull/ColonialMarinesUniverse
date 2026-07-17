# NTTS speech synthesis

CMU can synthesize local character speech through the
[NTTS API](https://ntts.fdev.team/docs). The implementation uses OGG responses,
plays them spatially from the speaker, sends radio speech only to the actual
headset recipients, limits whispers to clear whisper range, and keeps a small
server-side cache.

The feature is disabled until both of these server CVars are set:

```text
tts.enabled true
tts.api_token <secret Bearer token>
```

The default endpoint is `https://ntts.fdev.team/api/v1/tts`. It can be changed
with `tts.api_url`. Other server settings are `tts.api_timeout` (10 seconds by
default) and `tts.max_message_chars` (200 by default).

Each player can disable playback with `tts.client_enabled false` or change the
linear gain with `tts.volume` (0 through 1). The API token is a confidential,
server-only CVar and must never be committed to the repository.

Players select a voice in character setup. The searchable list is filtered to
voices compatible with the character's sex, and the selection is stored with
the character profile. Existing profiles and mobs without a saved choice get a
compatible fallback voice automatically. A fixed voice may also be selected on
any entity prototype:

```yaml
- type: TTS
  voice: CMUTTSKleiner
```

The voice prototypes are synchronized from the live
[`/api/v1/tts/speakers`](https://ntts.fdev.team/api/v1/tts/speakers) response.
They include the API-provided display name, description, source, gender, and
exact speaker identifier.
