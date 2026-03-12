<script setup lang="ts">
import { ref, computed, onMounted } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useGroupsStore, useAuthStore, useUiStore } from '@/stores'
import { Avatar, Button, ConfirmationModal, LoadingSpinner, Header } from '@/components'

const route = useRoute()
const router = useRouter()
const groupsStore = useGroupsStore()
const authStore = useAuthStore()
const uiStore = useUiStore()

const groupId = route.params.id as string
const isConfirmOpen = ref(false)
const isLoading = ref(true)
const isLeaving = ref(false)

// 获取群组信息
const group = computed(() => {
  return groupsStore.groups.find(g => g.id === groupId)
})

// 获取群组成员
const members = computed(() => {
  return groupsStore.groupMembers.get(groupId) || []
})

// 显示的部分成员（最多显示 4 个）
const displayMembers = computed(() => {
  return members.value.slice(0, 4)
})

// 是否是群主
const isOwner = computed(() => {
  return group.value?.ownerId === authStore.user?.id
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
    console.error('加载群组详情失败:', error)
    uiStore.showToast('加载群组详情失败', 'error')
  } finally {
    isLoading.value = false
  }
})

function handleSendMessage() {
  router.push(`/groups/${groupId}/chat`)
}

function handleInviteMember() {
  router.push(`/groups/${groupId}/invite`)
}

function handleViewAllMembers() {
  router.push(`/groups/${groupId}/members`)
}

async function handleDeleteAndExit() {
  isLeaving.value = true

  try {
    await groupsStore.leaveGroup(groupId)
    uiStore.showToast('已退出群组', 'success')
    router.push('/groups')
  } catch (error) {
    console.error('退出群组失败:', error)
    uiStore.showToast('退出群组失败', 'error')
  } finally {
    isLeaving.value = false
    isConfirmOpen.value = false
  }
}
</script>

<template>
  <div class="min-h-screen w-full bg-slate-50 dark:bg-slate-900 pb-10">
    <Header title="群组详情" :show-back="true" @back="router.back()" />

    <!-- 加载中 -->
    <div v-if="isLoading" class="flex items-center justify-center py-20">
      <LoadingSpinner />
    </div>

    <div v-else>
      <!-- 群组信息 -->
      <div v-if="group" class="bg-white dark:bg-slate-800 p-6">
        <div class="flex flex-col items-center">
          <div
            v-if="group.avatar"
            class="size-20 rounded-lg bg-cover bg-center"
            :style="{ backgroundImage: `url(${group.avatar})` }"
          ></div>
          <div
            v-else
            class="size-20 rounded-lg bg-gradient-to-br from-primary to-orange-600 flex items-center justify-center shadow-sm"
          >
            <span class="material-symbols-outlined text-white text-4xl">groups</span>
          </div>
          <h2 class="text-xl font-bold text-slate-900 dark:text-white mt-4">
            {{ group.name }}
          </h2>
          <p class="text-sm text-slate-500 dark:text-slate-400 mt-1">
            {{ group.memberCount }} 人
          </p>
        </div>

        <!-- 操作按钮 -->
        <div class="flex gap-3 mt-6">
          <Button @click="handleSendMessage" class="flex-1">
            发消息
          </Button>
          <Button @click="handleInviteMember" variant="secondary" class="flex-1">
            邀请
          </Button>
        </div>
      </div>

      <!-- 群组成员 -->
      <div class="bg-white dark:bg-slate-800 p-4 mt-4">
        <div class="grid grid-cols-5 gap-4">
          <!-- 成员头像 -->
          <div v-for="member in displayMembers" :key="member.id" class="flex flex-col items-center gap-2">
            <div
              v-if="member.user?.avatar"
              class="w-full aspect-square rounded-xl bg-cover bg-center"
              :style="{ backgroundImage: `url(${member.user.avatar})` }"
            ></div>
            <div
              v-else
              class="w-full aspect-square rounded-xl bg-slate-200 dark:bg-slate-700 flex items-center justify-center"
            >
              <span class="material-symbols-outlined text-slate-400">person</span>
            </div>
            <p class="text-[10px] text-slate-500 dark:text-slate-400 truncate w-full text-center">
              {{ member.user?.displayName || member.user?.username }}
            </p>
          </div>

          <!-- 邀请按钮 -->
          <div class="flex flex-col items-center gap-2">
            <button
              @click="handleInviteMember"
              class="w-full aspect-square border-2 border-dashed border-slate-300 dark:border-slate-700 rounded-xl flex items-center justify-center text-slate-400 hover:border-primary hover:text-primary transition-colors cursor-pointer"
            >
              <span class="material-symbols-outlined">add</span>
            </button>
            <p class="text-[10px] text-slate-500 dark:text-slate-400">邀请</p>
          </div>
        </div>
        <button
          @click="handleViewAllMembers"
          class="w-full mt-4 pt-2 border-t border-slate-100 dark:border-slate-700 flex items-center justify-center gap-1 text-slate-400 hover:text-primary transition-colors py-2"
        >
          <span class="text-sm font-medium">查看全部群成员</span>
          <span class="material-symbols-outlined text-sm">chevron_right</span>
        </button>
      </div>

      <!-- 群组信息 -->
      <div class="mt-4 bg-white dark:bg-slate-800">
        <div class="px-4 py-4 border-b border-slate-100 dark:border-slate-700 flex justify-between items-center">
          <span class="text-slate-900 dark:text-white">群聊名称</span>
          <span class="text-slate-500 dark:text-slate-400">{{ group?.name }}</span>
        </div>
        <div class="px-4 py-4 border-b border-slate-100 dark:border-slate-700 flex justify-between items-center">
          <span class="text-slate-900 dark:text-white">群号</span>
          <span class="text-slate-500 dark:text-slate-400">{{ group?.groupNumber }}</span>
        </div>
        <div v-if="group?.description" class="px-4 py-4 border-b border-slate-100 dark:border-slate-700">
          <span class="text-slate-900 dark:text-white block mb-2">群简介</span>
          <span class="text-slate-500 dark:text-slate-400 text-sm">{{ group.description }}</span>
        </div>
      </div>

      <!-- 退出群组按钮 -->
      <div class="mt-8 px-4">
        <Button variant="danger" full-width @click="isConfirmOpen = true">
          {{ isOwner ? '解散群组' : '退出群组' }}
        </Button>
      </div>
    </div>

    <ConfirmationModal
      :is-open="isConfirmOpen"
      :title="isOwner ? '确认解散' : '确认退出'"
      :message="isOwner ? '确定要解散该群组吗？此操作不可恢复。' : '确定要退出该群组吗？'"
      variant="danger"
      :is-loading="isLeaving"
      @confirm="handleDeleteAndExit"
      @cancel="isConfirmOpen = false"
    />
  </div>
</template>
