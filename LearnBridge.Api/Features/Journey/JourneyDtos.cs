namespace LearnBridge.Api.Features.Journey;

public sealed record StartSessionRequest(Guid LearnerId);

public sealed record StartSessionResponse(Guid SessionId, DateTime StartedAt);

public sealed record SendMessageRequest(string Message);

public sealed record GoalUpdateDto(Guid Id, string Title, string? Description, string Status, bool WasCreated);

public sealed record SendMessageResponse(string Reply, IReadOnlyList<GoalUpdateDto> GoalUpdates, int MemoriesRecorded);
