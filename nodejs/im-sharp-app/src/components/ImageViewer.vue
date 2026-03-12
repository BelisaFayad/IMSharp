<script setup lang="ts">
import { ref, watch } from 'vue'

interface Props {
  isOpen: boolean
  imageUrl: string
}

const props = defineProps<Props>()
const emit = defineEmits<{
  close: []
}>()

const scale = ref(1)
const isDragging = ref(false)
const startX = ref(0)
const startY = ref(0)
const translateX = ref(0)
const translateY = ref(0)

// 重置状态
watch(() => props.isOpen, (newVal) => {
  if (newVal) {
    scale.value = 1
    translateX.value = 0
    translateY.value = 0
  }
})

function handleClose() {
  emit('close')
}

function handleZoomIn() {
  scale.value = Math.min(scale.value + 0.25, 3)
}

function handleZoomOut() {
  scale.value = Math.max(scale.value - 0.25, 0.5)
}

function handleReset() {
  scale.value = 1
  translateX.value = 0
  translateY.value = 0
}

function handleMouseDown(e: MouseEvent) {
  isDragging.value = true
  startX.value = e.clientX - translateX.value
  startY.value = e.clientY - translateY.value
}

function handleMouseMove(e: MouseEvent) {
  if (!isDragging.value) return
  translateX.value = e.clientX - startX.value
  translateY.value = e.clientY - startY.value
}

function handleMouseUp() {
  isDragging.value = false
}

function handleWheel(e: WheelEvent) {
  e.preventDefault()
  if (e.deltaY < 0) {
    handleZoomIn()
  } else {
    handleZoomOut()
  }
}
</script>

<template>
  <Teleport to="body">
    <Transition name="fade">
      <div
        v-if="isOpen"
        class="fixed inset-0 z-[9999] flex items-center justify-center bg-black/90"
        @click="handleClose"
      >
        <!-- 工具栏 -->
        <div class="absolute top-4 right-4 flex items-center gap-2 z-10">
          <button
            @click.stop="handleZoomOut"
            class="w-10 h-10 flex items-center justify-center bg-white/10 hover:bg-white/20 rounded-full text-white transition-colors"
            title="缩小"
          >
            <span class="material-symbols-outlined">zoom_out</span>
          </button>
          <button
            @click.stop="handleZoomIn"
            class="w-10 h-10 flex items-center justify-center bg-white/10 hover:bg-white/20 rounded-full text-white transition-colors"
            title="放大"
          >
            <span class="material-symbols-outlined">zoom_in</span>
          </button>
          <button
            @click.stop="handleReset"
            class="w-10 h-10 flex items-center justify-center bg-white/10 hover:bg-white/20 rounded-full text-white transition-colors"
            title="重置"
          >
            <span class="material-symbols-outlined">refresh</span>
          </button>
          <button
            @click.stop="handleClose"
            class="w-10 h-10 flex items-center justify-center bg-white/10 hover:bg-white/20 rounded-full text-white transition-colors"
            title="关闭"
          >
            <span class="material-symbols-outlined">close</span>
          </button>
        </div>

        <!-- 缩放比例显示 -->
        <div class="absolute top-4 left-4 px-3 py-1 bg-white/10 rounded-full text-white text-sm">
          {{ Math.round(scale * 100) }}%
        </div>

        <!-- 图片容器 -->
        <div
          class="relative w-full h-full flex items-center justify-center overflow-hidden"
          @click.stop
          @wheel="handleWheel"
        >
          <img
            :src="imageUrl"
            :alt="'查看图片'"
            :style="{
              transform: `translate(${translateX}px, ${translateY}px) scale(${scale})`,
              cursor: isDragging ? 'grabbing' : 'grab'
            }"
            class="max-w-full max-h-full object-contain transition-transform select-none"
            draggable="false"
            @mousedown="handleMouseDown"
            @mousemove="handleMouseMove"
            @mouseup="handleMouseUp"
            @mouseleave="handleMouseUp"
          />
        </div>
      </div>
    </Transition>
  </Teleport>
</template>

<style scoped>
.fade-enter-active,
.fade-leave-active {
  transition: opacity 0.2s ease;
}

.fade-enter-from,
.fade-leave-to {
  opacity: 0;
}
</style>
