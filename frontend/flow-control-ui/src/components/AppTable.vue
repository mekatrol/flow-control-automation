<template>
  <div class="table-scroll" tabindex="0" role="region" :aria-label="`${caption} table`">
    <table>
      <caption>
        {{
          caption
        }}
      </caption>
      <thead>
        <slot name="head"></slot>
      </thead>
      <tbody>
        <slot name="body"></slot>
      </tbody>
    </table>
  </div>
</template>

<script setup lang="ts">
defineProps<{
  caption: string;
}>();
</script>

<style scoped>
.table-scroll {
  max-width: 100%;
  overflow-x: auto;
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-subtle);
  border-radius: var(--radius-2xl);
  box-shadow: var(--shadow-card);
}

table {
  width: 100%;
  min-width: 760px;
  border-collapse: collapse;
}

caption {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: var(--space-0);
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: var(--border-width-none);
}

:deep(th),
:deep(td) {
  padding: var(--space-7) var(--space-8);
  text-align: left;
  border-bottom: var(--border-width-default) solid var(--color-border-subtle);
}

:deep(th) {
  color: var(--color-text-secondary);
  font-size: var(--font-size-md);
  letter-spacing: 0.04em;
  background: var(--color-surface-subtle);
}

:deep(tbody tr:last-child td) {
  border-bottom: var(--border-width-none);
}
</style>
