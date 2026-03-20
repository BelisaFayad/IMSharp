using IMSharp.Core.DTOs;

namespace IMSharp.Core.Services;

public interface IPrivateMessageService
{
    Task<PrivateMessageDto> SendMessageAsync(Guid senderId, SendPrivateMessageRequest request, CancellationToken cancellationToken = default);
    Task<PrivateMessagePageResponse> GetConversationWithCursorAsync(Guid userId, Guid friendId, CursorPaginationRequest request, CancellationToken cancellationToken = default);
    Task<UnreadCountResponse> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<DateTimeOffset?> MarkAsDeliveredAsync(Guid messageId, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(Guid userId, Guid messageId, CancellationToken cancellationToken = default);
    Task<(Guid SenderId, DateTimeOffset ReadAt)> MarkAsReadAndGetSenderAsync(Guid userId, Guid messageId, CancellationToken cancellationToken = default);
    Task<DateTimeOffset?> MarkAllAsReadAsync(Guid userId, Guid friendId, CancellationToken cancellationToken = default);
    Task DeleteConversationAsync(Guid userId, Guid friendId, CancellationToken cancellationToken = default);
}
