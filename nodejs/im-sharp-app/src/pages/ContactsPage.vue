<script setup lang="ts">
import { ref, onMounted, computed } from 'vue'
import { useRouter } from 'vue-router'
import { useContactsStore, useGroupsStore } from '@/stores'
import { Header, SearchInput, TabBar, ContactListItem, BottomNav, Badge } from '@/components'

const router = useRouter()
const contactsStore = useContactsStore()
const groupsStore = useGroupsStore()
const searchQuery = ref('')
const view = ref<'friends' | 'groups'>('friends')

const tabs = [
  { id: 'friends', label: '好友' },
  { id: 'groups', label: '群组' },
]

const pendingRequestsCount = computed(() =>
  contactsStore.receivedRequests.filter(r => r.status === 'Pending').length
)

const pendingJoinRequestsCount = computed(() => {
  return groupsStore.totalPendingRequestsCount
})

const filteredFriends = computed(() => {
  if (!searchQuery.value) return contactsStore.friends
  const query = searchQuery.value.toLowerCase()
  return contactsStore.friends.filter(
    f => f.displayName.toLowerCase().includes(query) || f.username.toLowerCase().includes(query)
  )
})

const filteredGroups = computed(() => {
  if (!searchQuery.value) return groupsStore.groups
  const query = searchQuery.value.toLowerCase()
  return groupsStore.groups.filter(g => g.name.toLowerCase().includes(query))
})

onMounted(async () => {
  await contactsStore.loadFriends()
  await contactsStore.loadReceivedRequests()
  await groupsStore.loadGroups()
  // 加载所有待处理的入群申请
  await groupsStore.loadAllPendingJoinRequests()
})

function handleAddClick() {
  if (view.value === 'friends') {
    router.push('/contacts/add')
  } else {
    router.push('/groups')
  }
}

function handleContactClick(contactId: string) {
  router.push(`/contacts/${contactId}`)
}

function handleGroupClick(groupId: string) {
  router.push(`/groups/${groupId}/chat`)
}

function handleRequestsClick() {
  router.push('/contacts/requests')
}

function handleJoinRequestsClick() {
  // 如果只有一个群组有待处理申请，直接跳转到该群组的申请页面
  const groupsWithRequests = Array.from(groupsStore.pendingJoinRequests.entries())
    .filter(([_, requests]) => requests.some(r => r.status === 'Pending'))

  if (groupsWithRequests.length === 1) {
    const firstGroup = groupsWithRequests[0]
    if (firstGroup) {
      const [groupId] = firstGroup
      router.push(`/groups/${groupId}/join-requests`)
    }
  } else {
    // 多个群组有申请，跳转到第一个
    if (groupsWithRequests.length > 0) {
      const firstGroup = groupsWithRequests[0]
      if (firstGroup) {
        const [groupId] = firstGroup
        router.push(`/groups/${groupId}/join-requests`)
      }
    }
  }
}
</script>

<template>
  <div class="flex flex-col h-screen overflow-hidden">
    <Header title="通讯录">
      <template #right>
        <button
          @click="handleAddClick"
          class="flex items-center gap-4 justify-end size-8 transition-all"
        >
          <span class="material-symbols-outlined text-primary text-xl">
            {{ view === 'friends' ? 'person_add' : 'group_add' }}
          </span>
        </button>
      </template>
    </Header>

    <div class="px-4 py-3 bg-white dark:bg-slate-900">
      <SearchInput
        v-model="searchQuery"
        :placeholder="view === 'friends' ? '搜索好友...' : '搜索群组...'"
      />
    </div>

    <TabBar v-model="view" :tabs="tabs" />

    <div class="flex-1 overflow-y-auto hide-scrollbar bg-slate-50 dark:bg-slate-900 pb-20">
      <!-- 好友请求入口 -->
      <div
        v-if="view === 'friends' && pendingRequestsCount > 0"
        @click="handleRequestsClick"
        class="flex items-center gap-3 px-4 py-3 bg-white dark:bg-slate-900 border-b border-slate-100 dark:border-slate-800 cursor-pointer hover:bg-slate-50 dark:hover:bg-slate-800/50 transition-colors"
      >
        <div class="size-12 rounded-full bg-primary/10 flex items-center justify-center">
          <span class="material-symbols-outlined text-primary text-2xl">person_add</span>
        </div>
        <div class="flex-1">
          <p class="font-semibold text-slate-900 dark:text-white">好友请求</p>
          <p class="text-xs text-slate-500 dark:text-slate-400">{{ pendingRequestsCount }} 条待处理</p>
        </div>
        <Badge :count="pendingRequestsCount" />
        <span class="material-symbols-outlined text-slate-400 text-sm">chevron_right</span>
      </div>

      <!-- 好友列表 -->
      <template v-if="view === 'friends'">
        <ContactListItem
          v-for="friend in filteredFriends"
          :key="friend.id"
          :avatar="friend.avatar || undefined"
          :name="friend.displayName || friend.username"
          :status="friend.remark || ''"
          :online="friend.isOnline"
          @click="handleContactClick(friend.id)"
        />
        <p v-if="filteredFriends.length === 0" class="text-slate-500 dark:text-slate-400 text-center py-8 text-sm">
          {{ searchQuery ? '未找到匹配的好友' : '暂无好友' }}
        </p>
      </template>

      <!-- 群组列表 -->
      <template v-else>
        <!-- 入群申请入口（群主） -->
        <div
          v-if="pendingJoinRequestsCount > 0"
          @click="handleJoinRequestsClick"
          class="flex items-center gap-3 px-4 py-3 bg-white dark:bg-slate-900 border-b border-slate-100 dark:border-slate-800 cursor-pointer hover:bg-slate-50 dark:hover:bg-slate-800/50 transition-colors"
        >
          <div class="size-12 rounded-full bg-primary/10 flex items-center justify-center">
            <span class="material-symbols-outlined text-primary text-2xl">group_add</span>
          </div>
          <div class="flex-1">
            <p class="font-semibold text-slate-900 dark:text-white">入群申请</p>
            <p class="text-xs text-slate-500 dark:text-slate-400">{{ pendingJoinRequestsCount }} 条待处理</p>
          </div>
          <Badge :count="pendingJoinRequestsCount" />
          <span class="material-symbols-outlined text-slate-400 text-sm">chevron_right</span>
        </div>

        <ContactListItem
          v-for="group in filteredGroups"
          :key="group.id"
          :avatar="group.avatar || undefined"
          :name="group.name"
          :status="`${group.memberCount} 人`"
          :is-group="true"
          @click="handleGroupClick(group.id)"
        />
        <p v-if="filteredGroups.length === 0" class="text-slate-500 dark:text-slate-400 text-center py-8 text-sm">
          {{ searchQuery ? '未找到匹配的群组' : '暂无群组' }}
        </p>
      </template>
    </div>

    <BottomNav />
  </div>
</template>
