// ReSharper disable CheckNamespace

using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    /// <summary>
    /// Enables server-side speech synthesis for entities with a TTS component.
    /// </summary>
    public static readonly CVarDef<bool> TTSEnabled =
        CVarDef.Create("tts.enabled", false, CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// NTTS synthesis endpoint. See https://ntts.fdev.team/docs.
    /// </summary>
    public static readonly CVarDef<string> TTSApiUrl =
        CVarDef.Create("tts.api_url", "https://ntts.fdev.team/api/v1/tts", CVar.SERVERONLY | CVar.ARCHIVE);

    /// <summary>
    /// Bearer token used to authenticate with NTTS. Never replicated to clients.
    /// </summary>
    public static readonly CVarDef<string> TTSApiToken =
        CVarDef.Create("tts.api_token", string.Empty, CVar.SERVERONLY | CVar.CONFIDENTIAL);

    public static readonly CVarDef<int> TTSApiTimeout =
        CVarDef.Create("tts.api_timeout", 10, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<int> TTSMaxMessageChars =
        CVarDef.Create("tts.max_message_chars", 200, CVar.SERVERONLY | CVar.ARCHIVE);

    public static readonly CVarDef<bool> TTSClientEnabled =
        CVarDef.Create("tts.client_enabled", true, CVar.CLIENTONLY | CVar.ARCHIVE);

    /// <summary>
    /// Linear TTS gain in the range 0 to 1.
    /// </summary>
    public static readonly CVarDef<float> TTSVolume =
        CVarDef.Create("tts.volume", 0.65f, CVar.CLIENTONLY | CVar.ARCHIVE);
}
