import type { ExecutableFlowSource } from './flowDebugApi';

export interface EmulatorSnapshot {
  emulatorId: string;
  flowId: string;
  controllerTemplateId: string;
  lifecycleState: string;
  virtualTimeMilliseconds: number;
  scanNumber: number;
  inputs: { pointId: string; value: boolean; isGood: boolean }[];
  outputHistory: {
    scanNumber: number;
    pointId: string;
    proposedValue: boolean;
    effectiveValue: boolean;
    quality: string;
  }[];
  activeFault?: string;
}

const json = async <T>(url: string, init?: RequestInit): Promise<T> => {
  const response = await fetch(url, init);
  if (!response.ok) throw new Error(`Emulator request failed with status ${response.status}.`);
  return (await response.json()) as T;
};

export const flowEmulatorApi = {
  create: (source: ExecutableFlowSource) =>
    json<EmulatorSnapshot>('/api/emulators', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ source })
    }),
  setInputs: (emulatorId: string, inputs: { pointId: string; value: boolean; isGood: boolean }[]) =>
    json<EmulatorSnapshot>(`/api/emulators/${encodeURIComponent(emulatorId)}/inputs`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ inputs })
    }),
  advance: (emulatorId: string, milliseconds: number, scan = true) =>
    json<EmulatorSnapshot>(`/api/emulators/${encodeURIComponent(emulatorId)}/advance`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ milliseconds, scan })
    }),
  fault: (emulatorId: string, fault: string | null) =>
    json<EmulatorSnapshot>(`/api/emulators/${encodeURIComponent(emulatorId)}/fault`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ fault })
    }),
  reset: (emulatorId: string, powerCycle = false) =>
    json<EmulatorSnapshot>(`/api/emulators/${encodeURIComponent(emulatorId)}/reset`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ powerCycle })
    })
};
