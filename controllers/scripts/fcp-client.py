#!/usr/bin/env python3
"""Send basic FCP version-one requests over a Linux serial device."""

import argparse
import hashlib
import hmac
import os
from pathlib import Path
import secrets
import select
import struct
import termios
import time

FRAME_HEADER_SIZE = 13
MINIMUM_FRAME_SIZE = 15
MAGIC = b"FC"
VERSION = 1
HOST_ADDRESS = 0xFFFE
BROADCAST_ADDRESS = 0xFFFF
BAUD_RATES = {9600: termios.B9600, 19200: termios.B19200, 38400: termios.B38400,
              57600: termios.B57600, 115200: termios.B115200}
OPERATIONS = {"echo": 0x01, "discover": 0x02, "capabilities": 0x03,
              "info": 0x04, "health": 0x05, "list-points": 0x10,
              "read-point": 0x12, "subscribe": 0x13, "changes": 0x14,
              "read-io": 0x15, "set-output": 0x18, "relinquish": 0x19,
              "set-outputs": 0x1A, "close-session": 0x32,
              "list-flows": 0x40, "flow-metadata": 0x41, "upload": 0x42,
              "upload-status": 0x43, "download": 0x48, "activate": 0x4A,
              "deactivate": 0x4B, "remove-flow": 0x4C, "flow-runtime": 0x4D,
              "debug-step": 0x50, "debug-live-step": 0x50}
PROTECTED_OPERATIONS = set(range(0x40, 0x4E)) | set(range(0x50, 0x5C)) | {0x18, 0x19, 0x1A, 0x32}
AUTH_CHALLENGE = 0x30
AUTH_PROVE = 0x31
UPLOAD_CHUNK = 0x44
UPLOAD_VALIDATE = 0x45
UPLOAD_COMMIT = 0x46
DOWNLOAD_CHUNK = 0x49
DEBUG_LOAD_CHUNK = 0x51
DEBUG_PREPARE = 0x52
DEBUG_STEP = 0x54
DEBUG_SNAPSHOT_HEADER = 0x55
DEBUG_SNAPSHOT_CHUNK = 0x56
DEBUG_STOP = 0x58
DEBUG_ENABLE_LIVE_OUTPUT = 0x5B
AUTHENTICATED_FLAG = 0x04
RESPONSE_FLAG = 0x01
ERROR_FLAG = 0x02
AUTH_TAG_SIZE = 32
FLOW_CHUNK_SIZE = 180
ERROR_NAMES = {1: "malformed", 2: "unsupported_version", 3: "unsupported_operation",
               4: "wrong_state", 5: "invalid_argument", 6: "not_found", 7: "not_ready",
               8: "unsupported", 9: "unauthorized", 10: "forbidden", 11: "replay",
               12: "busy", 13: "queue_full", 14: "storage_unavailable", 15: "storage_full",
               16: "revision_conflict", 17: "digest_mismatch", 18: "validation_failed",
               19: "safety_rejected", 20: "internal_error"}


# Calculates the normative CRC-16/Modbus value for one byte string.
def get_crc(data):
    crc = 0xFFFF
    for value in data:
        crc ^= value
        for _ in range(8):
            crc = (crc >> 1) ^ 0xA001 if crc & 1 else crc >> 1
    return crc


# Builds one little-endian FCP request with a valid CRC.
def get_frame(destination, transaction, operation, payload, flags=0):
    header = struct.pack("<2sBBHHHBH", MAGIC, VERSION, flags, destination, HOST_ADDRESS,
                         transaction, operation, len(payload))
    body = header + payload
    return body + struct.pack("<H", get_crc(body))


# Configures a serial file descriptor for raw 8N1 traffic at the requested rate.
def configure_port(file_descriptor, baud_rate):
    attributes = termios.tcgetattr(file_descriptor)
    attributes[0] = 0
    attributes[1] = 0
    attributes[2] = termios.CS8 | termios.CREAD | termios.CLOCAL
    attributes[3] = 0
    attributes[4] = BAUD_RATES[baud_rate]
    attributes[5] = BAUD_RATES[baud_rate]
    attributes[6][termios.VMIN] = 0
    attributes[6][termios.VTIME] = 0
    termios.tcsetattr(file_descriptor, termios.TCSANOW, attributes)


# Reads one complete response using its authoritative payload length.
def read_frame(file_descriptor, timeout_seconds):
    deadline = time.monotonic() + timeout_seconds
    response = bytearray()
    expected_size = None
    while time.monotonic() < deadline and (expected_size is None or len(response) < expected_size):
        readable, _, _ = select.select([file_descriptor], [], [], max(0, deadline - time.monotonic()))
        if not readable:
            break
        response.extend(os.read(file_descriptor, 256 - len(response)))
        if len(response) >= FRAME_HEADER_SIZE:
            expected_size = MINIMUM_FRAME_SIZE + struct.unpack_from("<H", response, 11)[0]
    return bytes(response)


# Validates a response frame and returns its decoded header and payload.
def get_decoded(frame):
    if len(frame) < MINIMUM_FRAME_SIZE or frame[:2] != MAGIC or frame[2] != VERSION:
        raise ValueError("missing or invalid FCP response")
    payload_size = struct.unpack_from("<H", frame, 11)[0]
    if len(frame) != MINIMUM_FRAME_SIZE + payload_size:
        raise ValueError("truncated or trailing response bytes")
    if struct.unpack_from("<H", frame, len(frame) - 2)[0] != get_crc(frame[:-2]):
        raise ValueError("response CRC failed")
    return struct.unpack_from("<BBHHHBH", frame, 2), frame[FRAME_HEADER_SIZE:-2]


# Parses command-line arguments for one bounded protocol transaction.
def get_arguments():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("port", help="serial path, preferably /dev/serial/by-id/...")
    parser.add_argument("command", choices=OPERATIONS)
    parser.add_argument("--address", type=int, default=0)
    parser.add_argument("--baud", type=int, choices=BAUD_RATES, default=115200)
    parser.add_argument("--text", default="hello", help="echo payload text")
    parser.add_argument("--point", help="point ID for read-point, such as input-01 or output-16")
    parser.add_argument("--state", choices=("on", "off"), help="logical state for set-output")
    parser.add_argument("--outputs", type=lambda value: int(value, 0), help="16-bit bitmap for set-outputs")
    parser.add_argument("--key", help="64-hex-character per-controller protocol credential")
    parser.add_argument("--file", type=Path, help="compiled artifact path for upload or download")
    parser.add_argument("--flow-id", default="flow-1", help="bounded flow identity for upload")
    parser.add_argument("--revision", type=int, default=1, help="positive artifact revision for upload")
    parser.add_argument("--schema", type=int, choices=(2,), default=2, help="Flow IL artifact version for upload")
    parser.add_argument("--expected-revision", type=int, help="optional committed revision precondition")
    parser.add_argument("--mask", type=lambda value: int(value, 0), help="16-bit output subscription mask")
    parser.add_argument("--source-id", default="fcp-client", help="command owner used by relinquish")
    parser.add_argument("--priority", type=int, default=8, help="arbitration priority 1-16 for authenticated set-output")
    parser.add_argument("--command-class", type=int, default=0, help="bounded application-defined command class")
    parser.add_argument("--correlation", help="command correlation ID; defaults to a random value")
    parser.add_argument("--transfer-id", type=lambda value: int(value, 0), help="active upload transfer ID for status")
    parser.add_argument("--confirm-output", action="append", default=[],
                        help="exact affected point ID; repeat in canonical order for debug-live-step")
    parser.add_argument("--timeout", type=float, default=2.0)
    parser.add_argument("--transaction", type=lambda value: int(value, 0),
                        help="explicit 16-bit transaction ID; defaults to a random value")
    return parser.parse_args()


# Sends one request and returns its validated response header and payload.
def transact(file_descriptor, arguments, operation, payload, transaction, flags=0):
    request = get_frame(arguments.address, transaction, operation, payload, flags)
    termios.tcflush(file_descriptor, termios.TCIFLUSH)
    os.write(file_descriptor, request)
    return get_decoded(read_frame(file_descriptor, arguments.timeout))


# Raises a readable exception when the controller returns a reason-coded error.
def require_success(header, payload):
    if header[1] & ERROR_FLAG:
        code = struct.unpack_from("<H", payload)[0] if len(payload) >= 2 else 0
        raise RuntimeError(f"controller error: {ERROR_NAMES.get(code, 'unknown')} ({code})")


# Establishes a challenge/proof session using the provisioned per-controller key.
def authenticate(file_descriptor, arguments, key, transaction):
    client_nonce = secrets.token_bytes(16)
    header, payload = transact(file_descriptor, arguments, AUTH_CHALLENGE, client_nonce, transaction)
    require_success(header, payload)
    if len(payload) != 20:
        raise ValueError("invalid authentication challenge response")
    session_id, device_nonce = struct.unpack("<I16s", payload)
    transcript = b"FCP1PROF" + struct.pack("<HI", HOST_ADDRESS, session_id) + client_nonce + device_nonce
    proof = hmac.new(key, transcript, hashlib.sha256).digest()
    header, payload = transact(file_descriptor, arguments, AUTH_PROVE,
                               struct.pack("<I", session_id) + proof, (transaction + 1) & 0xFFFF)
    require_success(header, payload)
    if payload != struct.pack("<I", session_id):
        raise ValueError("invalid authentication proof response")
    return session_id, (transaction + 2) & 0xFFFF


# Sends one authenticated request and verifies its independently sequenced response.
def transact_authenticated(file_descriptor, arguments, key, session_id, sequence, operation, body, transaction):
    transcript = b"FCP1REQT" + struct.pack("<HIQB", HOST_ADDRESS, session_id, sequence, operation) + body
    envelope = struct.pack("<IQ", session_id, sequence) + body + hmac.new(key, transcript, hashlib.sha256).digest()
    header, payload = transact(file_descriptor, arguments, operation, envelope, transaction, AUTHENTICATED_FLAG)
    if not header[1] & AUTHENTICATED_FLAG or len(payload) < 12 + AUTH_TAG_SIZE:
        require_success(header, payload)
        raise ValueError("missing authenticated response envelope")
    response_session, response_sequence = struct.unpack_from("<IQ", payload)
    response_body = payload[12:-AUTH_TAG_SIZE]
    expected = hmac.new(key, b"FCP1RESP" + struct.pack("<HIQB", HOST_ADDRESS, response_session,
                                                       response_sequence, operation) + response_body,
                        hashlib.sha256).digest()
    if response_session != session_id or response_sequence != sequence or not hmac.compare_digest(expected, payload[-AUTH_TAG_SIZE:]):
        raise ValueError("invalid authenticated response")
    require_success(header, response_body)
    return header, response_body


# Encodes the implemented upload-begin metadata body.
def get_upload_body(arguments, artifact):
    flow_id = arguments.flow_id.encode("utf-8")
    if not flow_id or len(flow_id) >= 65 or arguments.revision <= 0 or arguments.schema <= 0:
        raise ValueError("flow ID, revision, or schema is outside protocol bounds")
    expected = arguments.expected_revision is not None
    return (bytes([len(flow_id)]) + flow_id + struct.pack("<III", arguments.revision, arguments.schema, len(artifact)) +
            hashlib.sha256(artifact).digest() + bytes([expected]) +
            struct.pack("<I", arguments.expected_revision if expected else 0))


# Executes the complete validate/commit upload state machine in bounded chunks.
def upload_flow(file_descriptor, arguments, key, session_id, transaction, artifact):
    sequence = 1
    _, payload = transact_authenticated(file_descriptor, arguments, key, session_id, sequence, OPERATIONS["upload"],
                                        get_upload_body(arguments, artifact), transaction)
    transfer_id, chunk_limit = struct.unpack("<IH", payload)
    chunk_size = min(chunk_limit, FLOW_CHUNK_SIZE)
    for offset in range(0, len(artifact), chunk_size):
        sequence += 1
        transaction = (transaction + 1) & 0xFFFF
        body = struct.pack("<II", transfer_id, offset) + artifact[offset:offset + chunk_size]
        transact_authenticated(file_descriptor, arguments, key, session_id, sequence, UPLOAD_CHUNK, body, transaction)
    for operation in (UPLOAD_VALIDATE, UPLOAD_COMMIT):
        sequence += 1
        transaction = (transaction + 1) & 0xFFFF
        transact_authenticated(file_descriptor, arguments, key, session_id, sequence, operation,
                               struct.pack("<I", transfer_id), transaction)
    return sequence, transfer_id


# Downloads the committed artifact exactly and writes it to the requested path.
def download_flow(file_descriptor, arguments, key, session_id, transaction):
    sequence = 1
    _, metadata = transact_authenticated(file_descriptor, arguments, key, session_id, sequence,
                                         OPERATIONS["download"], b"", transaction)
    id_size = metadata[0]
    total_size = struct.unpack_from("<I", metadata, 1 + id_size + 8)[0]
    artifact = bytearray()
    while len(artifact) < total_size:
        sequence += 1
        transaction = (transaction + 1) & 0xFFFF
        body = struct.pack("<IB", len(artifact), min(FLOW_CHUNK_SIZE, total_size - len(artifact)))
        _, chunk = transact_authenticated(file_descriptor, arguments, key, session_id, sequence,
                                          DOWNLOAD_CHUNK, body, transaction)
        offset = struct.unpack_from("<I", chunk)[0]
        if offset != len(artifact) or len(chunk) == 4:
            raise ValueError("invalid or empty download chunk")
        artifact.extend(chunk[4:])
    arguments.file.write_bytes(artifact)
    return sequence, bytes(artifact)


# Loads, prepares, steps, verifies, and stops one volatile shadow debug session.
def debug_step(file_descriptor, arguments, key, auth_session_id, transaction, artifact, live_output=False):
    sequence = 1
    body = struct.pack("<IBI", secrets.randbits(32), 2, len(artifact)) + hashlib.sha256(artifact).digest()
    _, response = transact_authenticated(file_descriptor, arguments, key, auth_session_id, sequence,
                                         OPERATIONS["debug-step"], body, transaction)
    debug_session_id, chunk_limit, _ = struct.unpack("<QHI", response)
    for offset in range(0, len(artifact), chunk_limit):
        sequence += 1
        transaction = (transaction + 1) & 0xFFFF
        body = struct.pack("<QI", debug_session_id, offset) + artifact[offset:offset + chunk_limit]
        transact_authenticated(file_descriptor, arguments, key, auth_session_id, sequence,
                               DEBUG_LOAD_CHUNK, body, transaction)
    sequence += 1
    transaction = (transaction + 1) & 0xFFFF
    transact_authenticated(file_descriptor, arguments, key, auth_session_id, sequence,
                           DEBUG_PREPARE, struct.pack("<Q", debug_session_id), transaction)
    if live_output:
        if not arguments.confirm_output:
            raise ValueError("debug-live-step requires at least one --confirm-output")
        encoded_points = [point.encode("utf-8") for point in arguments.confirm_output]
        if any(not point or len(point) > 63 for point in encoded_points):
            raise ValueError("confirmed output IDs must contain 1-63 UTF-8 bytes")
        confirmation = struct.pack("<QB", debug_session_id, len(encoded_points)) + b"".join(
            bytes([len(point)]) + point for point in encoded_points)
        sequence += 1
        transaction = (transaction + 1) & 0xFFFF
        _, policy = transact_authenticated(file_descriptor, arguments, key, auth_session_id, sequence,
                                            DEBUG_ENABLE_LIVE_OUTPUT, confirmation, transaction)
        if policy != struct.pack("<BI", 8, 1000):
            raise ValueError("controller returned an unexpected live-output safety policy")
    sequence += 1
    transaction = (transaction + 1) & 0xFFFF
    _, step = transact_authenticated(file_descriptor, arguments, key, auth_session_id, sequence,
                                     DEBUG_STEP, struct.pack("<Q", debug_session_id), transaction)
    tick, snapshot_length, snapshot_digest = struct.unpack("<QI32s", step)
    sequence += 1
    transaction = (transaction + 1) & 0xFFFF
    _, header = transact_authenticated(file_descriptor, arguments, key, auth_session_id, sequence,
                                       DEBUG_SNAPSHOT_HEADER, struct.pack("<QQ", debug_session_id, tick), transaction)
    header_session, header_tick, total_length, chunk_count, _, header_digest = struct.unpack("<QQIHH32s", header)
    if (header_session != debug_session_id or header_tick != tick or total_length != snapshot_length or
            header_digest != snapshot_digest):
        raise ValueError("snapshot header does not match step response")
    snapshot = bytearray()
    for chunk_index in range(chunk_count):
        sequence += 1
        transaction = (transaction + 1) & 0xFFFF
        request = struct.pack("<QQH", debug_session_id, tick, chunk_index)
        _, chunk = transact_authenticated(file_descriptor, arguments, key, auth_session_id, sequence,
                                          DEBUG_SNAPSHOT_CHUNK, request, transaction)
        chunk_session, chunk_tick, returned_index, returned_count, offset = struct.unpack_from("<QQHHI", chunk)
        if (chunk_session != debug_session_id or chunk_tick != tick or returned_index != chunk_index or returned_count != chunk_count or offset != len(snapshot):
            raise ValueError("inconsistent snapshot chunk")
        snapshot.extend(chunk[24:])
    if len(snapshot) != snapshot_length or hashlib.sha256(snapshot).digest() != snapshot_digest:
        raise ValueError("snapshot digest validation failed")
    sequence += 1
    transaction = (transaction + 1) & 0xFFFF
    transact_authenticated(file_descriptor, arguments, key, auth_session_id, sequence,
                           DEBUG_STOP, struct.pack("<Q", debug_session_id), transaction)
    return sequence, debug_session_id, tick, bytes(snapshot)


# Authenticates the close response and releases one bounded controller session slot.
def close_session(file_descriptor, arguments, key, session_id, sequence, transaction):
    transact_authenticated(file_descriptor, arguments, key, session_id, sequence, OPERATIONS["close-session"],
                           struct.pack("<I", session_id), transaction)


# Runs one request and prints validated response metadata and payload bytes.
def main():
    arguments = get_arguments()
    operation = OPERATIONS[arguments.command]
    destination = BROADCAST_ADDRESS if arguments.command == "discover" else arguments.address
    payload = struct.pack("<IBH", int(time.time()) & 0xFFFFFFFF, 8, 10) if arguments.command == "discover" else b""
    if arguments.command == "echo":
        payload = arguments.text.encode("utf-8")
    elif arguments.command == "list-points":
        payload = struct.pack("<HB", 0, 32)
    elif arguments.command == "read-point":
        if not arguments.point:
            raise ValueError("read-point requires --point")
        point_id = arguments.point.encode("utf-8")
        payload = bytes([len(point_id)]) + point_id
    elif arguments.command == "set-output":
        if not arguments.point or arguments.state is None:
            raise ValueError("set-output requires --point output-NN and --state on|off")
        point_id = arguments.point.encode("utf-8")
        payload = bytes([len(point_id)]) + point_id + bytes([arguments.state == "on"])
    elif arguments.command == "set-outputs":
        if arguments.outputs is None or not 0 <= arguments.outputs <= 0xFFFF:
            raise ValueError("set-outputs requires a 16-bit --outputs bitmap")
        payload = struct.pack("<H", arguments.outputs)
    elif arguments.command == "subscribe":
        if arguments.mask is None or not 0 <= arguments.mask <= 0xFFFF:
            raise ValueError("subscribe requires a 16-bit --mask")
        payload = struct.pack("<H", arguments.mask)
    elif arguments.command == "relinquish":
        if not arguments.point or not arguments.point.startswith("output-"):
            raise ValueError("relinquish requires --point output-NN")
        output = int(arguments.point.removeprefix("output-")) - 1
        source_id = arguments.source_id.encode("utf-8")
        payload = bytes([output, len(source_id)]) + source_id
    elif arguments.command == "upload-status":
        if arguments.transfer_id is None:
            raise ValueError("upload-status requires --transfer-id")
        payload = struct.pack("<I", arguments.transfer_id)
    transaction = secrets.randbits(16) if arguments.transaction is None else arguments.transaction
    if not 0 <= transaction <= 0xFFFF:
        raise ValueError("transaction ID must fit in 16 bits")
    key = bytes.fromhex(arguments.key) if arguments.key else None
    if key is not None and len(key) != 32:
        raise ValueError("--key must contain exactly 64 hexadecimal characters")
    is_protected = operation in PROTECTED_OPERATIONS
    if is_protected and key is None:
        raise ValueError(f"{arguments.command} requires --key")
    if arguments.command in ("upload", "download", "debug-step", "debug-live-step") and arguments.file is None:
        raise ValueError(f"{arguments.command} requires --file")
    file_descriptor = os.open(arguments.port, os.O_RDWR | os.O_NOCTTY | os.O_NONBLOCK)
    try:
        configure_port(file_descriptor, arguments.baud)
        if is_protected:
            session_id, transaction = authenticate(file_descriptor, arguments, key, transaction)
            if arguments.command == "close-session":
                payload = struct.pack("<I", session_id)
            elif arguments.command == "set-output":
                output = int(arguments.point.removeprefix("output-")) - 1
                source_id = arguments.source_id.encode("utf-8")
                correlation = (arguments.correlation or secrets.token_hex(8)).encode("utf-8")
                if not 0 <= output < 16 or not 1 <= arguments.priority <= 16 or len(source_id) not in range(1, 33) or len(correlation) not in range(1, 33):
                    raise ValueError("authenticated command metadata is outside protocol bounds")
                payload = (bytes([output, arguments.state == "on", len(source_id)]) + source_id +
                           bytes([arguments.command_class & 0xFF, arguments.priority, len(correlation)]) + correlation +
                           struct.pack("<qq", 0, -(1 << 63)))
            if arguments.command == "upload":
                artifact = arguments.file.read_bytes()
                sequence, transfer_id = upload_flow(file_descriptor, arguments, key, session_id, transaction, artifact)
                close_session(file_descriptor, arguments, key, session_id, sequence + 1,
                              (transaction + sequence) & 0xFFFF)
                print(f"uploaded={len(artifact)} transfer={transfer_id} sequence={sequence}")
                return
            if arguments.command == "download":
                sequence, artifact = download_flow(file_descriptor, arguments, key, session_id, transaction)
                close_session(file_descriptor, arguments, key, session_id, sequence + 1,
                              (transaction + sequence) & 0xFFFF)
                print(f"downloaded={len(artifact)} path={arguments.file} sequence={sequence}")
                return
            if arguments.command in ("debug-step", "debug-live-step"):
                artifact = arguments.file.read_bytes()
                sequence, debug_session_id, tick, snapshot = debug_step(
                    file_descriptor, arguments, key, session_id, transaction, artifact,
                    arguments.command == "debug-live-step")
                close_session(file_descriptor, arguments, key, session_id, sequence + 1,
                              (transaction + sequence) & 0xFFFF)
                print(f"debug_session={debug_session_id} tick={tick} snapshot_bytes={len(snapshot)}")
                print(snapshot.hex(" "))
                return
            header, response_payload = transact_authenticated(file_descriptor, arguments, key, session_id, 1,
                                                              operation, payload, transaction)
            if arguments.command != "close-session":
                close_session(file_descriptor, arguments, key, session_id, 2, (transaction + 1) & 0xFFFF)
        else:
            arguments.address = destination
            header, response_payload = transact(file_descriptor, arguments, operation, payload, transaction)
        print(f"flags=0x{header[1]:02x} source={header[3]} transaction={header[4]} operation=0x{header[5]:02x}")
        if header[1] & ERROR_FLAG and len(response_payload) >= 2:
            error_code = struct.unpack_from("<H", response_payload)[0]
            print(f"error={ERROR_NAMES.get(error_code, 'unknown')} code={error_code}")
        if arguments.command == "read-io" and len(response_payload) == 17:
            inputs, outputs, validity = struct.unpack_from("<HHB", response_payload)
            print(f"inputs=0x{inputs:04x} outputs=0x{outputs:04x} validity=0x{validity:02x}")
        print(response_payload.hex(" "))
    finally:
        os.close(file_descriptor)


if __name__ == "__main__":
    main()
