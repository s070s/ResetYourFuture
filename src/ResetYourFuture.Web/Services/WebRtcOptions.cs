using System.ComponentModel.DataAnnotations;

namespace ResetYourFuture.Web.Services;

/// <summary>
/// WebRTC/signaling configuration bound from the "WebRtc" config section.
/// Validated at startup (CFG-4) so nonsensical values (negative timeout, &lt; 2 participants)
/// fail fast instead of binding silently.
/// </summary>
public class WebRtcOptions
{
    [Range(1, 3600)]
    public int RingTimeoutSeconds { get; set; } = 45;

    [Range(2, 100)]
    public int MaxParticipants { get; set; } = 6;

    public List<IceServerOptions> IceServers { get; set; } = [];
}

/// <summary>
/// A single ICE server entry. Username/Credential are optional and only needed for TURN.
/// </summary>
public class IceServerOptions
{
    public List<string> Urls { get; set; } = [];

    public string? Username { get; set; }

    public string? Credential { get; set; }
}
