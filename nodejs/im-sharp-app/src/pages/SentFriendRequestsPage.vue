<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useContactsStore } from '@/stores'
import { Avatar, LoadingSpinner, Header } from '@/components'

const router = useRouter()
const contactsStore = useContactsStore()

const isLoading = ref(false)

onMounted(async () => {
  isLoading.value = true
  try {
    await contactsStore.loadSentRequests()
  } catch (error) {
    console.error('加载已发送请求失败:', error)
  } finally {
    isLoading.value = false
  }
})

const sentRequests = computed(() => contactsStore.sentRequests)

function getStatusText(status: string) {
  const statusMap: Record<string, string> = {
    Pending: '待处理',
    Accepted: '已接受',
    Rejected: '已拒绝'
  }
  return statusMap[status] || status
}

function getStatusColor(status: string) {
  const colorMap: Record<string, string> = {
    Pending: 'text-yellow-600 dark:text-yellow-500',
    Accepted: 'text-green-600 dark:text-green-500',
    Rejected: 'text-red-600 dark:text-red-500'
  }
  return colorMap[status] || 'text-slate-500'
}

function formatTime(time: string) {
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
</script>

<template>
  <div class="flex flex-col h-screen bg-slate-50 dark:bg-slate-900">
    <Header title="我的申请" :show-back="true" @back="router.back()" />

    <!-- 加载中 -->
    <div v-if="isLoading" class="flex-1 flex items-center justify-center">
      <LoadingSpinner />
    </div>

    <!-- 请求列表 -->
    <div v-else class="flex-1 overflow-y-auto p-4 space-y-3">
      <template v-if="sentRequests.length > 0">
        <div
          v-for="request in sentRequests"
          :key="request.id"
          class="flex items-center gap-3 p-4 bg-white dark:bg-slate-800 rounded-xl shadow-sm"
        >
          <Avatar
            :src="request.receiver?.avatar || undefined"
            :alt="request.receiver?.displayName || request.receiver?.username"
            size="lg"
          />
          <div class="flex-1 min-w-0">
            <p class="text-sm font-bold text-slate-900 dark:text-white truncate">
              {{ request.receiver?.displayName || request.receiver?.username }}
            </p>
            <p class="text-xs text-slate-500 dark:text-slate-400">
              {{ formatTime(request.createdAt) }}
            </p>
            <p v-if="request.message" class="text-xs text-slate-600 dark:text-slate-300 mt-1 line-clamp-2">
              {{ request.message }}
            </p>
          </div>
          <div class="shrink-0">
            <span
              :class="['text-xs font-semibold px-3 py-1.5 rounded-full', getStatusColor(request.status)]"
            >
              {{ getStatusText(request.status) }}
            </span>
          </div>
        </div>
      </template>

      <!-- 空状态 -->
      <div v-else class="flex flex-col items-center justify-center py-20">
        <span class="material-symbols-outlined text-6xl text-slate-300 dark:text-slate-700 mb-4">pending_actions</span>
        <p class="text-slate-500 dark:text-slate-400 text-sm">暂无申请记录</p>
      </div>
    </div>
  </div>
</template>
