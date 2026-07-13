import type { FlowDefinition } from './types';

export const sampleFlows: FlowDefinition[] = [
  {
    id: 'climate-control',
    name: 'Climate control',
    description: 'Balances temperature inputs and applies the configured override.',
    status: 'draft',
    updatedAt: '2026-07-13T09:30:00+10:00',
    nodes: [
      {
        id: 'temperature-average',
        kind: 'calculator',
        label: 'Average temperature',
        x: 90,
        y: 110,
        color: '#ef8354'
      },
      {
        id: 'comfort-pulse',
        kind: 'pulse',
        label: 'Comfort pulse',
        x: 430,
        y: 220,
        color: '#f5b942'
      },
      {
        id: 'manual-override',
        kind: 'override',
        label: 'Manual override',
        x: 780,
        y: 320,
        color: '#65d6ad'
      },
      {
        id: 'zone-split',
        kind: 'split',
        label: 'Zone outputs',
        x: 780,
        y: 90,
        color: '#64a7ff'
      }
    ],
    connections: [
      {
        id: 'temperature-to-pulse',
        start: { nodeId: 'temperature-average', connectorId: 'output' },
        end: { nodeId: 'comfort-pulse', connectorId: 'input' }
      },
      {
        id: 'pulse-to-override',
        start: { nodeId: 'comfort-pulse', connectorId: 'output' },
        end: { nodeId: 'manual-override', connectorId: 'input' }
      }
    ]
  },
  {
    id: 'garden-irrigation',
    name: 'Garden irrigation',
    description: 'Runs watering zones from a schedule and moisture readings.',
    status: 'deployed',
    updatedAt: '2026-07-12T18:15:00+10:00',
    nodes: [],
    connections: []
  }
];
