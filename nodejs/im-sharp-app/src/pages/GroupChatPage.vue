<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useChatStore, useGroupsStore, useAuthStore, useUiStore } from '@/stores'
import { MessageBubble, LoadingSpinner, Header, ChatInputBar } from '@/components'
import { containsScript } from '@/utils/contentValidator'
import { signalRService } from '@/services'
import type { GroupMessage, User } from '@/types'

interface SystemEvent {
  id: string
  type: 'system'
  text: string
  createdAt: string
}

const systemEvents = ref<SystemEvent[]>([])

// 合并消息和系统事件，按时间排序
const allItems = computed(() => {
  const msgs = messages.value.map(m => ({ ...m, type: m.type as string }))
  return [...msgs, ...systemEvents.value].sort(
    (a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()
  )
})

const route = useRoute()
const router = useRouter()
const chatStore = useChatStore()
const groupsStore = useGroupsStore()
const authStore = useAuthStore()
const uiStore = useUiStore()

const groupId = route.params.id as string
const messagesContainer = ref<HTMLElement | null>(null)
const isLoading = ref(true)
const showAnnouncement = ref(true)
const showAnnouncementDetail = ref(false)

// 获取群组信息
const group = computed(() => {
  return groupsStore.groups.find(g => g.id === groupId)
})

// 获取群组成员
const members = computed(() => {
  return groupsStore.groupMembers.get(groupId) || []
})

// 获取消息列表(按时间升序排序,最旧的在上,最新的在下)
const messages = computed(() => {
  const msgs = chatStore.groupMessages.get(groupId) || []
  return [...msgs].sort((a, b) =>
    new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()
  )
})

function handleMemberJoined(member: import('@/types').GroupMember) {
  if (member.groupId !== groupId) return
  const name = member.user?.displayName || member.user?.username || '新成员'
  systemEvents.value.push({
    id: `join-${member.userId}-${Date.now()}`,
    type: 'system',
    text: `${name} 加入了群聊`,
    createdAt: new Date().toISOString(),
  })
}

function handleMemberLeft(gId: string, userId: string) {
  if (gId !== groupId) return
  const member = members.value.find(m => m.userId === userId)
  const name = member?.user?.displayName || member?.user?.username || '成员'
  systemEvents.value.push({
    id: `leave-${userId}-${Date.now()}`,
    type: 'system',
    text: `${name} 离开了群聊`,
    createdAt: new Date().toISOString(),
  })
}

async function handleReconnected() {
  try {
    await signalRService.joinGroup(groupId)
    await chatStore.loadGroupMessages(groupId)
  } catch (error) {
    console.error('重连后恢复群聊失败:', error)
  }
}

onMounted(async () => {
  signalRService.on('Reconnected', handleReconnected)
  signalRService.on('GroupMemberJoined', handleMemberJoined)
  signalRService.on('MemberLeftGroup', handleMemberLeft)

  try {
    // 设置当前聊天 ID
    chatStore.setCurrentChatId(groupId)

    // 加载群组信息
    if (!group.value) {
      await groupsStore.loadGroups()
    }

    // 加载群组成员
    await groupsStore.loadGroupMembers(groupId)

    // 加入 SignalR 群组房间
    await signalRService.joinGroup(groupId)

    // 加载消息历史
    await chatStore.loadGroupMessages(groupId)

    // 标记群聊消息为已读（清除未读数）
    chatStore.clearUnreadCount(groupId)
  } catch (error) {
    console.error('加载群聊失败:', error)
    uiStore.showToast('加载群聊失败', 'error')
  } finally {
    isLoading.value = false
    // 等待加载状态更新和 DOM 渲染完成后滚动到底部
    await nextTick()
    setTimeout(() => {
      scrollToBottom()
    }, 100)
  }
})

onUnmounted(async () => {
  signalRService.off('Reconnected', handleReconnected)
  signalRService.off('GroupMemberJoined', handleMemberJoined)
  signalRService.off('MemberLeftGroup', handleMemberLeft)

  // 清除当前聊天 ID
  chatStore.setCurrentChatId(null)

  // 离开 SignalR 群组房间
  try {
    await signalRService.leaveGroup(groupId)
  } catch (error) {
    console.error('离开群组房间失败:', error)
  }
})

// 监听新消息,自动滚动到底部
watch(allItems, async () => {
  await nextTick()
  scrollToBottom()
})

async function handleSendText(content: string) {
  if (containsScript(content)) {
    uiStore.showToast('消息内容包含不允许的脚本内容', 'error')
    return
  }
  try {
    await chatStore.sendGroupMessage(groupId, content)
  } catch (error) {
    console.error('发送消息失败:', error)
    uiStore.showToast('发送消息失败', 'error')
  }
}

async function handleSendImage(url: string) {
  try {
    await chatStore.sendGroupMessage(groupId, url, 'Image')
  } catch (error) {
    console.error('发送图片失败:', error)
    uiStore.showToast('图片发送失败', 'error')
  }
}

function scrollToBottom() {
  if (messagesContainer.value) {
    messagesContainer.value.scrollTop = messagesContainer.value.scrollHeight
  }
}

function formatTime(time: string) {
  const date = new Date(time)
  return date.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' })
}

function formatDate(time: string) {
  const date = new Date(time)
  const now = new Date()
  const diff = now.getTime() - date.getTime()
  const days = Math.floor(diff / (1000 * 60 * 60 * 24))

  if (days === 0) {
    return '今天'
  } else if (days === 1) {
    return '昨天'
  } else if (days < 7) {
    return `${days}天前`
  } else {
    return date.toLocaleDateString('zh-CN', { month: '2-digit', day: '2-digit' })
  }
}

// 判断是否需要显示时间分隔符
function shouldShowTimestamp(index: number): boolean {
  const item = allItems.value[index]
  if (!item || item.type === 'system') return false
  if (index === 0) return true

  // 找前一个非系统消息
  let prevIndex = index - 1
  while (prevIndex >= 0 && allItems.value[prevIndex]?.type === 'system') {
    prevIndex--
  }
  if (prevIndex < 0) return true

  const prevItem = allItems.value[prevIndex]
  if (!prevItem) return false
  return new Date(item.createdAt).getTime() - new Date(prevItem.createdAt).getTime() > 5 * 60 * 1000
}

// 获取消息发送者信息
function getSenderInfo(message: GroupMessage) {
  // 如果消息包含 sender 信息,直接使用
  if (message.sender) {
    return {
      displayName: message.sender.displayName || message.sender.username,
      avatar: message.sender.avatar
    }
  }

  // 获取发送者 ID（兼容两种格式）
  const senderId = message.senderId || (message as any).sender?.id

  // 否则从成员列表中查找
  const member = members.value.find(m => m.userId === senderId)
  if (member?.user) {
    return {
      displayName: member.user.displayName || member.user.username,
      avatar: member.user.avatar
    }
  }

  // 如果是当前用户
  if (senderId && authStore.user && senderId === authStore.user.id) {
    return {
      displayName: authStore.user.displayName || authStore.user.username,
      avatar: authStore.user.avatar
    }
  }

  // 默认值
  return {
    displayName: '未知用户',
    avatar: null
  }
}
</script>

<template>
  <div class="text-slate-900 dark:text-slate-100 h-screen flex flex-col relative">
    <Header :title="group?.name || '群聊'" :show-back="true" @back="router.back()">
      <template #title>
        <div v-if="group" class="flex flex-col items-center">
          <h1 class="text-lg font-bold leading-none text-slate-900 dark:text-white">{{ group.name }}</h1>
          <span class="text-[10px] text-slate-400 font-medium">
            {{ group.memberCount }} 人
          </span>
        </div>
        <div v-else class="flex flex-col items-center">
          <h1 class="text-lg font-bold leading-none text-slate-900 dark:text-white">群聊</h1>
        </div>
      </template>
      <template #right>
        <button
          @click="router.push(`/groups/${groupId}/settings`)"
          class="flex items-center justify-center p-1 hover:bg-slate-100 dark:hover:bg-slate-700 rounded-full transition-colors"
        >
          <span class="material-symbols-outlined text-xl text-slate-900 dark:text-white">more_horiz</span>
        </button>
      </template>
    </Header>

    <!-- 群公告栏 -->
    <div
      v-if="group?.announcement?.content && showAnnouncement"
      @click="showAnnouncementDetail = true"
      class="bg-primary/10 dark:bg-primary/20 px-4 py-2 flex items-center gap-3 cursor-pointer hover:bg-primary/20 dark:hover:bg-primary/30 transition-colors"
    >
      <span class="material-symbols-outlined text-primary text-sm shrink-0">campaign</span>
      <p class="text-xs text-primary font-medium flex-1 truncate">{{ group.announcement?.content }}</p>
      <button
        @click.stop="showAnnouncement = false"
        class="text-primary/60 hover:text-primary"
      >
        <span class="material-symbols-outlined text-sm">close</span>
      </button>
    </div>

    <!-- 公告详情弹窗 -->
    <div
      v-if="showAnnouncementDetail"
      class="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/40 backdrop-blur-sm"
      @click.self="showAnnouncementDetail = false"
    >
      <div class="bg-white dark:bg-slate-800 w-full max-w-sm rounded-2xl p-6 shadow-2xl">
        <div class="flex items-center gap-2 mb-4 text-primary">
          <span class="material-symbols-outlined">campaign</span>
          <h3 class="text-lg font-bold">群公告</h3>
        </div>
        <div class="max-h-[60vh] overflow-y-auto mb-6">
          <p class="text-sm text-slate-700 dark:text-slate-300 leading-relaxed whitespace-pre-wrap">{{ group?.announcement?.content }}</p>
        </div>
        <button
          @click="showAnnouncementDetail = false"
          class="w-full py-3 rounded-xl bg-primary text-white font-bold text-sm"
        >我知道了</button>
      </div>
    </div>

    <!-- 加载中 -->
    <div v-if="isLoading" class="flex-1 flex items-center justify-center bg-slate-50 dark:bg-slate-900">
      <LoadingSpinner />
    </div>

    <!-- 消息列表 -->
    <main
      v-else
      ref="messagesContainer"
      class="flex-1 overflow-y-auto p-4 pb-32 space-y-4 bg-slate-50 dark:bg-slate-900"
    >
      <template v-if="allItems.length > 0">
        <template v-for="(item, index) in allItems" :key="item.id">
          <!-- 系统消息 -->
          <div v-if="item.type === 'system'" class="flex justify-center">
            <span class="text-xs text-slate-400 bg-slate-100 dark:bg-slate-800 px-3 py-1 rounded-full">
              {{ (item as any).text }}
            </span>
          </div>

          <template v-else>
          <!-- 时间分隔符 -->
          <div v-if="shouldShowTimestamp(index)" class="flex justify-center">
            <span class="text-xs text-slate-400 bg-slate-100 dark:bg-slate-800 px-2 py-1 rounded-full">
              {{ formatDate(item.createdAt) }} {{ formatTime(item.createdAt) }}
            </span>
          </div>

          <!-- 消息气泡 -->
          <div
            :class="[
              'flex items-start gap-3',
              ((item as any).senderId || (item as any).sender?.id) === authStore.user?.id ? 'justify-end' : ''
            ]"
          >
            <!-- 头像（他人消息在左，自己消息在右） -->
            <div
              v-if="((item as any).senderId || (item as any).sender?.id) !== authStore.user?.id"
              class="shrink-0"
            >
              <div
                v-if="getSenderInfo(item as any).avatar"
                class="size-10 rounded-full bg-cover bg-center border border-slate-200 dark:border-slate-700"
                :style="{ backgroundImage: `url(${getSenderInfo(item as any).avatar})` }"
              ></div>
              <div
                v-else
                class="size-10 rounded-full bg-slate-200 dark:bg-slate-700 flex items-center justify-center border border-slate-200 dark:border-slate-700"
              >
                <span class="material-symbols-outlined text-slate-400 text-xl">person</span>
              </div>
            </div>

            <div :class="['flex flex-col gap-1 max-w-[75%]', ((item as any).senderId || (item as any).sender?.id) === authStore.user?.id ? 'items-end' : 'items-start']">
              <!-- 发送者昵称 (非自己的消息才显示) -->
              <span
                v-if="((item as any).senderId || (item as any).sender?.id) !== authStore.user?.id"
                class="text-slate-500 dark:text-slate-400 text-xs font-medium ml-1"
              >
                {{ getSenderInfo(item as any).displayName }}
              </span>

              <!-- 文本消息 -->
              <div
                v-if="item.type === 'Text'"
                :class="[
                  'px-4 py-2.5 rounded-xl shadow-sm text-sm leading-relaxed break-words',
                  ((item as any).senderId || (item as any).sender?.id) === authStore.user?.id
                    ? 'bg-primary text-white rounded-br-none'
                    : 'bg-white dark:bg-slate-800 text-slate-900 dark:text-slate-100 rounded-bl-none',
                ]"
              >
                <p class="whitespace-pre-wrap">{{ (item as any).content }}</p>
              </div>

              <!-- 图片消息 -->
              <div
                v-else-if="item.type === 'Image'"
                :class="[
                  'rounded-xl overflow-hidden shadow-sm',
                  ((item as any).senderId || (item as any).sender?.id) === authStore.user?.id ? 'rounded-br-none' : 'rounded-bl-none',
                ]"
              >
                <img
                  :src="(item as any).content"
                  :alt="'图片消息'"
                  class="max-w-[200px] max-h-[200px] object-contain cursor-pointer hover:opacity-90 transition-opacity"
                />
              </div>

              <!-- 其他类型消息 -->
              <div
                v-else
                :class="[
                  'px-4 py-2.5 rounded-xl shadow-sm',
                  ((item as any).senderId || (item as any).sender?.id) === authStore.user?.id
                    ? 'bg-primary text-white rounded-br-none'
                    : 'bg-white dark:bg-slate-800 text-slate-900 dark:text-slate-100 rounded-bl-none',
                ]"
              >
                <p class="text-sm">[{{ item.type }}]</p>
              </div>
            </div>

            <!-- 自己的头像在右边 -->
            <div
              v-if="((item as any).senderId || (item as any).sender?.id) === authStore.user?.id"
              class="shrink-0"
            >
              <div
                v-if="authStore.user?.avatar"
                class="size-10 rounded-full bg-cover bg-center border border-primary/20"
                :style="{ backgroundImage: `url(${authStore.user.avatar})` }"
              ></div>
              <div
                v-else
                class="size-10 rounded-full bg-slate-200 dark:bg-slate-700 flex items-center justify-center border border-primary/20"
              >
                <span class="material-symbols-outlined text-slate-400 text-xl">person</span>
              </div>
            </div>
          </div>
          </template>
        </template>
      </template>

      <!-- 空状态 -->
      <div v-else class="flex flex-col items-center justify-center py-20">
        <span class="material-symbols-outlined text-6xl text-slate-300 dark:text-slate-700 mb-4">chat_bubble_outline</span>
        <p class="text-slate-500 dark:text-slate-400 text-sm">暂无消息</p>
      </div>
    </main>

    <ChatInputBar @send-text="handleSendText" @send-image="handleSendImage" />
  </div>
</template>
