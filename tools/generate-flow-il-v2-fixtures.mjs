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
const opcodes = { readPoint: 1, constant: 2, not: 3, and: 4, or: 5, loadState: 6, proposeOutput: 7, stageState: 8, commit: 255 };

const u8 = (value) => Buffer.from([value]);
const u16 = (value) => { const bytes = Buffer.alloc(2); bytes.writeUInt16LE(value); return bytes; };
const u32 = (value) => { const bytes = Buffer.alloc(4); bytes.writeUInt32LE(value); return bytes; };
const string8 = (value) => Buffer.concat([u8(Buffer.byteLength(value)), Buffer.from(value)]);
const sha256 = (value) => createHash('sha256').update(value).digest();
const sha256Hex = (value) => createHash('sha256').update(value).digest('hex');
const compare = (left, right) => Buffer.compare(Buffer.from(left), Buffer.from(right));
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
  const stateSlotByNode = new Map(memoryIds.map((id, index) => [id, orderedIds.length + index]));
  const points = [...new Map(source.nodes.filter((node) => node.pointId).map((node) => [node.pointId, {
    id: node.pointId, direction: node.kind === 'input' ? 1 : 2
  }])).values()].sort((left, right) => compare(left.id, right.id) || left.direction - right.direction);
  const pointIndex = new Map(points.map((point, index) => [point.id, index]));
  const constants = [...new Set(source.nodes.filter((node) => node.kind === 'constant' || node.kind === 'memory')
    .map((node) => Boolean(node.value)))].sort();
  const constantIndex = (value) => constants.indexOf(Boolean(value));
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
    if (node.kind === 'constant') Object.assign(instruction, { opcode: opcodes.constant, aux: constantIndex(node.value) });
    if (node.kind === 'not') Object.assign(instruction, { opcode: opcodes.not, op0: inputSlot(id, 'in') });
    if (node.kind === 'and') Object.assign(instruction, { opcode: opcodes.and, op0: inputSlot(id, 'a'), op1: inputSlot(id, 'b') });
    if (node.kind === 'or') Object.assign(instruction, { opcode: opcodes.or, op0: inputSlot(id, 'a'), op1: inputSlot(id, 'b') });
    if (node.kind === 'memory') Object.assign(instruction, { opcode: opcodes.loadState, aux: stateSlotByNode.get(id) });
    if (node.kind === 'output') Object.assign(instruction, { opcode: opcodes.proposeOutput, op0: inputSlot(id, 'in'), aux: pointIndex.get(node.pointId) });
    instructions.push(instruction);
  }
  for (const id of memoryIds) instructions.push({ opcode: opcodes.stageState, nodeId: id, discriminator: 1,
    result: 0xffff, op0: inputSlot(id, 'in'), op1: 0xffff, aux: stateSlotByNode.get(id) });
  instructions.push({ opcode: opcodes.commit, nodeId: '', discriminator: 0, result: 0xffff, op0: 0xffff, op1: 0xffff, aux: 0xffff });

  const slots = orderedIds.map((id, index) => Buffer.concat([u8(2), u8(1), u16(0), u16(index), u16(0xffff)]));
  for (const id of memoryIds) slots.push(Buffer.concat([u8(3), u8(1), u16(0), u16(stateSlotByNode.get(id)), u16(constantIndex(nodes.get(id).value))]));
  const instructionBytes = instructions.map((item) => Buffer.concat([u8(item.opcode), u8(0), u16(item.result), u16(item.op0), u16(item.op1), u16(item.aux), u16(0)]));
  const commits = [];
  for (const id of memoryIds) commits.push(Buffer.concat([u8(1), u8(0), u16(stateSlotByNode.get(id)), u16(inputSlot(id, 'in')), u16(0)]));
  for (const id of orderedIds.filter((nodeId) => nodes.get(nodeId).kind === 'output')) commits.push(Buffer.concat([u8(2), u8(0), u16(pointIndex.get(nodes.get(id).pointId)), u16(slotByNode.get(id)), u16(0)]));
  const symbols = instructions.map((item, index) => Buffer.concat([u16(index), u8(item.discriminator), string8(item.nodeId)]));
  const debugMap = instructions.filter((item) => item.nodeId).map((item, index) => Buffer.concat([u16(index), u16(item.result), string8(item.nodeId)]));
  const sections = [
    { id: sectionIds.constants, count: constants.length, bytes: Buffer.concat(constants.map((value) => Buffer.concat([u8(1), u8(value ? 1 : 0), u16(0)]))) },
    { id: sectionIds.points, count: points.length, bytes: Buffer.concat(points.map((point) => Buffer.concat([u8(point.direction), u8(1), u8(1), u8(0), string8(point.id)]))) },
    { id: sectionIds.slots, count: slots.length, bytes: Buffer.concat(slots) },
    { id: sectionIds.instructions, count: instructionBytes.length, bytes: Buffer.concat(instructionBytes) },
    { id: sectionIds.commit, count: commits.length, bytes: Buffer.concat(commits) },
    { id: sectionIds.symbols, count: symbols.length, bytes: Buffer.concat(symbols) },
    { id: sectionIds.debugMap, count: debugMap.length, bytes: Buffer.concat(debugMap) },
    { id: sectionIds.dependencies, count: 1, bytes: Buffer.concat([u8(1), string8(source.controllerTemplateId), u32(source.controllerTemplateRevision)]) }
  ];
  let offset = envelopeLength + sections.length * directoryEntryLength;
  const directory = [];
  for (const section of sections) {
    directory.push(Buffer.concat([u16(section.id), u16(1), u32(offset), u32(section.bytes.length), u32(section.count), sha256(section.bytes)]));
    offset += section.bytes.length;
  }
  const envelope = Buffer.alloc(envelopeLength);
  envelope.write('FIL2', 0, 'ascii');
  envelope.writeUInt16LE(2, 4); envelope.writeUInt16LE(envelopeLength, 6); envelope.writeUInt32LE(offset, 8);
  envelope.writeUInt32LE(1, 12); envelope.writeUInt32LE(source.revision, 16); envelope.writeUInt32LE(source.controllerTemplateRevision, 20);
  envelope.writeUInt16LE(1, 24); envelope.writeUInt16LE(sections.length, 26); envelope.writeUInt8(1, 28);
  envelope.writeUInt32LE(instructions.length, 32); envelope.writeBigUInt64LE(0x1fn, 36);
  envelope.writeUInt32LE((orderedIds.length + memoryIds.length) * 2, 44); envelope.writeUInt32LE(16384, 48);
  envelope.writeUInt8(Buffer.byteLength(source.id), 52); envelope.write(source.id, 53, 'utf8'); envelope.writeUInt32LE(envelopeLength, 116);
  const artifact = Buffer.concat([envelope, ...directory, ...sections.map((section) => section.bytes)]);
  return { artifact, metadata: { flowId: source.id, flowRevision: source.revision, sectionCount: sections.length,
    instructionCount: instructions.length, slotCount: slots.length, pointCount: points.length, stateCount: memoryIds.length,
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
