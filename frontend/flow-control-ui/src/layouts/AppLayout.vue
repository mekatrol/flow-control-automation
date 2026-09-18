<template>
  <div class="app-shell">
    <AppHeader />

    <div class="page-layout-content">
      <aside class="primary-aside">
        <slot name="primary-sidebar" />
      </aside>
      <main id="main-content" class="page-layout-main" tabindex="-1">
        <RouterView />
      </main>
      <aside class="secondary-aside">
        <slot name="secondary-sidebar" />
      </aside>
    </div>

    <footer class="footer">
      <slot name="footer" />
    </footer>
  </div>
</template>

<script setup lang="ts">
import AppHeader from '@/components/AppHeader.vue';
</script>

<style scoped lang="css">
.app-shell {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
}
</style>
