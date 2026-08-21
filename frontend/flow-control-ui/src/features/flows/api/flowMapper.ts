import type { FlowDefinition } from '@/features/flows/types';
import type { FlowDto } from './flowDto';

// API data and editable data must not share nested objects. Editing a connector or
// endpoint in the designer must not mutate the server-confirmed baseline used for
// dirty-state comparison and explicit discard.
const copyFlow = (flow: FlowDefinition | FlowDto): FlowDto => ({
  ...flow,
  ...(flow.revision !== undefined ? { revision: flow.revision } : {}),
  ...(flow.virtualPointDeclarations !== undefined
    ? { virtualPointDeclarations: flow.virtualPointDeclarations.map((entry) => ({ ...entry })) }
    : {}),
  interface: {
    schemaVersion: 1,
    inputs: flow.interface.inputs.map((entry) => ({ ...entry })),
    outputs: flow.interface.outputs.map((entry) => ({ ...entry }))
  },
  nodes: flow.nodes.map((node) => {
    const reference = node.configuration.interfaceId;
    const entry =
      node.kind === 'flowInput'
        ? flow.interface.inputs.find((candidate) => candidate.id === reference)
        : node.kind === 'flowOutput'
          ? flow.interface.outputs.find((candidate) => candidate.id === reference)
          : undefined;
    return {
      ...node,
      ...(entry
        ? {
            label: entry.name,
            connectors: [
              {
                id: 'value',
                label: entry.units ? `${entry.name} (${entry.units})` : entry.name,
                direction: node.kind === 'flowInput' ? ('output' as const) : ('input' as const),
                dataType: entry.dataType,
                side: node.kind === 'flowInput' ? ('right' as const) : ('left' as const)
              }
            ]
          }
        : { connectors: node.connectors.map((connector) => ({ ...connector })) }),
      configuration: { ...node.configuration }
    };
  }),
  connections: flow.connections.map((connection) => ({
    ...connection,
    start: { ...connection.start },
    end: { ...connection.end }
  }))
});

export const flowDtoToDomain = (dto: FlowDto): FlowDefinition => copyFlow(dto);

export const flowDomainToDto = (flow: FlowDefinition): FlowDto => copyFlow(flow);
