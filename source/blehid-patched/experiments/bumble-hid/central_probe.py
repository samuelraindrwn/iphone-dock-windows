"""Pair with a BLE peripheral as a central and report the identity it hands over.

Used to check what the Windows GattServiceProvider peripheral distributes in
SMP_IDENTITY_ADDRESS_INFORMATION versus the address it actually advertises on. A
mismatch there stops a bonded host from ever reconnecting, because the host stores
the identity address and connects to that.

  python central_probe.py --transport usb:0B05:190E --scan
  python central_probe.py --transport usb:0B05:190E --address AA:BB:CC:DD:EE:FF/P
"""

import argparse
import asyncio
import logging
import os
from pathlib import Path

from bumble.core import AdvertisingData, UUID
from bumble.device import Device, Peer
from bumble.hci import Address
from bumble.pairing import PairingConfig, PairingDelegate
from bumble.transport import open_transport

HERE = Path(__file__).resolve().parent
HID_SERVICE_UUID = '1812'

logger = logging.getLogger('central_probe')


def describe_flags(value: int) -> str:
    names = [flag.name for flag in AdvertisingData.Flags if value & flag]
    return ' | '.join(names) or 'none'


async def dump(device: Device, name_filter: str, seconds: float) -> None:
    """Prints every advertising structure rather than just the name, because the Flags
    byte and the address type are what separate a dual-mode host from an LE-only one."""
    seen: set[tuple[str, bytes]] = set()

    def on_advertisement(advertisement):
        name = advertisement.data.get(AdvertisingData.COMPLETE_LOCAL_NAME) or ''
        if name_filter and name_filter.lower() not in name.lower():
            return
        # Keyed on the payload too, so a rotating address or a changed payload reprints.
        key = (str(advertisement.address), bytes(advertisement.data))
        if key in seen:
            return
        seen.add(key)
        print(
            f'\n{advertisement.address}  rssi={advertisement.rssi}  '
            f'connectable={advertisement.is_connectable}'
        )
        for ad_type, value in advertisement.data.ad_structures:
            label = getattr(ad_type, 'name', None) or f'0x{int(ad_type):02X}'
            print(f'  {label:<44} {value.hex()}')
            if ad_type == AdvertisingData.FLAGS and value:
                print(f'  {"":<44} {describe_flags(value[0])}')

    device.on('advertisement', on_advertisement)
    await device.start_scanning(active=True)
    await asyncio.sleep(seconds)
    await device.stop_scanning()
    if not seen:
        logger.warning('nothing matched %r', name_filter or 'any advertiser')


async def scan(device: Device, seconds: float) -> None:
    seen: dict[str, str] = {}

    def on_advertisement(advertisement):
        address = str(advertisement.address)
        if address in seen:
            return
        name = advertisement.data.get(AdvertisingData.COMPLETE_LOCAL_NAME) or ''
        uuids = advertisement.data.get(
            AdvertisingData.COMPLETE_LIST_OF_16_BIT_SERVICE_CLASS_UUIDS
        ) or []
        uuids += advertisement.data.get(
            AdvertisingData.INCOMPLETE_LIST_OF_16_BIT_SERVICE_CLASS_UUIDS
        ) or []
        seen[address] = name
        marker = ' <-- HID' if any(HID_SERVICE_UUID in str(u) for u in uuids) else ''
        print(f'{address}  rssi={advertisement.rssi:4}  {name}{marker}')

    device.on('advertisement', on_advertisement)
    await device.start_scanning(active=True)
    await asyncio.sleep(seconds)
    await device.stop_scanning()


async def find_by_name(device: Device, name_filter: str, seconds: float) -> str | None:
    """Windows advertises a resolvable private address that rotates, so the address has
    to be captured and used immediately rather than passed in from an earlier scan."""
    found: asyncio.Future[str] = asyncio.get_running_loop().create_future()

    def on_advertisement(advertisement):
        if found.done():
            return
        name = advertisement.data.get(AdvertisingData.COMPLETE_LOCAL_NAME) or ''
        if name_filter.lower() in name.lower():
            logger.info('found %s at %s', name, advertisement.address)
            found.set_result(str(advertisement.address))

    device.on('advertisement', on_advertisement)
    await device.start_scanning(active=True)
    try:
        return await asyncio.wait_for(found, timeout=seconds)
    except asyncio.TimeoutError:
        return None
    finally:
        device.remove_listener('advertisement', on_advertisement)
        await device.stop_scanning()


async def probe(device: Device, address: str, hold: float) -> None:
    logger.info('connecting to %s', address)
    connection = await device.connect(address)
    logger.info('connected: %s', connection)

    # Windows puts up a consent dialog, so pairing can take as long as a human takes.
    try:
        await connection.pair()
        logger.info('paired')
    except Exception as error:
        logger.error('pairing failed: %s', error)

    if device.keystore is not None:
        # The keystore key IS the identity address the peer distributed; PairingKeys
        # itself has no identity_address field.
        for identity, keys in await device.keystore.get_all():
            logger.info(
                'bond: identity=%s irk=%s ltk=%s',
                identity,
                'present' if keys.irk else 'absent',
                'present' if keys.ltk else 'absent',
            )

    logger.info('holding the connection for %.0fs', hold)
    await asyncio.sleep(hold)

    if connection.is_encrypted:
        logger.info('link is encrypted')

    await connection.disconnect()


async def dump_gatt(device: Device, address: str) -> None:
    """Discovery only, no pairing: shows whether the handle layout moves between runs."""
    logger.info('connecting to %s', address)
    connection = await device.connect(address)
    peer = Peer(connection)

    await peer.discover_services()
    for service in peer.services:
        await peer.discover_characteristics(service=service)
        logger.info(
            'service %s handles 0x%04X-0x%04X', service.uuid, service.handle, service.end_group_handle
        )
        for characteristic in service.characteristics:
            await peer.discover_descriptors(characteristic)
            logger.info(
                '  char %s decl=0x%04X value=0x%04X',
                characteristic.uuid,
                characteristic.handle - 1,
                characteristic.handle,
            )
            if characteristic.uuid == UUID.from_16_bits(0x2B2A):
                logger.info('    database hash %s', (await characteristic.read_value()).hex())
            for descriptor in characteristic.descriptors:
                logger.info('    desc %s 0x%04X', descriptor.type, descriptor.handle)

    await connection.disconnect()


async def run(args: argparse.Namespace) -> None:
    async with await open_transport(args.transport) as hci_transport:
        device = Device.with_hci(
            'central probe',
            Address('F1:F2:F3:F4:F5:F6'),
            hci_transport.source,
            hci_transport.sink,
        )
        device.pairing_config_factory = lambda _connection: PairingConfig(
            sc=True,
            mitm=False,
            bonding=True,
            delegate=PairingDelegate(PairingDelegate.IoCapability.NO_OUTPUT_NO_INPUT),
        )

        await device.power_on()

        if args.dump is not None:
            await dump(device, args.dump, args.seconds)
        elif args.gatt:
            address = args.address or await find_by_name(device, args.name, args.seconds)
            if address is None:
                logger.error('no advertiser matching %r', args.name)
                return
            await dump_gatt(device, address)
        elif args.name:
            address = await find_by_name(device, args.name, args.seconds)
            if address is None:
                logger.error('no advertiser matching %r', args.name)
                return
            await probe(device, address, args.hold)
        elif args.address:
            await probe(device, args.address, args.hold)
        else:
            await scan(device, args.seconds)


def main() -> None:
    os.chdir(HERE)
    os.environ.setdefault('BUMBLE_RTK_FIRMWARE_DIR', str(HERE / 'rtk_fw'))

    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--transport', default='usb:0B05:190E')
    parser.add_argument('--address', help='peer address, e.g. AA:BB:CC:DD:EE:FF/P')
    parser.add_argument('--name', help='scan for this name, then connect immediately')
    parser.add_argument('--scan', action='store_true', help='list nearby advertisers')
    parser.add_argument(
        '--dump',
        nargs='?',
        const='',
        metavar='NAME',
        help='print full advertising payloads, optionally filtered by name substring',
    )
    parser.add_argument('--seconds', type=float, default=8.0)
    parser.add_argument(
        '--gatt',
        action='store_true',
        help='discover services and print handles, without pairing',
    )
    parser.add_argument(
        '--hold',
        type=float,
        default=90.0,
        help='seconds to keep the link open after pairing',
    )
    args = parser.parse_args()

    logging.basicConfig(
        level=os.environ.get('BUMBLE_LOGLEVEL', 'INFO').upper(),
        format='%(asctime)s %(levelname)s %(message)s',
    )
    asyncio.run(run(args))


if __name__ == '__main__':
    main()
