import type { JSONSchema } from '@/components/yaml/MonacoYaml';
import contractSchema from '@contracts/point-sources/point-source-v1.schema.json';

type SchemaWithDefinitions = JSONSchema & { $defs?: Record<string, JSONSchema> };

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
    definitions.point!.description =
      'Point fields are validated in context from valueType and commandable.';
  }

  return schema;
};

// Validation remains equivalent to the published contract, with a completion-friendly
// representation of its discriminated source variants.
export const pointSourceSchema: JSONSchema = createEditorSchema();
