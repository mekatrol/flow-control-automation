<template>
  <div class="app-content" :inert="isWaiting || undefined">
    <RouterView />
  </div>
  <AppSpinnerOverlay automation="spinner-overlay" />
</template>

<script setup lang="ts">
import AppSpinnerOverlay from '@/components/AppSpinnerOverlay.vue';
import { useWait } from '@/composables/useWait';

const { isWaiting } = useWait();
</script>
