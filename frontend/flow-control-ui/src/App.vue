<template>
  <div class="app-shell">
    <a class="skip-link" href="#main-content">Skip to main content</a>
    <header class="app-header">
      <RouterLink class="brand" :to="{ name: 'flows' }">
        <span class="brand-mark" aria-hidden="true">FC</span>
        <span>
          <strong>Flow Control</strong>
          <small>Automation designer</small>
        </span>
      </RouterLink>

      <nav aria-label="Primary navigation">
        <RouterLink :to="{ name: 'flows' }">Flows</RouterLink>
        <ThemeSelector />
      </nav>
    </header>

    <main id="main-content" tabindex="-1">
      <RouterView />
    </main>
  </div>
</template>

<script setup lang="ts">
import ThemeSelector from '@/components/ThemeSelector.vue';
</script>

<style scoped>
.skip-link {
  position: fixed;
  z-index: 100;
  top: 8px;
  left: 8px;
  padding: 10px 14px;
  color: var(--color-text-on-strong);
  background: var(--color-header-background);
  border-radius: 7px;
  transform: translateY(-160%);
}

.skip-link:focus {
  transform: translateY(0);
}
</style>
