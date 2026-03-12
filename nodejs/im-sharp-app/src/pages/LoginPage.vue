<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '@/stores'

const route = useRoute()
const router = useRouter()
const authStore = useAuthStore()

const errorMessage = ref('')

onMounted(async () => {
  // 已登录则直接跳转
  if (authStore.isAuthenticated) {
    router.push('/')
    return
  }

  const token = route.query.oauth_token as string | undefined
  if (!token) {
    errorMessage.value = '缺少 oauth_token 参数'
    return
  }
  try {
    await authStore.login(token)
    router.push('/')
  } catch (error: any) {
    errorMessage.value = error.response?.data?.message || '登录失败，请重试'
  }
})
</script>

<template>
  <div class="min-h-screen flex items-center justify-center bg-slate-50 dark:bg-slate-900 p-4">
    <div class="flex flex-col items-center gap-6">
      <div class="inline-flex items-center justify-center w-20 h-20 rounded-2xl bg-primary/10">
        <span class="material-symbols-outlined text-primary text-5xl">chat_bubble</span>
      </div>
      <h1 class="text-2xl font-bold text-slate-900 dark:text-white">IMSharp</h1>

      <!-- 登录中 -->
      <template v-if="!errorMessage">
        <div class="flex items-center gap-3 text-slate-500 dark:text-slate-400">
          <span class="material-symbols-outlined animate-spin text-primary">progress_activity</span>
          <span class="text-sm">正在登录...</span>
        </div>
      </template>

      <!-- 错误 -->
      <template v-else>
        <p class="text-sm text-red-500">{{ errorMessage }}</p>
      </template>
    </div>
  </div>
</template>
