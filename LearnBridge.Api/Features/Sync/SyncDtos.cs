namespace LearnBridge.Api.Features.Sync;

public sealed record SyncGoalDto(Guid Id, string Title, string? Description, string Status, DateTime UpdatedAt);

public sealed record SyncJourneyMemoryDto(
    Guid Id,
    Guid? ConversationSessionId,
    string Category,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record SyncBatchRequest(
    Guid LearnerId,
    List<SyncGoalDto> Goals,
    List<SyncJourneyMemoryDto> JourneyMemories);

public sealed record SyncBatchResponse(List<SyncGoalDto> Goals, List<SyncJourneyMemoryDto> JourneyMemories);
