<template>
  <section class="configuration-page editor-page">
    <nav aria-label="Breadcrumb">
      <RouterLink :to="{ name: listRoute }">{{ pluralLabel }}</RouterLink> /
      {{ isNew ? `New ${singularLabel}` : readOnly ? singularLabel : `Edit ${singularLabel}` }}
    </nav>
    <div class="page-heading">
      <div>
        <p>YAML configuration</p>
        <h1>{{ heading }}</h1>
        <p>{{ helpText }}</p>
      </div>
    </div>

    <div
      v-if="error"
      ref="errorSummary"
      class="request-error error-summary"
      role="alert"
      tabindex="-1"
    >
      <strong>There is a problem</strong>
      <span>{{ error }}</span>
      <ul v-if="serverDiagnostics.length">
        <li v-for="item in serverDiagnostics" :key="`${item.path}:${item.message}`">
          <strong>{{ item.path }}</strong
          >: {{ item.message
          }}<span v-if="item.line"> (line {{ item.line }}, column {{ item.column }})</span>
        </li>
      </ul>
    </div>
    <p v-if="loading" role="status">Loading {{ singularLabel }}…</p>
    <form v-else @submit.prevent="save">
      <div v-if="isNew && kind === 'point'" class="example-picker">
        <label for="point-example">Start with a point example</label>
        <select id="point-example" v-model="selectedExample" @change="useExample">
          <option v-for="example in pointExamples" :key="example.name" :value="example.name">
            {{ example.name }}
          </option>
        </select>
      </div>
      <AppYamlEditor
        v-model="yaml"
        automation="yaml-resource-editor"
        :label="`${singularLabel} YAML`"
        :help="editorHelp"
        :schema="schema"
        :schema-uri="schemaUri"
        min-height="620px"
        :read-only="readOnly"
        @diagnostics="editorDiagnostics = $event"
      />
      <div class="editor-actions">
        <AppButton
          v-if="!readOnly"
          automation="yaml-resource-save"
          type="submit"
          :text="saving ? 'Saving…' : 'Save'"
          :icon="saveIcon"
          :disabled="busy || hasEditorErrors"
        />
        <AppButton
          v-if="kind === 'controller' && !readOnly"
          automation="yaml-resource-validate"
          text="Validate"
          :icon="checkIcon"
          :disabled="busy || hasEditorErrors"
          @click="validateTemplate"
        />
        <AppButton
          v-if="!isNew && !readOnly"
          automation="yaml-resource-delete"
          text="Delete"
          :icon="deleteIcon"
          :disabled="busy"
          @click="remove"
        />
        <AppButton
          v-if="kind === 'group' && !isNew && deleteConflict"
          automation="point-group-make-standalone"
          text="Make member points standalone"
          :icon="checkIcon"
          @click="makeStandalone"
        />
        <RouterLink
          v-if="kind === 'controller' && readOnly"
          class="primary-link"
          :to="{ name: 'controller-template-new' }"
        >
          Create custom template from example
        </RouterLink>
      </div>
    </form>

    <section
      v-if="kind === 'point' && !isNew"
      class="runtime-panel"
      aria-labelledby="runtime-heading"
    >
      <div>
        <h2 id="runtime-heading">Live point value</h2>
        <AppButton
          automation="point-runtime-toggle-updates"
          :text="runtimePaused ? 'Resume updates' : 'Pause updates'"
          :icon="runtimePaused ? playIcon : pauseIcon"
          @click="runtimePaused = !runtimePaused"
        />
        <AppButton
          automation="point-runtime-retry"
          text="Retry now"
          :icon="retryIcon"
          @click="loadRuntime"
        />
      </div>
      <p v-if="runtimeLoading" role="status">Reading point value…</p>
      <dl v-if="runtime">
        <div>
          <dt>Status</dt>
          <dd>{{ runtime.status }}</dd>
        </div>
        <div>
          <dt>Value</dt>
          <dd>{{ displayValue }}</dd>
        </div>
        <div>
          <dt>Units</dt>
          <dd>{{ runtime.units || '—' }}</dd>
        </div>
        <div>
          <dt>Quality</dt>
          <dd>{{ runtime.quality }}</dd>
        </div>
        <div>
          <dt>Reliability</dt>
          <dd>{{ runtime.reliability }}</dd>
        </div>
        <div>
          <dt>Connection</dt>
          <dd>{{ runtime.connectionState }}</dd>
        </div>
        <div>
          <dt>Source timestamp</dt>
          <dd>{{ runtime.sourceTimestamp || '—' }}</dd>
        </div>
      </dl>
      <p v-if="runtime" class="runtime-diagnostic">{{ runtime.diagnostic }}</p>
    </section>
    <p class="visually-hidden" role="status" aria-live="polite">{{ status }}</p>
  </section>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { onBeforeRouteLeave, useRouter } from 'vue-router';
import checkIcon from '@/assets/icons/check-icon.svg';
import deleteIcon from '@/assets/icons/delete-flow-icon.svg';
import pauseIcon from '@/assets/icons/pause-icon.svg';
import playIcon from '@/assets/icons/play-icon.svg';
import retryIcon from '@/assets/icons/retry-icon.svg';
import saveIcon from '@/assets/icons/save-icon.svg';
import AppButton from '@/components/AppButton.vue';
import AppYamlEditor, { type YamlDiagnostic } from '@/components/AppYamlEditor.vue';
import {
  controllerTemplateConfigurationApi,
  pointConfigurationApi,
  pointGroupConfigurationApi,
  type RuntimeEnvelope,
  type ValidationDiagnostic,
  YamlResourceError
} from '@/features/configuration/api/yamlResourceApi';
import {
  controllerTemplateSchema,
  pointGroupSchema,
  pointSchema
} from '@/features/configuration/configurationSchemas';

type ResourceKind = 'point' | 'group' | 'controller';
const props = defineProps<{ kind: ResourceKind; resourceId?: string }>();
const router = useRouter();
const isNew = computed(() => !props.resourceId);
const singularLabel = computed(() =>
  props.kind === 'point' ? 'point' : props.kind === 'group' ? 'point group' : 'controller template'
);
const pluralLabel = computed(() =>
  props.kind === 'point'
    ? 'Points'
    : props.kind === 'group'
      ? 'Point groups'
      : 'Controller templates'
);
const listRoute = computed(() =>
  props.kind === 'point'
    ? 'points'
    : props.kind === 'group'
      ? 'point-groups'
      : 'controller-templates'
);
const detailRoute = computed(() =>
  props.kind === 'point'
    ? 'point-detail'
    : props.kind === 'group'
      ? 'point-group-detail'
      : 'controller-template-detail'
);
const schema = computed(() =>
  props.kind === 'point'
    ? pointSchema
    : props.kind === 'group'
      ? pointGroupSchema
      : controllerTemplateSchema
);
const schemaUri = computed(() => schema.value.$id as string);
const helpText = computed(() =>
  props.kind === 'point'
    ? 'Configure type, membership, source mapping, limits, and safe behavior.'
    : props.kind === 'group'
      ? 'Configure shared source and mapping defaults for member points.'
      : 'Define the capabilities and limits supported by this deployment target.'
);
const editorHelp = computed(() =>
  readOnly.value
    ? 'This built-in example is read-only. Its YAML remains selectable and can be copied.'
    : 'The editor provides schema feedback; the server performs authoritative validation.'
);

const pointExamples = [
  {
    name: 'Analog virtual',
    yaml: `schemaVersion: 1
groups: []
points:
  - id: new-analog
    name: New analog point
    enabled: true
    implementation: virtual
    direction: value
    valueType: analog
    units: percent
    readable: true
    commandable: true
    persistence: volatile
`
  },
  {
    name: 'Digital retained',
    yaml: `schemaVersion: 1
groups: []
points:
  - id: new-digital
    name: New digital point
    enabled: true
    implementation: virtual
    direction: value
    valueType: digital
    stateLabels: {false: "Off", true: "On"}
    readable: true
    commandable: true
    persistence: retained
    relinquishDefault: false
`
  },
  {
    name: 'HTTP/JSON bound input',
    yaml: `schemaVersion: 1
groups: []
points:
  - id: new-http-input
    name: New HTTP input
    enabled: true
    implementation: bound
    direction: input
    valueType: analog
    readable: true
    commandable: false
    persistence: volatile
    sourceId: http-source
    mapping: {path: /value, method: GET, selector: $.value}
`
  },
  {
    name: 'MQTT text input',
    yaml: `schemaVersion: 1
groups: []
points:
  - id: new-mqtt-input
    name: New MQTT input
    enabled: true
    implementation: bound
    direction: input
    valueType: text
    readable: true
    commandable: false
    persistence: volatile
    sourceId: mqtt-source
    mapping: {topic: plant/value, selector: $.value}
    limits: {maxLength: 256}
`
  }
];
const groupExample = `schemaVersion: 1
groups:
  - id: new-group
    name: New point group
    description: Shared source group
points: []
`;
const controllerExample = `schemaVersion: 1
id: custom-controller
name: Custom controller
description: Constrained deployment target
readOnly: false
capabilities:
  pointTypes: [digital]
  pointDirections: [input, output]
  pointFeatures: [read, command]
  connectorDataTypes: [boolean]
  flowFunctions: [and, read-point, write-point]
  executionModes: [interval]
  runtimeFeatures: [bound_points]
limits:
  maxFlows: 8
  maxNodesPerFlow: 64
  maxConnectionsPerFlow: 96
  minimumIntervalMilliseconds: 100
`;
const initial = computed(() =>
  props.kind === 'point'
    ? pointExamples[0]!.yaml
    : props.kind === 'group'
      ? groupExample
      : controllerExample
);
const selectedExample = ref(pointExamples[0]!.name);
const yaml = ref('');
const baseline = ref('');
const revision = ref(0);
const loading = ref(false);
const saving = ref(false);
const validating = ref(false);
const error = ref('');
const status = ref('');
const deleteConflict = ref(false);
const serverDiagnostics = ref<ValidationDiagnostic[]>([]);
const editorDiagnostics = ref<YamlDiagnostic[]>([]);
const errorSummary = ref<HTMLElement>();
const runtime = ref<RuntimeEnvelope>();
const runtimeLoading = ref(false);
const runtimePaused = ref(false);
let allowNavigation = false;
let loadController: AbortController | undefined;
let runtimeController: AbortController | undefined;
let runtimeTimer: number | undefined;
const dirty = computed(() => yaml.value !== baseline.value);
const busy = computed(() => saving.value || validating.value);
const hasEditorErrors = computed(() =>
  editorDiagnostics.value.some(({ severity }) => severity === 'error')
);
const readOnly = computed(() => props.kind === 'controller' && props.resourceId === 'default');
const heading = computed(() =>
  isNew.value
    ? `New ${singularLabel.value}`
    : readOnly.value
      ? 'Built-in default controller'
      : singularLabel.value
);
const displayValue = computed(() =>
  runtime.value?.value === null || runtime.value?.value === undefined
    ? 'Unavailable'
    : typeof runtime.value.value === 'string'
      ? runtime.value.value
      : JSON.stringify(runtime.value.value)
);

const api = computed(() =>
  props.kind === 'point'
    ? pointConfigurationApi
    : props.kind === 'group'
      ? pointGroupConfigurationApi
      : controllerTemplateConfigurationApi
);
const resourceIdFromYaml = (): string =>
  yaml.value.match(/(?:^|\n)(?:\s*-\s*)?id:\s*([a-z0-9-]+)/)?.[1] ?? '';
const useExample = (): void => {
  yaml.value =
    pointExamples.find(({ name }) => name === selectedExample.value)?.yaml ??
    pointExamples[0]!.yaml;
};
const showFailure = async (reason: unknown, fallback: string): Promise<void> => {
  error.value = reason instanceof Error ? reason.message : fallback;
  serverDiagnostics.value = [];
  if (reason instanceof YamlResourceError && reason.details) {
    const details = reason.details as { diagnostics?: ValidationDiagnostic[] };
    serverDiagnostics.value = details.diagnostics ?? [];
  }
  await nextTick();
  errorSummary.value?.focus();
};
const load = async (): Promise<void> => {
  yaml.value = baseline.value = initial.value;
  if (!props.resourceId) return;
  loadController = new AbortController();
  loading.value = true;
  try {
    const result = await api.value.get(props.resourceId, loadController.signal);
    yaml.value = baseline.value = result.yaml;
    revision.value = result.revision;
  } catch (reason) {
    if (!loadController.signal.aborted)
      await showFailure(reason, `Unable to load ${singularLabel.value}`);
  } finally {
    loading.value = false;
  }
};
const save = async (): Promise<void> => {
  saving.value = true;
  error.value = '';
  serverDiagnostics.value = [];
  try {
    const result = props.resourceId
      ? await api.value.update(props.resourceId, yaml.value, revision.value)
      : await api.value.create(yaml.value);
    yaml.value = baseline.value = result.yaml;
    revision.value = result.revision;
    status.value = `${singularLabel.value} saved.`;
    if (!props.resourceId) {
      allowNavigation = true;
      await router.replace({
        name: detailRoute.value,
        params: { resourceId: resourceIdFromYaml() }
      });
    }
  } catch (reason) {
    await showFailure(reason, `Unable to save ${singularLabel.value}`);
  } finally {
    saving.value = false;
  }
};
const validateTemplate = async (): Promise<void> => {
  validating.value = true;
  error.value = '';
  try {
    const diagnostics = await controllerTemplateConfigurationApi.validate(yaml.value);
    serverDiagnostics.value = diagnostics;
    if (diagnostics.length) {
      error.value = 'Controller template validation found problems.';
      await nextTick();
      errorSummary.value?.focus();
    } else status.value = 'Controller template YAML is valid.';
  } catch (reason) {
    await showFailure(reason, 'Unable to validate controller template');
  } finally {
    validating.value = false;
  }
};
const remove = async (): Promise<void> => {
  if (!props.resourceId || !window.confirm(`Delete this ${singularLabel.value}?`)) return;
  deleteConflict.value = false;
  try {
    await api.value.delete(props.resourceId, revision.value);
    allowNavigation = true;
    await router.push({ name: listRoute.value });
  } catch (reason) {
    deleteConflict.value =
      props.kind === 'group' && reason instanceof YamlResourceError && reason.status === 409;
    await showFailure(reason, `Unable to delete ${singularLabel.value}`);
  }
};
const makeStandalone = async (): Promise<void> => {
  if (!props.resourceId || props.kind !== 'group') return;
  try {
    await pointGroupConfigurationApi.makeStandalone(props.resourceId, revision.value);
    deleteConflict.value = false;
    status.value = 'Member points are now standalone.';
  } catch (reason) {
    await showFailure(reason, 'Unable to make member points standalone');
  }
};
const loadRuntime = async (): Promise<void> => {
  if (!props.resourceId || props.kind !== 'point' || runtimePaused.value || document.hidden) return;
  runtimeController?.abort();
  runtimeController = new AbortController();
  runtimeLoading.value = true;
  try {
    runtime.value = await pointConfigurationApi.runtime(props.resourceId, runtimeController.signal);
  } catch (reason) {
    if (!runtimeController.signal.aborted)
      await showFailure(reason, 'Unable to read point runtime');
  } finally {
    runtimeLoading.value = false;
  }
};
const scheduleRuntime = (): void => {
  window.clearInterval(runtimeTimer);
  if (props.kind === 'point' && props.resourceId) {
    runtimeTimer = window.setInterval(() => void loadRuntime(), 5000);
  }
};
watch(runtimePaused, (paused) => {
  if (!paused) void loadRuntime();
});
watch(
  () => props.resourceId,
  () => {
    void loadRuntime();
    scheduleRuntime();
  }
);
watch(error, async (value) => {
  if (value) {
    await nextTick();
    errorSummary.value?.focus();
  }
});
onBeforeRouteLeave(
  () =>
    allowNavigation ||
    !dirty.value ||
    window.confirm(`Discard unsaved ${singularLabel.value} changes?`)
);
onMounted(() => {
  void load().then(() => {
    void loadRuntime();
    scheduleRuntime();
  });
});
onBeforeUnmount(() => {
  loadController?.abort();
  runtimeController?.abort();
  window.clearInterval(runtimeTimer);
});
</script>
