namespace ResetYourFuture.Web.Services;

/// <summary>
/// WebRTC/signaling configuration bound from the "WebRtc" config section.
/// </summary>
public class WebRtcOptions
{
    public int RingTimeoutSeconds { get; set; } = 45;

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
