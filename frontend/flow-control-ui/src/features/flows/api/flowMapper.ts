import type { FlowDefinition } from '../types';
import type { FlowDto } from './flowDto';

// API data and editable data must not share nested objects. Editing a connector or
// endpoint in the designer must not mutate the server-confirmed baseline used for
// dirty-state comparison and explicit discard.
const copyFlow = (flow: FlowDefinition | FlowDto): FlowDto => ({
  ...flow,
  nodes: flow.nodes.map((node) => ({
    ...node,
    connectors: node.connectors.map((connector) => ({ ...connector })),
    configuration: { ...node.configuration }
  })),
  connections: flow.connections.map((connection) => ({
    ...connection,
    start: { ...connection.start },
    end: { ...connection.end }
  }))
});

export const flowDtoToDomain = (dto: FlowDto): FlowDefinition => copyFlow(dto);

export const flowDomainToDto = (flow: FlowDefinition): FlowDto => copyFlow(flow);
