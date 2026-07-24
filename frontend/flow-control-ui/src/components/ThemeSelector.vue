<template>
  <button
    class="theme-selector"
    type="button"
    :aria-label="themeButtonLabel"
    aria-describedby="theme-selector-help"
    :title="themeButtonLabel"
    :data-theme-preference="themePreference"
    @click="selectNextTheme"
  >
    <span class="theme-selector-icon" :style="themeIconStyle" aria-hidden="true" />
  </button>
  <span id="theme-selector-help" class="visually-hidden">
    Cycles between system, dark, and light theme preferences.
  </span>
  <span class="visually-hidden" role="status" aria-live="polite" aria-atomic="true">
    {{ themeStatus }}
  </span>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';

import darkThemeIcon from '@/assets/dark-mode-toggle-icon.svg';
import lightThemeIcon from '@/assets/light-mode-toggle-icon.svg';
import systemThemeIcon from '@/assets/system-mode-toggle-icon.svg';

type ThemePreference = 'light' | 'system' | 'dark';

const THEME_STORAGE_KEY = 'theme-preference';
const themeOrder: ThemePreference[] = ['light', 'system', 'dark'];
const themeIcons: Record<ThemePreference, string> = {
  light: lightThemeIcon,
  system: systemThemeIcon,
  dark: darkThemeIcon
};

const themePreference = ref<ThemePreference>('system');
const nextTheme = computed(
  () => themeOrder[(themeOrder.indexOf(themePreference.value) + 1) % themeOrder.length]!
);
const themeName = computed(
  () => `${themePreference.value[0]!.toUpperCase()}${themePreference.value.slice(1)}`
);
const nextThemeName = computed(
  () => `${nextTheme.value[0]!.toUpperCase()}${nextTheme.value.slice(1)}`
);
const themeButtonLabel = computed(
  () => `Theme preference: ${themeName.value}. Activate to use ${nextThemeName.value} theme`
);
const themeStatus = computed(() => `${themeName.value} theme preference selected`);
const themeIconStyle = computed(() => ({
  '--theme-icon': `url("${themeIcons[themePreference.value]}")`
}));

const applyTheme = (preference: ThemePreference): void => {
  const root = document.documentElement;

  root.dataset.themePreference = preference;
  if (preference === 'system') {
    delete root.dataset.theme;
  } else {
    root.dataset.theme = preference;
  }
};

const getStoredTheme = (): ThemePreference | null => {
  try {
    const savedTheme = globalThis.localStorage?.getItem(THEME_STORAGE_KEY);
    return savedTheme === 'light' || savedTheme === 'dark' || savedTheme === 'system'
      ? savedTheme
      : null;
  } catch {
    return null;
  }
};

const storeTheme = (preference: ThemePreference): void => {
  try {
    globalThis.localStorage?.setItem(THEME_STORAGE_KEY, preference);
  } catch {
    // Theme selection still works when storage is disabled or unavailable.
  }
};

const selectNextTheme = (): void => {
  themePreference.value = nextTheme.value;
  storeTheme(themePreference.value);
  applyTheme(themePreference.value);
};

onMounted(() => {
  const savedTheme = getStoredTheme();
  if (savedTheme !== null) {
    themePreference.value = savedTheme;
  }

  applyTheme(themePreference.value);
});
</script>

<style scoped>
.theme-selector {
  display: inline-grid;
  width: 72px;
  height: 40px;
  padding: 4px;
  place-items: center;
  color: #b9cad8;
  background: transparent;
  border: 0;
  border-radius: 8px;
  cursor: pointer;
}

.theme-selector:hover {
  color: #fff;
  background: #20384d;
}

.theme-selector-icon {
  width: 64px;
  height: 32px;
  background-color: currentColor;
  mask: var(--theme-icon) center / contain no-repeat;
}

.visually-hidden {
  position: absolute;
  width: 1px;
  height: 1px;
  padding: 0;
  overflow: hidden;
  white-space: nowrap;
  border: 0;
  clip: rect(0 0 0 0);
  clip-path: inset(50%);
}
</style>
