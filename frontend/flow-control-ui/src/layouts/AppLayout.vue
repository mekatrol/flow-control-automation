<template>
  <div class="app-shell">
    <a class="skip-link" href="#main-content">Skip to main content</a>
    <header class="app-header header">
      <slot name="header">
        <RouterLink class="brand" :to="{ name: 'flows' }">
          <img
            class="brand-logo"
            src="@/assets/icons/mekatrol-logo.svg"
            alt=""
            aria-hidden="true"
          />
          <span>
            <strong>Flow Control</strong>
            <small>Automation designer</small>
          </span>
        </RouterLink>

        <nav aria-label="Primary navigation">
          <RouterLink :to="{ name: 'flows' }">Flows</RouterLink>
          <RouterLink to="/points">Points</RouterLink>
          <RouterLink to="/point-groups">Point groups</RouterLink>
          <RouterLink to="/point-sources">Point sources</RouterLink>
          <RouterLink to="/controller-templates">Controllers</RouterLink>
          <RouterLink to="/credentials">Credentials</RouterLink>
          <AppThemeSelector automation="theme-selector" />
        </nav>
      </slot>
    </header>

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
import AppThemeSelector from '@/components/AppThemeSelector.vue';
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
