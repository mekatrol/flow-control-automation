import { FlowApiError } from './flowApi';
import type { ExecutableFlowSource } from './flowDebugApi';
import type { EmulatorInputChange, EmulatorValue } from './flowEmulatorApi';

export type ScenarioAction = 'apply' | 'step' | 'advance' | 'reset';
export type ExpectationOperator = 'equals' | 'approximately' | 'changes' | 'remains';

export interface FlowScenario {
  schemaVersion: 1;
  id: string;
  name: string;
  description?: string;
  flowId: string;
  flowRevision: number;
  steps: {
    atMilliseconds: number;
    action: ScenarioAction;
    inputs: EmulatorInputChange[];
    powerCycle: boolean;
  }[];
  expectations: {
    scan?: number;
    outputId: string;
    operator: ExpectationOperator;
    expectedValue?: EmulatorValue;
    tolerance?: number;
  }[];
}

export interface FlowScenarioRunResult {
  scenarioId: string;
  passed: boolean;
  scanNumber: number;
  expectations: {
    passed: boolean;
    outputId: string;
    operator: ExpectationOperator;
    expectedValue?: EmulatorValue;
    actualValue?: EmulatorValue;
    quality?: string;
    diagnosticCode?: string;
  }[];
}

export const parseScenario = (value: unknown): FlowScenario => {
  if (typeof value !== 'object' || value === null)
    throw new TypeError('Scenario must be an object.');
  const item = value as Partial<FlowScenario>;
  if (
    item.schemaVersion !== 1 ||
    typeof item.id !== 'string' ||
    typeof item.name !== 'string' ||
    typeof item.flowId !== 'string' ||
    typeof item.flowRevision !== 'number' ||
    !Array.isArray(item.steps) ||
    !Array.isArray(item.expectations)
  )
    throw new TypeError('Scenario fields are invalid or unsupported.');
  return item as FlowScenario;
};

const json = async <T>(url: string, init?: RequestInit): Promise<T> => {
  const response = await fetch(url, init);
  if (!response.ok) {
    const body = (await response.json().catch(() => ({}))) as { message?: unknown };
    throw new FlowApiError(
      'http',
      typeof body.message === 'string'
        ? body.message
        : `Scenario request failed with status ${response.status}.`,
      response.status
    );
  }
  return (await response.json()) as T;
};
const base = (flowId: string): string => `/api/flows/${encodeURIComponent(flowId)}/scenarios`;

export const flowScenarioApi = {
  list: (flowId: string) => json<FlowScenario[]>(base(flowId)),
  save: (scenario: FlowScenario) =>
    json<FlowScenario>(`${base(scenario.flowId)}/${encodeURIComponent(scenario.id)}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(scenario)
    }),
  remove: async (flowId: string, scenarioId: string): Promise<void> => {
    const response = await fetch(`${base(flowId)}/${encodeURIComponent(scenarioId)}`, {
      method: 'DELETE'
    });
    if (!response.ok)
      throw new FlowApiError(
        'http',
        `Delete scenario failed with status ${response.status}.`,
        response.status
      );
  },
  run: (scenario: FlowScenario, source: ExecutableFlowSource) =>
    json<FlowScenarioRunResult>(`${base(scenario.flowId)}/run`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ scenario, source })
    })
};
