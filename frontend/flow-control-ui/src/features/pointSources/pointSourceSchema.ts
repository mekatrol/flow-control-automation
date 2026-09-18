import type { JSONSchema } from '@/components/yaml/MonacoYaml';
import contractSchema from '@contracts/point-sources/point-source-v1.schema.json';

type SchemaWithDefinitions = JSONSchema & { $defs?: Record<string, JSONSchema> };

const forbiddenProperties = (schema: JSONSchema): string[] | undefined => {
  if (schema.required?.length === 1) return schema.required;
  if (!schema.anyOf?.length) return undefined;

  const names = schema.anyOf.map((branch) =>
    typeof branch === 'object' && branch.required?.length === 1 ? branch.required[0] : undefined
  );
  return names.every((name): name is string => name !== undefined) ? names : undefined;
};

const improveForbiddenPropertyDiagnostics = (value: unknown): void => {
  if (!value || typeof value !== 'object') return;
  if (Array.isArray(value)) {
    value.forEach(improveForbiddenPropertyDiagnostics);
    return;
  }

  const schema = value as JSONSchema;
  if (typeof schema.not === 'object') {
    const names = forbiddenProperties(schema.not);
    if (names) {
      schema.properties ??= {};
      names.forEach((name) => (schema.properties![name] = false));
      delete schema.not;
    }
  }

  Object.values(schema).forEach(improveForbiddenPropertyDiagnostics);
};

/*
 * The contract uses allOf/if/then because that is a concise way to validate each
 * source kind. Completion engines can validate that shape correctly, but tend to
 * merge the broad base schema into every completion list. Presenting the same
 * rules as discriminated oneOf branches lets the YAML language service select a
 * branch from `kind` and only suggest fields for that source type.
 */
const createEditorSchema = (): SchemaWithDefinitions => {
  const schema = structuredClone(contractSchema) as unknown as SchemaWithDefinitions;
  const conditionalBranches = schema.allOf?.filter(
    (branch): branch is JSONSchema => typeof branch === 'object'
  );

  if (conditionalBranches?.length) {
    schema.oneOf = conditionalBranches.map(({ if: condition, then }) => ({
      allOf: [condition ?? true, then ?? true]
    }));
    delete schema.allOf;
  }

  const definitions = schema.$defs;
  if (definitions) {
    definitions.connection!.description =
      'Connection fields are narrowed to the protocol selected by the top-level kind.';
    definitions.homeAssistantConnection!.description = 'Home Assistant connection settings.';
    definitions.mqttConnection!.description = 'MQTT broker connection settings.';
    definitions.httpConnection!.description = 'HTTP endpoint connection settings.';
    definitions.digitalLabels!.defaultSnippets = [
      {
        label: 'Digital state labels',
        description: 'Labels for the false and true states.',
        // monaco-yaml emits object snippet keys verbatim. Include the quotes so
        // YAML 1.2 does not parse these property names as booleans.
        body: { '"false"': 'Off', '"true"': 'On' }
      }
    ];
    definitions.point!.description =
      'Point fields are validated in context from valueType and commandable.';
  }

  // `not: { required: [...] }` is valid JSON Schema, but the YAML language
  // service reports it as the unhelpful "Matches a schema that is not allowed".
  // A false property schema is equivalent for these single-property rules and
  // produces "Property <name> is not allowed" at the offending key instead.
  improveForbiddenPropertyDiagnostics(schema);

  return schema;
};

// Validation remains equivalent to the published contract, with a completion-friendly
// representation of its discriminated source variants.
export const pointSourceSchema: JSONSchema = createEditorSchema();
