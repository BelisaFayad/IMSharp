<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useGroupsStore, useAuthStore, useUiStore } from '@/stores'
import { LoadingSpinner, ConfirmationModal, Header } from '@/components'
import type { GroupMember } from '@/types'

const route = useRoute()
const router = useRouter()
const groupsStore = useGroupsStore()
const authStore = useAuthStore()
const uiStore = useUiStore()

const groupId = route.params.id as string
const isLoading = ref(true)
const showRemoveDialog = ref(false)
const selectedMember = ref<GroupMember | null>(null)
const isRemoving = ref(false)

// 获取群组信息
const group = computed(() => {
  return groupsStore.groups.find(g => g.id === groupId)
})

// 获取群组成员
const members = computed(() => {
  return groupsStore.groupMembers.get(groupId) || []
})

// 当前用户的成员信息
const currentMember = computed(() => {
  return members.value.find(m => m.userId === authStore.user?.id)
})

// 是否是群主
const isOwner = computed(() => {
  return group.value?.ownerId === authStore.user?.id
})

// 是否是管理员
const isAdmin = computed(() => {
  return currentMember.value?.role === 'Admin' || isOwner.value
})

// 按角色排序的成员列表
const sortedMembers = computed(() => {
  const roleOrder = { Owner: 0, Admin: 1, Member: 2 }
  return [...members.value].sort((a, b) => {
    const roleA = a.role || 'Member'
    const roleB = b.role || 'Member'
    return roleOrder[roleA] - roleOrder[roleB]
  })
})

onMounted(async () => {
  try {
    // 加载群组信息
    if (!group.value) {
      await groupsStore.loadGroups()
    }

    // 加载群组成员
    await groupsStore.loadGroupMembers(groupId)
  } catch (error) {
    console.error('加载群组成员失败:', error)
    uiStore.showToast('加载群组成员失败', 'error')
  } finally {
    isLoading.value = false
  }
})

// 获取角色显示文本
function getRoleText(role: string) {
  const roleMap: Record<string, string> = {
    Owner: '群主',
    Admin: '管理员',
    Member: '成员'
  }
  return roleMap[role] || '成员'
}

// 获取角色颜色
function getRoleColor(role: string) {
  const colorMap: Record<string, string> = {
    Owner: 'text-yellow-500',
    Admin: 'text-blue-500',
    Member: 'text-slate-500'
  }
  return colorMap[role] || 'text-slate-500'
}

// 点击成员
function handleMemberClick(member: GroupMember) {
  // 只有群主和管理员可以操作成员
  if (!isAdmin.value) return

  // 不能操作自己
  if (member.userId === authStore.user?.id) return

  // 群主不能被移除
  if (member.userId === group.value?.ownerId) return

  selectedMember.value = member
  showRemoveDialog.value = true
}

// 移除成员
async function handleRemoveMember() {
  if (!selectedMember.value) return

  isRemoving.value = true

  try {
    await groupsStore.removeMember(groupId, selectedMember.value.userId)
    uiStore.showToast('成员已移除', 'success')
  } catch (error) {
    console.error('移除成员失败:', error)
    uiStore.showToast('移除成员失败', 'error')
  } finally {
    isRemoving.value = false
    showRemoveDialog.value = false
    selectedMember.value = null
  }
}

// 邀请成员
function handleInviteMembers() {
  router.push(`/groups/${groupId}/invite`)
}
</script>

<template>
  <div class="flex flex-col h-screen bg-slate-50 dark:bg-slate-900">
    <Header title="群组成员" :show-back="true" @back="router.back()">
      <template #right>
        <button
          v-if="isAdmin"
          @click="handleInviteMembers"
          class="flex items-center justify-center p-1 hover:bg-slate-100 dark:hover:bg-slate-700 rounded-full transition-colors"
        >
          <span class="material-symbols-outlined text-xl text-slate-900 dark:text-white">person_add</span>
        </button>
        <div v-else class="size-8"></div>
      </template>
    </Header>

    <!-- 加载中 -->
    <div v-if="isLoading" class="flex-1 flex items-center justify-center">
      <LoadingSpinner />
    </div>

    <!-- 成员列表 -->
    <main v-else class="flex-1 overflow-y-auto">
      <div>
        <div class="p-4">
          <p class="text-sm text-slate-500 dark:text-slate-400">
            共 {{ members.length }} 人
          </p>
        </div>

        <div class="bg-white dark:bg-slate-800 divide-y divide-slate-200 dark:divide-slate-700">
          <div
            v-for="member in sortedMembers"
            :key="member.id"
            @click="handleMemberClick(member)"
            :class="[
              'flex items-center gap-3 p-4 transition-colors',
              isAdmin && member.userId !== authStore.user?.id && member.userId !== group?.ownerId
                ? 'hover:bg-slate-50 dark:hover:bg-slate-700 cursor-pointer'
                : ''
            ]"
          >
            <div
              v-if="member.user?.avatar"
              class="size-12 rounded-full bg-cover bg-center shrink-0"
              :style="{ backgroundImage: `url(${member.user.avatar})` }"
            ></div>
            <div
              v-else
              class="size-12 rounded-full bg-slate-200 dark:bg-slate-700 flex items-center justify-center shrink-0"
            >
              <span class="material-symbols-outlined text-slate-400 text-xl">person</span>
            </div>

            <div class="flex-1 min-w-0">
              <div class="flex items-center gap-2">
                <p class="text-sm font-medium text-slate-900 dark:text-white truncate">
                  {{ member.user?.displayName || member.user?.username }}
                </p>
                <span
                  v-if="member.role && member.role !== 'Member'"
                  :class="['text-xs font-medium', getRoleColor(member.role)]"
                >
                  {{ getRoleText(member.role) }}
                </span>
              </div>
              <p
                v-if="member.userId === authStore.user?.id"
                class="text-xs text-slate-500 dark:text-slate-400"
              >
                (我)
              </p>
            </div>

            <div
              v-if="member.user?.isOnline"
              class="size-2 rounded-full bg-emerald-500 shrink-0"
            ></div>
          </div>
        </div>
      </div>
    </main>

    <!-- 移除成员确认对话框 -->
    <ConfirmationModal
      :is-open="showRemoveDialog"
      title="移除成员"
      :message="`确定要将 ${selectedMember?.user?.displayName || selectedMember?.user?.username} 移出群组吗？`"
      confirm-text="移除"
      cancel-text="取消"
      :is-loading="isRemoving"
      @confirm="handleRemoveMember"
      @cancel="showRemoveDialog = false"
    />
  </div>
</template>
