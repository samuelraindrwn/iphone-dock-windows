"""Summarise what a phone's Bluetooth stack did, from an Android btsnoop_hci.log.

Answers one question: when a bonded peripheral goes away and comes back, does the host
try to reconnect, and over which transport? Prints connection attempts (LE and Classic),
scanning, and accept/resolving list changes, so a silent host can be told apart from one
that tries and fails.

Samsung keeps the log at FS/data/log/bt/btsnoop_hci.log inside a bug report, NOT the
AOSP path FS/data/misc/bluetooth/logs/.

    python analyze_btsnoop.py btsnoop_hci.log --since 03:00:00 --address AA:BB:CC:DD:EE:FF
"""

import argparse
import datetime
import struct

# btsnoop timestamps count microseconds from year 0; 62168256000 s is the Unix epoch offset.
EPOCH_OFFSET_US = 62168256000 * 1000000

HCI_COMMAND, HCI_ACL, HCI_SCO, HCI_EVENT = 0x01, 0x02, 0x03, 0x04

COMMANDS = {
    0x0405: 'Create Connection (CLASSIC page)',
    0x0406: 'Disconnect',
    0x042B: 'Accept Connection Request (CLASSIC)',
    0x200C: 'LE Set Scan Enable',
    0x200D: 'LE Create Connection',
    0x200E: 'LE Create Connection Cancel',
    0x2011: 'LE Add Device To Filter Accept List',
    0x2012: 'LE Remove Device From Filter Accept List',
    0x2010: 'LE Clear Filter Accept List',
    0x2027: 'LE Add Device To Resolving List',
    0x2028: 'LE Remove Device From Resolving List',
    0x2029: 'LE Clear Resolving List',
    0x202D: 'LE Set Address Resolution Enable',
    0x2042: 'LE Set Extended Scan Enable',
    0x2043: 'LE Extended Create Connection',
    0x2019: 'LE Enable Encryption',
    0x201A: 'LE Long Term Key Request Reply',
    0x201B: 'LE Long Term Key Request Negative Reply',
    0x0419: 'Authentication Requested (CLASSIC)',
    0x0413: 'Set Connection Encryption (CLASSIC)',
}

ATT_OPCODES = {
    0x01: 'Error Response', 0x02: 'Exchange MTU Request', 0x03: 'Exchange MTU Response',
    0x04: 'Find Information Request', 0x05: 'Find Information Response',
    0x06: 'Find By Type Value Request', 0x07: 'Find By Type Value Response',
    0x08: 'Read By Type Request', 0x09: 'Read By Type Response',
    0x0A: 'Read Request', 0x0B: 'Read Response',
    0x0C: 'Read Blob Request', 0x0D: 'Read Blob Response',
    0x10: 'Read By Group Type Request', 0x11: 'Read By Group Type Response',
    0x12: 'Write Request', 0x13: 'Write Response', 0x52: 'Write Command',
    0x1B: 'Handle Value Notification', 0x1D: 'Handle Value Indication',
    0x1E: 'Handle Value Confirmation',
}

SMP_OPCODES = {
    0x01: 'Pairing Request', 0x02: 'Pairing Response', 0x03: 'Pairing Confirm',
    0x04: 'Pairing Random', 0x05: 'Pairing Failed', 0x06: 'Encryption Information',
    0x07: 'Central Identification', 0x08: 'Identity Information',
    0x09: 'Identity Address Information', 0x0A: 'Signing Information',
    0x0B: 'Security Request',
}

L2CAP_CIDS = {0x0001: 'signalling', 0x0004: 'ATT', 0x0005: 'LE signalling', 0x0006: 'SMP'}


def address(raw: bytes) -> str:
    return ':'.join(f'{b:02X}' for b in reversed(raw))


def read_records(path: str):
    with open(path, 'rb') as f:
        header = f.read(16)
        if not header.startswith(b'btsnoop\x00'):
            raise SystemExit('not a btsnoop file')
        while True:
            record_header = f.read(24)
            if len(record_header) < 24:
                return
            _, included, flags, _, timestamp = struct.unpack('>IIIIq', record_header)
            payload = f.read(included)
            if len(payload) < included:
                return
            # Android writes wall-clock time, so no timezone conversion here.
            when = datetime.datetime(1970, 1, 1) + datetime.timedelta(
                microseconds=timestamp - EPOCH_OFFSET_US
            )
            yield when, flags, payload


def describe_acl(body: bytes, handles: set[int] | None) -> str | None:
    if len(body) < 8:
        return None
    header = struct.unpack('<H', body[:2])[0]
    handle, pb = header & 0x0FFF, (header >> 12) & 0x03
    if handles is not None and handle not in handles:
        return None
    if pb == 0x01:  # continuation of an earlier L2CAP frame, no header to read
        return f'ACL handle=0x{handle:04X} (continuation, {len(body) - 4} bytes)'
    cid = struct.unpack('<H', body[6:8])[0]
    data = body[8:]
    label = L2CAP_CIDS.get(cid, f'CID 0x{cid:04X}')
    if cid == 0x0004 and data:
        name = ATT_OPCODES.get(data[0], f'ATT opcode 0x{data[0]:02X}')
        if data[0] == 0x01 and len(data) >= 5:
            return (
                f'ACL handle=0x{handle:04X} ATT {name}: req=0x{data[1]:02X} '
                f'attr=0x{struct.unpack("<H", data[2:4])[0]:04X} error=0x{data[4]:02X}'
            )
        if data[0] in (0x08, 0x10) and len(data) >= 7:
            start, end = struct.unpack('<HH', data[1:5])
            uuid = (
                f'0x{struct.unpack("<H", data[5:7])[0]:04X}'
                if len(data) == 7
                else address(data[5:21])
            )
            return (
                f'ACL handle=0x{handle:04X} ATT {name} '
                f'handles 0x{start:04X}-0x{end:04X} uuid={uuid}'
            )
        if data[0] in (0x09, 0x11) and len(data) >= 2:
            return (
                f'ACL handle=0x{handle:04X} ATT {name} '
                f'({(len(data) - 2) // data[1] if data[1] else 0} entries) '
                f'{data[2:].hex()}'
            )
        if data[0] == 0x0B:
            return f'ACL handle=0x{handle:04X} ATT {name} value={data[1:].hex()}'
        if data[0] in (0x0A, 0x12, 0x52, 0x1B, 0x1D) and len(data) >= 3:
            extra = data[3:].hex()
            return (
                f'ACL handle=0x{handle:04X} ATT {name} '
                f'attr=0x{struct.unpack("<H", data[1:3])[0]:04X}'
                + (f' value={extra}' if extra else '')
            )
        return f'ACL handle=0x{handle:04X} ATT {name}'
    if cid == 0x0006 and data:
        name = SMP_OPCODES.get(data[0], f'opcode 0x{data[0]:02X}')
        if data[0] == 0x09 and len(data) >= 8:
            kind = 'public' if data[1] == 0 else 'random'
            return f'ACL handle=0x{handle:04X} SMP {name} {address(data[2:8])} ({kind})'
        return f'ACL handle=0x{handle:04X} SMP {name}'
    return f'ACL handle=0x{handle:04X} {label} ({len(data)} bytes)'


def describe(payload: bytes, acl_handles: set[int] | None = None, verbose: bool = False) -> str | None:
    kind, body = payload[0], payload[1:]

    if kind == HCI_ACL:
        return describe_acl(body, acl_handles)

    if kind == HCI_COMMAND and len(body) >= 3:
        opcode = struct.unpack('<H', body[:2])[0]
        name = COMMANDS.get(opcode)
        if name is None:
            return None
        params = body[3:]
        if opcode in (0x2019, 0x201A, 0x201B, 0x0419, 0x0413) and len(params) >= 2:
            return f'{name} handle=0x{struct.unpack("<H", params[:2])[0]:04X}'
        if opcode == 0x0406 and len(params) >= 3:
            return f'{name} handle=0x{struct.unpack("<H", params[:2])[0]:04X} reason=0x{params[2]:02X}'
        if opcode == 0x0405 and len(params) >= 6:
            return f'{name} -> {address(params[:6])}'
        if opcode == 0x200D and len(params) >= 12:
            return f'{name} -> {address(params[6:12])} (type {params[5]})'
        if opcode == 0x2043 and len(params) >= 9:
            return f'{name} -> {address(params[3:9])} (type {params[2]})'
        if opcode in (0x2011, 0x2012, 0x2027, 0x2028) and len(params) >= 7:
            return f'{name} {address(params[1:7])} (type {params[0]})'
        if opcode in (0x200C, 0x2042) and params:
            return f'{name} {"on" if params[0] else "off"}'
        return name

    if kind == HCI_EVENT and len(body) >= 2:
        code, data = body[0], body[2:]
        if code == 0x03 and len(data) >= 11:  # Connection Complete (Classic)
            return (
                f'CLASSIC Connection Complete status=0x{data[0]:02X} '
                f'{address(data[3:9])} link_type={data[9]}'
            )
        if code == 0x05 and len(data) >= 4:  # Disconnection Complete
            return (
                f'Disconnection Complete status=0x{data[0]:02X} '
                f'handle=0x{struct.unpack("<H", data[1:3])[0]:04X} reason=0x{data[3]:02X}'
            )
        if code == 0x04 and len(data) >= 6:  # Connection Request (inbound Classic)
            return f'CLASSIC Connection Request from {address(data[:6])}'
        if code == 0x08 and len(data) >= 4:  # Encryption Change
            return (
                f'Encryption Change status=0x{data[0]:02X} '
                f'handle=0x{struct.unpack("<H", data[1:3])[0]:04X} enabled={data[3]}'
            )
        if code == 0x30 and len(data) >= 3:  # Encryption Key Refresh Complete
            return (
                f'Encryption Key Refresh status=0x{data[0]:02X} '
                f'handle=0x{struct.unpack("<H", data[1:3])[0]:04X}'
            )
        if code == 0x59 and len(data) >= 5:  # Encryption Change v2, supersedes 0x08
            return (
                f'Encryption Change v2 status=0x{data[0]:02X} '
                f'handle=0x{struct.unpack("<H", data[1:3])[0]:04X} '
                f'enabled={data[3]} key_size={data[4]}'
            )
        if code == 0x3E and data:  # LE Meta
            subevent, le = data[0], data[1:]
            if subevent == 0x05 and len(le) >= 2:
                return (
                    'LE Long Term Key Request '
                    f'handle=0x{struct.unpack("<H", le[:2])[0]:04X}'
                )
            if subevent == 0x01 and len(le) >= 11:
                return (
                    f'LE Connection Complete status=0x{le[0]:02X} '
                    f'handle=0x{struct.unpack("<H", le[1:3])[0]:04X} '
                    f'{address(le[5:11])} type={le[4]} role={le[3]}'
                )
            if subevent == 0x0A and len(le) >= 23:
                return (
                    f'LE Enhanced Connection Complete status=0x{le[0]:02X} '
                    f'handle=0x{struct.unpack("<H", le[1:3])[0]:04X} '
                    f'{address(le[5:11])} type={le[4]} role={le[3]} '
                    f'peer_rpa={address(le[17:23])}'
                )
            if verbose:
                return f'LE Meta subevent 0x{subevent:02X} ({len(le)} bytes)'
        if verbose:
            return f'event 0x{code:02X} ({len(data)} bytes)'
    return None


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('path')
    parser.add_argument('--since', help='only show events at or after this HH:MM:SS')
    parser.add_argument('--until', help='only show events before this HH:MM:SS')
    parser.add_argument('--address', help='also report every mention of this address')
    parser.add_argument(
        '--handle',
        action='append',
        help='also decode ACL traffic on this connection handle, e.g. 0x0007',
    )
    parser.add_argument(
        '--verbose',
        action='store_true',
        help='also print HCI events this tool does not decode, so none are missed',
    )
    args = parser.parse_args()

    acl_handles = {int(h, 0) for h in args.handle} if args.handle else None

    total = 0
    first = last = None
    shown = 0
    hits = 0
    needle = args.address.upper() if args.address else None

    for when, flags, payload in read_records(args.path):
        total += 1
        first = first or when
        last = when
        stamp = when.strftime('%H:%M:%S.%f')[:-3]
        if args.since and stamp < args.since:
            continue
        if args.until and stamp >= args.until:
            continue
        text = describe(payload, acl_handles, args.verbose)
        if text is None:
            continue
        direction = '>' if flags & 1 == 0 else '<'
        if needle and needle in text:
            hits += 1
            text += '   <<< TARGET'
        print(f'{stamp} {direction} {text}')
        shown += 1

    print(f'\n{total} records, {first:%H:%M:%S} to {last:%H:%M:%S}, {shown} of interest')
    if needle:
        print(f'{hits} mention {needle}')


if __name__ == '__main__':
    main()
