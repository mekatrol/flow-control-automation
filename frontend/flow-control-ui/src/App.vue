<template>
  <div class="app-shell">
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
      </nav>
    </header>

    <main>
      <RouterView />
    </main>
  </div>
</template>
