<template>
  <div class="yaml-editor" v-bind="automation()">
    <label :id="labelId">{{ label }}</label>
    <div
      ref="container"
      class="yaml-editor-surface"
      :style="{ minHeight }"
      role="group"
      :aria-labelledby="labelId"
    ></div>
    <p :id="helpId" class="yaml-editor-help">{{ help }}</p>
    <section
      v-if="diagnostics.length > 0"
      class="yaml-diagnostics"
      aria-labelledby="diagnosticsHeadingId"
    >
      <h3 :id="diagnosticsHeadingId">
        {{ diagnostics.length }} YAML {{ diagnostics.length === 1 ? 'problem' : 'problems' }}
      </h3>
      <ol>
        <li v-for="diagnostic in diagnostics" :key="diagnostic.key">
          <button type="button" @click="revealDiagnostic(diagnostic)">
            Line {{ diagnostic.line }}, column {{ diagnostic.column }}:
            {{ diagnostic.message }}
          </button>
        </li>
      </ol>
    </section>
  </div>
</template>

<script setup lang="ts">
import { onBeforeUnmount, onMounted, ref, watch } from 'vue';

import { useAutomation } from '@/composables/useAutomation';
import {
  configureYamlSchema,
  type JSONSchema,
  monaco,
  removeYamlSchema
} from '@/components/yaml/MonacoYaml';

export interface YamlDiagnostic {
  key: string;
  message: string;
  line: number;
  column: number;
  severity: 'error' | 'warning';
}

const props = withDefaults(
  defineProps<{
    modelValue: string;
    automation: string;
    label: string;
    help?: string;
    schema: JSONSchema;
    schemaUri: string;
    minHeight?: string;
    readOnly?: boolean;
  }>(),
  {
    help: 'Use Ctrl+Space for suggestions and Shift+Alt+F to format the document.',
    minHeight: '560px',
    readOnly: false
  }
);
const automation = useAutomation(props.automation);
const emit = defineEmits<{
  'update:modelValue': [value: string];
  diagnostics: [diagnostics: YamlDiagnostic[]];
}>();

const editorId = Math.random().toString(36).slice(2);
const labelId = `yaml-editor-label-${editorId}`;
const helpId = `yaml-editor-help-${editorId}`;
const diagnosticsHeadingId = `yaml-editor-diagnostics-${editorId}`;
const modelUri = `file:///configuration-${editorId}.yaml`;
const container = ref<HTMLElement>();
const diagnostics = ref<YamlDiagnostic[]>([]);
let editor: monaco.editor.IStandaloneCodeEditor | undefined;
let model: monaco.editor.ITextModel | undefined;
let markerSubscription: monaco.IDisposable | undefined;
let contentSubscription: monaco.IDisposable | undefined;
let themeObserver: MutationObserver | undefined;

const applyTheme = (): void => {
  const preference = document.documentElement.dataset.theme;
  const systemDark = globalThis.matchMedia?.('(prefers-color-scheme: dark)').matches ?? false;
  monaco.editor.setTheme(preference === 'dark' || (!preference && systemDark) ? 'vs-dark' : 'vs');
};

const updateDiagnostics = (): void => {
  if (!model) return;
  diagnostics.value = monaco.editor
    .getModelMarkers({ resource: model.uri })
    .filter(({ severity }) =>
      [monaco.MarkerSeverity.Error, monaco.MarkerSeverity.Warning].includes(severity)
    )
    .map((marker) => ({
      key: `${marker.startLineNumber}:${marker.startColumn}:${marker.message}`,
      message: marker.message,
      line: marker.startLineNumber,
      column: marker.startColumn,
      severity:
        marker.severity === monaco.MarkerSeverity.Error ? ('error' as const) : ('warning' as const)
    }));
  emit('diagnostics', diagnostics.value);
};

const revealDiagnostic = (diagnostic: YamlDiagnostic): void => {
  editor?.setPosition({ lineNumber: diagnostic.line, column: diagnostic.column });
  editor?.revealPositionInCenter({ lineNumber: diagnostic.line, column: diagnostic.column });
  editor?.focus();
};

watch(
  () => props.modelValue,
  (value) => {
    if (model && model.getValue() !== value) model.setValue(value);
  }
);

watch(
  () => props.readOnly,
  (readOnly) => editor?.updateOptions({ readOnly })
);

onMounted(async () => {
  await configureYamlSchema(props.schemaUri, modelUri, props.schema);
  // Route navigation may finish while the lazily loaded language worker is
  // starting. Do not attach an editor to a component that has since unmounted.
  if (!container.value) {
    await removeYamlSchema(modelUri);
    return;
  }
  model = monaco.editor.createModel(props.modelValue, 'yaml', monaco.Uri.parse(modelUri));
  editor = monaco.editor.create(container.value, {
    model,
    ariaLabel: props.label,
    automaticLayout: true,
    fontSize: 14,
    lineNumbersMinChars: 3,
    minimap: { enabled: false },
    readOnly: props.readOnly,
    scrollBeyondLastLine: false,
    tabSize: 2,
    wordWrap: 'on'
  });
  contentSubscription = model.onDidChangeContent(() =>
    emit('update:modelValue', model!.getValue())
  );
  markerSubscription = monaco.editor.onDidChangeMarkers((resources) => {
    if (model && resources.some((resource) => resource.toString() === model!.uri.toString())) {
      updateDiagnostics();
    }
  });
  themeObserver = new MutationObserver(applyTheme);
  themeObserver.observe(document.documentElement, {
    attributes: true,
    attributeFilter: ['data-theme']
  });
  applyTheme();
});

onBeforeUnmount(() => {
  themeObserver?.disconnect();
  markerSubscription?.dispose();
  contentSubscription?.dispose();
  editor?.dispose();
  model?.dispose();
  void removeYamlSchema(modelUri);
});
</script>

<style scoped>
.yaml-editor > label {
  display: block;
  margin-bottom: 7px;
  font-weight: 700;
}

.yaml-editor-surface {
  width: 100%;
  overflow: hidden;
  border: 1px solid var(--color-border-default);
  border-radius: 8px;
}

.yaml-editor-help {
  color: var(--color-text-secondary);
  font-size: 13px;
}

.yaml-diagnostics {
  margin-top: 12px;
  padding: 14px;
  border: 1px solid var(--color-danger-border);
  border-radius: 8px;
}

.yaml-diagnostics h3 {
  margin: 0 0 8px;
  font-size: 14px;
}

.yaml-diagnostics ol {
  display: grid;
  gap: 5px;
  margin: 0;
  padding-left: 22px;
}

.yaml-diagnostics button {
  padding: 2px;
  text-align: left;
  background: transparent;
  border: 0;
}
</style>
