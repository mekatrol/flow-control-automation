import EditorWorker from 'monaco-editor/esm/vs/editor/editor.worker?worker';
import * as monaco from 'monaco-editor/esm/vs/editor/editor.main';
import 'monaco-editor/esm/vs/basic-languages/yaml/yaml.contribution';
import YamlWorker from '@/components/yaml/YamlWorker?worker';
import { configureMonacoYaml, type JSONSchema, type MonacoYaml } from 'monaco-yaml';

import 'monaco-editor/min/vs/editor/editor.main.css';

type MonacoEnvironment = {
  getWorker: (_moduleId: string, label: string) => Worker;
};

const environment = globalThis as typeof globalThis & {
  MonacoEnvironment?: MonacoEnvironment;
};

environment.MonacoEnvironment = {
  getWorker(_moduleId, label) {
    if (label === 'yaml') return new YamlWorker();
    return new EditorWorker();
  }
};

let yamlService: MonacoYaml | undefined;
const registeredSchemas = new Map<
  string,
  { uri: string; fileMatch: string[]; schema: JSONSchema }
>();

export const configureYamlSchema = async (
  schemaUri: string,
  modelUri: string,
  schema: JSONSchema
): Promise<void> => {
  registeredSchemas.set(modelUri, { uri: schemaUri, fileMatch: [modelUri], schema });
  const options = {
    completion: true,
    enableSchemaRequest: false,
    format: { enable: true, printWidth: 100, proseWrap: 'preserve' as const },
    hover: true,
    schemas: [...registeredSchemas.values()],
    validate: true,
    yamlVersion: '1.2' as const
  };
  if (yamlService) {
    await yamlService.update(options);
  } else {
    yamlService = configureMonacoYaml(monaco, options);
  }
};

export const removeYamlSchema = async (modelUri: string): Promise<void> => {
  registeredSchemas.delete(modelUri);
  await yamlService?.update({ schemas: [...registeredSchemas.values()] });
};

export { monaco };
export type { JSONSchema };
