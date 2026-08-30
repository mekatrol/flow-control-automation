<template>
  <section class="configuration-page editor-page">
    <AppErrorNotice
      id="yaml-resource-error-notice"
      :message="apiError"
      :details="noticeErrorDetails"
    />
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
      <div class="editor-actions">
        <AppButton
          v-if="!readOnly"
          type="submit"
          :text="saving ? 'Saving…' : 'Save'"
          :icon="saveIcon"
          :disabled="busy || hasEditorErrors"
        />
        <AppButton
          v-if="kind === 'controller' && !readOnly"
          text="Validate"
          :icon="checkIcon"
          :disabled="busy || hasEditorErrors"
          @click="validateTemplate"
        />
        <AppButton
          v-if="kind === 'point'"
          :text="pointTesting ? 'Stop testing' : 'Test point'"
          :icon="checkIcon"
          :disabled="busy || (!pointTesting && hasEditorErrors)"
          @click="pointTesting ? stopPointTest() : testPoint('read')"
        />
        <AppButton
          v-if="!isNew && !readOnly"
          text="Delete"
          :icon="deleteIcon"
          :disabled="busy"
          @click="remove"
        />
        <AppButton
          v-if="kind === 'group' && !isNew && deleteConflict"
          text="Make member points standalone"
          :icon="checkIcon"
          @click="makeStandalone"
        />
        <RouterLink
          v-if="kind === 'controller' && readOnly"
          class="primary-link"
          :to="{ name: 'controller-template-new' }"
        >
          <AppSvg :src="newIcon" size="1em" />
          Create custom template from example
        </RouterLink>
      </div>
      <AppYamlEditor
        v-model="yaml"
        :label="`${singularLabel} YAML`"
        :help="editorHelp"
        :schema="schema"
        :schema-uri="schemaUri"
        min-height="620px"
        :read-only="readOnly"
        @[EVENTS.DIAGNOSTICS]="setEditorDiagnostics"
      />
      <section
        v-if="kind === 'point' && (pointTesting || pointTestResult || pointTestError)"
        ref="pointTestPanel"
        class="point-test-panel"
        aria-labelledby="point-test-heading"
        aria-live="polite"
        tabindex="-1"
      >
        <h2 id="point-test-heading">Point test</h2>
        <p v-if="pointTesting">{{ pointTesting === 'read' ? 'Reading' : 'Writing' }} point…</p>
        <p v-if="pointTestError" class="request-error" role="alert">{{ pointTestError }}</p>
        <template v-if="pointTestResult">
          <div v-if="pointTestUrl" class="tested-request-url">
            <span>Request URL</span>
            <code>{{ pointTestUrl }}</code>
          </div>
          <div class="tested-value">
            <span>Point value</span>
            <strong>{{ displayTestValue }}</strong>
          </div>
          <h3>Test response</h3>
          <p>
            Status: {{ pointTestResult.httpResponse.statusCode }}
            {{ pointTestResult.httpResponse.reasonPhrase }}
          </p>
          <p v-if="pointTestResult.httpResponse.contentType">
            Content-Type: {{ pointTestResult.httpResponse.contentType }}
          </p>
          <pre tabindex="0"><code>{{ pointTestResult.httpResponse.body }}</code></pre>
        </template>
        <div v-if="pointCommandable" class="point-write-controls">
          <label for="test-point-value">Set point value</label>
          <input id="test-point-value" v-model="pointWriteValue" type="text" />
          <AppButton
            :text="pointTesting === 'write' ? 'Writing…' : 'Write point'"
            :icon="checkIcon"
            :disabled="pointTesting !== undefined || !pointWriteValue.trim()"
            @click="testPoint('write')"
          />
        </div>
        <p v-else class="runtime-diagnostic">This point is read-only.</p>
      </section>
    </form>

    <section
      v-if="kind === 'point' && !isNew"
      class="runtime-panel"
      aria-labelledby="runtime-heading"
    >
      <div>
        <h2 id="runtime-heading">Live point value</h2>
        <AppButton
          :text="runtimePaused ? 'Resume updates' : 'Pause updates'"
          :icon="runtimePaused ? playIcon : pauseIcon"
          @click="runtimePaused = !runtimePaused"
        />
        <AppButton text="Retry now" :icon="retryIcon" @click="loadRuntime" />
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
import { parse } from 'yaml';
import { useSaveShortcut } from '@/composables/useSaveShortcut';
import { onBeforeRouteLeave, useRouter } from 'vue-router';
import checkIcon from '@/assets/icons/check-icon.svg';
import deleteIcon from '@/assets/icons/delete-flow-icon.svg';
import newIcon from '@/assets/icons/new-icon.svg';
import pauseIcon from '@/assets/icons/pause-icon.svg';
import playIcon from '@/assets/icons/play-icon.svg';
import retryIcon from '@/assets/icons/retry-icon.svg';
import saveIcon from '@/assets/icons/save-icon.svg';
import AppButton from '@/components/AppButton.vue';
import AppErrorNotice from '@/components/AppErrorNotice.vue';
import AppSvg from '@/components/AppSvg.vue';
import AppYamlEditor, { type YamlDiagnostic } from '@/components/AppYamlEditor.vue';
import { EVENTS } from '@/constants/events';
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
import { pointSourceApi, type PointTestResult } from '@/features/pointSources/api/pointSourceApi';

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
  // Empty for now, but could be used to provide additional context or guidance in the editor.
  readOnly.value ? '' : ''
);

const pointExamples = [
  {
    name: 'AI — Analog input',
    yaml: `schemaVersion: 1
groups: []
points:
  - id: new-analog-input
    name: New analog input
    enabled: true
    implementation: bound
    direction: input
    valueType: analog
    units: percent
    readable: true
    commandable: false
    persistence: volatile
    sourceId: point-source
    mapping: {channel: AI-1}
`
  },
  {
    name: 'DI — Digital input',
    yaml: `schemaVersion: 1
groups: []
points:
  - id: new-digital-input
    name: New digital input
    enabled: true
    implementation: bound
    direction: input
    valueType: digital
    stateLabels: {false: "Off", true: "On"}
    readable: true
    commandable: false
    persistence: volatile
    sourceId: point-source
    mapping: {channel: DI-1}
`
  },
  {
    name: 'AO — Analog output',
    yaml: `schemaVersion: 1
groups: []
points:
  - id: new-analog-output
    name: New analog output
    enabled: true
    implementation: bound
    direction: output
    valueType: analog
    units: percent
    readable: true
    commandable: true
    persistence: volatile
    sourceId: point-source
    mapping: {channel: AO-1}
`
  },
  {
    name: 'DO — Digital output',
    yaml: `schemaVersion: 1
groups: []
points:
  - id: new-digital-output
    name: New digital output
    enabled: true
    implementation: bound
    direction: output
    valueType: digital
    stateLabels: {false: "Off", true: "On"}
    readable: true
    commandable: true
    persistence: volatile
    sourceId: point-source
    mapping: {channel: DO-1}
`
  },
  {
    name: 'AV — Analog virtual',
    yaml: `schemaVersion: 1
groups: []
points:
  - id: new-analog-virtual
    name: New analog virtual point
    enabled: true
    implementation: virtual
    direction: value
    valueType: analog
    units: percent
    readable: true
    commandable: true
    persistence: retained
    relinquishDefault: 0
`
  },
  {
    name: 'DV — Digital virtual',
    yaml: `schemaVersion: 1
groups: []
points:
  - id: new-digital-virtual
    name: New digital virtual point
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
  flowFunctions: [and, readPoint, writePoint]
  executionModes: [interval]
  runtimeFeatures: [boundPoints]
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
const apiError = ref('');
const apiErrorDetails = ref<string[]>([]);
const noticeErrorDetails = computed(() =>
  apiErrorDetails.value.length > 0 ? apiErrorDetails.value : apiError.value ? [apiError.value] : []
);
const status = ref('');
const deleteConflict = ref(false);
const serverDiagnostics = ref<ValidationDiagnostic[]>([]);
const editorDiagnostics = ref<YamlDiagnostic[]>([]);
const setEditorDiagnostics = (diagnostics: YamlDiagnostic[]): void => {
  editorDiagnostics.value = diagnostics;
};
const errorSummary = ref<HTMLElement>();
const runtime = ref<RuntimeEnvelope>();
const runtimeLoading = ref(false);
const runtimePaused = ref(false);
const pointTesting = ref<'read' | 'write'>();
const pointTestResult = ref<PointTestResult>();
const pointTestError = ref('');
const pointWriteValue = ref('');
const pointTestUrl = ref('');
const pointTestPanel = ref<HTMLElement>();
let allowNavigation = false;
let loadController: AbortController | undefined;
let runtimeController: AbortController | undefined;
let pointTestController: AbortController | undefined;
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
const pointDefinition = computed(() => {
  try {
    return (
      parse(yaml.value) as {
        points?: {
          sourceId?: string;
          commandable?: boolean;
          mapping?: { path?: string; method?: string };
        }[];
      }
    ).points?.[0];
  } catch {
    return undefined;
  }
});
const pointCommandable = computed(() => pointDefinition.value?.commandable === true);
const displayTestValue = computed(() => {
  const value = pointTestResult.value?.value;
  if (value === null || value === undefined) return 'Unavailable';
  return typeof value === 'string' ? value : JSON.stringify(value);
});

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
  apiError.value = reason instanceof Error ? reason.message : fallback;
  apiErrorDetails.value = [];
  serverDiagnostics.value = [];
  if (reason instanceof YamlResourceError && reason.details) {
    const details = reason.details as { diagnostics?: ValidationDiagnostic[] };
    serverDiagnostics.value = details.diagnostics ?? [];
    apiErrorDetails.value = serverDiagnostics.value.map(
      ({ path, message }) => `${path || 'Request'}: ${message}`
    );
  }
};
const load = async (): Promise<void> => {
  yaml.value = baseline.value = initial.value;
  if (!props.resourceId) return;
  apiError.value = '';
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
  apiError.value = '';
  saving.value = true;
  error.value = '';
  serverDiagnostics.value = [];
  try {
    const savedResourceId = resourceIdFromYaml();
    const result = props.resourceId
      ? await api.value.update(props.resourceId, yaml.value, revision.value)
      : await api.value.create(yaml.value);
    yaml.value = baseline.value = result.yaml;
    revision.value = result.revision;
    status.value = `${singularLabel.value} saved.`;
    if (!props.resourceId || savedResourceId !== props.resourceId) {
      allowNavigation = true;
      await router.replace({
        name: detailRoute.value,
        params: { resourceId: savedResourceId }
      });
    }
  } catch (reason) {
    await showFailure(reason, `Unable to save ${singularLabel.value}`);
  } finally {
    saving.value = false;
  }
};
useSaveShortcut(
  save,
  () => !loading.value && !busy.value && !readOnly.value && !hasEditorErrors.value
);
const validateTemplate = async (): Promise<void> => {
  apiError.value = '';
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
const testPoint = async (operation: 'read' | 'write'): Promise<void> => {
  const sourceId = pointDefinition.value?.sourceId;
  pointTestResult.value = undefined;
  pointTestError.value = '';
  pointTestUrl.value = '';
  pointTestController?.abort();
  pointTestController = new AbortController();
  const timeout = window.setTimeout(
    () => pointTestController?.abort('Point test timed out.'),
    15000
  );
  pointTesting.value = operation;
  try {
    if (!sourceId) {
      pointTestError.value = 'The point YAML must reference a point source with sourceId.';
      return;
    }
    const source = await pointSourceApi.get(sourceId, pointTestController.signal, {
      trackWait: false
    });
    const parsedSource = parse(source.yaml) as {
      sources?: { connection?: { baseUrl?: string } }[];
    };
    const baseUrl = parsedSource.sources?.[0]?.connection?.baseUrl;
    const path = pointDefinition.value?.mapping?.path;
    if (baseUrl && path) {
      try {
        pointTestUrl.value = new URL(path, baseUrl).toString();
      } catch {
        pointTestUrl.value = `${baseUrl.replace(/\/$/, '')}/${path.replace(/^\//, '')}`;
      }
    }
    if (operation === 'read' && props.resourceId) {
      const result = await pointConfigurationApi.runtime(
        props.resourceId,
        pointTestController.signal,
        { trackWait: false }
      );
      if (!result.deviceResponse) {
        throw new Error(result.diagnostic || 'The point device did not return an HTTP response.');
      }
      pointTestResult.value = {
        operation,
        value: result.value,
        httpResponse: result.deviceResponse
      };
      status.value = 'Point read test completed.';
      return;
    }
    let value: unknown;
    if (operation === 'write') {
      try {
        value = JSON.parse(pointWriteValue.value);
      } catch {
        value = pointWriteValue.value;
      }
    }
    pointTestResult.value = await pointSourceApi.testPoint(
      source.yaml,
      yaml.value,
      operation,
      value,
      pointTestController.signal,
      { trackWait: false }
    );
    status.value = `Point ${operation} test completed.`;
  } catch (reason) {
    pointTestError.value = pointTestController.signal.aborted
      ? pointTestController.signal.reason === 'Point test stopped by user.'
        ? 'The point test was stopped.'
        : 'The point test timed out after 15 seconds.'
      : reason instanceof Error
        ? reason.message
        : `Unable to ${operation} point`;
  } finally {
    window.clearTimeout(timeout);
    pointTesting.value = undefined;
    await nextTick();
    pointTestPanel.value?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    pointTestPanel.value?.focus({ preventScroll: true });
  }
};
const stopPointTest = (): void => {
  if (!pointTestController || !pointTesting.value) return;
  pointTestController.abort('Point test stopped by user.');
  status.value = 'Point test stopped.';
};
const remove = async (): Promise<void> => {
  if (!props.resourceId || !window.confirm(`Delete this ${singularLabel.value}?`)) return;
  apiError.value = '';
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
  apiError.value = '';
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
  apiError.value = '';
  runtimeController?.abort();
  runtimeController = new AbortController();
  runtimeLoading.value = true;
  try {
    runtime.value = await pointConfigurationApi.runtime(
      props.resourceId,
      runtimeController.signal,
      { trackWait: false }
    );
  } catch (reason) {
    if (!runtimeController.signal.aborted)
      await showFailure(reason, 'Unable to read point runtime');
  } finally {
    runtimeLoading.value = false;
  }
};
watch(runtimePaused, (paused) => {
  if (!paused) void loadRuntime();
});
watch(
  () => props.resourceId,
  () => {
    void loadRuntime();
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
  });
});
onBeforeUnmount(() => {
  loadController?.abort();
  runtimeController?.abort();
  pointTestController?.abort();
});
</script>

<style scoped lang="css">
.example-picker {
  display: grid;
  gap: var(--space-3);
  max-width: 360px;
  margin-bottom: var(--space-9);
  font-weight: var(--font-weight-bold);
}

.example-picker select {
  min-height: 44px;
  padding: var(--space-3-5) var(--space-4-5);
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-lg);
}

.runtime-panel {
  margin-top: var(--space-14);
  padding: var(--space-10);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-2xl);
}

.point-test-panel {
  margin-top: var(--space-10);
  padding: var(--space-10);
  background: var(--color-surface-subtle);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-2xl);
}

.tested-value {
  display: flex;
  gap: var(--space-5);
  align-items: baseline;
  padding: var(--space-5);
  background: var(--color-surface-raised);
  border-radius: var(--radius-lg);
}

.tested-value span {
  color: var(--color-text-secondary);
}

.tested-request-url {
  display: grid;
  gap: var(--space-2);
  margin-bottom: var(--space-5);
  padding: var(--space-5);
  overflow-wrap: anywhere;
  background: var(--color-surface-raised);
  border-radius: var(--radius-lg);
}

.tested-request-url span {
  color: var(--color-text-secondary);
}

.point-test-panel pre {
  max-height: 280px;
  padding: var(--space-5);
  overflow: auto;
  background: var(--color-surface-inset);
  border-radius: var(--radius-lg);
}
.point-write-controls {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-4-5);
  align-items: center;
  margin-top: var(--space-8);
}
.point-write-controls label {
  font-weight: var(--font-weight-strong);
}
.point-write-controls input {
  min-height: 42px;
  padding: var(--space-3) var(--space-4);
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-lg);
}

.runtime-panel > div:first-child,
.runtime-panel dl {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-5-5) var(--space-10);
  align-items: center;
}

.runtime-panel h2 {
  margin-right: auto;
}

.runtime-panel dl div {
  min-width: 130px;
}

.runtime-panel dt {
  color: var(--color-text-secondary);
  font-size: var(--font-size-md);
  font-weight: var(--font-weight-strong);
  text-transform: uppercase;
}

.runtime-panel dd {
  margin: var(--space-1-5) var(--space-0) var(--space-0);
  font-weight: var(--font-weight-semibold);
}

.runtime-diagnostic {
  padding: var(--space-5-5);
  background: var(--color-surface-subtle);
  border-radius: var(--radius-lg);
}
</style>
