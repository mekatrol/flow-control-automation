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

<style scoped lang="css">
.app-shell {
  display: flex;
  flex-direction: column;
  min-height: 100vh;
}

.app-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  min-height: 72px;
  padding: 0 32px;
  color: var(--color-header-text);
  background: var(--color-header-background);
  border-bottom: 1px solid var(--color-header-border);
}

.brand {
  display: inline-flex;
  flex: none;
  gap: 12px;
  align-items: center;
  color: inherit;
  text-decoration: none;
}

.brand-logo {
  display: block;
  width: auto;
  height: 38px;
}

.brand strong,
.brand small {
  display: block;
}

.brand strong {
  font-size: 15px;
  letter-spacing: 0.01em;
}

.brand small {
  margin-top: 2px;
  color: var(--color-header-text-muted);
  font-size: 11px;
}

.app-header nav {
  display: flex;
  min-width: 0;
  overflow-x: auto;
  gap: 8px;
  align-items: center;
}

.app-header nav a {
  flex: none;
  padding: 9px 13px;
  color: var(--color-header-nav-text);
  font-size: 14px;
  font-weight: 650;
  text-decoration: none;
  border-radius: 8px;
}

.app-header nav a:hover,
.app-header nav a.router-link-active {
  color: var(--color-text-on-strong);
  background: var(--color-header-nav-background-active);
}

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

@media (max-width: 640px) {
  .app-header {
    padding: 0 18px;
  }

  .brand small {
    display: none;
  }
}
</style>
