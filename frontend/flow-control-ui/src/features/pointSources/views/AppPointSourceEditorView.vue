<template>
  <section class="configuration-page editor-page">
    <AppErrorNotice id="point-source-error-notice" :message="error" />
    <nav aria-label="Breadcrumb">
      <RouterLink :to="{ name: 'point-sources' }">Point sources</RouterLink> /
      {{ isNew ? 'New source' : 'Edit source' }}
    </nav>
    <div class="page-heading">
      <div>
        <p>YAML configuration</p>
        <h1>{{ isNew ? 'New point source' : 'Point source' }}</h1>
        <p>
          Credentials are managed separately and referenced as <code>secret://credential-id</code>.
        </p>
      </div>
    </div>

    <p v-if="loading" role="status">Loading source…</p>
    <div v-else class="source-editor-layout" :class="{ 'has-guidance': isNew }">
      <form @submit.prevent="save">
        <div class="editor-actions">
          <AppButton
            type="submit"
            :text="saving ? 'Saving…' : 'Save'"
            :icon="saveIcon"
            :disabled="saving || hasEditorErrors"
          />
          <AppButton
            :text="testing ? 'Testing…' : 'Test connection'"
            :icon="testConnectionIcon"
            :disabled="testing || hasEditorErrors"
            @click="testConnection"
          />
          <AppButton v-if="testing" text="Cancel test" :icon="cancelIcon" @click="cancelTest" />
          <AppButton v-if="!isNew" text="Delete" :icon="deleteIcon" @click="remove" />
        </div>
        <section
          v-if="testing || testResult || testError"
          class="test-result"
          aria-live="polite"
          aria-atomic="true"
        >
          <h2 v-if="testing">Connection test in progress…</h2>
          <template v-else-if="testResult">
            <h2>Connection test: {{ testResult.status }}</h2>
            <p>Completed in {{ testResult.durationMilliseconds }} ms.</p>
            <ol>
              <li v-for="stage in testResult.stages" :key="stage.name">
                <strong>{{ stage.name }}</strong
                >: {{ stage.status }}<span v-if="stage.diagnostic"> — {{ stage.diagnostic }}</span>
              </li>
            </ol>
            <section v-if="testResult.httpResponse" class="http-response-preview">
              <h3>HTTP response</h3>
              <p>
                Status: {{ testResult.httpResponse.statusCode }}
                {{ testResult.httpResponse.reasonPhrase }}
              </p>
              <p v-if="testResult.httpResponse.contentType">
                Content-Type: {{ testResult.httpResponse.contentType }}
              </p>
              <pre tabindex="0"><code>{{ testResult.httpResponse.body }}</code></pre>
            </section>
            <AppButton
              v-if="testResult.status === 'failed'"
              text="Retry test"
              :icon="retryIcon"
              @click="testConnection"
            />
          </template>
          <template v-else>
            <h2>Connection test: failed</h2>
            <p>{{ testError }}</p>
            <AppButton text="Retry test" :icon="retryIcon" @click="testConnection" />
          </template>
        </section>
        <AppYamlEditor
          v-model="yaml"
          label="Point source YAML"
          help="Errors and suggestions use the point-source schema. The server validates again when you test or save."
          :schema="pointSourceSchema"
          schema-uri="app://schemas/point-source-v1.json"
          min-height="650px"
          @[EVENTS.DIAGNOSTICS]="setEditorDiagnostics"
        />
        <section v-if="isHttpJson" class="point-test" aria-labelledby="point-test-heading">
          <div class="point-test-heading">
            <div>
              <p>Interactive test</p>
              <h2 id="point-test-heading">Test an HTTP JSON point</h2>
              <p>The point mapping below is combined with the unsaved source YAML above.</p>
            </div>
          </div>
          <AppYamlEditor
            v-model="pointYaml"
            label="Test point YAML"
            help="Define one point and its HTTP path, method, and JSON pointer. Testing does not save it."
            :schema="pointTestSchema"
            schema-uri="app://schemas/http-json-point-test-v1.json"
            min-height="390px"
            @[EVENTS.DIAGNOSTICS]="setPointDiagnostics"
          />
          <div class="point-test-actions">
            <AppButton
              :text="pointTesting === 'read' ? 'Reading…' : 'Read point'"
              :icon="testConnectionIcon"
              :disabled="pointTestDisabled"
              @click="testPoint('read')"
            />
            <template v-if="pointCommandable">
              <label for="point-test-value">Value to write</label>
              <input id="point-test-value" v-model="writeValue" type="text" />
              <AppButton
                :text="pointTesting === 'write' ? 'Writing…' : 'Write point'"
                :icon="checkIcon"
                :disabled="pointTestDisabled || !writeValue.trim()"
                @click="testPoint('write')"
              />
            </template>
            <p v-else class="readonly-note">This point is read-only.</p>
          </div>
          <section v-if="pointTestResult" class="point-test-result" aria-live="polite">
            <h3>{{ pointTestResult.operation === 'read' ? 'Read' : 'Write' }} result</h3>
            <div class="point-value">
              <span>Point value</span>
              <strong>{{ displayPointValue }}</strong>
            </div>
            <h4>HTTP response</h4>
            <p>
              Status: {{ pointTestResult.httpResponse.statusCode }}
              {{ pointTestResult.httpResponse.reasonPhrase }}
            </p>
            <p v-if="pointTestResult.httpResponse.contentType">
              Content-Type: {{ pointTestResult.httpResponse.contentType }}
            </p>
            <pre tabindex="0"><code>{{ pointTestResult.httpResponse.body }}</code></pre>
          </section>
          <p v-if="pointTestError" class="request-error" role="alert">{{ pointTestError }}</p>
        </section>
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
        <pre
          tabindex="0"
          :aria-label="`${selectedExample.name} example YAML`"
        ><code>{{ selectedExample.yaml }}</code></pre>
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
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import { parse } from 'yaml';
import { useSaveShortcut } from '@/composables/useSaveShortcut';
import { onBeforeRouteLeave, useRouter } from 'vue-router';
import cancelIcon from '@/assets/icons/cancel-icon.svg';
import checkIcon from '@/assets/icons/check-icon.svg';
import deleteIcon from '@/assets/icons/delete-flow-icon.svg';
import retryIcon from '@/assets/icons/retry-icon.svg';
import saveIcon from '@/assets/icons/save-icon.svg';
import testConnectionIcon from '@/assets/icons/test-connection-icon.svg';
import AppButton from '@/components/AppButton.vue';
import AppErrorNotice from '@/components/AppErrorNotice.vue';
import AppYamlEditor, { type YamlDiagnostic } from '@/components/AppYamlEditor.vue';
import { EVENTS } from '@/constants/events';
import {
  pointSourceApi,
  type ConnectionTestResult,
  type PointTestResult,
  type PointSourceKind
} from '@/features/pointSources/api/pointSourceApi';
import { pointSourceSchema } from '@/features/pointSources/pointSourceSchema';

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
    kind: 'homeAssistant',
    name: 'Home Assistant',
    summary: 'Read entities and subscribe to Home Assistant events.',
    yaml: `schemaVersion: 1
sources:
  - id: home-assistant
    name: Home Assistant
    description: Home automation server
    enabled: true
    kind: homeAssistant
    connection:
      baseUrl: https://homeassistant.local:8123
      subscribeEvents: true
    credentialRef: secret://home-assistant
    tls:
      verifyServerCertificate: true
    timeouts:
      connectMilliseconds: 2000
      requestMilliseconds: 5000
`
  },
  {
    kind: 'mqtt',
    name: 'MQTT',
    summary: 'Connect to a broker for read-only topic subscriptions.',
    yaml: `schemaVersion: 1
sources:
  - id: plant-mqtt
    name: Plant MQTT
    description: Read-only telemetry broker
    enabled: true
    kind: mqtt
    connection:
      brokerUrl: mqtts://mqtt.example.com:8883
      clientIdPrefix: flow-control
      testTopic: plant/telemetry/temperature
      allowPrivateNetwork: true
      qos: 1
      cleanStart: true
      keepAliveSeconds: 30
    credentialRef: secret://plant-mqtt
    tls:
      verifyServerCertificate: true
    timeouts:
      connectMilliseconds: 3000
`
  },
  {
    kind: 'httpJson',
    name: 'HTTP / JSON',
    summary: 'Read and write points through a JSON web API.',
    yaml: `schemaVersion: 1
sources:
  - id: new-source
    name: HTTP JSON API
    description: Read-only web API
    enabled: true
    kind: httpJson
    connection:
      baseUrl: https://api.example.com
      allowedReadMethods: [GET]
      allowedWriteMethods: [PUT, POST, PATCH]
      defaultPollMilliseconds: 60000
      followRedirects: false
      maximumResponseBytes: 65536
    tls:
      verifyServerCertificate: true
    timeouts:
      connectMilliseconds: 2000
      requestMilliseconds: 5000
`
  }
];
const selectedExampleKind = ref<PointSourceKind>('httpJson');
const selectedExample = computed(
  () => sourceExamples.find(({ kind }) => kind === selectedExampleKind.value) ?? sourceExamples[2]!
);
const example = selectedExample.value.yaml;
const yaml = ref(example);
const baseline = ref(example);
const revision = ref(0);
const loading = ref(false);
const saving = ref(false);
const testing = ref(false);
const error = ref('');
const status = ref('');
const testResult = ref<ConnectionTestResult>();
const testError = ref('');
const editorDiagnostics = ref<YamlDiagnostic[]>([]);
const pointDiagnostics = ref<YamlDiagnostic[]>([]);
const pointYaml = ref(`schemaVersion: 1
groups: []
points:
  - id: test-temperature
    name: Test temperature
    enabled: true
    implementation: bound
    direction: input
    valueType: analog
    units: celsius
    readable: true
    commandable: false
    persistence: volatile
    sourceId: new-source
    mapping:
      path: /temperature
      method: GET
      jsonPointer: /value
`);
const writeValue = ref('');
const pointTesting = ref<'read' | 'write'>();
const pointTestResult = ref<PointTestResult>();
const pointTestError = ref('');
let pointTestController: AbortController | undefined;
const setPointDiagnostics = (diagnostics: YamlDiagnostic[]): void => {
  pointDiagnostics.value = diagnostics;
};
const setEditorDiagnostics = (diagnostics: YamlDiagnostic[]): void => {
  editorDiagnostics.value = diagnostics;
};
const hasEditorErrors = computed(() =>
  editorDiagnostics.value.some(({ severity }) => severity === 'error')
);
const parsedSource = computed(() => {
  try {
    return parse(yaml.value) as { sources?: { kind?: string }[] };
  } catch {
    return undefined;
  }
});
const parsedPoint = computed(() => {
  try {
    return parse(pointYaml.value) as { points?: { commandable?: boolean; valueType?: string }[] };
  } catch {
    return undefined;
  }
});
const isHttpJson = computed(() => parsedSource.value?.sources?.[0]?.kind === 'httpJson');
const pointCommandable = computed(() => parsedPoint.value?.points?.[0]?.commandable === true);
const pointTestDisabled = computed(
  () =>
    pointTesting.value !== undefined ||
    hasEditorErrors.value ||
    pointDiagnostics.value.some(({ severity }) => severity === 'error')
);
const displayPointValue = computed(() => {
  const value = pointTestResult.value?.value;
  return typeof value === 'string' ? value : JSON.stringify(value);
});
const pointTestSchema = {
  ...pointSourceSchema,
  required: ['schemaVersion', 'groups', 'points'],
  properties: {
    schemaVersion: { const: 1 },
    groups: { type: 'array', maxItems: 0 },
    points: {
      type: 'array',
      minItems: 1,
      maxItems: 1,
      items: {
        type: 'object',
        required: [
          'id',
          'name',
          'enabled',
          'implementation',
          'direction',
          'valueType',
          'readable',
          'commandable',
          'persistence',
          'sourceId',
          'mapping'
        ],
        properties: {
          id: { type: 'string' },
          name: { type: 'string' },
          enabled: { type: 'boolean' },
          implementation: { const: 'bound' },
          direction: { enum: ['input', 'output', 'inputOutput'] },
          valueType: { enum: ['analog', 'digital', 'multiState', 'integer', 'text'] },
          units: { type: 'string' },
          readable: { type: 'boolean' },
          commandable: { type: 'boolean' },
          persistence: { enum: ['volatile', 'retained'] },
          sourceId: { type: 'string' },
          mapping: {
            type: 'object',
            required: ['path', 'method'],
            properties: {
              path: { type: 'string', pattern: '^/' },
              method: { enum: ['GET', 'HEAD', 'POST', 'PUT', 'PATCH'] },
              jsonPointer: { type: 'string' },
              valuePointer: { type: 'string' }
            }
          }
        }
      }
    }
  }
};
let loadController: AbortController | undefined;
let testController: AbortController | undefined;
let allowNavigation = false;
const dirty = computed(() => yaml.value !== baseline.value);
const useSelectedExample = (): void => {
  yaml.value = selectedExample.value.yaml;
  testResult.value = undefined;
  testError.value = '';
  status.value = `${selectedExample.value.name} example loaded into the editor.`;
};
onBeforeRouteLeave(
  () => allowNavigation || !dirty.value || window.confirm('Discard unsaved point source changes?')
);
const load = async (): Promise<void> => {
  if (!props.sourceId) return;
  loadController = new AbortController();
  loading.value = true;
  try {
    const result = await pointSourceApi.get(props.sourceId, loadController.signal);
    yaml.value = baseline.value = result.yaml;
    revision.value = result.revision;
  } catch (reason) {
    if (!loadController.signal.aborted)
      error.value = reason instanceof Error ? reason.message : 'Unable to load source';
  } finally {
    loading.value = false;
  }
};
const save = async (): Promise<void> => {
  saving.value = true;
  error.value = '';
  try {
    const result = props.sourceId
      ? await pointSourceApi.update(props.sourceId, yaml.value, revision.value)
      : await pointSourceApi.create(yaml.value);
    yaml.value = baseline.value = result.yaml;
    revision.value = result.revision;
    status.value = 'Point source saved.';
    if (!props.sourceId) {
      const match = yaml.value.match(/\bid:\s*([^\s]+)/);
      allowNavigation = true;
      await router.replace({ name: 'point-source-detail', params: { sourceId: match?.[1] ?? '' } });
    }
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'Unable to save source';
  } finally {
    saving.value = false;
  }
};
useSaveShortcut(save, () => !loading.value && !saving.value && !hasEditorErrors.value);
const testConnection = async (): Promise<void> => {
  testController?.abort();
  testController = new AbortController();
  testing.value = true;
  error.value = '';
  testError.value = '';
  testResult.value = undefined;
  status.value = 'Connection test started.';
  try {
    testResult.value = await pointSourceApi.test(yaml.value, testController.signal);
    status.value = `Connection test ${testResult.value.status}.`;
  } catch (reason) {
    if (testController.signal.aborted) {
      status.value = 'Connection test cancelled.';
      testError.value = 'The connection test was cancelled.';
    } else {
      testError.value = reason instanceof Error ? reason.message : 'Connection test failed';
      status.value = `Connection test failed: ${testError.value}`;
    }
  } finally {
    testing.value = false;
  }
};
const cancelTest = (): void => testController?.abort();
const testPoint = async (operation: 'read' | 'write'): Promise<void> => {
  pointTestController?.abort();
  pointTestController = new AbortController();
  pointTesting.value = operation;
  pointTestResult.value = undefined;
  pointTestError.value = '';
  try {
    let value: unknown;
    if (operation === 'write') {
      try {
        value = JSON.parse(writeValue.value);
      } catch {
        value = writeValue.value;
      }
    }
    pointTestResult.value = await pointSourceApi.testPoint(
      yaml.value,
      pointYaml.value,
      operation,
      value,
      pointTestController.signal,
      { trackWait: false }
    );
    status.value = `Point ${operation} test completed.`;
  } catch (reason) {
    if (!pointTestController.signal.aborted)
      pointTestError.value =
        reason instanceof Error ? reason.message : `Unable to ${operation} point`;
  } finally {
    pointTesting.value = undefined;
  }
};
const remove = async (): Promise<void> => {
  if (!props.sourceId || !window.confirm('Delete this point source?')) return;
  try {
    await pointSourceApi.delete(props.sourceId, revision.value);
    allowNavigation = true;
    await router.push({ name: 'point-sources' });
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'Unable to delete source';
  }
};
onMounted(() => void load());
onBeforeUnmount(() => {
  loadController?.abort();
  testController?.abort();
  pointTestController?.abort();
});
</script>

<style scoped lang="css">
.source-editor-layout.has-guidance {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(320px, 0.46fr);
  gap: var(--space-14);
  align-items: start;
}

.source-guidance {
  position: sticky;
  top: 24px;
  padding: var(--space-11);
  background: var(--color-surface-subtle);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-2xl);
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

.test-result {
  margin-top: var(--space-14);
  padding: var(--space-10);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-xl);
}

.point-test {
  margin-top: var(--space-14);
  padding: var(--space-10);
  background: var(--color-surface-subtle);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-2xl);
}

.point-test-heading h2 {
  margin: var(--space-2) 0 var(--space-3);
}
.point-test-actions {
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-4-5);
  align-items: center;
  margin-top: var(--space-7);
}
.point-test-actions label {
  font-weight: var(--font-weight-strong);
}
.point-test-actions input {
  min-height: 42px;
  padding: var(--space-3) var(--space-4);
  color: var(--color-text-primary);
  background: var(--color-surface-raised);
  border: var(--border-width-default) solid var(--color-border-default);
  border-radius: var(--radius-lg);
}
.readonly-note {
  color: var(--color-text-secondary);
}
.point-test-result {
  margin-top: var(--space-8);
  padding-top: var(--space-7);
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
