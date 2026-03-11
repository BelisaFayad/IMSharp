import Dexie, { type Table } from 'dexie'
import type { PrivateMessage, GroupMessage } from '@/types'

// 会话元数据接口
interface ConversationMeta {
  id: string // 会话 ID (私聊: userId, 群聊: groupId)
  type: 'private' | 'group'
  lastMessage: string
  lastMessageTime: string
  unreadCount: number
}

// 已读位置接口
interface LastReadPosition {
  conversationId: string // 会话 ID (私聊: userId, 群聊: groupId)
  lastReadMessageId: string // 最后已读消息的 ID
  updatedAt: string // 更新时间
}

// IndexedDB 数据库类
class IMSharpDatabase extends Dexie {
  // 私聊消息表
  privateMessages!: Table<PrivateMessage, string>
  // 群聊消息表
  groupMessages!: Table<GroupMessage, string>
  // 会话元数据表
  conversations!: Table<ConversationMeta, string>
  // 已读位置表
  lastReadPositions!: Table<LastReadPosition, string>

  constructor() {
    super('IMSharpDB')

    // 定义数据库版本和表结构
    this.version(1).stores({
      // 私聊消息: 主键 id, 复合索引 [senderId+receiverId], 时间索引 createdAt
      privateMessages: 'id, [senderId+receiverId], createdAt',
      // 群聊消息: 主键 id, 群组索引 groupId, 时间索引 createdAt
      groupMessages: 'id, groupId, createdAt',
      // 会话元数据: 主键 id, 时间索引 lastMessageTime
      conversations: 'id, lastMessageTime'
    })

    // 版本 2: 添加已读位置表
    this.version(2).stores({
      privateMessages: 'id, [senderId+receiverId], createdAt',
      groupMessages: 'id, groupId, createdAt',
      conversations: 'id, lastMessageTime',
      // 已读位置: 主键 conversationId, 时间索引 updatedAt
      lastReadPositions: 'conversationId, updatedAt'
    })
  }
}

// 导出数据库实例
export const db = new IMSharpDatabase()

// 导出类型
export type { ConversationMeta, LastReadPosition }
