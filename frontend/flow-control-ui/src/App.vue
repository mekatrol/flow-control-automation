<template>
  <RouterView />
  <AppNotice
    id="welcome-notice"
    ref="welcomeNotice"
    automation="welcome-notice"
    title="Welcome to Flow Control"
    message="Welcome! Use Flow Control to create, configure, and monitor your automation flows."
    variant="info"
  />
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue';

import AppNotice from '@/components/AppNotice.vue';

const welcomeNotice = ref<InstanceType<typeof AppNotice>>();

onMounted(() => {
  welcomeNotice.value?.showModal();
});
</script>
