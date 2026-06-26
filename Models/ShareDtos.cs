namespace R3.Models;

public record ClaimableParticipant(long ParticipantId, string Name);

public record JoinInfo(long TripId, string Title, IReadOnlyList<ClaimableParticipant> Claimable, bool AlreadyMember);

public enum ClaimOutcome { Success, AlreadyMember, InvalidToken, ParticipantNotFound, AlreadyClaimed }

public record ClaimResult(ClaimOutcome Outcome, long TripId);

public record ClaimDto(long ParticipantId);
