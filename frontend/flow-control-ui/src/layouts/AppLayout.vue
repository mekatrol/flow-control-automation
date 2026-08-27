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
          <AppThemeSelector />
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
  padding: var(--space-0) var(--space-16);
  color: var(--color-header-text);
  background: var(--color-header-background);
  border-bottom: var(--border-width-default) solid var(--color-header-border);
}

.brand {
  display: inline-flex;
  flex: none;
  gap: var(--space-5-5);
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
  font-size: var(--font-size-2xl);
  letter-spacing: 0.01em;
}

.brand small {
  margin-top: var(--space-0-5);
  color: var(--color-header-text-muted);
  font-size: var(--font-size-sm);
}

.app-header nav {
  display: flex;
  min-width: 0;
  overflow-x: auto;
  gap: var(--space-3-5);
  align-items: center;
}

.app-header nav a {
  flex: none;
  padding: var(--space-4) var(--space-6);
  color: var(--color-header-nav-text);
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-semibold);
  text-decoration: none;
  border-radius: var(--radius-lg);
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
  padding: var(--space-4-5) var(--space-6-5);
  color: var(--color-text-on-strong);
  background: var(--color-header-background);
  border-radius: var(--radius-md);
  transform: translateY(-160%);
}

.skip-link:focus {
  transform: translateY(0);
}

/* Mobile breakpoint (40rem): stacks page and navigation content for phone layouts. */
@media (max-width: 40rem) {
  .app-header {
    padding: var(--space-0) var(--space-9);
  }

  .brand small {
    display: none;
  }
}
</style>
