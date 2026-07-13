import type { FlowDefinition } from '../../types';

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
        zOrder: 0,
        color: '#ef8354',
        connectors: [
          { id: 'input', label: 'Values', direction: 'input', dataType: 'number', side: 'left' },
          { id: 'output', label: 'Average', direction: 'output', dataType: 'number', side: 'right' }
        ],
        configuration: { operation: 'average' }
      },
      {
        id: 'comfort-pulse',
        kind: 'pulse',
        label: 'Comfort pulse',
        x: 430,
        y: 220,
        zOrder: 1,
        color: '#f5b942',
        connectors: [
          { id: 'input', label: 'Value', direction: 'input', dataType: 'number', side: 'left' },
          { id: 'output', label: 'Pulse', direction: 'output', dataType: 'number', side: 'right' }
        ],
        configuration: { durationSeconds: 30 }
      },
      {
        id: 'manual-override',
        kind: 'override',
        label: 'Manual override',
        x: 780,
        y: 320,
        zOrder: 2,
        color: '#65d6ad',
        connectors: [
          { id: 'input', label: 'Automatic', direction: 'input', dataType: 'number', side: 'left' },
          {
            id: 'output',
            label: 'Effective',
            direction: 'output',
            dataType: 'number',
            side: 'right'
          }
        ],
        configuration: { enabled: false }
      },
      {
        id: 'zone-split',
        kind: 'split',
        label: 'Zone outputs',
        x: 780,
        y: 90,
        zOrder: 3,
        color: '#64a7ff',
        connectors: [
          { id: 'input', label: 'Source', direction: 'input', dataType: 'number', side: 'left' },
          { id: 'output', label: 'Zones', direction: 'output', dataType: 'number', side: 'right' }
        ],
        configuration: { outputs: 2 }
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
    nodes: [
      {
        id: 'watering-pulse',
        kind: 'pulse',
        label: 'Watering pulse',
        x: 120,
        y: 120,
        zOrder: 0,
        color: '#f5b942',
        connectors: [
          {
            id: 'input',
            label: 'Schedule',
            direction: 'input',
            dataType: 'event',
            side: 'left'
          },
          {
            id: 'output',
            label: 'Watering state',
            direction: 'output',
            dataType: 'boolean',
            side: 'right'
          }
        ],
        configuration: { durationSeconds: 900 }
      }
    ],
    connections: []
  }
];
