#!/usr/bin/env python3
"""Send basic FCP version-one requests over a Linux serial device."""

import argparse
import os
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
              "info": 0x04, "health": 0x05}


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
    parser.add_argument("--timeout", type=float, default=2.0)
    return parser.parse_args()


# Runs one request and prints validated response metadata and payload bytes.
def main():
    arguments = get_arguments()
    operation = OPERATIONS[arguments.command]
    destination = BROADCAST_ADDRESS if arguments.command == "discover" else arguments.address
    payload = struct.pack("<IBH", int(time.time()) & 0xFFFFFFFF, 8, 10) if arguments.command == "discover" else b""
    if arguments.command == "echo":
        payload = arguments.text.encode("utf-8")
    request = get_frame(destination, 1, operation, payload)
    file_descriptor = os.open(arguments.port, os.O_RDWR | os.O_NOCTTY | os.O_NONBLOCK)
    try:
        configure_port(file_descriptor, arguments.baud)
        termios.tcflush(file_descriptor, termios.TCIOFLUSH)
        os.write(file_descriptor, request)
        header, response_payload = get_decoded(read_frame(file_descriptor, arguments.timeout))
        print(f"flags=0x{header[1]:02x} source={header[3]} transaction={header[4]} operation=0x{header[5]:02x}")
        print(response_payload.hex(" "))
    finally:
        os.close(file_descriptor)


if __name__ == "__main__":
    main()
