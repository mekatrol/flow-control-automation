<template>
  <section class="configuration-page editor-page">
    <AppErrorNotice id="point-source-error-notice" :message="error" />
    <nav aria-label="Breadcrumb">
      <RouterLink :to="{ name: 'point-sources' }">Point sources</RouterLink> /
      {{ isNew ? 'New source' : 'Edit source' }}
    </nav>

    <p v-if="isSpinnerVisible" role="status">Loading source…</p>
    <div v-else class="source-editor-layout" :class="{ 'has-guidance': isNew }">
      <form @submit.prevent="save">
        <header class="editor-toolbar">
          <div class="editor-actions">
            <AppConfigurationGuidance
              type="point-source"
              :yaml="yaml"
              :point-id="selectedPointId"
            />
            <AppButton
              type="submit"
              :text="saving ? 'Saving…' : 'Save'"
              :icon="saveIcon"
              :disabled="saving || hasEditorErrors"
            />
            <AppButton v-if="!isNew" text="Delete" :icon="deleteIcon" @click="remove" />
          </div>
          <section v-if="mappings.length" class="point-test" aria-labelledby="mapping-test-heading">
            <div class="point-test-heading">
              <p>Interactive test</p>
              <h2 id="mapping-test-heading">Test a mapping</h2>
            </div>
            <div class="point-test-actions">
              <label for="mapping-test-selection">Mapping</label>
              <select id="mapping-test-selection" v-model="selectedMappingId">
                <option v-for="mapping in mappings" :key="mapping.id" :value="mapping.id">
                  {{ mapping.id }}
                </option>
              </select>
              <AppButton
                :text="mappingTesting === 'read' ? 'Reading…' : 'Read mapping'"
                :icon="testConnectionIcon"
                :disabled="mappingTestDisabled || !selectedMapping?.read"
                @click="testMapping('read')"
              />
              <template v-if="selectedMapping?.command">
                <label for="mapping-test-payload">Command payload</label>
                <textarea id="mapping-test-payload" v-model="mappingPayload" rows="2"></textarea>
                <AppButton
                  :text="mappingTesting === 'command' ? 'Sending…' : 'Send payload'"
                  :icon="checkIcon"
                  :disabled="mappingTestDisabled || !mappingPayload.trim()"
                  @click="testMapping('command')"
                />
              </template>
            </div>
            <section v-if="mappingTestResult" class="point-test-result" aria-live="polite">
              <h3>
                {{ mappingTestResult.operation === 'read' ? 'Mapping read' : 'Mapping command' }}
                result
              </h3>
              <p>
                Mapping: <code>{{ mappingTestResult.mappingId }}</code>
              </p>
              <pre
                v-if="mappingTestResult.values"
                tabindex="0"
              ><code>{{ formatMappingValues(mappingTestResult.values) }}</code></pre>
              <p v-if="mappingTestResult.httpResponse?.requestUri">
                Request:
                <code
                  >{{ mappingTestResult.httpResponse.requestMethod }}
                  {{ mappingTestResult.httpResponse.requestUri }}</code
                >
              </p>
              <p v-if="mappingTestResult.httpResponse">
                Status: {{ mappingTestResult.httpResponse.statusCode }}
                {{ mappingTestResult.httpResponse.reasonPhrase }}
              </p>
              <pre
                v-if="mappingTestResult.renderedRequest"
                tabindex="0"
              ><code>{{ mappingTestResult.renderedRequest }}</code></pre>
              <pre
                v-if="mappingTestResult.httpResponse"
                tabindex="0"
              ><code>{{ mappingTestResult.httpResponse.body }}</code></pre>
              <p v-if="mappingTestResult.diagnostic">{{ mappingTestResult.diagnostic }}</p>
            </section>
            <p v-if="mappingTestError" class="request-error" role="alert">{{ mappingTestError }}</p>
          </section>
          <section
            v-if="nestedPoints.length"
            class="point-test"
            aria-labelledby="point-test-heading"
          >
            <div class="point-test-heading">
              <p>Interactive test</p>
              <h2 id="point-test-heading">Test a nested point</h2>
              <p>The server resolves the selected point's mapping and alias from this aggregate.</p>
            </div>
            <div class="point-test-actions">
              <label for="point-test-selection">Point</label>
              <select id="point-test-selection" v-model="selectedPointId">
                <option v-for="point in nestedPoints" :key="point.id" :value="point.id">
                  {{ point.name }} ({{ point.mapping }})
                </option>
              </select>
              <AppButton
                :text="pointTesting === 'read' ? 'Reading…' : 'Read point'"
                :icon="testConnectionIcon"
                :disabled="pointTestDisabled || !selectedPoint?.readable"
                @click="testPoint('read')"
              />
              <template v-if="pointCommandable">
                <label for="point-test-value">Value to write</label>
                <input id="point-test-value" v-model="writeValue" type="text" />
                <AppButton
                  :text="pointTesting === 'command' ? 'Commanding…' : 'Command point'"
                  :icon="checkIcon"
                  :disabled="pointTestDisabled || !writeValue.trim()"
                  @click="testPoint('command')"
                />
              </template>
              <p v-else class="readonly-note">This point is read-only.</p>
            </div>
            <section v-if="pointTestResult" class="point-test-result" aria-live="polite">
              <h3>{{ pointTestResult.operation === 'read' ? 'Read' : 'Command' }} result</h3>
              <p>
                Mapping: <code>{{ pointTestResult.mappingId }}/{{ pointTestResult.alias }}</code>
                <span v-if="pointTestResult.quality">
                  · Quality: {{ pointTestResult.quality }}</span
                >
              </p>
              <div class="point-value">
                <span>Point value</span><strong>{{ displayPointValue }}</strong>
              </div>
              <p v-if="pointTestResult.httpResponse?.requestUri">
                Request:
                <code
                  >{{ pointTestResult.httpResponse.requestMethod }}
                  {{ pointTestResult.httpResponse.requestUri }}</code
                >
              </p>
              <p v-if="pointTestResult.httpResponse">
                Status: {{ pointTestResult.httpResponse.statusCode }}
                {{ pointTestResult.httpResponse.reasonPhrase }}
              </p>
              <pre
                v-if="pointTestResult.renderedRequest"
                tabindex="0"
              ><code>{{ pointTestResult.renderedRequest }}</code></pre>
              <pre
                v-if="pointTestResult.httpResponse"
                tabindex="0"
              ><code>{{ pointTestResult.httpResponse.body }}</code></pre>
              <p v-if="pointTestResult.diagnostic">{{ pointTestResult.diagnostic }}</p>
            </section>
            <p v-if="pointTestError" class="request-error" role="alert">{{ pointTestError }}</p>
          </section>
        </header>
        <AppYamlEditor
          v-model="yaml"
          label="Point source YAML"
          help=""
          :schema="pointSourceSchema"
          schema-uri="app://schemas/point-source-v1.json"
          fill-available
          @[EVENTS.DIAGNOSTICS]="setEditorDiagnostics"
        />
      </form>

      <aside v-if="isNew" class="source-guidance" aria-labelledby="source-guidance-heading">
        <p>Configuration guide</p>
        <h2 id="source-guidance-heading">Start with an example</h2>
        <p>
          Select the system you want to read data from. Replace the example addresses and credential
          references with values for your system.
        </p>
        <fieldset>
          <legend>Source type</legend>
          <label v-for="option in sourceExamples" :key="option.kind">
            <input
              v-model="selectedExampleKind"
              type="radio"
              name="example-source-kind"
              :value="option.kind"
            />
            <span>
              <strong>{{ option.name }}</strong>
              <small>{{ option.summary }}</small>
            </span>
          </label>
        </fieldset>
        <h3>{{ selectedExample.name }} YAML</h3>
        <AppYamlEditor
          :model-value="selectedExample.yaml"
          :label="`${selectedExample.name} example YAML`"
          help="Read-only example for the selected point-source type."
          :schema="pointSourceSchema"
          schema-uri="app://schemas/point-source-example-v1.json"
          min-height="360px"
          read-only
        />
        <AppButton text="Use this example" :icon="checkIcon" @click="useSelectedExample" />
        <div v-if="selectedExample.kind === 'mqtt'" class="mqtt-credential-help">
          <h3>MQTT credentials</h3>
          <p>
            Create an MQTT username and password on the
            <RouterLink :to="{ name: 'credentials' }">Credentials screen</RouterLink>, then use its
            <code>secret://</code> reference:
          </p>
          <pre
            tabindex="0"
            aria-label="MQTT credential reference example"
          ><code>credentialRef: secret://plant-mqtt</code></pre>
          <p>
            The connection test logs in and makes a read-only subscription to
            <code>testTopic</code>. It does not publish or retain a message.
          </p>
          <p>
            Set <code>allowPrivateNetwork: true</code> only when the broker is intentionally hosted
            on your local network. Loopback and link-local destinations remain blocked.
          </p>
        </div>
        <p class="guidance-note">
          <strong>Credential safety:</strong> create secrets on the
          <RouterLink :to="{ name: 'credentials' }">Credentials screen</RouterLink>. Never paste a
          token or password into YAML.
        </p>
      </aside>
    </div>

    <p class="visually-hidden" role="status" aria-live="polite">{{ status }}</p>
  </section>
</template>

<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { parse } from 'yaml';
import { useSaveShortcut } from '@/composables/useSaveShortcut';
import { useSpinner } from '@/composables/useSpinner';
import { onBeforeRouteLeave, useRouter } from 'vue-router';
import checkIcon from '@/assets/icons/check-icon.svg';
import deleteIcon from '@/assets/icons/delete-flow-icon.svg';
import saveIcon from '@/assets/icons/save-icon.svg';
import testConnectionIcon from '@/assets/icons/test-connection-icon.svg';
import AppButton from '@/components/AppButton.vue';
import AppErrorNotice from '@/components/AppErrorNotice.vue';
import AppYamlEditor, { type YamlDiagnostic } from '@/components/AppYamlEditor.vue';
import AppConfigurationGuidance from '@/features/configuration/components/AppConfigurationGuidance.vue';
import { EVENTS } from '@/constants/events';
import {
  pointSourceApi,
  type MappingTestResult,
  type PointTestResult,
  type PointSourceKind
} from '@/features/pointSources/api/pointSourceApi';
import { formatPointTestValue } from '@/features/pointSources/formatPointTestValue';
import { pointSourceSchema } from '@/features/pointSources/pointSourceSchema';
import homeAssistantYaml from '@contracts/point-sources/valid/home-assistant.v1.yaml?raw';
import httpYaml from '@contracts/point-sources/valid/http.v1.yaml?raw';
import mqttYaml from '@contracts/point-sources/valid/mqtt.v1.yaml?raw';
import physicalYaml from '@contracts/point-sources/valid/physical.v1.yaml?raw';
import virtualYaml from '@contracts/point-sources/valid/virtual.v1.yaml?raw';

const props = defineProps<{ sourceId?: string }>();
const router = useRouter();
const isNew = computed(() => !props.sourceId);
interface SourceExample {
  kind: PointSourceKind;
  name: string;
  summary: string;
  yaml: string;
}
const sourceExamples: SourceExample[] = [
  {
    kind: 'virtual',
    name: 'Virtual',
    summary: 'Store source-owned runtime values without external communication.',
    yaml: virtualYaml
  },
  {
    kind: 'physical',
    name: 'Physical',
    summary: 'Bind controller channels and electrical I/O.',
    yaml: physicalYaml
  },
  {
    kind: 'homeAssistant',
    name: 'Home Assistant',
    summary: 'Read entities and subscribe to Home Assistant events.',
    yaml: homeAssistantYaml
  },
  {
    kind: 'mqtt',
    name: 'MQTT',
    summary: 'Connect to a broker for read-only topic subscriptions.',
    yaml: mqttYaml
  },
  {
    kind: 'http',
    name: 'HTTP / JSON',
    summary: 'Read and write points through a JSON web API.',
    yaml: httpYaml
  }
];
const selectedExampleKind = ref<PointSourceKind>('http');
const selectedExample = computed(
  () => sourceExamples.find(({ kind }) => kind === selectedExampleKind.value) ?? sourceExamples[2]!
);
const example = selectedExample.value.yaml;
const yaml = ref(example);
const baseline = ref(example);
const revision = ref(0);
const saving = ref(false);
const error = ref('');
const status = ref('');
const editorDiagnostics = ref<YamlDiagnostic[]>([]);
interface NestedPoint {
  id: string;
  name: string;
  mapping: string;
  valueType: string;
  units?: string;
  readable: boolean;
  commandable: boolean;
}
interface SourceMapping {
  id: string;
  read?: unknown;
  command?: unknown;
}
const selectedMappingId = ref('');
const mappingPayload = ref('');
const mappingTesting = ref<'read' | 'command'>();
const mappingTestResult = ref<MappingTestResult>();
const mappingTestError = ref('');
let mappingTestController: AbortController | undefined;
const selectedPointId = ref('');
const writeValue = ref('');
const pointTesting = ref<'read' | 'command'>();
const pointTestResult = ref<PointTestResult>();
const pointTestError = ref('');
let pointTestController: AbortController | undefined;
const setEditorDiagnostics = (diagnostics: YamlDiagnostic[]): void => {
  editorDiagnostics.value = diagnostics;
};
const hasEditorErrors = computed(() =>
  editorDiagnostics.value.some(({ severity }) => severity === 'error')
);
const parsedSource = computed(() => {
  try {
    return parse(yaml.value) as {
      kind?: string;
      mappings?: SourceMapping[];
      points?: NestedPoint[];
    };
  } catch {
    return undefined;
  }
});
const mappings = computed(() => parsedSource.value?.mappings ?? []);
const nestedPoints = computed(() => parsedSource.value?.points ?? []);
const selectedMapping = computed(() =>
  mappings.value.find(({ id }) => id === selectedMappingId.value)
);
watch(
  mappings,
  (items) => {
    if (!items.some(({ id }) => id === selectedMappingId.value)) {
      selectedMappingId.value = items[0]?.id ?? '';
    }
  },
  { immediate: true }
);
const selectedPoint = computed(() =>
  nestedPoints.value.find(({ id }) => id === selectedPointId.value)
);
watch(
  nestedPoints,
  (points) => {
    if (!points.some(({ id }) => id === selectedPointId.value)) {
      selectedPointId.value = points[0]?.id ?? '';
    }
  },
  { immediate: true }
);
const pointCommandable = computed(() => selectedPoint.value?.commandable === true);
const pointTestDisabled = computed(
  () => pointTesting.value !== undefined || hasEditorErrors.value || !selectedPoint.value
);
const mappingTestDisabled = computed(
  () => mappingTesting.value !== undefined || hasEditorErrors.value || !selectedMapping.value
);
const formatMappingValues = (values: Record<string, unknown>): string =>
  JSON.stringify(values, null, 2);
const displayPointValue = computed(() =>
  formatPointTestValue(pointTestResult.value?.value, selectedPoint.value?.units)
);
let loadController: AbortController | undefined;
let allowNavigation = false;
const { isSpinnerVisible, withSpinner } = useSpinner();
const dirty = computed(() => yaml.value !== baseline.value);
const useSelectedExample = (): void => {
  yaml.value = selectedExample.value.yaml;
  status.value = `${selectedExample.value.name} example loaded into the editor.`;
};
onBeforeRouteLeave(
  () => allowNavigation || !dirty.value || window.confirm('Discard unsaved point source changes?')
);
const load = async (): Promise<void> => {
  if (!props.sourceId) return;
  const controller = new AbortController();
  try {
    await withSpinner(
      () => {
        loadController?.abort();
        loadController = controller;
      },
      () => pointSourceApi.get(props.sourceId!, controller.signal),
      (result) => {
        if (loadController !== controller) return;
        yaml.value = baseline.value = result.yaml;
        revision.value = result.revision;
      }
    );
  } catch (reason) {
    if (loadController === controller && !controller.signal.aborted)
      error.value = reason instanceof Error ? reason.message : 'Unable to load source';
  } finally {
    if (loadController === controller) {
      loadController = undefined;
    }
  }
};
const save = async (): Promise<void> => {
  try {
    await withSpinner(
      () => {
        saving.value = true;
        error.value = '';
      },
      () =>
        props.sourceId
          ? pointSourceApi.update(props.sourceId, yaml.value, revision.value)
          : pointSourceApi.create(yaml.value),
      async (result) => {
        yaml.value = baseline.value = result.yaml;
        revision.value = result.revision;
        status.value = 'Point source saved.';
        if (!props.sourceId) {
          const match = yaml.value.match(/\bid:\s*([^\s]+)/);
          allowNavigation = true;
          await router.replace({
            name: 'point-source-detail',
            params: { sourceId: match?.[1] ?? '' }
          });
        }
      }
    );
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'Unable to save source';
  } finally {
    saving.value = false;
  }
};
useSaveShortcut(save, () => !isSpinnerVisible.value && !saving.value && !hasEditorErrors.value);
const testMapping = async (operation: 'read' | 'command'): Promise<void> => {
  const controller = new AbortController();
  try {
    let payload: unknown;
    if (operation === 'command') {
      try {
        payload = JSON.parse(mappingPayload.value);
      } catch {
        payload = mappingPayload.value;
      }
    }
    mappingTestController?.abort();
    mappingTestController = controller;
    mappingTesting.value = operation;
    mappingTestResult.value = undefined;
    mappingTestError.value = '';
    const result = props.sourceId
      ? await pointSourceApi.testSavedMapping(
          props.sourceId,
          selectedMappingId.value,
          operation,
          payload,
          controller.signal
        )
      : await pointSourceApi.testMapping(
          yaml.value,
          selectedMappingId.value,
          operation,
          payload,
          controller.signal
        );
    if (mappingTestController !== controller) return;
    mappingTestResult.value = result;
    status.value = `Mapping ${operation} test completed.`;
  } catch (reason) {
    if (mappingTestController === controller && !controller.signal.aborted)
      mappingTestError.value =
        reason instanceof Error ? reason.message : `Unable to ${operation} mapping`;
  } finally {
    if (mappingTestController === controller) {
      mappingTestController = undefined;
      mappingTesting.value = undefined;
    }
  }
};
const testPoint = async (operation: 'read' | 'command'): Promise<void> => {
  const controller = new AbortController();
  try {
    let value: unknown;
    if (operation === 'command') {
      try {
        value = JSON.parse(writeValue.value);
      } catch {
        value = writeValue.value;
      }
    }
    pointTestController?.abort();
    pointTestController = controller;
    pointTesting.value = operation;
    pointTestResult.value = undefined;
    pointTestError.value = '';
    const result = props.sourceId
      ? await pointSourceApi.testSavedPoint(
          props.sourceId,
          selectedPointId.value,
          operation,
          value,
          controller.signal
        )
      : await pointSourceApi.testPoint(
          yaml.value,
          selectedPointId.value,
          operation,
          value,
          controller.signal,
          { trackWait: false }
        );
    if (pointTestController !== controller) return;
    pointTestResult.value = result;
    status.value = `Point ${operation} test completed.`;
  } catch (reason) {
    if (pointTestController === controller && !controller.signal.aborted)
      pointTestError.value =
        reason instanceof Error ? reason.message : `Unable to ${operation} point`;
  } finally {
    if (pointTestController === controller) {
      pointTestController = undefined;
      pointTesting.value = undefined;
    }
  }
};
const remove = async (): Promise<void> => {
  if (!props.sourceId || !window.confirm('Delete this point source?')) return;
  try {
    await withSpinner(
      null,
      () => pointSourceApi.delete(props.sourceId!, revision.value),
      async () => {
        allowNavigation = true;
        await router.push({ name: 'point-sources' });
      }
    );
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'Unable to delete source';
  }
};
onMounted(() => void load());
onBeforeUnmount(() => {
  loadController?.abort();
  mappingTestController?.abort();
  pointTestController?.abort();
});
</script>

<style scoped lang="css">
.editor-page {
  height: 100dvh;
  overflow: hidden;
}

.source-editor-layout {
  display: flex;
  flex: 1;
  flex-direction: column;
  min-height: 0;
}

.editor-page,
.source-editor-layout > form {
  display: flex;
  flex-direction: column;
}

.source-editor-layout > form {
  flex: 1;
  min-width: 0;
  min-height: 0;
}

.editor-toolbar {
  position: sticky;
  z-index: 5;
  top: 0;
  flex: none;
  padding: var(--space-5) var(--space-0);
  background: var(--color-page-background);
  border-bottom: var(--border-width-default) solid var(--color-border-subtle);
}

.source-editor-layout > form > :deep(.yaml-editor) {
  padding-top: var(--space-5);
}

.source-editor-layout.has-guidance {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(320px, 0.46fr);
  grid-template-rows: minmax(0, 1fr);
  min-height: 0;
  gap: var(--space-14);
  align-items: stretch;
}

.source-guidance {
  position: sticky;
  top: 24px;
  padding: var(--space-11);
  background: var(--color-surface-subtle);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-2xl);
  max-height: 100%;
  overflow: auto;
}

.source-guidance h2 {
  margin: var(--space-2) var(--space-0) var(--space-3-5);
  font-size: var(--font-size-heading-md);
}

.source-guidance h3 {
  margin: var(--space-11) var(--space-0) var(--space-3-5);
  font-size: var(--font-size-2xl);
}

.source-guidance > p {
  line-height: 1.5;
}

.source-guidance fieldset {
  display: grid;
  gap: var(--space-3-5);
  margin: var(--space-10) var(--space-0) var(--space-0);
  padding: var(--space-0);
  border: var(--border-width-none);
}

.source-guidance legend {
  margin-bottom: var(--space-3-5);
  font-weight: var(--font-weight-strong);
}

.source-guidance fieldset label {
  display: flex;
  gap: var(--space-4-5);
  padding: var(--space-5);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-lg);
  cursor: pointer;
}

.source-guidance fieldset label:has(input:checked) {
  border-color: var(--color-focus-ring);
  box-shadow: var(--shadow-focus);
}

.source-guidance input {
  align-self: start;
  margin-top: var(--space-1);
}

.source-guidance small {
  display: block;
  margin-top: var(--space-1);
  color: var(--color-text-secondary);
  line-height: 1.35;
}

.source-guidance > .yaml-editor {
  margin-bottom: var(--space-5-5);
}

.source-guidance pre {
  max-height: 360px;
  margin: var(--space-0) var(--space-0) var(--space-5-5);
  padding: var(--space-6-5);
  overflow: auto;
  color: var(--color-text-primary);
  font-size: var(--font-size-md);
  line-height: 1.45;
  white-space: pre;
  background: var(--color-surface-inset);
  border: var(--border-width-default) solid var(--color-border-subtle);
  border-radius: var(--radius-lg);
}

.source-guidance .guidance-note {
  margin: var(--space-9) var(--space-0) var(--space-0);
  padding-top: var(--space-8);
  border-top: var(--border-width-default) solid var(--color-border-subtle);
}

.point-test {
  margin-top: var(--space-4);
  padding: var(--space-4);
  background: var(--color-surface-subtle);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-2xl);
}

.point-test-heading h2 {
  margin: var(--space-1) 0;
  font-size: var(--font-size-xl);
}
.point-test-heading p {
  margin: 0;
}
.point-test-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-4-5);
  align-items: center;
  margin-top: var(--space-3);
}
.point-test-actions label {
  font-weight: var(--font-weight-strong);
}
.point-test-actions input,
.point-test-actions textarea {
  min-height: 42px;
  padding: var(--space-3) var(--space-4);
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-lg);
}
.point-test-actions textarea {
  min-width: min(32rem, 100%);
  resize: vertical;
}
.readonly-note {
  color: var(--color-text-secondary);
}
.point-test-result {
  margin-top: var(--space-4);
  padding-top: var(--space-4);
  border-top: var(--border-width-default) solid var(--color-border-subtle);
}
.point-value {
  display: flex;
  gap: var(--space-5);
  align-items: baseline;
  padding: var(--space-5);
  background: var(--color-surface-raised);
  border-radius: var(--radius-lg);
}
.point-value span {
  color: var(--color-text-secondary);
}
.point-test-result pre {
  max-height: 280px;
  padding: var(--space-5);
  overflow: auto;
  background: var(--color-surface-inset);
  border-radius: var(--radius-lg);
}

/* Wide-tablet breakpoint (56.25rem): collapses editor columns before content becomes cramped. */
@media (max-width: 56.25rem) {
  .source-editor-layout.has-guidance {
    display: flex;
    flex-direction: column;
  }

  .source-guidance {
    position: static;
    order: -1;
  }
}
</style>
