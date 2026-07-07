using ResetYourFuture.Domain.Enums;

namespace ResetYourFuture.Application.DTOs;

/// <summary>
/// An incoming call invite pushed to a callee (or their other tabs).
/// </summary>
public record CallInviteDto(
    Guid CallId,
    string InitiatorId,
    string InitiatorName,
    Guid? ConversationId,
    bool IsGroup,
    List<string> ParticipantNames
);

/// <summary>
/// A single participant's live state within an active call, as seen by other participants.
/// </summary>
public record CallParticipantDto(
    string UserId,
    string DisplayName,
    string ConnectionId,
    bool MicOn,
    bool CameraOn,
    bool ScreenSharing
);

/// <summary>
/// Outcome of a StartCall request.
/// </summary>
public enum StartCallStatus
{
    Ringing,
    AllUnavailable,
    CallerBusy,
    NoAccess
}

/// <summary>
/// Result returned to the caller after requesting a new call.
/// </summary>
public record StartCallResultDto(
    Guid? CallId,
    StartCallStatus StartCallStatus,
    List<string> UnavailableUserIds
);

/// <summary>
/// Result returned to a callee after accepting/rejoining a call.
/// </summary>
public record JoinCallResultDto(
    Guid CallId,
    List<CallParticipantDto> ExistingParticipants
);

/// <summary>
/// A participant's mic/camera/screen-share state, broadcast on change.
/// </summary>
public record MediaStateDto(
    bool MicOn,
    bool CameraOn,
    bool ScreenSharing
);
