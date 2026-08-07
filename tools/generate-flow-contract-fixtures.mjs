import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const outputDirectory = join(root, 'testdata', 'contracts', 'flow-executable-v1');
const checkOnly = process.argv.includes('--check');

const u8 = (value) => Buffer.from([value]);
const u16 = (value) => {
  const bytes = Buffer.alloc(2);
  bytes.writeUInt16LE(value);
  return bytes;
};
const u32 = (value) => {
  const bytes = Buffer.alloc(4);
  bytes.writeUInt32LE(value);
  return bytes;
};
const string8 = (value) => Buffer.concat([u8(Buffer.byteLength(value)), Buffer.from(value)]);
const sha256 = (bytes) => createHash('sha256').update(bytes).digest();
const hex = (bytes) => `${bytes.toString('hex').match(/.{1,64}/g).join('\n')}\n`;
const compareBytes = (left, right) => Buffer.compare(Buffer.from(left), Buffer.from(right));

const portShapes = {
  digitalInput: [{ id: 'value', direction: 2 }],
  digitalConstant: [{ id: 'value', direction: 2 }],
  not: [{ id: 'in', direction: 1 }, { id: 'value', direction: 2 }],
  and: [{ id: 'a', direction: 1 }, { id: 'b', direction: 1 }, { id: 'value', direction: 2 }],
  or: [{ id: 'a', direction: 1 }, { id: 'b', direction: 1 }, { id: 'value', direction: 2 }],
  memory: [{ id: 'in', direction: 1 }, { id: 'value', direction: 2 }],
  digitalOutput: [{ id: 'in', direction: 1 }]
};

const opcodes = { digitalInput: 1, digitalConstant: 2, not: 3, and: 4, or: 5, memory: 6, digitalOutput: 7 };

function canonicalSource(flow) {
  return {
    ...flow,
    nodes: [...flow.nodes].sort((left, right) => compareBytes(left.id, right.id)),
    connections: [...flow.connections].sort((left, right) =>
      compareBytes(`${left.target.nodeId}\0${left.target.portId}\0${left.source.nodeId}\0${left.source.portId}`,
        `${right.target.nodeId}\0${right.target.portId}\0${right.source.nodeId}\0${right.source.portId}`))
  };
}

function compile(source, options = {}) {
  const flow = canonicalSource(source);
  const points = [];
  for (const node of flow.nodes) {
    if (node.kind === 'digitalInput') points.push({ id: node.configuration.pointId, direction: 1 });
    if (node.kind === 'digitalOutput') points.push({ id: node.configuration.pointId, direction: 2 });
  }
  points.sort((left, right) => compareBytes(left.id, right.id) || left.direction - right.direction);

  const nodeRecords = flow.nodes.map((node) => {
    let configuration = Buffer.alloc(0);
    if (node.kind === 'digitalInput') configuration = u16(points.findIndex((point) => point.id === node.configuration.pointId && point.direction === 1));
    if (node.kind === 'digitalConstant' || node.kind === 'memory') configuration = u8(node.configuration.value ? 1 : 0);
    if (node.kind === 'digitalOutput') configuration = Buffer.concat([
      u16(points.findIndex((point) => point.id === node.configuration.pointId && point.direction === 2)),
      u8(1), u8(8), u32(0)
    ]);
    return Buffer.concat([string8(node.id), u8(opcodes[node.kind]), u16(configuration.length), configuration]);
  });
  if (options.permuteNodes) nodeRecords.reverse();

  const ports = [];
  flow.nodes.forEach((node, nodeIndex) => {
    for (const shape of portShapes[node.kind]) {
      ports.push({ nodeIndex, nodeId: node.id, ...shape, valueType: 2 });
    }
  });
  if (options.incompatiblePort) {
    const target = ports.find((port) => port.nodeId === options.incompatiblePort.nodeId && port.id === options.incompatiblePort.portId);
    target.valueType = 1;
  }
  const portRecords = ports.map((port) => Buffer.concat([
    u16(port.nodeIndex), string8(port.id), u8(port.direction), u8(port.valueType), u8(1), u8(0)
  ]));

  const connectionRecords = flow.connections.map((connection) => {
    const sourceNodeIndex = flow.nodes.findIndex((node) => node.id === connection.source.nodeId);
    const targetNodeIndex = flow.nodes.findIndex((node) => node.id === connection.target.nodeId);
    const sourcePortIndex = ports.findIndex((port) => port.nodeId === connection.source.nodeId && port.id === connection.source.portId);
    const targetPortIndex = ports.findIndex((port) => port.nodeId === connection.target.nodeId && port.id === connection.target.portId);
    return Buffer.concat([u16(sourceNodeIndex), u16(sourcePortIndex), u16(targetNodeIndex), u16(targetPortIndex)]);
  });
  const pointRecords = points.map((point) => Buffer.concat([
    string8(point.id), u8(point.direction), u8(2), u8(1), u8(0)
  ]));
  const table = (records) => Buffer.concat([u16(records.length), ...records]);
  const nodeTable = table(nodeRecords);
  const portTable = table(portRecords);
  const connectionTable = table(connectionRecords);
  const pointTable = table(pointRecords);
  const nodeOffset = 24;
  const portOffset = nodeOffset + nodeTable.length;
  const connectionOffset = portOffset + portTable.length;
  const pointOffset = connectionOffset + connectionTable.length;
  const bodyLength = pointOffset + pointTable.length;
  const body = Buffer.concat([
    u32(bodyLength), u32(nodeOffset), u32(portOffset), u32(connectionOffset), u32(pointOffset), u32(0),
    nodeTable, portTable, connectionTable, pointTable
  ]);

  const envelope = Buffer.alloc(192);
  envelope.write('FCEX', 0, 'ascii');
  envelope.writeUInt16LE(1, 4);
  envelope.writeUInt16LE(1, 6);
  envelope.writeUInt16LE(192, 8);
  envelope.writeUInt32LE(192 + body.length, 12);
  envelope.writeUInt32LE(flow.revision, 16);
  envelope.writeUInt32LE(flow.controllerTemplateRevision, 20);
  envelope.writeUInt8(1, 24);
  envelope.writeUInt8(1, 25);
  envelope.writeUInt16LE(flow.nodes.length, 32);
  envelope.writeUInt16LE(ports.length, 34);
  envelope.writeUInt16LE(flow.connections.length, 36);
  envelope.writeUInt16LE(points.length, 38);
  const capabilities = 1 | 2 | 16 | (flow.nodes.some((node) => node.kind === 'memory') ? 4 : 0) |
    (flow.nodes.some((node) => node.kind === 'digitalOutput') ? 8 : 0);
  envelope.writeUInt32LE(capabilities, 40);
  envelope.writeUInt32LE(4096, 44);
  envelope.writeUInt8(Buffer.byteLength(flow.id), 48);
  envelope.write(flow.id, 49, 'utf8');
  envelope.writeUInt8(Buffer.byteLength(flow.controllerTemplateId), 112);
  envelope.write(flow.controllerTemplateId, 113, 'utf8');
  sha256(body).copy(envelope, 160);
  return { artifact: Buffer.concat([envelope, body]), flow, points, ports };
}

const twoButton = {
  schemaVersion: 1,
  id: 'two-button-and',
  revision: 7,
  controllerTemplateId: 'kincony-kc868-a16',
  controllerTemplateRevision: 3,
  execution: { mode: 'manual', intervalMs: 0, inputQualityPolicy: 'require_good' },
  nodes: [
    { id: 'input-01-node', kind: 'digitalInput', configuration: { pointId: 'input-01' } },
    { id: 'input-08-node', kind: 'digitalInput', configuration: { pointId: 'input-08' } },
    { id: 'and-main', kind: 'and', configuration: {} },
    { id: 'output-01-node', kind: 'digitalOutput', configuration: { pointId: 'output-01' } }
  ],
  connections: [
    { source: { nodeId: 'input-01-node', portId: 'value' }, target: { nodeId: 'and-main', portId: 'a' } },
    { source: { nodeId: 'input-08-node', portId: 'value' }, target: { nodeId: 'and-main', portId: 'b' } },
    { source: { nodeId: 'and-main', portId: 'value' }, target: { nodeId: 'output-01-node', portId: 'in' } }
  ]
};

const memory = {
  ...twoButton,
  id: 'memory-feedback', revision: 2,
  nodes: [
    { id: 'constant-true', kind: 'digitalConstant', configuration: { value: true } },
    { id: 'memory-1', kind: 'memory', configuration: { value: false } },
    { id: 'or-1', kind: 'or', configuration: {} },
    { id: 'output-01-node', kind: 'digitalOutput', configuration: { pointId: 'output-01' } }
  ],
  connections: [
    { source: { nodeId: 'constant-true', portId: 'value' }, target: { nodeId: 'or-1', portId: 'a' } },
    { source: { nodeId: 'memory-1', portId: 'value' }, target: { nodeId: 'or-1', portId: 'b' } },
    { source: { nodeId: 'or-1', portId: 'value' }, target: { nodeId: 'memory-1', portId: 'in' } },
    { source: { nodeId: 'memory-1', portId: 'value' }, target: { nodeId: 'output-01-node', portId: 'in' } }
  ]
};

const missingPoint = structuredClone(twoButton);
missingPoint.id = 'missing-point';
missingPoint.nodes.find((node) => node.id === 'input-08-node').configuration.pointId = 'input-99';

const cycle = {
  ...twoButton, id: 'combinational-cycle', revision: 1,
  nodes: [
    { id: 'not-a', kind: 'not', configuration: {} },
    { id: 'not-b', kind: 'not', configuration: {} }
  ],
  connections: [
    { source: { nodeId: 'not-a', portId: 'value' }, target: { nodeId: 'not-b', portId: 'in' } },
    { source: { nodeId: 'not-b', portId: 'value' }, target: { nodeId: 'not-a', portId: 'in' } }
  ]
};

const fixtures = [];
function addFixture(id, source, expected, options = {}) {
  const compiled = compile(source, options);
  const artifact = options.truncate ? compiled.artifact.subarray(0, compiled.artifact.length - 1) : compiled.artifact;
  const directory = join(outputDirectory, id);
  const decoded = {
    envelopeSchema: 1, bodySchema: 1, flowId: source.id, revision: source.revision,
    controllerTemplateId: source.controllerTemplateId,
    controllerTemplateRevision: source.controllerTemplateRevision,
    nodeIds: compiled.flow.nodes.map((node) => node.id),
    pointReferences: compiled.points.map((point) => ({ pointId: point.id, direction: point.direction === 1 ? 'read' : 'proposed_write' }))
  };
  const files = {
    'source-flow.json': `${JSON.stringify(source, null, 2)}\n`,
    'artifact.bin': artifact,
    'artifact.hex': hex(artifact),
    'decoded.json': `${JSON.stringify(decoded, null, 2)}\n`,
    'expected-validation.json': `${JSON.stringify(expected, null, 2)}\n`
  };
  for (const [name, contents] of Object.entries(files)) write(join(directory, name), contents);
  fixtures.push({ id, artifactLength: artifact.length, artifactSha256: sha256(artifact).toString('hex'), expected });
}

function write(path, contents) {
  if (checkOnly) {
    const existing = readFileSync(path);
    const wanted = Buffer.isBuffer(contents) ? contents : Buffer.from(contents);
    if (!existing.equals(wanted)) throw new Error(`Fixture is stale: ${path}`);
    return;
  }
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, contents);
}

addFixture('valid-two-button-and', twoButton, { valid: true, reasonCode: 0, reason: 'ok', path: '' });
addFixture('valid-memory-feedback', memory, { valid: true, reasonCode: 0, reason: 'ok', path: '' });
addFixture('malformed-truncated', twoButton, { valid: false, reasonCode: 3, reason: 'length_mismatch', path: '/artifactLength' }, { truncate: true });
addFixture('incompatible-types', twoButton, { valid: false, reasonCode: 13, reason: 'incompatible_type', path: '/connections/0' }, { incompatiblePort: { nodeId: 'and-main', portId: 'a' } });
addFixture('missing-point', missingPoint, { valid: false, reasonCode: 14, reason: 'missing_point', path: '/points/input-99' });
addFixture('combinational-cycle', cycle, { valid: false, reasonCode: 16, reason: 'combinational_cycle', path: '/nodes/not-a' });
addFixture('noncanonical-node-order', twoButton, { valid: false, reasonCode: 7, reason: 'non_canonical_order', path: '/nodes/1' }, { permuteNodes: true });

const permutedSource = structuredClone(twoButton);
permutedSource.nodes.reverse();
permutedSource.connections.reverse();
addFixture('valid-source-order-permutation', permutedSource, { valid: true, reasonCode: 0, reason: 'ok', path: '' });

const frames = [
  { tick: 1, sampledAtMs: 1000, inputs: { 'input-01': false, 'input-08': false }, expected: { 'and-main': false, 'input-01-node': false, 'input-08-node': false, 'output-01-node': false } },
  { tick: 2, sampledAtMs: 1100, inputs: { 'input-01': true, 'input-08': false }, expected: { 'and-main': false, 'input-01-node': true, 'input-08-node': false, 'output-01-node': false } },
  { tick: 3, sampledAtMs: 1200, inputs: { 'input-01': true, 'input-08': true }, expected: { 'and-main': true, 'input-01-node': true, 'input-08-node': true, 'output-01-node': true } }
];
write(join(outputDirectory, 'valid-two-button-and', 'input-frames.json'), `${JSON.stringify(frames, null, 2)}\n`);
write(join(outputDirectory, 'valid-two-button-and', 'expected-snapshots.json'), `${JSON.stringify(frames.map((frame) => ({
  debugSessionId: '1', flowId: twoButton.id, revision: twoButton.revision,
  lifecycleState: 'paused', mode: 'manual', tickNumber: frame.tick, sampledAtMs: frame.sampledAtMs,
  completedAtMs: frame.sampledAtMs + 1, executionDurationUs: 100, inputValidity: ['coherent', 'all_present', 'all_good'],
  nodes: Object.entries(frame.expected).map(([nodeId, value]) => ({ nodeId, state: 'evaluated', quality: 'good', typedValue: { type: 'digital', value } })),
  proposedOutputs: [{ pointId: 'output-01', state: 'evaluated', quality: 'good', proposedValue: frame.expected['output-01-node'] }],
  overrunCount: 0, evaluationFailureCount: 0, lastReasonCode: 0, lastReason: 'ok', lastReasonPath: ''
})), null, 2)}\n`);
write(join(outputDirectory, 'valid-memory-feedback', 'input-frames.json'), '[{"tick":1,"inputs":{}},{"tick":2,"inputs":{}}]\n');
write(join(outputDirectory, 'valid-memory-feedback', 'expected-snapshots.json'), '[{"tickNumber":1,"nodes":{"memory-1":false,"or-1":true},"proposedOutputs":{"output-01":false}},{"tickNumber":2,"nodes":{"memory-1":true,"or-1":true},"proposedOutputs":{"output-01":true}}]\n');
write(join(outputDirectory, 'target-points.json'), `${JSON.stringify([
  { id: 'input-01', direction: 'input', type: 'digital' },
  { id: 'input-08', direction: 'input', type: 'digital' },
  { id: 'output-01', direction: 'output', type: 'digital' }
], null, 2)}\n`);
write(join(outputDirectory, 'manifest.json'), `${JSON.stringify({ contract: 'flow-executable-v1', fixtures }, null, 2)}\n`);
