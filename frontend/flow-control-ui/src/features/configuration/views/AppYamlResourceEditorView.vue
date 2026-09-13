<template>
  <section class="configuration-page editor-page">
    <AppErrorNotice id="yaml-resource-error-notice" :message="apiError" :details="noticeDetails" />
    <nav aria-label="Breadcrumb">
      <RouterLink :to="{ name: 'controller-templates' }">Controller templates</RouterLink> /
      {{ isNew ? 'New' : 'Edit' }}
    </nav>
    <div class="page-heading">
      <div>
        <p>YAML configuration</p>
        <h1>{{ heading }}</h1>
        <p>Define the capabilities and limits supported by this deployment target.</p>
      </div>
    </div>
    <div
      v-if="error"
      ref="errorSummary"
      class="request-error error-summary"
      role="alert"
      tabindex="-1"
    >
      <strong>There is a problem</strong><span>{{ error }}</span>
      <ul v-if="serverDiagnostics.length">
        <li v-for="item in serverDiagnostics" :key="`${item.path}:${item.message}`">
          <strong>{{ item.path }}</strong
          >: {{ item.message }}
        </li>
      </ul>
    </div>
    <p v-if="isSpinnerVisible" role="status">Loading controller template…</p>
    <form v-else @submit.prevent="save">
      <div class="editor-actions">
        <AppConfigurationGuidance type="controller-template" :yaml="yaml" />
        <AppButton
          v-if="!readOnly"
          type="submit"
          :text="saving ? 'Saving…' : 'Save'"
          :icon="saveIcon"
          :disabled="busy || hasEditorErrors"
        />
        <AppButton
          v-if="!readOnly"
          text="Validate"
          :icon="checkIcon"
          :disabled="busy || hasEditorErrors"
          @click="validateTemplate"
        />
        <AppButton
          v-if="!isNew && !readOnly"
          text="Delete"
          :icon="deleteIcon"
          :disabled="busy"
          @click="remove"
        />
        <RouterLink v-if="readOnly" class="primary-link" :to="{ name: 'controller-template-new' }">
          <AppSvg :src="newIcon" size="1em" />
          Create custom template from example
        </RouterLink>
      </div>
      <AppYamlEditor
        v-model="yaml"
        label="Controller template YAML"
        help="Use schema version 1."
        :schema="controllerTemplateSchema"
        :schema-uri="schemaUri"
        min-height="620px"
        :read-only="readOnly"
        @[EVENTS.DIAGNOSTICS]="setEditorDiagnostics"
      />
    </form>
    <p class="visually-hidden" role="status" aria-live="polite">{{ status }}</p>
  </section>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref } from 'vue';
import { parse } from 'yaml';
import { onBeforeRouteLeave, useRouter } from 'vue-router';
import checkIcon from '@/assets/icons/check-icon.svg';
import deleteIcon from '@/assets/icons/delete-flow-icon.svg';
import newIcon from '@/assets/icons/new-icon.svg';
import saveIcon from '@/assets/icons/save-icon.svg';
import AppButton from '@/components/AppButton.vue';
import AppErrorNotice from '@/components/AppErrorNotice.vue';
import AppSvg from '@/components/AppSvg.vue';
import AppYamlEditor, { type YamlDiagnostic } from '@/components/AppYamlEditor.vue';
import { EVENTS } from '@/constants/events';
import { useSaveShortcut } from '@/composables/useSaveShortcut';
import { useSpinner } from '@/composables/useSpinner';
import {
  controllerTemplateConfigurationApi,
  type ValidationDiagnostic,
  YamlResourceError
} from '@/features/configuration/api/yamlResourceApi';
import AppConfigurationGuidance from '@/features/configuration/components/AppConfigurationGuidance.vue';
import { controllerTemplateSchema } from '@/features/configuration/configurationSchemas';

const props = defineProps<{ resourceId?: string }>();
const router = useRouter();
const isNew = computed(() => !props.resourceId);
const readOnly = computed(() => props.resourceId === 'default');
const heading = computed(() =>
  isNew.value
    ? 'New controller template'
    : readOnly.value
      ? 'Default controller template'
      : `Edit ${props.resourceId}`
);
const schemaUri = String(controllerTemplateSchema.$id);
const example = `schemaVersion: 1
id: custom-controller
name: Custom controller
readOnly: false
capabilities:
  pointTypes: [digital]
  pointDirections: [input, output]
  pointFeatures: [read, command]
  connectorDataTypes: [boolean]
  flowFunctions: [and, readPoint, writePoint]
  executionModes: [interval]
  runtimeFeatures: [physicalPoints]
limits: {maxFlows: 8, maxNodesPerFlow: 128, maxConnectionsPerFlow: 256}
`;
const yaml = ref(example);
const baseline = ref(example);
const revision = ref(0);
const saving = ref(false);
const validating = ref(false);
const deleting = ref(false);
const error = ref('');
const apiError = ref('');
const status = ref('');
const serverDiagnostics = ref<ValidationDiagnostic[]>([]);
const editorDiagnostics = ref<YamlDiagnostic[]>([]);
const errorSummary = ref<HTMLElement>();
const loadController = new AbortController();
const { isSpinnerVisible, withSpinner } = useSpinner();
const busy = computed(() => saving.value || validating.value || deleting.value);
const hasEditorErrors = computed(() =>
  editorDiagnostics.value.some(({ severity }) => severity === 'error')
);
const noticeDetails = computed(() =>
  serverDiagnostics.value.map((item) => `${item.path}: ${item.message}`)
);
const setEditorDiagnostics = (items: YamlDiagnostic[]): void => {
  editorDiagnostics.value = items;
};
const resourceIdFromYaml = (): string => {
  try {
    const id = (parse(yaml.value) as { id?: unknown }).id;
    return typeof id === 'string' ? id : '';
  } catch {
    return '';
  }
};
const showFailure = async (reason: unknown, fallback: string): Promise<void> => {
  error.value = reason instanceof Error ? reason.message : fallback;
  apiError.value = error.value;
  serverDiagnostics.value =
    reason instanceof YamlResourceError && Array.isArray(reason.details)
      ? (reason.details as ValidationDiagnostic[])
      : [];
  await nextTick();
  errorSummary.value?.focus();
};
const load = async (): Promise<void> => {
  if (!props.resourceId) return;
  try {
    await withSpinner(
      null,
      async () => {
        const result = await controllerTemplateConfigurationApi.get(
          props.resourceId!,
          loadController.signal
        );
        yaml.value = result.yaml;
        baseline.value = result.yaml;
        revision.value = result.revision;
      },
      null
    );
  } catch (reason) {
    if (!loadController.signal.aborted)
      await showFailure(reason, 'Unable to load controller template');
  }
};
const save = async (): Promise<void> => {
  if (readOnly.value || busy.value) return;
  const id = resourceIdFromYaml();
  if (!id) {
    await showFailure(new Error('The YAML must contain an id.'), 'Unable to save');
    return;
  }
  saving.value = true;
  try {
    const result = props.resourceId
      ? await controllerTemplateConfigurationApi.update(
          props.resourceId,
          yaml.value,
          revision.value
        )
      : await controllerTemplateConfigurationApi.create(yaml.value);
    baseline.value = result.yaml;
    revision.value = result.revision;
    status.value = 'Controller template saved.';
    if (!props.resourceId)
      await router.replace({ name: 'controller-template-detail', params: { resourceId: id } });
  } catch (reason) {
    await showFailure(reason, 'Unable to save controller template');
  } finally {
    saving.value = false;
  }
};
const validateTemplate = async (): Promise<void> => {
  validating.value = true;
  try {
    serverDiagnostics.value = await controllerTemplateConfigurationApi.validate(yaml.value);
    error.value = serverDiagnostics.value.length ? 'Validation failed.' : '';
    status.value = serverDiagnostics.value.length
      ? 'Controller template is invalid.'
      : 'Controller template is valid.';
  } catch (reason) {
    await showFailure(reason, 'Unable to validate controller template');
  } finally {
    validating.value = false;
  }
};
const remove = async (): Promise<void> => {
  if (!props.resourceId || !window.confirm('Delete this controller template?')) return;
  deleting.value = true;
  try {
    await controllerTemplateConfigurationApi.delete(props.resourceId, revision.value);
    await router.push({ name: 'controller-templates' });
  } catch (reason) {
    await showFailure(reason, 'Unable to delete controller template');
  } finally {
    deleting.value = false;
  }
};
useSaveShortcut(save);
onBeforeRouteLeave(() =>
  yaml.value !== baseline.value && !window.confirm('Discard unsaved changes?') ? false : true
);
onMounted(load);
onBeforeUnmount(() => loadController.abort());
</script>
