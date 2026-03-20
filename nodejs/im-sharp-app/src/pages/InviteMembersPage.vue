<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useGroupsStore, useContactsStore, useUiStore } from '@/stores'
import { Input, Button, LoadingSpinner, Header } from '@/components'

const route = useRoute()
const router = useRouter()
const groupsStore = useGroupsStore()
const contactsStore = useContactsStore()
const uiStore = useUiStore()

const groupId = route.params.id as string
const isLoading = ref(true)
const selectedMembers = ref<Set<string>>(new Set())
const isInviting = ref(false)
const searchQuery = ref('')

// 获取群组信息
const group = computed(() => {
  return groupsStore.groups.find(g => g.id === groupId)
})

// 获取群组成员
const members = computed(() => {
  return groupsStore.groupMembers.get(groupId) || []
})

// 获取群组成员 ID 集合
const memberIds = computed(() => {
  return new Set(members.value.map(m => m.userId))
})

// 可邀请的好友列表（排除已在群组中的好友）
const availableFriends = computed(() => {
  return contactsStore.friends.filter(friend => !memberIds.value.has(friend.id))
})

// 过滤好友列表
const filteredFriends = computed(() => {
  if (!searchQuery.value.trim()) {
    return availableFriends.value
  }
  const query = searchQuery.value.toLowerCase()
  return availableFriends.value.filter(friend =>
    (friend.displayName || friend.username).toLowerCase().includes(query)
  )
})

// 已选成员列表
const selectedMembersList = computed(() => {
  return availableFriends.value.filter(friend => selectedMembers.value.has(friend.id))
})

onMounted(async () => {
  try {
    // 加载群组信息
    if (!group.value) {
      await groupsStore.loadGroups()
    }

    // 加载群组成员
    await groupsStore.loadGroupMembers(groupId)

    // 加载好友列表
    if (contactsStore.friends.length === 0) {
      await contactsStore.loadFriends()
    }
  } catch (error) {
    console.error('加载数据失败:', error)
    uiStore.showToast('加载数据失败', 'error')
  } finally {
    isLoading.value = false
  }
})

// 切换成员选择
function toggleMember(userId: string) {
  if (selectedMembers.value.has(userId)) {
    selectedMembers.value.delete(userId)
  } else {
    selectedMembers.value.add(userId)
  }
}

// 移除已选成员
function removeMember(userId: string) {
  selectedMembers.value.delete(userId)
}

// 邀请成员
async function handleInviteMembers() {
  if (selectedMembers.value.size === 0) {
    uiStore.showToast('请至少选择一个成员', 'error')
    return
  }

  isInviting.value = true

  try {
    await groupsStore.inviteMembers(groupId, Array.from(selectedMembers.value))
    uiStore.showToast('邀请成功', 'success')
    router.push(`/groups/${groupId}`)
  } catch (error) {
    console.error('邀请成员失败:', error)
    uiStore.showToast('邀请成员失败', 'error')
  } finally {
    isInviting.value = false
  }
}
</script>

<template>
  <div class="flex flex-col h-screen bg-slate-50 dark:bg-slate-900">
    <Header title="邀请成员" :show-back="true" @back="router.push(`/groups/${groupId}`)" />

    <!-- 加载中 -->
    <div v-if="isLoading" class="flex-1 flex items-center justify-center">
      <LoadingSpinner />
    </div>

    <!-- 内容区 -->
    <main v-else class="flex-1 overflow-y-auto">
      <div class="p-4 space-y-4">
        <!-- 已选成员 -->
        <div v-if="selectedMembersList.length > 0" class="bg-white dark:bg-slate-800 rounded-lg p-4">
          <h2 class="text-sm font-semibold text-slate-700 dark:text-slate-300 mb-3">
            已选成员 ({{ selectedMembersList.length }})
          </h2>
          <div class="flex flex-wrap gap-2">
            <div
              v-for="member in selectedMembersList"
              :key="member.id"
              class="flex items-center gap-2 bg-slate-100 dark:bg-slate-700 rounded-full pl-1 pr-3 py-1"
            >
              <div
                v-if="member.avatar"
                class="size-6 rounded-full bg-cover bg-center"
                :style="{ backgroundImage: `url(${member.avatar})` }"
              ></div>
              <div
                v-else
                class="size-6 rounded-full bg-slate-200 dark:bg-slate-600 flex items-center justify-center"
              >
                <span class="material-symbols-outlined text-slate-400 text-sm">person</span>
              </div>
              <span class="text-sm text-slate-900 dark:text-white">
                {{ member.displayName || member.username }}
              </span>
              <button
                @click="removeMember(member.id)"
                class="text-slate-400 hover:text-slate-600 dark:hover:text-slate-300"
              >
                <span class="material-symbols-outlined text-sm">close</span>
              </button>
            </div>
          </div>
        </div>

        <!-- 选择成员 -->
        <div class="bg-white dark:bg-slate-800 rounded-lg p-4">
          <h2 class="text-sm font-semibold text-slate-700 dark:text-slate-300 mb-3">
            选择好友
          </h2>

          <!-- 搜索框 -->
          <div class="mb-3">
            <Input
              v-model="searchQuery"
              placeholder="搜索好友..."
              icon="search"
            />
          </div>

          <!-- 好友列表 -->
          <div class="space-y-2 max-h-96 overflow-y-auto">
            <div
              v-for="friend in filteredFriends"
              :key="friend.id"
              @click="toggleMember(friend.id)"
              class="flex items-center gap-3 p-3 rounded-lg hover:bg-slate-50 dark:hover:bg-slate-700 cursor-pointer transition-colors"
            >
              <div
                v-if="friend.avatar"
                class="size-10 rounded-full bg-cover bg-center shrink-0"
                :style="{ backgroundImage: `url(${friend.avatar})` }"
              ></div>
              <div
                v-else
                class="size-10 rounded-full bg-slate-200 dark:bg-slate-700 flex items-center justify-center shrink-0"
              >
                <span class="material-symbols-outlined text-slate-400 text-xl">person</span>
              </div>

              <div class="flex-1 min-w-0">
                <p class="text-sm font-medium text-slate-900 dark:text-white truncate">
                  {{ friend.displayName || friend.username }}
                </p>
              </div>

              <div
                :class="[
                  'size-5 rounded border-2 flex items-center justify-center shrink-0',
                  selectedMembers.has(friend.id)
                    ? 'bg-primary border-primary'
                    : 'border-slate-300 dark:border-slate-600'
                ]"
              >
                <span
                  v-if="selectedMembers.has(friend.id)"
                  class="material-symbols-outlined text-white text-sm"
                >
                  check
                </span>
              </div>
            </div>

            <p
              v-if="filteredFriends.length === 0"
              class="text-center text-sm text-slate-500 dark:text-slate-400 py-8"
            >
              {{ searchQuery ? '未找到匹配的好友' : availableFriends.length === 0 ? '所有好友都已在群组中' : '暂无好友' }}
            </p>
          </div>
        </div>
      </div>
    </main>

    <!-- 底部按钮 -->
    <footer class="bg-white dark:bg-slate-800 border-t border-slate-200 dark:border-slate-800 p-4">
      <Button
        @click="handleInviteMembers"
        :disabled="isInviting || selectedMembers.size === 0"
        class="w-full"
      >
        <span v-if="isInviting" class="flex items-center justify-center gap-2">
          <span class="material-symbols-outlined animate-spin">progress_activity</span>
          邀请中...
        </span>
        <span v-else>邀请 ({{ selectedMembers.size }})</span>
      </Button>
    </footer>
  </div>
</template>
