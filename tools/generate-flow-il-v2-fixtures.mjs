import { createHash } from 'node:crypto';
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const outputRoot = join(root, 'testdata', 'contracts', 'flow-il-v2');
const checkOnly = process.argv.includes('--check');
const envelopeLength = 128;
const directoryEntryLength = 48;
const sectionIds = { constants: 1, points: 2, slots: 3, instructions: 4, commit: 5, symbols: 6, debugMap: 7, dependencies: 8 };
const opcodes = { readPoint: 1, constant: 2, not: 3, and: 4, or: 5, loadState: 6, proposeOutput: 7, stageState: 8,
  nand: 9, nor: 10, xor: 11, xnor: 12, numericConstant: 13, add: 14, comparator: 15, levelShifter: 16, commit: 255 };

const u8 = (value) => Buffer.from([value]);
const u16 = (value) => { const bytes = Buffer.alloc(2); bytes.writeUInt16LE(value); return bytes; };
const u32 = (value) => { const bytes = Buffer.alloc(4); bytes.writeUInt32LE(value); return bytes; };
const f64 = (value) => { const bytes = Buffer.alloc(8); bytes.writeDoubleLE(value); return bytes; };
const string8 = (value) => Buffer.concat([u8(Buffer.byteLength(value)), Buffer.from(value)]);
const sha256 = (value) => createHash('sha256').update(value).digest();
const sha256Hex = (value) => createHash('sha256').update(value).digest('hex');
const compare = (left, right) => Buffer.compare(Buffer.from(left), Buffer.from(right));
const authoringKind = (kind) => ({ input: 'digitalInput', constant: 'digitalConstant', output: 'digitalOutput' })[kind] ?? kind;
const authoringLabel = (kind) => authoringKind(kind).replace(/([A-Z])/g, ' $1').replace(/^./, (letter) => letter.toUpperCase());
const hex = (bytes) => `${bytes.toString('hex').match(/.{1,64}/g).join('\n')}\n`;

function write(path, contents) {
  const wanted = Buffer.isBuffer(contents) ? contents : Buffer.from(contents);
  if (checkOnly) {
    if (!readFileSync(path).equals(wanted)) throw new Error(`Fixture is stale: ${path}`);
    return;
  }
  mkdirSync(dirname(path), { recursive: true });
  writeFileSync(path, wanted);
}

function schedule(source) {
  const nodes = new Map(source.nodes.map((node) => [node.id, node]));
  const incoming = new Map(source.nodes.map((node) => [node.id, 0]));
  const outgoing = new Map(source.nodes.map((node) => [node.id, []]));
  for (const connection of source.connections) {
    if (nodes.get(connection.target).kind === 'memory') continue;
    incoming.set(connection.target, incoming.get(connection.target) + 1);
    outgoing.get(connection.source).push(connection.target);
  }
  const ready = source.nodes.filter((node) => incoming.get(node.id) === 0).map((node) => node.id).sort(compare);
  const result = [];
  while (ready.length > 0) {
    const id = ready.shift();
    result.push(id);
    for (const target of outgoing.get(id).sort(compare)) {
      incoming.set(target, incoming.get(target) - 1);
      if (incoming.get(target) === 0) ready.push(target);
    }
    ready.sort(compare);
  }
  if (result.length !== source.nodes.length) throw new Error('combinational cycle');
  return result;
}

function compile(source) {
  const orderedIds = schedule(source);
  const nodes = new Map(source.nodes.map((node) => [node.id, node]));
  const slotByNode = new Map(orderedIds.map((id, index) => [id, index]));
  const memoryIds = orderedIds.filter((id) => nodes.get(id).kind === 'memory');
  const stateIds = orderedIds.filter((id) => ['memory', 'onDelay', 'risingEdge'].includes(nodes.get(id).kind));
  const stateSlotByNode = new Map(stateIds.map((id, index) => [id, orderedIds.length + index]));
  const points = [...new Map(source.nodes.filter((node) => node.pointId).map((node) => [node.pointId, {
    id: node.pointId, direction: node.kind === 'input' ? 1 : 2
  }])).values()].sort((left, right) => compare(left.id, right.id) || left.direction - right.direction);
  const pointIndex = new Map(points.map((point, index) => [point.id, index]));
  const requestedConstants = source.nodes.flatMap((node) => {
    if (node.kind === 'constant' || node.kind === 'memory') return [{ type: 1, value: Boolean(node.value) ? 1 : 0 }];
    if (node.kind === 'numericConstant') return [{ type: 2, value: node.value }];
    if (node.kind === 'levelShifter') return [{ type: 2, value: node.gain }, { type: 2, value: node.offset }];
    if (node.kind === 'onDelay') return [{ type: 2, value: node.durationMs }];
    if (node.kind === 'risingEdge') return [{ type: 1, value: 0 }];
    return [];
  });
  const constants = [...new Map(requestedConstants.map((value) => [`${value.type}:${value.value}`, value])).values()]
    .sort((left, right) => left.type - right.type || left.value - right.value);
  const constantIndex = (type, value) => constants.findIndex((constant) => constant.type === type && constant.value === value);
  const inputSlot = (target, port) => {
    const connection = source.connections.find((candidate) => candidate.target === target && candidate.port === port);
    if (!connection) throw new Error(`missing input ${target}.${port}`);
    return slotByNode.get(connection.source);
  };
  const instructions = [];
  for (const id of orderedIds) {
    const node = nodes.get(id);
    const result = slotByNode.get(id);
    const instruction = { nodeId: id, discriminator: 0, result, op0: 0xffff, op1: 0xffff, aux: 0xffff };
    if (node.kind === 'input') Object.assign(instruction, { opcode: opcodes.readPoint, aux: pointIndex.get(node.pointId) });
    if (node.kind === 'constant') Object.assign(instruction, { opcode: opcodes.constant, aux: constantIndex(1, Boolean(node.value) ? 1 : 0) });
    if (node.kind === 'not') Object.assign(instruction, { opcode: opcodes.not, op0: inputSlot(id, 'in') });
    if (node.kind === 'and') Object.assign(instruction, { opcode: opcodes.and, op0: inputSlot(id, 'a'), op1: inputSlot(id, 'b') });
    if (node.kind === 'or') Object.assign(instruction, { opcode: opcodes.or, op0: inputSlot(id, 'a'), op1: inputSlot(id, 'b') });
    if (node.kind === 'nand') Object.assign(instruction, { opcode: opcodes.nand, op0: inputSlot(id, 'a'), op1: inputSlot(id, 'b') });
    if (node.kind === 'nor') Object.assign(instruction, { opcode: opcodes.nor, op0: inputSlot(id, 'a'), op1: inputSlot(id, 'b') });
    if (node.kind === 'xor') Object.assign(instruction, { opcode: opcodes.xor, op0: inputSlot(id, 'a'), op1: inputSlot(id, 'b') });
    if (node.kind === 'xnor') Object.assign(instruction, { opcode: opcodes.xnor, op0: inputSlot(id, 'a'), op1: inputSlot(id, 'b') });
    if (node.kind === 'numericConstant') Object.assign(instruction, { opcode: opcodes.numericConstant, aux: constantIndex(2, node.value) });
    if (node.kind === 'add') Object.assign(instruction, { opcode: opcodes.add, op0: inputSlot(id, 'a'), op1: inputSlot(id, 'b') });
    if (node.kind === 'comparator') Object.assign(instruction, { opcode: opcodes.comparator, op0: inputSlot(id, 'a'), op1: inputSlot(id, 'b'), aux: node.operator });
    if (node.kind === 'levelShifter') Object.assign(instruction, { opcode: opcodes.levelShifter, op0: inputSlot(id, 'in'), op1: constantIndex(2, node.gain), aux: constantIndex(2, node.offset) });
    if (node.kind === 'qualityGood') Object.assign(instruction, { opcode: 17, op0: inputSlot(id, 'in') });
    if (node.kind === 'onDelay') Object.assign(instruction, { opcode: 18, op0: inputSlot(id, 'in'), aux: stateSlotByNode.get(id) });
    if (node.kind === 'risingEdge') Object.assign(instruction, { opcode: 19, op0: inputSlot(id, 'in'), aux: stateSlotByNode.get(id) });
    if (node.kind === 'memory') Object.assign(instruction, { opcode: opcodes.loadState, aux: stateSlotByNode.get(id) });
    if (node.kind === 'output') Object.assign(instruction, { opcode: opcodes.proposeOutput, op0: inputSlot(id, 'in'), aux: pointIndex.get(node.pointId) });
    instructions.push(instruction);
  }
  for (const id of memoryIds) instructions.push({ opcode: opcodes.stageState, nodeId: id, discriminator: 1,
    result: 0xffff, op0: inputSlot(id, 'in'), op1: 0xffff, aux: stateSlotByNode.get(id) });
  instructions.push({ opcode: opcodes.commit, nodeId: '', discriminator: 0, result: 0xffff, op0: 0xffff, op1: 0xffff, aux: 0xffff });

  const numericKinds = new Set(['numericConstant', 'add', 'levelShifter']);
  const slots = orderedIds.map((id, index) => Buffer.concat([u8(2), u8(numericKinds.has(nodes.get(id).kind) ? 2 : 1), u16(0), u16(index), u16(0xffff)]));
  for (const id of stateIds) {
    const node = nodes.get(id);
    const kind = node.kind === 'memory' ? 3 : node.kind === 'onDelay' ? 4 : 5;
    const initial = node.kind === 'onDelay' ? constantIndex(2, node.durationMs) : constantIndex(1, node.value ? 1 : 0);
    slots.push(Buffer.concat([u8(kind), u8(1), u16(0), u16(stateSlotByNode.get(id)), u16(initial)]));
  }
  const instructionBytes = instructions.map((item) => Buffer.concat([u8(item.opcode), u8(0), u16(item.result), u16(item.op0), u16(item.op1), u16(item.aux), u16(0)]));
  const commits = [];
  for (const id of memoryIds) commits.push(Buffer.concat([u8(1), u8(0), u16(stateSlotByNode.get(id)), u16(inputSlot(id, 'in')), u16(0)]));
  for (const id of orderedIds.filter((nodeId) => nodes.get(nodeId).kind === 'output')) commits.push(Buffer.concat([u8(2), u8(0), u16(pointIndex.get(nodes.get(id).pointId)), u16(slotByNode.get(id)), u16(0)]));
  const symbols = instructions.map((item, index) => {
    const node = nodes.get(item.nodeId);
    const label = node ? (node.label ?? authoringLabel(node.kind)) : '';
    return Buffer.concat([u16(index), u8(item.discriminator), string8(item.nodeId), string8(label),
      f64(node?.x ?? 0), f64(node?.y ?? 0), f64(node?.zOrder ?? 0)]);
  });
  const debugMap = instructions.filter((item) => item.nodeId).map((item, index) => Buffer.concat([u16(index), u16(item.result), string8(item.nodeId)]));
  const sections = [
    { id: sectionIds.constants, count: constants.length, bytes: Buffer.concat(constants.map((constant) => constant.type === 1
      ? Buffer.concat([u8(1), u8(constant.value), u16(0)])
      : Buffer.concat([u8(2), u8(0), u16(0), f64(constant.value)]))) },
    { id: sectionIds.points, count: points.length, bytes: Buffer.concat(points.map((point) => Buffer.concat([u8(point.direction), u8(1), u8(1), u8(0), string8(point.id)]))) },
    { id: sectionIds.slots, count: slots.length, bytes: Buffer.concat(slots) },
    { id: sectionIds.instructions, count: instructionBytes.length, bytes: Buffer.concat(instructionBytes) },
    { id: sectionIds.commit, count: commits.length, bytes: Buffer.concat(commits) },
    { id: sectionIds.symbols, version: 2, count: symbols.length, bytes: Buffer.concat(symbols) },
    { id: sectionIds.debugMap, count: debugMap.length, bytes: Buffer.concat(debugMap) },
    { id: sectionIds.dependencies, count: 1 + points.length, bytes: Buffer.concat([
      u8(1), string8(source.controllerTemplateId), u32(source.controllerTemplateRevision),
      ...points.map((point) => Buffer.concat([u8(2), string8(point.id), u32(1)]))
    ]) }
  ];
  let offset = envelopeLength + sections.length * directoryEntryLength;
  const directory = [];
  for (const section of sections) {
    directory.push(Buffer.concat([u16(section.id), u16(section.version ?? 1), u32(offset), u32(section.bytes.length), u32(section.count), sha256(section.bytes)]));
    offset += section.bytes.length;
  }
  const envelope = Buffer.alloc(envelopeLength);
  envelope.write('FIL2', 0, 'ascii');
  envelope.writeUInt16LE(2, 4); envelope.writeUInt16LE(envelopeLength, 6); envelope.writeUInt32LE(offset, 8);
  envelope.writeUInt32LE(1, 12); envelope.writeUInt32LE(source.revision, 16); envelope.writeUInt32LE(source.controllerTemplateRevision, 20);
  envelope.writeUInt16LE(2, 24); envelope.writeUInt16LE(sections.length, 26); envelope.writeUInt8(source.inputQualityPolicy === 'propagate' ? 2 : 1, 28);
  let capabilities = 1n | 16n;
  if (points.some((point) => point.direction === 1)) capabilities |= 2n;
  if (points.some((point) => point.direction === 2)) capabilities |= 4n;
  if (memoryIds.length > 0) capabilities |= 8n;
  if (orderedIds.some((id) => ['nand', 'nor', 'xor', 'xnor'].includes(nodes.get(id).kind))) capabilities |= 32n;
  if (orderedIds.some((id) => ['numericConstant', 'add', 'comparator', 'levelShifter'].includes(nodes.get(id).kind))) capabilities |= 64n;
  if (orderedIds.some((id) => nodes.get(id).kind === 'comparator')) capabilities |= 128n;
  if (orderedIds.some((id) => nodes.get(id).kind === 'levelShifter')) capabilities |= 256n;
  if (orderedIds.some((id) => nodes.get(id).kind === 'qualityGood')) capabilities |= 512n;
  if (orderedIds.some((id) => nodes.get(id).kind === 'onDelay')) capabilities |= 1024n;
  if (orderedIds.some((id) => nodes.get(id).kind === 'risingEdge')) capabilities |= 2048n;
  envelope.writeUInt32LE(instructions.length, 32); envelope.writeBigUInt64LE(capabilities, 36);
  envelope.writeUInt32LE((orderedIds.length + stateIds.length) * 32, 44); envelope.writeUInt32LE(16384, 48);
  envelope.writeUInt8(Buffer.byteLength(source.id), 52); envelope.write(source.id, 53, 'utf8'); envelope.writeUInt32LE(envelopeLength, 116);
  const artifact = Buffer.concat([envelope, ...directory, ...sections.map((section) => section.bytes)]);
  return { artifact, metadata: { flowId: source.id, flowRevision: source.revision, sectionCount: sections.length,
    instructionCount: instructions.length, slotCount: slots.length, pointCount: points.length, stateCount: stateIds.length,
    schedule: orderedIds, slots: Object.fromEntries(slotByNode), artifactLength: artifact.length } };
}

const base = { schemaVersion: 1, id: 'two-button-and', revision: 7, controllerTemplateId: 'kincony-kc868-a16', controllerTemplateRevision: 3,
  nodes: [{ id: 'input-01-node', kind: 'input', pointId: 'input-01' }, { id: 'input-08-node', kind: 'input', pointId: 'input-08' },
    { id: 'and-main', kind: 'and' }, { id: 'output-01-node', kind: 'output', pointId: 'output-01' }],
  connections: [{ source: 'input-01-node', target: 'and-main', port: 'a' }, { source: 'input-08-node', target: 'and-main', port: 'b' },
    { source: 'and-main', target: 'output-01-node', port: 'in' }] };
const memory = { ...structuredClone(base), id: 'memory-feedback', revision: 2,
  nodes: [{ id: 'constant-true', kind: 'constant', value: true }, { id: 'memory-1', kind: 'memory', value: false },
    { id: 'or-1', kind: 'or' }, { id: 'output-01-node', kind: 'output', pointId: 'output-01' }],
  connections: [{ source: 'constant-true', target: 'or-1', port: 'a' }, { source: 'memory-1', target: 'or-1', port: 'b' },
    { source: 'or-1', target: 'memory-1', port: 'in' }, { source: 'memory-1', target: 'output-01-node', port: 'in' }] };
const expandedBoolean = { ...structuredClone(base), id: 'expanded-boolean', revision: 1,
  nodes: [{ id: 'constant-false', kind: 'constant', value: false }, { id: 'constant-true', kind: 'constant', value: true },
    { id: 'nand-1', kind: 'nand' }, { id: 'nor-1', kind: 'nor' }, { id: 'xor-1', kind: 'xor' }, { id: 'xnor-1', kind: 'xnor' },
    { id: 'output-nand', kind: 'output', pointId: 'output-nand' }, { id: 'output-nor', kind: 'output', pointId: 'output-nor' },
    { id: 'output-xor', kind: 'output', pointId: 'output-xor' }, { id: 'output-xnor', kind: 'output', pointId: 'output-xnor' }],
  connections: ['nand', 'nor', 'xor', 'xnor'].flatMap((kind) => [
    { source: 'constant-true', target: `${kind}-1`, port: 'a' },
    { source: 'constant-false', target: `${kind}-1`, port: 'b' },
    { source: `${kind}-1`, target: `output-${kind}`, port: 'in' }
  ]) };
const numeric = { ...structuredClone(base), id: 'numeric-language', revision: 1,
  nodes: [{ id: 'constant-2', kind: 'numericConstant', value: 2 }, { id: 'constant-3', kind: 'numericConstant', value: 3 },
    { id: 'add-1', kind: 'add' }, { id: 'shift-1', kind: 'levelShifter', gain: 2, offset: -1 },
    { id: 'compare-1', kind: 'comparator', operator: 5 }],
  connections: [{ source: 'constant-2', target: 'add-1', port: 'a' }, { source: 'constant-3', target: 'add-1', port: 'b' },
    { source: 'add-1', target: 'shift-1', port: 'in' }, { source: 'shift-1', target: 'compare-1', port: 'a' },
    { source: 'constant-3', target: 'compare-1', port: 'b' }] };
const stateful = { ...structuredClone(base), id: 'quality-timer-event', revision: 1, inputQualityPolicy: 'propagate',
  nodes: [{ id: 'input-01-node', kind: 'input', pointId: 'input-01' }, { id: 'quality-1', kind: 'qualityGood' },
    { id: 'timer-1', kind: 'onDelay', durationMs: 100 }, { id: 'edge-1', kind: 'risingEdge' }],
  connections: [{ source: 'input-01-node', target: 'quality-1', port: 'in' },
    { source: 'input-01-node', target: 'timer-1', port: 'in' }, { source: 'input-01-node', target: 'edge-1', port: 'in' }] };
const maximum = { ...structuredClone(base), id: 'maximum-boolean', revision: 1,
  nodes: Array.from({ length: 128 }, (_, index) => ({ id: `constant-${String(index).padStart(3, '0')}`, kind: 'constant', value: index % 2 === 0 })), connections: [] };

const fixtures = [];
function fixture(id, source, mutate, expected) {
  const compiled = compile(source);
  let artifact = Buffer.from(compiled.artifact);
  if (mutate) artifact = mutate(artifact);
  const directory = join(outputRoot, id);
  write(join(directory, 'source-flow.json'), `${JSON.stringify(source, null, 2)}\n`);
  write(join(directory, 'artifact.bin'), artifact); write(join(directory, 'artifact.hex'), hex(artifact));
  write(join(directory, 'metadata.json'), `${JSON.stringify(compiled.metadata, null, 2)}\n`);
  write(join(directory, 'expected-validation.json'), `${JSON.stringify(expected, null, 2)}\n`);
  fixtures.push({ id, artifactLength: artifact.length, artifactSha256: sha256Hex(artifact), expected });
  return compiled;
}
fixture('valid-two-button-and', base, null, { valid: true, reason: 'ok', path: '' });
fixture('valid-memory-feedback', memory, null, { valid: true, reason: 'ok', path: '' });
fixture('valid-expanded-boolean', expandedBoolean, null, { valid: true, reason: 'ok', path: '' });
fixture('valid-numeric-language', numeric, null, { valid: true, reason: 'ok', path: '' });
fixture('valid-quality-timer-event', stateful, null, { valid: true, reason: 'ok', path: '' });
const permuted = structuredClone(base); permuted.nodes.reverse(); permuted.connections.reverse();
fixture('valid-source-order-permutation', permuted, null, { valid: true, reason: 'ok', path: '' });
fixture('maximum-boolean', maximum, null, { valid: true, reason: 'ok', path: '' });
fixture('malformed-truncated', base, (artifact) => artifact.subarray(0, artifact.length - 1), { valid: false, reason: 'length_mismatch', path: '/artifactLength' });
fixture('invalid-operand', base, (artifact) => {
  const copy = Buffer.from(artifact);
  const entryOffset = envelopeLength + 3 * directoryEntryLength;
  const instructionOffset = copy.readUInt32LE(entryOffset + 4);
  const instructionLength = copy.readUInt32LE(entryOffset + 8);
  copy.writeUInt16LE(0xfffe, instructionOffset + 2);
  sha256(copy.subarray(instructionOffset, instructionOffset + instructionLength)).copy(copy, entryOffset + 16);
  return copy;
}, { valid: false, reason: 'invalid_operand', path: '/instructions/0/resultSlot' });
fixture('unknown-section', base, (artifact) => { const copy = Buffer.from(artifact); copy.writeUInt16LE(99, envelopeLength); return copy; }, { valid: false, reason: 'unknown_section', path: '/sections/0/id' });
fixture('noncanonical-section-order', base, (artifact) => { const copy = Buffer.from(artifact); copy.writeUInt16LE(2, envelopeLength); copy.writeUInt16LE(1, envelopeLength + directoryEntryLength); return copy; }, { valid: false, reason: 'non_canonical_order', path: '/sections/0/id' });

const canonical = compile(base).artifact;
if (!compile(permuted).artifact.equals(canonical)) throw new Error('source permutation changed canonical Flow IL');
write(join(outputRoot, 'manifest.json'), `${JSON.stringify({ contract: 'flow-il-v2', fixtures }, null, 2)}\n`);
