using IMSharp.Domain.Entities;
using IMSharp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace IMSharp.Infrastructure.Repositories;

public class PrivateMessageRepository(ApplicationDbContext context) : IPrivateMessageRepository
{
    public async Task AddAsync(PrivateMessage message, CancellationToken cancellationToken = default)
    {
        await context.PrivateMessages.AddAsync(message, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<PrivateMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.PrivateMessages
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<List<PrivateMessage>> GetConversationWithCursorAsync(
        Guid userId, Guid friendId, Guid? before, Guid? after, int limit, CancellationToken cancellationToken = default)
    {
        var baseQuery = context.PrivateMessages
            .Include(m => m.Sender)
            .Include(m => m.Receiver)
            .Where(m => (m.SenderId == userId && m.ReceiverId == friendId) ||
                        (m.SenderId == friendId && m.ReceiverId == userId));

        List<PrivateMessage> messages;

        if (after.HasValue)
        {
            // 向后加载新消息：获取 ID 大于 after 的消息
            messages = await baseQuery
                .Where(m => m.Id.CompareTo(after.Value) > 0)
                .OrderBy(m => m.Id)
                .Take(limit + 1)
                .ToListAsync(cancellationToken);

            // 反转为倒序（最新在前）
            messages.Reverse();
        }
        else if (before.HasValue)
        {
            // 向前加载历史：获取 ID 小于 before 的消息
            messages = await baseQuery
                .Where(m => m.Id.CompareTo(before.Value) < 0)
                .OrderByDescending(m => m.Id)
                .Take(limit + 1)
                .ToListAsync(cancellationToken);
        }
        else
        {
            // 首次加载：获取最新的消息
            messages = await baseQuery
                .OrderByDescending(m => m.Id)
                .Take(limit + 1)
                .ToListAsync(cancellationToken);
        }

        return messages;
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await context.PrivateMessages
            .Where(m => m.ReceiverId == userId && m.Status != MessageStatus.Read)
            .CountAsync(cancellationToken);
    }

    public async Task<int> GetUnreadCountWithUserAsync(Guid userId, Guid friendId, CancellationToken cancellationToken = default)
    {
        return await context.PrivateMessages
            .Where(m => m.ReceiverId == userId && m.SenderId == friendId && m.Status != MessageStatus.Read)
            .CountAsync(cancellationToken);
    }

    public async Task<DateTimeOffset?> MarkAsDeliveredAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var message = await context.PrivateMessages.FindAsync([messageId], cancellationToken);
        if (message == null)
        {
            return null;
        }

        if (message.Status == MessageStatus.Sent)
        {
            var deliveredAt = DateTimeOffset.UtcNow;
            message.Status = MessageStatus.Delivered;
            message.DeliveredAt = deliveredAt;
            await context.SaveChangesAsync(cancellationToken);
            return deliveredAt;
        }

        return message.DeliveredAt;
    }

    public async Task<DateTimeOffset?> MarkAsReadAsync(Guid messageId, CancellationToken cancellationToken = default)
    {
        var message = await context.PrivateMessages.FindAsync([messageId], cancellationToken);
        if (message == null)
        {
            return null;
        }

        if (message.Status != MessageStatus.Read)
        {
            var readAt = DateTimeOffset.UtcNow;
            message.Status = MessageStatus.Read;
            message.ReadAt = readAt;
            await context.SaveChangesAsync(cancellationToken);
            return readAt;
        }

        return message.ReadAt;
    }

    public async Task<DateTimeOffset?> MarkAllAsReadAsync(Guid userId, Guid friendId, CancellationToken cancellationToken = default)
    {
        var messages = await context.PrivateMessages
            .Where(m => m.ReceiverId == userId && m.SenderId == friendId && m.Status != MessageStatus.Read)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            return null;
        }

        var readAt = DateTimeOffset.UtcNow;
        foreach (var message in messages)
        {
            message.Status = MessageStatus.Read;
            message.ReadAt = readAt;
        }

        await context.SaveChangesAsync(cancellationToken);
        return readAt;
    }

    public async Task DeleteConversationAsync(Guid userId, Guid friendId, CancellationToken cancellationToken = default)
    {
        await context.PrivateMessages
            .Where(m => (m.SenderId == userId && m.ReceiverId == friendId) ||
                        (m.SenderId == friendId && m.ReceiverId == userId))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<int> DeleteOldMessagesAsync(DateTimeOffset before, CancellationToken cancellationToken = default)
    {
        return await context.PrivateMessages
            .Where(m => m.CreatedAt < before)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
