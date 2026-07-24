<template>
  <div class="table-scroll" tabindex="0" role="region" :aria-label="`${caption} table`">
    <table>
      <caption>
        {{ caption }}
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
  border: 1px solid var(--color-border-subtle);
  border-radius: 12px;
  box-shadow: 0 12px 30px var(--color-shadow-card);
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
  padding: 0;
  overflow: hidden;
  clip: rect(0, 0, 0, 0);
  white-space: nowrap;
  border: 0;
}

:deep(th),
:deep(td) {
  padding: 15px 16px;
  text-align: left;
  border-bottom: 1px solid var(--color-border-subtle);
}

:deep(th) {
  color: var(--color-text-secondary);
  font-size: 12px;
  letter-spacing: 0.04em;
  background: var(--color-surface-subtle);
}

:deep(tbody tr:last-child td) {
  border-bottom: 0;
}
</style>
