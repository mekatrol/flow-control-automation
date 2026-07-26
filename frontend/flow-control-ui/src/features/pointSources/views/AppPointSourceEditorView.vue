<template>
  <section class="configuration-page editor-page">
    <nav aria-label="Breadcrumb">
      <RouterLink :to="{ name: 'point-sources' }">Point sources</RouterLink> /
      {{ isNew ? 'New source' : 'Edit source' }}
    </nav>
    <div class="page-heading">
      <div>
        <p class="eyebrow">YAML configuration</p>
        <h1>{{ isNew ? 'New point source' : 'Point source' }}</h1>
        <p>
          Credentials are managed separately and referenced as <code>secret://credential-id</code>.
        </p>
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
    </div>
    <p v-if="loading" role="status">Loading source…</p>
    <div v-else class="source-editor-layout" :class="{ 'has-guidance': isNew }">
      <form @submit.prevent="save">
        <AppYamlEditor
          v-model="yaml"
          automation="point-source-yaml-editor"
          label="Point source YAML"
          help="Errors and suggestions use the point-source schema. The server validates again when you test or save."
          :schema="pointSourceSchema"
          schema-uri="app://schemas/point-source-v1.json"
          min-height="650px"
          @diagnostics="editorDiagnostics = $event"
        />
        <div class="editor-actions">
          <AppButton
            automation="point-source-save"
            type="submit"
            :text="saving ? 'Saving…' : 'Save'"
            :icon="saveIcon"
            :disabled="saving || hasEditorErrors"
          />
          <AppButton
            automation="point-source-test-connection"
            :text="testing ? 'Testing…' : 'Test connection'"
            :icon="testConnectionIcon"
            :disabled="testing || hasEditorErrors"
            @click="testConnection"
          />
          <AppButton
            v-if="testing"
            automation="point-source-cancel-test"
            text="Cancel test"
            :icon="cancelIcon"
            @click="cancelTest"
          />
          <AppButton
            v-if="!isNew"
            automation="point-source-delete"
            text="Delete"
            :icon="deleteIcon"
            @click="remove"
          />
        </div>
      </form>

      <aside v-if="isNew" class="source-guidance" aria-labelledby="source-guidance-heading">
        <p class="eyebrow">Configuration guide</p>
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
        <AppButton
          automation="point-source-use-example"
          text="Use this example"
          :icon="checkIcon"
          @click="useSelectedExample"
        />
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

    <section v-if="testResult" class="test-result" aria-live="polite" aria-atomic="true">
      <h2>Connection test: {{ testResult.status }}</h2>
      <ol>
        <li v-for="stage in testResult.stages" :key="stage.name">
          <strong>{{ stage.name }}</strong
          >: {{ stage.status }}<span v-if="stage.diagnostic"> — {{ stage.diagnostic }}</span>
        </li>
      </ol>
      <AppButton
        v-if="testResult.status === 'failed'"
        automation="point-source-retry-test"
        text="Retry test"
        :icon="retryIcon"
        @click="testConnection"
      />
    </section>
    <p class="visually-hidden" role="status" aria-live="polite">{{ status }}</p>
  </section>
</template>

<script setup lang="ts">
import { computed, nextTick, onBeforeUnmount, onMounted, ref, watch } from 'vue';
import { onBeforeRouteLeave, useRouter } from 'vue-router';
import cancelIcon from '@/assets/icons/cancel-icon.svg';
import checkIcon from '@/assets/icons/check-icon.svg';
import deleteIcon from '@/assets/icons/delete-flow-icon.svg';
import retryIcon from '@/assets/icons/retry-icon.svg';
import saveIcon from '@/assets/icons/save-icon.svg';
import testConnectionIcon from '@/assets/icons/test-connection-icon.svg';
import AppButton from '@/components/AppButton.vue';
import AppYamlEditor, { type YamlDiagnostic } from '@/components/AppYamlEditor.vue';
import {
  pointSourceApi,
  type ConnectionTestResult,
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
    kind: 'home_assistant',
    name: 'Home Assistant',
    summary: 'Read entities and subscribe to Home Assistant events.',
    yaml: `schemaVersion: 1
sources:
  - id: home-assistant
    name: Home Assistant
    description: Home automation server
    enabled: true
    kind: home_assistant
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
    kind: 'http_json',
    name: 'HTTP / JSON',
    summary: 'Poll a read-only web API that returns JSON.',
    yaml: `schemaVersion: 1
sources:
  - id: new-source
    name: HTTP JSON API
    description: Read-only web API
    enabled: true
    kind: http_json
    connection:
      baseUrl: https://api.example.com
      allowedReadMethods: [GET]
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
const selectedExampleKind = ref<PointSourceKind>('http_json');
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
const editorDiagnostics = ref<YamlDiagnostic[]>([]);
const hasEditorErrors = computed(() =>
  editorDiagnostics.value.some(({ severity }) => severity === 'error')
);
const errorSummary = ref<HTMLElement>();
let loadController: AbortController | undefined;
let testController: AbortController | undefined;
let allowNavigation = false;
const dirty = computed(() => yaml.value !== baseline.value);
const useSelectedExample = (): void => {
  yaml.value = selectedExample.value.yaml;
  testResult.value = undefined;
  status.value = `${selectedExample.value.name} example loaded into the editor.`;
};
watch(error, async (value) => {
  if (value) {
    await nextTick();
    errorSummary.value?.focus();
  }
});
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
const testConnection = async (): Promise<void> => {
  testController?.abort();
  testController = new AbortController();
  testing.value = true;
  error.value = '';
  testResult.value = undefined;
  status.value = 'Connection test started.';
  try {
    testResult.value = await pointSourceApi.test(yaml.value, testController.signal);
    status.value = `Connection test ${testResult.value.status}.`;
  } catch (reason) {
    if (testController.signal.aborted) status.value = 'Connection test cancelled.';
    else error.value = reason instanceof Error ? reason.message : 'Connection test failed';
  } finally {
    testing.value = false;
  }
};
const cancelTest = (): void => testController?.abort();
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
});
</script>
