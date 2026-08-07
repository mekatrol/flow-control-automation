#!/usr/bin/env python3
"""Send basic FCP version-one requests over a Linux serial device."""

import argparse
import os
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
              "read-point": 0x12, "read-io": 0x15, "set-output": 0x18,
              "set-outputs": 0x1A}
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
def get_frame(destination, transaction, operation, payload):
    header = struct.pack("<2sBBHHHBH", MAGIC, VERSION, 0, destination, HOST_ADDRESS,
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
    parser.add_argument("--timeout", type=float, default=2.0)
    parser.add_argument("--transaction", type=lambda value: int(value, 0),
                        help="explicit 16-bit transaction ID; defaults to a random value")
    return parser.parse_args()


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
    transaction = secrets.randbits(16) if arguments.transaction is None else arguments.transaction
    if not 0 <= transaction <= 0xFFFF:
        raise ValueError("transaction ID must fit in 16 bits")
    request = get_frame(destination, transaction, operation, payload)
    file_descriptor = os.open(arguments.port, os.O_RDWR | os.O_NOCTTY | os.O_NONBLOCK)
    try:
        configure_port(file_descriptor, arguments.baud)
        termios.tcflush(file_descriptor, termios.TCIOFLUSH)
        os.write(file_descriptor, request)
        header, response_payload = get_decoded(read_frame(file_descriptor, arguments.timeout))
        print(f"flags=0x{header[1]:02x} source={header[3]} transaction={header[4]} operation=0x{header[5]:02x}")
        if header[1] & 0x02 and len(response_payload) >= 2:
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
