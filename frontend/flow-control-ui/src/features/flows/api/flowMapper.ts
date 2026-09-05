import { FlowNodeType } from '@/types/serverTypes';
import type { FlowDefinition } from '@/features/flows/types';
import { nodeTypeRegistry } from '@/features/flows/nodeTypes';
import type { FlowDto } from './flowDto';

// API data and editable data must not share nested objects. Editing a connector or
// endpoint in the designer must not mutate the server-confirmed baseline used for
// dirty-state comparison and explicit discard.
const copyFlow = (flow: FlowDefinition | FlowDto): FlowDto => ({
  ...flow,
  ...(flow.revision !== undefined ? { revision: flow.revision } : {}),
  nodes: flow.nodes.map((node) => {
    const connectors = node.connectors.map((connector) => ({ ...connector }));
    if (
      node.nodeType === FlowNodeType.AnalogVirtual ||
      node.nodeType === FlowNodeType.DigitalVirtual
    ) {
      for (const connector of nodeTypeRegistry[node.nodeType].connectors)
        if (!connectors.some(({ id }) => id === connector.id)) connectors.push({ ...connector });
    }
    return {
      ...node,
      connectors,
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
