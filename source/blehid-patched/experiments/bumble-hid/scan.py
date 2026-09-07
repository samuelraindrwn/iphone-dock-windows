"""Scan for BLE advertisements with the PC's own radio.

Independent check that the Bumble peripheral on the USB dongle is actually on air,
using the Windows stack rather than the dongle itself.
"""

import argparse
import asyncio

from bleak import BleakClient, BleakScanner


async def scan(seconds: float, name_filter: str | None) -> None:
    devices = await BleakScanner.discover(timeout=seconds, return_adv=True)
    for device, adv in devices.values():
        name = adv.local_name or device.name or ''
        if name_filter and name_filter.lower() not in name.lower():
            continue
        print(f'{device.address}  rssi={adv.rssi:4d}  {name}')
        if adv.service_uuids:
            print(f'    services: {", ".join(adv.service_uuids)}')


async def connect(address: str) -> None:
    async with BleakClient(address, timeout=20.0) as client:
        print(f'connected: {client.is_connected}')
        for service in client.services:
            print(f'{service.uuid}  {service.description}')
            for char in service.characteristics:
                print(f'    {char.uuid}  {",".join(char.properties)}')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--seconds', type=float, default=10.0)
    parser.add_argument('--name', help='Only show devices whose name contains this')
    parser.add_argument('--connect', help='Connect to this address and dump its GATT')
    args = parser.parse_args()
    if args.connect:
        asyncio.run(connect(args.connect))
    else:
        asyncio.run(scan(args.seconds, args.name))
