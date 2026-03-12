<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useGroupsStore, useUiStore } from '@/stores'
import { Header, LoadingSpinner } from '@/components'
import type { GroupJoinRequest } from '@/types'

const route = useRoute()
const router = useRouter()
const groupsStore = useGroupsStore()
const uiStore = useUiStore()

const groupId = route.params.id as string | undefined
const requests = ref<GroupJoinRequest[]>([])
const isLoading = ref(true)
const processingRequestId = ref<string | null>(null)

// 判断是查看群组申请还是我的申请
const isGroupRequests = computed(() => !!groupId)

onMounted(async () => {
  await loadRequests()
})

async function loadRequests() {
  isLoading.value = true
  try {
    if (isGroupRequests.value && groupId) {
      // 加载群组的入群申请
      requests.value = await groupsStore.getGroupJoinRequests(groupId)
    } else {
      // 加载我的入群申请
      requests.value = await groupsStore.getMyJoinRequests()
    }
  } catch (error) {
    console.error('Load join requests failed:', error)
    uiStore.showToast('加载失败', 'error')
  } finally {
    isLoading.value = false
  }
}

// 处理入群申请
async function handleProcessRequest(requestId: string, accept: boolean) {
  processingRequestId.value = requestId
  try {
    await groupsStore.processJoinRequest(requestId, accept)
    uiStore.showToast(accept ? '已接受' : '已拒绝', 'success')
    // 重新加载列表
    await loadRequests()
  } catch (error) {
    console.error('Process request failed:', error)
    uiStore.showToast('操作失败', 'error')
  } finally {
    processingRequestId.value = null
  }
}

// 格式化时间
function formatTime(time: string) {
  const date = new Date(time)
  const now = new Date()
  const diff = now.getTime() - date.getTime()
  const days = Math.floor(diff / (1000 * 60 * 60 * 24))

  if (days === 0) {
    const hours = Math.floor(diff / (1000 * 60 * 60))
    if (hours === 0) {
      const minutes = Math.floor(diff / (1000 * 60))
      return minutes === 0 ? '刚刚' : `${minutes}分钟前`
    }
    return `${hours}小时前`
  } else if (days === 1) {
    return '昨天'
  } else if (days < 7) {
    return `${days}天前`
  } else {
    return date.toLocaleDateString('zh-CN', { month: '2-digit', day: '2-digit' })
  }
}

// 获取状态文本
function getStatusText(status: string) {
  switch (status) {
    case 'Pending':
      return '待处理'
    case 'Accepted':
      return '已接受'
    case 'Rejected':
      return '已拒绝'
    default:
      return status
  }
}

// 获取状态颜色
function getStatusColor(status: string) {
  switch (status) {
    case 'Pending':
      return 'text-yellow-600 dark:text-yellow-500'
    case 'Accepted':
      return 'text-green-600 dark:text-green-500'
    case 'Rejected':
      return 'text-red-600 dark:text-red-500'
    default:
      return 'text-slate-500'
  }
}
</script>

<template>
  <div class="flex flex-col h-screen overflow-hidden bg-slate-50 dark:bg-slate-900">
    <Header
      :title="isGroupRequests ? '入群申请' : '我的申请'"
      :show-back="true"
      @back="router.back()"
    />

    <!-- 加载中 -->
    <div v-if="isLoading" class="flex-1 flex items-center justify-center">
      <LoadingSpinner />
    </div>

    <!-- 申请列表 -->
    <div v-else class="flex-1 overflow-y-auto">
      <div v-if="requests.length > 0" class="divide-y divide-slate-200 dark:divide-slate-800">
        <div
          v-for="request in requests"
          :key="request.id"
          class="bg-white dark:bg-slate-800 p-4 hover:bg-slate-50 dark:hover:bg-slate-800/80 transition-colors"
        >
          <div class="flex items-start gap-3">
            <!-- 用户头像 -->
            <div
              v-if="request.user?.avatar"
              class="size-12 rounded-full bg-cover bg-center border border-slate-200 dark:border-slate-700 shrink-0"
              :style="{ backgroundImage: `url(${request.user.avatar})` }"
            ></div>
            <div
              v-else
              class="size-12 rounded-full bg-slate-200 dark:bg-slate-700 flex items-center justify-center border border-slate-200 dark:border-slate-700 shrink-0"
            >
              <span class="material-symbols-outlined text-slate-400 text-xl">person</span>
            </div>

            <!-- 申请信息 -->
            <div class="flex-1 min-w-0">
              <div class="flex items-center justify-between mb-1">
                <h3 class="font-medium text-slate-900 dark:text-slate-100">
                  {{ request.user?.displayName || request.user?.username }}
                </h3>
                <span class="text-xs" :class="getStatusColor(request.status)">
                  {{ getStatusText(request.status) }}
                </span>
              </div>

              <!-- 群组名称 -->
              <p v-if="request.group" class="text-sm text-slate-500 dark:text-slate-400 mb-1">
                申请加入：{{ request.group.name }} ({{ request.group.groupNumber }})
              </p>

              <!-- 申请消息 -->
              <p v-if="request.message" class="text-sm text-slate-600 dark:text-slate-300 mb-2">
                {{ request.message }}
              </p>

              <!-- 申请时间 -->
              <p class="text-xs text-slate-400 dark:text-slate-500">
                {{ formatTime(request.createdAt) }}
              </p>

              <!-- 操作按钮（仅群组申请且待处理状态） -->
              <div
                v-if="isGroupRequests && request.status === 'Pending'"
                class="flex gap-2 mt-3"
              >
                <button
                  @click="handleProcessRequest(request.id, false)"
                  :disabled="processingRequestId === request.id"
                  class="flex-1 px-4 py-2 text-sm bg-slate-100 dark:bg-slate-700 text-slate-700 dark:text-slate-300 rounded-lg hover:bg-slate-200 dark:hover:bg-slate-600 transition-colors disabled:opacity-50"
                >
                  拒绝
                </button>
                <button
                  @click="handleProcessRequest(request.id, true)"
                  :disabled="processingRequestId === request.id"
                  class="flex-1 px-4 py-2 text-sm bg-primary text-white rounded-lg hover:bg-primary/90 transition-colors disabled:opacity-50"
                >
                  接受
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- 空状态 -->
      <div v-else class="flex flex-col items-center justify-center py-20">
        <span class="material-symbols-outlined text-6xl text-slate-300 dark:text-slate-700 mb-4">
          inbox
        </span>
        <p class="text-slate-500 dark:text-slate-400 text-sm">
          {{ isGroupRequests ? '暂无入群申请' : '暂无申请记录' }}
        </p>
      </div>
    </div>
  </div>
</template>
