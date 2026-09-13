import type { JSONSchema } from '@/components/yaml/MonacoYaml';
import schema from '@contracts/point-sources/point-source-v1.schema.json';

// The editor consumes the same published schema as external configuration tooling.
export const pointSourceSchema = schema as JSONSchema;
