import { db } from './db'
import type { PrivateMessage, GroupMessage } from '@/types'
import type { LastReadPosition } from './db'

/**
 * IndexedDB 消息存储服务
 * 提供消息的本地持久化存储功能
 */
export const messageStorage = {
  // ==================== 私聊消息操作 ====================

  /**
   * 保存单条私聊消息
   */
  async savePrivateMessage(message: PrivateMessage): Promise<void> {
    try {
      await db.privateMessages.put(message)
    } catch (error) {
      console.error('保存私聊消息失败:', error)
      throw error
    }
  },

  /**
   * 获取私聊消息列表
   * @param userId 对方用户 ID
   * @param currentUserId 当前用户 ID
   * @param limit 限制数量 (默认 50)
   */
  async getPrivateMessages(
    userId: string,
    currentUserId: string,
    limit = 50
  ): Promise<PrivateMessage[]> {
    try {
      // 查询双向消息 (我发给对方 + 对方发给我)
      const messages = await db.privateMessages
        .where('[senderId+receiverId]')
        .equals([userId, currentUserId])
        .or('[senderId+receiverId]')
        .equals([currentUserId, userId])
        .sortBy('createdAt')

      // 返回最新的 limit 条消息
      return messages.slice(-limit)
    } catch (error) {
      console.error('获取私聊消息失败:', error)
      return []
    }
  },

  /**
   * 批量保存私聊消息
   */
  async savePrivateMessages(messages: PrivateMessage[]): Promise<void> {
    try {
      await db.privateMessages.bulkPut(messages)
    } catch (error) {
      console.error('批量保存私聊消息失败:', error)
      throw error
    }
  },

  /**
   * 删除与指定用户的所有私聊消息
   * @param userId 对方用户 ID
   * @param currentUserId 当前用户 ID
   */
  async deletePrivateMessages(userId: string, currentUserId: string): Promise<void> {
    try {
      console.log('[MessageStorage] 开始删除私聊消息:', { userId, currentUserId })

      // 删除双向消息 (我发给对方 + 对方发给我)
      const deleted1 = await db.privateMessages
        .where('[senderId+receiverId]')
        .equals([userId, currentUserId])
        .delete()

      const deleted2 = await db.privateMessages
        .where('[senderId+receiverId]')
        .equals([currentUserId, userId])
        .delete()

      console.log('[MessageStorage] 删除私聊消息完成:', {
        userId,
        currentUserId,
        deleted1,
        deleted2,
        total: deleted1 + deleted2
      })
    } catch (error) {
      console.error('删除私聊消息失败:', error)
      throw error
    }
  },

  // ==================== 群聊消息操作 ====================

  /**
   * 保存单条群聊消息
   */
  async saveGroupMessage(message: GroupMessage): Promise<void> {
    try {
      await db.groupMessages.put(message)
    } catch (error) {
      console.error('保存群聊消息失败:', error)
      throw error
    }
  },

  /**
   * 获取群聊消息列表
   * @param groupId 群组 ID
   * @param limit 限制数量 (默认 50)
   */
  async getGroupMessages(groupId: string, limit = 50): Promise<GroupMessage[]> {
    try {
      const messages = await db.groupMessages
        .where('groupId')
        .equals(groupId)
        .sortBy('createdAt')

      // 返回最新的 limit 条消息
      return messages.slice(-limit)
    } catch (error) {
      console.error('获取群聊消息失败:', error)
      return []
    }
  },

  /**
   * 批量保存群聊消息
   */
  async saveGroupMessages(messages: GroupMessage[]): Promise<void> {
    try {
      await db.groupMessages.bulkPut(messages)
    } catch (error) {
      console.error('批量保存群聊消息失败:', error)
      throw error
    }
  },

  // ==================== 清理操作 ====================

  /**
   * 清空所有消息和会话
   */
  async clearAllMessages(): Promise<void> {
    try {
      await db.privateMessages.clear()
      await db.groupMessages.clear()
      await db.conversations.clear()
    } catch (error) {
      console.error('清空消息失败:', error)
      throw error
    }
  },

  /**
   * 删除过期消息
   * @param daysToKeep 保留天数 (默认 30 天)
   */
  async deleteOldMessages(daysToKeep = 30): Promise<void> {
    try {
      const cutoffDate = new Date()
      cutoffDate.setDate(cutoffDate.getDate() - daysToKeep)
      const cutoffTime = cutoffDate.toISOString()

      // 删除私聊消息
      await db.privateMessages.where('createdAt').below(cutoffTime).delete()

      // 删除群聊消息
      await db.groupMessages.where('createdAt').below(cutoffTime).delete()
    } catch (error) {
      console.error('删除过期消息失败:', error)
      throw error
    }
  },

  // ==================== 统计信息 ====================

  /**
   * 获取存储统计信息
   */
  async getStorageStats(): Promise<{
    privateMessageCount: number
    groupMessageCount: number
    conversationCount: number
  }> {
    try {
      const [privateMessageCount, groupMessageCount, conversationCount] = await Promise.all([
        db.privateMessages.count(),
        db.groupMessages.count(),
        db.conversations.count()
      ])

      return {
        privateMessageCount,
        groupMessageCount,
        conversationCount
      }
    } catch (error) {
      console.error('获取存储统计失败:', error)
      return {
        privateMessageCount: 0,
        groupMessageCount: 0,
        conversationCount: 0
      }
    }
  },

  // ==================== 已读位置操作 ====================

  /**
   * 保存已读位置
   * @param conversationId 会话 ID
   * @param lastReadMessageId 最后已读消息 ID
   */
  async saveLastReadPosition(conversationId: string, lastReadMessageId: string): Promise<void> {
    try {
      const position: LastReadPosition = {
        conversationId,
        lastReadMessageId,
        updatedAt: new Date().toISOString()
      }
      await db.lastReadPositions.put(position)
    } catch (error) {
      console.error('保存已读位置失败:', error)
      throw error
    }
  },

  /**
   * 获取已读位置
   * @param conversationId 会话 ID
   * @returns 最后已读消息 ID，如果不存在返回 null
   */
  async getLastReadPosition(conversationId: string): Promise<string | null> {
    try {
      const position = await db.lastReadPositions.get(conversationId)
      return position?.lastReadMessageId || null
    } catch (error) {
      console.error('获取已读位置失败:', error)
      return null
    }
  },

  /**
   * 获取所有已读位置
   * @returns Map<conversationId, lastReadMessageId>
   */
  async getAllLastReadPositions(): Promise<Map<string, string>> {
    try {
      const positions = await db.lastReadPositions.toArray()
      return new Map(positions.map(p => [p.conversationId, p.lastReadMessageId]))
    } catch (error) {
      console.error('获取所有已读位置失败:', error)
      return new Map()
    }
  },

  /**
   * 获取会话最新一条消息（私聊或群聊）
   */
  async getLastMessage(conversationId: string): Promise<{ id: string } | null> {
    try {
      // 先尝试群聊
      const groupMsg = await db.groupMessages
        .where('groupId')
        .equals(conversationId)
        .last()
      if (groupMsg) return groupMsg

      // 再尝试私聊（双向）
      const sent = await db.privateMessages
        .where('[senderId+receiverId]')
        .between([conversationId, ''], [conversationId, '\uffff'])
        .last()
      const received = await db.privateMessages
        .where('[senderId+receiverId]')
        .between(['', conversationId], ['\uffff', conversationId])
        .last()

      if (sent && received) {
        return sent.createdAt > received.createdAt ? sent : received
      }
      return sent || received || null
    } catch (error) {
      console.error('获取最新消息失败:', error)
      return null
    }
  },

  /**
   * 删除已读位置
   * @param conversationId 会话 ID
   */
  async deleteLastReadPosition(conversationId: string): Promise<void> {
    try {
      await db.lastReadPositions.delete(conversationId)
    } catch (error) {
      console.error('删除已读位置失败:', error)
      throw error
    }
  },

  /**
   * 计算私聊未读消息数（lastReadMessageId 之后收到的消息）
   */
  async countPrivateUnreadMessages(
    friendId: string,
    currentUserId: string,
    lastReadMessageId: string
  ): Promise<number> {
    try {
      // 查对方发给我的消息（未读候选）
      const received = await db.privateMessages
        .where('[senderId+receiverId]')
        .equals([friendId, currentUserId])
        .sortBy('createdAt')

      // 查所有双向消息，用于定位 lastReadMessageId 的时间戳
      const allMessages = await db.privateMessages
        .where('[senderId+receiverId]')
        .anyOf([[friendId, currentUserId], [currentUserId, friendId]])
        .sortBy('createdAt')

      const lastRead = allMessages.find(m => m.id === lastReadMessageId)
      if (!lastRead) return 0

      // 统计已读位置之后收到的消息数
      return received.filter(m => m.createdAt > lastRead.createdAt).length
    } catch (error) {
      console.error('计算私聊未读数失败:', error)
      return 0
    }
  },

  /**
   * 计算群聊未读消息数（lastReadMessageId 之后的消息，排除自己发送的）
   */
  async countGroupUnreadMessages(
    groupId: string,
    currentUserId: string,
    lastReadMessageId: string
  ): Promise<number> {
    try {
      const messages = await db.groupMessages
        .where('groupId')
        .equals(groupId)
        .sortBy('createdAt')

      const lastRead = messages.find(m => m.id === lastReadMessageId)
      if (!lastRead) return 0

      return messages
        .filter(m => m.createdAt > lastRead.createdAt && m.senderId !== currentUserId)
        .length
    } catch (error) {
      console.error('计算群聊未读数失败:', error)
      return 0
    }
  },
}
