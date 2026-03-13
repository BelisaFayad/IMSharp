<script setup lang="ts">
import { ref, computed, onMounted, onUnmounted, nextTick, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useChatStore, useContactsStore, useAuthStore, useUiStore } from '@/stores'
import { MessageBubble, LoadingSpinner, ConfirmationModal, Header, ChatInputBar } from '@/components'
import { containsScript } from '@/utils/contentValidator'
import { signalRService } from '@/services'
import type { PrivateMessage } from '@/types'

const route = useRoute()
const router = useRouter()
const chatStore = useChatStore()
const contactsStore = useContactsStore()
const authStore = useAuthStore()
const uiStore = useUiStore()

const chatId = route.params.id as string
const messagesContainer = ref<HTMLElement | null>(null)
const chatInputBarRef = ref<InstanceType<typeof ChatInputBar> | null>(null)
const isLoading = ref(true)
const isSending = ref(false)
const showFriendDeletedDialog = ref(false)

// 获取聊天对象信息
const chatUser = computed(() => {
  return contactsStore.friends.find(f => f.id === chatId)
})

// 获取消息列表(按时间升序排序,最旧的在上,最新的在下)
const messages = computed(() => {
  const msgs = chatStore.privateMessages.get(chatId) || []
  return [...msgs].sort((a, b) =>
    new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime()
  )
})

// 是否正在输入
const isTyping = computed(() => {
  return chatStore.typingUsers.get(chatId) || false
})

async function handleReconnected() {
  try {
    await chatStore.loadPrivateMessages(chatId)
  } catch (error) {
    console.error('重连后恢复私聊失败:', error)
  }
}

onMounted(async () => {
  signalRService.on('Reconnected', handleReconnected)

  try {
    // 设置当前聊天 ID
    chatStore.setCurrentChatId(chatId)

    // 加载好友信息
    if (!chatUser.value) {
      await contactsStore.loadFriends()
    }

    // 加载消息历史
    await chatStore.loadPrivateMessages(chatId)

    // 标记所有消息为已读
    await chatStore.markAllAsRead(chatId)
  } catch (error) {
    console.error('加载聊天失败:', error)
  } finally {
    isLoading.value = false
    // 等待加载状态更新和 DOM 渲染完成后滚动到底部
    await nextTick()
    setTimeout(() => {
      scrollToBottom()
    }, 100)
  }
})

onUnmounted(() => {
  signalRService.off('Reconnected', handleReconnected)
  // 清除当前聊天 ID
  chatStore.setCurrentChatId(null)
})

// 监听新消息,自动滚动到底部
watch(messages, async () => {
  await nextTick()
  scrollToBottom()
})

// 监听好友被删除事件
watch(() => chatStore.deletedFriendId, (deletedId) => {
  console.log('[ChatDetailPage] deletedFriendId 变化:', {
    deletedId,
    chatId,
    shouldShow: deletedId === chatId
  })
  if (deletedId === chatId) {
    console.log('[ChatDetailPage] 显示好友删除对话框')
    showFriendDeletedDialog.value = true
  }
})

function handleFriendDeletedConfirm() {
  console.log('[ChatDetailPage] 用户确认好友删除提示')
  showFriendDeletedDialog.value = false
  chatStore.clearDeletedFriendNotification()
  // 返回到聊天列表页面
  router.push('/chats')
}

async function handleSendText(content: string) {
  if (containsScript(content)) {
    uiStore.showToast('消息内容包含不允许的脚本内容', 'error')
    return
  }

  isSending.value = true
  try {
    await chatStore.sendPrivateMessage(chatId, content)
    // 发送成功后清空输入框
    chatInputBarRef.value?.clearInput()
  } catch (error) {
    console.error('发送消息失败:', error)
    uiStore.showToast('发送消息失败', 'error')
  } finally {
    isSending.value = false
  }
}

async function handleSendImage(url: string) {
  try {
    await chatStore.sendPrivateMessage(chatId, url, 'Image')
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
  if (index === 0) return true
  const currentMsg = messages.value[index]
  const prevMsg = messages.value[index - 1]
  if (!currentMsg || !prevMsg) return false

  const currentTime = new Date(currentMsg.createdAt).getTime()
  const prevTime = new Date(prevMsg.createdAt).getTime()
  // 超过5分钟显示时间
  return currentTime - prevTime > 5 * 60 * 1000
}
</script>

<template>
  <div class="text-slate-900 dark:text-slate-100 h-screen flex flex-col relative">
    <Header :title="chatUser?.displayName || chatUser?.username || '聊天详情'" :show-back="true" @back="router.back()">
      <template #title>
        <div v-if="chatUser" class="flex flex-col items-center">
          <h1 class="text-lg font-bold leading-none text-slate-900 dark:text-white">{{ chatUser.displayName || chatUser.username }}</h1>
          <span
            v-if="isTyping"
            class="text-[10px] text-primary font-medium"
          >
            正在输入...
          </span>
          <span
            v-else
            class="text-[10px] font-medium"
            :class="chatUser.isOnline ? 'text-emerald-500' : 'text-slate-400'"
          >
            {{ chatUser.isOnline ? '在线' : '离线' }}
          </span>
        </div>
        <div v-else class="flex flex-col items-center">
          <h1 class="text-lg font-bold leading-none text-slate-900 dark:text-white">聊天详情</h1>
        </div>
      </template>
      <template #right>
        <button
          @click="router.push(`/chats/${chatId}/settings`)"
          class="flex items-center justify-center p-1 hover:bg-slate-100 dark:hover:bg-slate-700 rounded-full transition-colors"
        >
          <span class="material-symbols-outlined text-xl text-slate-900 dark:text-white">more_horiz</span>
        </button>
      </template>
    </Header>

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
      <template v-if="messages.length > 0">
        <template v-for="(message, index) in messages" :key="message.id">
          <!-- 时间分隔符 -->
          <div v-if="shouldShowTimestamp(index)" class="flex justify-center">
            <span class="text-xs text-slate-400 bg-slate-100 dark:bg-slate-800 px-2 py-1 rounded-full">
              {{ formatDate(message.createdAt) }} {{ formatTime(message.createdAt) }}
            </span>
          </div>

          <!-- 消息气泡 -->
          <MessageBubble
            :content="message.content"
            :type="message.type"
            :is-self="message.senderId === authStore.user?.id"
            :time="formatTime(message.createdAt)"
            :status="message.status.toLowerCase() as 'sent' | 'delivered' | 'read'"
            :avatar="message.senderId === authStore.user?.id ? authStore.user?.avatar || undefined : chatUser?.avatar || undefined"
          />
        </template>
      </template>

      <!-- 空状态 -->
      <div v-else class="flex flex-col items-center justify-center py-20">
        <span class="material-symbols-outlined text-6xl text-slate-300 dark:text-slate-700 mb-4">chat_bubble_outline</span>
        <p class="text-slate-500 dark:text-slate-400 text-sm">暂无消息</p>
      </div>
    </main>

    <ChatInputBar ref="chatInputBarRef" :is-sending="isSending" @send-text="handleSendText" @send-image="handleSendImage" />

    <!-- 好友删除确认对话框 -->
    <ConfirmationModal
      :is-open="showFriendDeletedDialog"
      title="好友关系已删除"
      message="你已被对方删除好友关系"
      confirm-text="确定"
      @confirm="handleFriendDeletedConfirm"
      @cancel="handleFriendDeletedConfirm"
    />
  </div>
</template>
