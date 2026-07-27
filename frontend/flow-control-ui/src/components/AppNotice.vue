<template>
  <AppDialog
    :id="id"
    ref="dialog"
    class="app-error-dialog"
    :content-label="title"
    :automation="props.automation"
    :dismissible="dismissible"
  >
    <article
      :class="['notice', `notice--${variant}`]"
      :aria-labelledby="titleId"
      :aria-describedby="contentId"
      :role="variant === 'error' ? 'alert' : 'status'"
    >
      <header class="notice-header">
        <slot name="header" :title="title" :variant="variant" :icon="variantIcon">
          <AppSvg
            class="notice-icon"
            :src="variantIcon"
            automation="notice-variant-icon"
            :size="28"
          />
          <h2 :id="titleId">{{ title }}</h2>
        </slot>
      </header>

      <div :id="contentId" ref="content" v-bind="automation('content')" class="notice-content">
        <slot name="content" :message="message">
          <p>{{ message }}</p>
        </slot>
      </div>

      <footer class="notice-footer">
        <slot name="footer" :close="close" :copy="copyToClipboard" :copied="copyState === 'copied'">
          <p class="copy-status" aria-live="polite">{{ copyStatus }}</p>
          <AppButton
            v-if="copyable"
            v-bind="automation('copy')"
            :text="copyLabel"
            :icon="copyIcon"
            @click="copyToClipboard"
          />
          <AppButton v-bind="automation('close')" :text="closeLabel" @click="close" />
        </slot>
      </footer>
    </article>
  </AppDialog>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';

import copyIcon from '@/assets/icons/copy-icon.svg';
import debugIcon from '@/assets/icons/debug-notice-icon.svg';
import errorIcon from '@/assets/icons/error-notice-icon.svg';
import infoIcon from '@/assets/icons/info-notice-icon.svg';
import warningIcon from '@/assets/icons/warning-notice-icon.svg';
import AppButton from '@/components/AppButton.vue';
import AppDialog from '@/components/AppDialog.vue';
import AppSvg from '@/components/AppSvg.vue';
import { useAutomation } from '@/composables/useAutomation';

export type AppNoticeVariant = 'info' | 'debug' | 'warning' | 'error';

const props = withDefaults(
  defineProps<{
    id: string;
    automation: string;
    title: string;
    message: string;
    variant?: AppNoticeVariant;
    copyable?: boolean;
    copyLabel?: string;
    closeLabel?: string;
    dismissible?: boolean;
  }>(),
  {
    variant: 'error',
    copyable: true,
    copyLabel: 'Copy details',
    closeLabel: 'Close',
    dismissible: true
  }
);

const icons: Record<AppNoticeVariant, string> = {
  info: infoIcon,
  debug: debugIcon,
  warning: warningIcon,
  error: errorIcon
};

const dialog = ref<InstanceType<typeof AppDialog>>();
const content = ref<HTMLElement>();
const copyState = ref<'idle' | 'copied' | 'failed'>('idle');
const automation = useAutomation(props.automation);
const titleId = `${props.id}-title`;
const contentId = `${props.id}-content`;
const variantIcon = computed(() => icons[props.variant]);
const copyStatus = computed(() => {
  if (copyState.value === 'copied') return 'Details copied to clipboard.';
  if (copyState.value === 'failed') return 'Unable to copy details.';
  return '';
});

const close = (): void => {
  dialog.value?.close();
};

const copyToClipboard = async (): Promise<void> => {
  const plainText = content.value?.textContent?.trim() ?? props.message;

  try {
    await navigator.clipboard.writeText(plainText);
    copyState.value = 'copied';
  } catch {
    copyState.value = 'failed';
  }
};

defineExpose({
  showModal: (): void => dialog.value?.showModal(),
  close,
  copyToClipboard
});
</script>

<style scoped>
:deep(.app-error-dialog) {
  width: min(560px, calc(100vw - 2rem));
  padding: 0;
  background: var(--color-surface-raised);
}

.notice {
  color: var(--notice-text);
  border-top: 5px solid var(--notice-accent);
}

.notice--info {
  --notice-text: var(--color-info-text);
  --notice-accent: var(--color-button-primary);
  --notice-surface: var(--color-info-surface);
}

.notice--debug {
  --notice-text: var(--color-text-secondary);
  --notice-accent: var(--color-control-neutral);
  --notice-surface: var(--color-surface-neutral);
}

.notice--warning {
  --notice-text: var(--color-warning-text);
  --notice-accent: var(--color-focus-ring);
  --notice-surface: var(--color-warning-surface);
}

.notice--error {
  --notice-text: var(--color-danger-text);
  --notice-accent: var(--color-danger-border);
  --notice-surface: var(--color-danger-surface);
}

.notice-header {
  display: flex;
  gap: 0.75rem;
  align-items: center;
  padding: 1.25rem 1.25rem 1rem;
  background: var(--notice-surface);
}

.notice-header h2 {
  margin: 0;
  color: var(--notice-text);
  font-size: 1.25rem;
}

.notice-icon {
  flex: 0 0 auto;
}

.notice-content {
  padding: 1.25rem;
  color: var(--color-text-primary);
  overflow-wrap: anywhere;
}

.notice-content :deep(p:first-child) {
  margin-top: 0;
}

.notice-content :deep(p:last-child) {
  margin-bottom: 0;
}

.notice-content :deep(a) {
  color: var(--color-action-primary-text);
}

.notice-footer {
  display: flex;
  flex-wrap: wrap;
  gap: 0.625rem;
  align-items: center;
  justify-content: flex-end;
  padding: 1rem 1.25rem;
  border-top: 1px solid var(--color-border-subtle);
}

.copy-status {
  min-height: 1.5rem;
  margin: 0 auto 0 0;
  color: var(--color-text-muted);
}

@media (max-width: 480px) {
  .notice-footer :deep(button) {
    width: 100%;
  }
}
</style>
