# Creating a new Vue component

Use this guide when adding a component to `frontend/flow-control-ui`. First inspect the nearest related components and their tests; preserve the local feature's vocabulary and API shape instead of introducing a new pattern without a clear reason.

## 1. Choose the location and name

- Put reusable application-wide UI in `src/components/`.
- Put feature-specific UI in `src/features/<feature>/components/`.
- Put route-level screens in `src/features/<feature>/views/`.
- Name component and view files in PascalCase with the `App` prefix, for example `AppStatusBadge.vue`. The custom filename lint rule enforces the repository's naming conventions.
- Keep a component focused. Before writing component-local logic, search existing composables for equivalent behavior.
- If logic is generic and could be used by another component, place it in a typed composable instead of the component. Put application-wide composables in `src/composables/` and feature-specific composables in `src/features/<feature>/composables/`.
- Components may coordinate their own presentation-specific state, but reusable state, computed behavior, lifecycle handling, and operations belong in composables. Do not copy and adapt the same logic across components; extract one shared implementation and test it directly.
- Put shared domain types in the feature's `types` module when appropriate.

## 2. Follow the single-file component structure

Use Vue 3 Composition API and TypeScript. The enforced block order is:

```vue
<template>
  <!-- Semantic HTML with a single meaningful root -->
</template>

<script setup lang="ts">
// Imports and component logic
</script>

<style scoped>
/* Component-specific styles */
</style>
```

Omit the style block when the component needs no styles. Do not use the Options API unless an existing integration specifically requires it.

## 3. Define a small, typed public API

- Declare props with `defineProps<{ ... }>()`. Use `withDefaults` for optional props that need defaults.
- Define every component-emitted event name once in `src/constants/events.ts`. Event keys use uppercase snake case and values use Vue event naming conventions (lowercase kebab-case):

  ```ts
  // src/constants/events.ts
  export const EVENTS = {
    SAVE: 'save',
    CANCEL: 'cancel',
    DELETE: 'delete',
    ITEM_SELECTED: 'item-selected',
    REFRESH: 'refresh'
  } as const;
  ```

- Reuse the existing entry when an event has the same meaning. Add a new constant when introducing a genuinely new event. Do not scatter event-name string literals through components.
- Import `EVENTS` and declare emitted events with typed call signatures based on the constant values:

  ```ts
  import { EVENTS } from '@/constants/events';

  const emit = defineEmits<{
    (event: typeof EVENTS.SAVE): void;
    (event: typeof EVENTS.ITEM_SELECTED, id: number): void;
  }>();

  const onClick = (): void => {
    emit(EVENTS.SAVE);
  };
  ```

- Use the same constants for listeners in parent templates:

  ```vue
  <script setup lang="ts">
  import { EVENTS } from '@/constants/events';
  </script>

  <template>
    <MyComponent
      automation="item-editor"
      @[EVENTS.SAVE]="onSave"
      @[EVENTS.ITEM_SELECTED]="onItemSelected"
    />
  </template>
  ```

- This constant-based pattern is mandatory for all custom component events, including `update:modelValue` events. Never use raw custom-event strings in `defineEmits`, `emit(...)`, or parent listener directives.
- Native element events such as `click`, `input`, `keydown`, and `submit` remain Vue template directives (`@click`, `@input`, and so on); they are not custom component events.

- Use `modelValue` plus `update:modelValue` for a `v-model` contract.
- Treat props as immutable. Derive values with `computed` and emit changes rather than mutating parent-owned state.
- Type refs to their DOM or component type, such as `ref<HTMLDialogElement>()`.
- Use `defineExpose` only for an intentionally imperative API, as in `AppDialog`.
- Export a component-local interface only when consumers genuinely need it.
- Give functions explicit return types. The lint configuration enforces explicit function return types and unused-variable checks.

## 4. Use repository import conventions

- Use `@/` imports for anything outside the current directory, including shared components, composables, feature modules, and assets.
- Use `./` only for files in the same directory. Parent-relative imports such as `../` are prohibited.
- Use `import type` for type-only imports.
- First look for and use an existing `App*` component, such as `AppButton`, `AppDialog`, `AppPopover`, `AppTable`, or `AppFormGroup`. These components carry the project's established behavior, styling, accessibility, and automation contracts.
- When no suitable `App*` component exists, use the correct native semantic HTML element. Do not recreate an existing application primitive or substitute a generic `div`/`span` for meaningful HTML.
- Import icons from `@/assets/icons/`; decorative icons must be hidden from assistive technology.

## 5. Add stable automation metadata

Automation hooks are a repository requirement, not optional test decoration.

- Every component must declare `automation: string` as a mandatory prop with no default. Do not use `automation?: string`, do not default it to `''`, and do not make the automation hook conditional.
- Create the binding with `useAutomation`:

  ```ts
  import { useAutomation } from '@/composables/useAutomation';

  const props = defineProps<{
    automation: string;
  }>();

  const automation = useAutomation(props.automation);
  ```

- Bind the base identifier to the meaningful root element:

  ```vue
  <div v-bind="automation()"></div>
  ```

- Bind child identifiers with a stable kebab-case suffix:

  ```vue
  <button v-bind="automation('save-button')">Save</button>
  ```

- Pass an `automation="lowercase-kebab-case"` prop whenever rendering an `App*` or `Base*` component. A forwarded `v-bind="automation('child')"` is also supported.
- Automation names and suffixes must start with a lowercase letter and contain only lowercase letters, numbers, and single hyphens. Generated output uses `base.suffix`.
- Never base automation identifiers on translated labels, DOM position, CSS classes, or other unstable presentation details. Dynamic entity IDs are acceptable when they identify a stable domain item.

## 6. Meet WCAG 2.2 Level AA

Every new component must be WCAG 2.2 Level AA compliant in all of its states and at the supported responsive sizes. Treat accessibility as an acceptance criterion, not a later enhancement.

- After checking for a suitable existing `App*` component, prefer native semantic elements (`button`, `dialog`, `nav`, `section`, `table`, `label`) over simulated controls.
- Every control needs an accessible name. Associate labels with inputs using `for`/`id`, or use an accurate `aria-label`/`aria-labelledby`.
- Icon-only controls need a useful accessible label; decorative icons use `aria-hidden="true"`.
- Use `role="status"` or `aria-live="polite"` for non-urgent updates and `role="alert"` for errors requiring immediate announcement.
- Expose control state with the appropriate attributes, for example `aria-expanded`, `aria-pressed`, `aria-current`, `aria-invalid`, and `aria-sort`.
- Ensure keyboard operation and visible focus. Custom interactive elements need equivalent keyboard behavior, though a native control is preferred.
- Dialogs and popovers need an accessible name, correct focus handling, Escape behavior, and focus restoration. Reuse the existing primitives/composables.
- Keep touch targets usable; existing controls generally use a minimum height around 44px.
- Preserve Level AA color contrast, do not rely on color alone, support text zoom/reflow, avoid keyboard traps, and provide sufficiently large or spaced pointer targets.
- Verify the rendered component with automated accessibility coverage and manual keyboard/focus checks. Automated checks alone do not prove WCAG conformance.

## 7. Manage state and lifecycle safely

- Prefer Vue features over direct native JavaScript DOM manipulation. Use Vue reactivity, directives, bindings, template refs, computed values, watchers, and lifecycle hooks instead of manually querying or modifying the DOM.
- Declare element events in the template with Vue event bindings such as `@click`, `@input`, `@keydown`, `@submit.prevent`, and event modifiers. Do not call `addEventListener` for an element rendered by the component.
- Communicate from child to parent with typed Vue emits rather than dispatching custom native DOM events.
- Use `v-model` or `:value` plus Vue event bindings for form state rather than reading values from the DOM.
- Native browser APIs are appropriate only when Vue does not provide the required capability, for example document/window events, observers, focus, selection, or native dialog methods. Access rendered elements through typed template refs, register external listeners in the appropriate Vue lifecycle hook, and always remove or dispose them in `onBeforeUnmount`.
- Use `ref`, `reactive`, and `computed` for local state; use the relevant Pinia store for shared feature state.
- Keep async loading, success, empty, and error states explicit in the UI.
- Prevent stale async responses from replacing newer state. Abort requests or use the existing latest-request guard where relevant.
- Pair every global listener, observer, timer, or pending request with cleanup in `onBeforeUnmount`.
- Use `watch` only for actual side effects. Prefer `computed` for derived values.
- Do not silently swallow errors; present a useful message and keep recovery actions available.

## 8. Match the visual system

- Keep component-specific CSS local in the component's `<style scoped>` block.
- Put CSS in `src/assets/styles/main.css` only when the same rule is intentionally shared by multiple components. Do not move one-component styling into a global stylesheet.
- Use color variables defined in `src/assets/styles/theme.css`, such as `--color-text-primary`, `--color-surface-raised`, `--color-border-default`, and the action/status tokens. Do not hard-code colors in component or shared CSS.
- Follow nearby spacing, radius, typography, shadow, hover, focus, disabled, and responsive patterns.
- Use `:deep(...)` deliberately when a scoped parent must style slotted or child content.
- Add a focused media query when the layout does not work on narrow screens.
- Avoid global styling from a component. Shared structural CSS belongs in `main.css`; shared color values belong in `theme.css`.

## 9. Test observable behavior

- Every new component must have both a unit test and an end-to-end test.
- Add a colocated Vitest test under `__tests__/`. Use `@vue/test-utils` and `// @vitest-environment jsdom` for DOM component tests.
- Test what a user or consumer observes: rendered content, accessible names/states, emitted events and payloads, automation attributes, disabled behavior, slots, and cleanup.
- Add or update a Playwright spec under `e2e/` that exercises the component through a real user-facing route.
- Run the E2E test across every browser/device project configured in `playwright.config.ts`, rather than selecting only one project. Currently this means desktop Chromium, desktop Firefox, desktop Edge, and mobile Chromium.
- Include accessibility assertions appropriate to the component and verify keyboard interaction in E2E coverage.
- Follow `.codex/test-documentation-rules.md` when documenting tests.
- Avoid tests coupled only to implementation details or fragile CSS selectors; prefer roles, labels, and `data-automation`.

## 10. Verify before handing off

From `frontend/flow-control-ui`, run the checks appropriate to the change:

```text
npm run format
npm run lint
npm run type-check
npm run test:unit
npm run test:e2e
npm run build
```

Do not consider the component complete until its unit test passes, its E2E scenario passes in every configured browser/device project, its consumer is wired up, it meets WCAG 2.2 AA in all public states, resources are cleaned up, and the affected checks pass.

## Minimal starting example

```vue
<template>
  <section v-bind="automation()" class="status-card" :aria-labelledby="headingId">
    <h2 :id="headingId">{{ title }}</h2>
    <p role="status">{{ message }}</p>
    <AppButton
      v-bind="automation('refresh')"
      text="Refresh"
      :disabled="loading"
      @click="emit(EVENTS.REFRESH)"
    />
  </section>
</template>

<script setup lang="ts">
import { useId } from 'vue';

import AppButton from '@/components/AppButton.vue';
import { useAutomation } from '@/composables/useAutomation';
import { EVENTS } from '@/constants/events';

const props = withDefaults(
  defineProps<{
    title: string;
    message: string;
    automation: string;
    loading?: boolean;
  }>(),
  {
    loading: false
  }
);

const emit = defineEmits<{
  (event: typeof EVENTS.REFRESH): void;
}>();

const headingId = useId();
const automation = useAutomation(props.automation);
</script>

<style scoped>
.status-card {
  padding: 16px;
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: 1px solid var(--color-border-default);
  border-radius: 8px;
}
</style>
```
