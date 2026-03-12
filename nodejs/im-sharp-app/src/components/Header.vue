<script setup lang="ts">
import { computed } from 'vue'
import { useUiStore } from '@/stores'
import { SignalRConnectionState } from '@/types'

interface Props {
  title: string
  showBack?: boolean
  showMenu?: boolean
  rightIcon?: string
  rightText?: string
  rightDisabled?: boolean
}

const props = withDefaults(defineProps<Props>(), {
  showBack: false,
  showMenu: false,
  rightIcon: '',
  rightText: '',
  rightDisabled: false,
})

const emit = defineEmits<{
  back: []
  menu: []
  right: []
}>()

const uiStore = useUiStore()

const connectionBanner = computed(() => {
  const state = uiStore.signalRState
  if (state === SignalRConnectionState.Reconnecting) {
    return { text: '正在重连...', class: 'bg-yellow-500' }
  }
  if (state === SignalRConnectionState.Disconnected) {
    return { text: '连接已断开', class: 'bg-red-500' }
  }
  if (state === SignalRConnectionState.Connecting) {
    return { text: '正在连接...', class: 'bg-yellow-500' }
  }
  return null
})
</script>

<template>
  <div>
    <!-- 连接状态横幅（仅异常时显示） -->
    <div
      v-if="connectionBanner"
      :class="connectionBanner.class"
      class="text-white text-xs text-center py-1"
    >
      {{ connectionBanner.text }}
    </div>

    <header class="sticky top-0 z-10 flex items-center justify-between px-4 py-3 bg-white dark:bg-slate-800 border-b border-slate-200 dark:border-slate-700 shrink-0">
    <!-- 左侧按钮 -->
    <slot name="left">
      <button
        v-if="showBack"
        @click="emit('back')"
        class="flex size-8 items-center justify-center rounded-full hover:bg-slate-100 dark:hover:bg-slate-700 transition-colors"
      >
        <span class="material-symbols-outlined text-xl text-slate-900 dark:text-white">arrow_back</span>
      </button>
      <div v-else class="size-8"></div>
    </slot>

    <!-- 标题 -->
    <slot name="title">
      <h1 class="text-lg font-bold tracking-tight flex-1 text-center text-slate-900 dark:text-white">
        {{ title }}
      </h1>
    </slot>

    <!-- 右侧按钮 -->
    <slot name="right">
      <button
        v-if="showMenu"
        @click="emit('menu')"
        class="flex size-8 items-center justify-center rounded-full hover:bg-slate-100 dark:hover:bg-slate-700 transition-colors"
      >
        <span class="material-symbols-outlined text-xl text-slate-900 dark:text-white">more_vert</span>
      </button>
      <button
        v-else-if="rightIcon || rightText"
        @click="emit('right')"
        :disabled="rightDisabled"
        class="flex size-8 items-center justify-center transition-colors disabled:opacity-50"
        :class="rightText ? 'text-primary font-bold text-sm' : 'rounded-full hover:bg-slate-100 dark:hover:bg-slate-700'"
      >
        <span v-if="rightIcon" class="material-symbols-outlined text-xl" :class="rightIcon.includes('primary') ? 'text-primary' : 'text-slate-900 dark:text-white'">{{ rightIcon.replace('primary:', '') }}</span>
        <span v-else-if="rightText">{{ rightText }}</span>
      </button>
      <div v-else class="size-8"></div>
    </slot>
  </header>
  </div>
</template>
