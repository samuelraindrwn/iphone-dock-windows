"""BLE HID keyboard/mouse peripheral built on Bumble, talking straight to a USB HCI dongle.

Experiment: Windows' GattServiceProvider only exposes IsConnectable/IsDiscoverable, so the
app cannot advertise restricted to already-bonded centrals. That is the leading explanation
for hosts never reconnecting on their own. Bumble drives the controller directly, so the
advertising filter policy and the filter accept list are both reachable here.

  --advertising open     undirected, anyone may connect (needed for the first pairing)
  --advertising bonded   accept-list filtered, only bonded peers may connect
  --advertising directed directed at one bonded peer's identity address

Run `bonded` or `directed` after a bond exists to see whether the phone reconnects unaided.
"""

from __future__ import annotations

import argparse
import asyncio
import logging
import os
import sys
from pathlib import Path

from bumble import data_types, hci
from bumble.core import AdvertisingData, Appearance
from bumble.device import (
    AdvertisingEventProperties,
    AdvertisingParameters,
    AdvertisingType,
    Device,
)
from bumble.gatt import (
    GATT_APPEARANCE_CHARACTERISTIC,
    GATT_BATTERY_LEVEL_CHARACTERISTIC,
    GATT_BATTERY_SERVICE,
    GATT_DEVICE_INFORMATION_SERVICE,
    GATT_GENERIC_ACCESS_SERVICE,
    GATT_HID_CONTROL_POINT_CHARACTERISTIC,
    GATT_HID_INFORMATION_CHARACTERISTIC,
    GATT_HUMAN_INTERFACE_DEVICE_SERVICE,
    GATT_MANUFACTURER_NAME_STRING_CHARACTERISTIC,
    GATT_PNP_ID_CHARACTERISTIC,
    GATT_PROTOCOL_MODE_CHARACTERISTIC,
    GATT_REPORT_CHARACTERISTIC,
    GATT_REPORT_MAP_CHARACTERISTIC,
    GATT_REPORT_REFERENCE_DESCRIPTOR,
    Characteristic,
    CharacteristicValue,
    Descriptor,
    Service,
)
from bumble.pairing import PairingConfig, PairingDelegate
from bumble.transport import open_transport

import hid_descriptors as hid

logger = logging.getLogger(__name__)

HERE = Path(__file__).resolve().parent

# Process scan and connection requests only from devices in the filter accept list.
FILTER_POLICY_ACCEPT_LIST_ONLY = 3

KEYBOARD_APPEARANCE = 0x03C1

# What Android recorded for the Windows PC: major class computer, minor class desktop.
WINDOWS_CLASS_OF_DEVICE = 0x2E4104

# vendor_id_source=USB-IF, vendor=Linux Foundation, product 1, version 1.0.0
PNP_ID = bytes([0x02, 0x6B, 0x1D, 0x01, 0x00, 0x00, 0x01])


def build_hid_service() -> tuple[Service, Characteristic, Characteristic]:
    keyboard_input = Characteristic(
        GATT_REPORT_CHARACTERISTIC,
        Characteristic.Properties.READ | Characteristic.Properties.NOTIFY,
        Characteristic.READ_REQUIRES_ENCRYPTION,
        hid.keyboard_release(),
        [
            Descriptor(
                GATT_REPORT_REFERENCE_DESCRIPTOR,
                Descriptor.READABLE,
                bytes([hid.KEYBOARD_REPORT_ID, hid.REPORT_TYPE_INPUT]),
            )
        ],
    )

    mouse_input = Characteristic(
        GATT_REPORT_CHARACTERISTIC,
        Characteristic.Properties.READ | Characteristic.Properties.NOTIFY,
        Characteristic.READ_REQUIRES_ENCRYPTION,
        hid.mouse_report(),
        [
            Descriptor(
                GATT_REPORT_REFERENCE_DESCRIPTOR,
                Descriptor.READABLE,
                bytes([hid.MOUSE_REPORT_ID, hid.REPORT_TYPE_INPUT]),
            )
        ],
    )

    def on_control_point_write(_connection, value):
        logger.info('HID control point write: %s', value.hex())

    service = Service(
        GATT_HUMAN_INTERFACE_DEVICE_SERVICE,
        [
            Characteristic(
                GATT_PROTOCOL_MODE_CHARACTERISTIC,
                Characteristic.Properties.READ
                | Characteristic.Properties.WRITE_WITHOUT_RESPONSE,
                Characteristic.READABLE | Characteristic.WRITEABLE,
                bytes([hid.PROTOCOL_MODE_REPORT]),
            ),
            Characteristic(
                GATT_HID_INFORMATION_CHARACTERISTIC,
                Characteristic.Properties.READ,
                Characteristic.READABLE,
                hid.HID_INFORMATION,
            ),
            Characteristic(
                GATT_HID_CONTROL_POINT_CHARACTERISTIC,
                Characteristic.Properties.WRITE_WITHOUT_RESPONSE,
                Characteristic.WRITEABLE,
                CharacteristicValue(write=on_control_point_write),
            ),
            Characteristic(
                GATT_REPORT_MAP_CHARACTERISTIC,
                Characteristic.Properties.READ,
                Characteristic.READABLE,
                hid.REPORT_MAP,
            ),
            keyboard_input,
            mouse_input,
        ],
    )

    return service, keyboard_input, mouse_input


def build_advertising_data(
    name: str, dual_mode: bool = False, appearance: bool = True
) -> bytes:
    # Windows advertises 0x1a: general discoverable, simultaneous LE and BR/EDR capable
    # on both controller and host, and no BR/EDR Not Supported bit. Bit 4 is the host
    # half of that claim and has no name in Bumble's enum, so the value is set raw.
    flags = (
        AdvertisingData.Flags(0x1A)
        if dual_mode
        else AdvertisingData.Flags.LE_GENERAL_DISCOVERABLE_MODE
        | AdvertisingData.Flags.BR_EDR_NOT_SUPPORTED
    )
    structures = [
        data_types.CompleteLocalName(name),
        data_types.IncompleteListOf16BitServiceUUIDs(
            [GATT_HUMAN_INTERFACE_DEVICE_SERVICE]
        ),
    ]
    if appearance:
        structures.append(
            data_types.Appearance(
                Appearance.Category.HUMAN_INTERFACE_DEVICE,
                Appearance.HumanInterfaceDeviceSubcategory.KEYBOARD,
            )
        )
    structures.append(data_types.Flags(flags))
    return bytes(AdvertisingData(structures))


def set_appearance(device: Device, appearance: int) -> None:
    """Bumble's built-in GAP service defaults Appearance to 0, which makes Android
    refuse to classify the device as a keyboard."""
    for service in device.gatt_server.services:
        if service.uuid == GATT_GENERIC_ACCESS_SERVICE:
            for characteristic in service.characteristics:
                if characteristic.uuid == GATT_APPEARANCE_CHARACTERISTIC:
                    characteristic.value = appearance.to_bytes(2, 'little')
                    return


async def bonded_peers(device: Device) -> list[hci.Address]:
    if device.keystore is None:
        return []

    peers = []
    for name, keys in await device.keystore.get_all():
        address_type = (
            keys.address_type
            if keys.address_type is not None
            else hci.Address.RANDOM_DEVICE_ADDRESS
        )
        peers.append(hci.Address(name, address_type))
    return peers


async def load_filter_accept_list(device: Device, peers: list[hci.Address]) -> None:
    await device.send_sync_command(hci.HCI_LE_Clear_Filter_Accept_List_Command())
    for peer in peers:
        await device.send_sync_command(
            hci.HCI_LE_Add_Device_To_Filter_Accept_List_Command(
                address_type=peer.address_type, address=peer
            )
        )
        logger.info('accept list += %s', peer)


async def start_advertising(
    device: Device,
    mode: str,
    name: str,
    dual_mode: bool = False,
    public: bool = False,
    appearance: bool = True,
) -> None:
    advertising_data = build_advertising_data(name, dual_mode, appearance)
    peers = await bonded_peers(device)

    if mode != 'open' and not peers:
        logger.warning('no bonded peers yet, falling back to open advertising')
        mode = 'open'

    if mode == 'open':
        await device.start_advertising(
            advertising_data=advertising_data,
            own_address_type=(
                hci.OwnAddressType.PUBLIC if public else hci.OwnAddressType.RANDOM
            ),
            auto_restart=True,
        )
        logger.info(
            'advertising: undirected, open to any device%s',
            ' (public address)' if public else '',
        )
        return

    if mode == 'directed':
        target = peers[0]
        await device.start_advertising(
            advertising_type=AdvertisingType.DIRECTED_CONNECTABLE_LOW_DUTY,
            target=target,
            auto_restart=True,
        )
        logger.info('advertising: directed at %s', target)
        return

    # The filter policy is only reachable through an advertising set, which needs
    # extended advertising support in the controller.
    if not device.supports_le_extended_advertising:
        logger.error(
            'controller does not support extended advertising, so the filter policy '
            'cannot be set; try --advertising directed instead'
        )
        return

    await load_filter_accept_list(device, peers)
    await device.create_advertising_set(
        advertising_parameters=AdvertisingParameters(
            advertising_event_properties=AdvertisingEventProperties(
                is_connectable=True, is_scannable=True, is_legacy=True
            ),
            own_address_type=hci.OwnAddressType.RANDOM,
            advertising_filter_policy=FILTER_POLICY_ACCEPT_LIST_ONLY,
        ),
        random_address=device.random_address,
        advertising_data=advertising_data,
        auto_start=True,
        auto_restart=True,
    )
    logger.info('advertising: accept-list filtered, %d bonded peer(s)', len(peers))


async def type_text(device: Device, characteristic: Characteristic, text: str) -> None:
    for char in text:
        mapped = hid.map_char(char)
        if mapped is None:
            logger.warning('no HID usage for %r, skipping', char)
            continue

        usage, modifiers = mapped
        characteristic.value = hid.keyboard_report(modifiers, usage)
        await device.notify_subscribers(characteristic)
        await asyncio.sleep(0.02)

        characteristic.value = hid.keyboard_release()
        await device.notify_subscribers(characteristic)
        await asyncio.sleep(0.03)


async def run(args: argparse.Namespace) -> None:
    async with await open_transport(args.transport) as hci_transport:
        device = Device.from_config_file_with_hci(
            args.config, hci_transport.source, hci_transport.sink
        )

        hid_service, keyboard_input, mouse_input = build_hid_service()
        if args.no_appearance:
            logger.warning(
                'omitting appearance: Android should file this as an unclassified device'
            )
        else:
            set_appearance(device, KEYBOARD_APPEARANCE)

        device_information_characteristics = [
            Characteristic(
                GATT_MANUFACTURER_NAME_STRING_CHARACTERISTIC,
                Characteristic.Properties.READ,
                Characteristic.READABLE,
                args.name.encode('utf-8'),
            )
        ]
        if args.no_pnp_id:
            logger.warning('omitting PnP ID: this violates HOGP, for testing only')
        else:
            # HOGP mandates PnP ID; Android uses it to build the HID device.
            device_information_characteristics.append(
                Characteristic(
                    GATT_PNP_ID_CHARACTERISTIC,
                    Characteristic.Properties.READ,
                    Characteristic.READABLE,
                    PNP_ID,
                )
            )

        services = [
            Service(
                GATT_DEVICE_INFORMATION_SERVICE,
                device_information_characteristics,
            ),
            hid_service,
        ]
        if args.no_battery:
            logger.warning('omitting Battery Service: this violates HOGP, for testing only')
        else:
            services.insert(
                0,
                Service(
                    GATT_BATTERY_SERVICE,
                    [
                        Characteristic(
                            GATT_BATTERY_LEVEL_CHARACTERISTIC,
                            Characteristic.Properties.READ,
                            Characteristic.READABLE,
                            bytes([100]),
                        )
                    ],
                ),
            )

        device.add_services(services)

        # Bonding is the whole point: the keys must survive a process restart for the
        # reconnect test to mean anything. Android rejects pairing (SMP UNSPECIFIED)
        # unless the responder also distributes its IRK, so keep the default key set.
        # identity_address_type must be RANDOM: otherwise Bumble hands the peer the
        # dongle's public address as our identity, the peer stores that, and every
        # later reconnect targets an address nothing is advertising on. --public-identity
        # deliberately takes that public address, and advertises on it too so the two
        # still agree, to mimic a dual-mode PC whose LE and Classic identities are one.
        identity_address_type = (
            PairingConfig.AddressType.PUBLIC
            if args.public_identity
            else PairingConfig.AddressType.RANDOM
        )
        # Android pairs a dual-mode device over BR/EDR and demands MITM there, which
        # Just Works cannot satisfy. Numeric comparison can, and the delegate auto-accepts.
        io_capability = (
            PairingDelegate.IoCapability.DISPLAY_OUTPUT_AND_YES_NO_INPUT
            if args.classic
            else PairingDelegate.IoCapability.NO_OUTPUT_NO_INPUT
        )
        device.pairing_config_factory = lambda _connection: PairingConfig(
            sc=True,
            mitm=args.classic,
            bonding=True,
            delegate=PairingDelegate(io_capability),
            identity_address_type=identity_address_type,
        )

        def on_connection(connection):
            logger.info('connected: %s', connection)
            # 'pairing' is emitted on the connection, not on the device.
            connection.on(
                'pairing',
                lambda *_: logger.info('paired with %s', connection.peer_address),
            )
            connection.on(
                'pairing_failure',
                lambda reason: logger.error('pairing failed: %s', reason),
            )
            connection.on(
                'connection_encryption_change',
                lambda: restore_subscriptions(connection),
            )

        def restore_subscriptions(connection):
            """HOGP hosts expect a bonded peripheral to remember the CCCD, so they never
            re-subscribe after reconnecting. Bumble keeps subscribers in memory only."""
            if not connection.is_encrypted:
                return
            # For a legacy ATT bearer the bearer key is the connection itself.
            cccds = device.gatt_server.subscribers.setdefault(connection, {})
            for characteristic in (keyboard_input, mouse_input):
                if characteristic.handle not in cccds:
                    cccds[characteristic.handle] = bytes([0x01, 0x00])
            logger.info('restored notifications for %s', connection.peer_address)

        device.on('connection', on_connection)
        device.on('disconnection', lambda reason: logger.info('disconnected: %s', reason))

        # Reproduces the .NET peripheral, which advertises a rotating RPA and leaves the
        # peer to resolve it. The identity handed over during pairing stays the static
        # address either way, so only the advertised address changes.
        if args.rpa:
            device.le_privacy_enabled = True

        # Android files a device as DUAL only when both transports share one identity,
        # which is why this pairs with --public-identity.
        if args.classic:
            device.classic_enabled = True
            device.class_of_device = WINDOWS_CLASS_OF_DEVICE
            logger.warning(
                'classic enabled, class of device 0x%06X (computer/desktop)',
                WINDOWS_CLASS_OF_DEVICE,
            )

        await device.power_on()

        if args.classic:
            await device.set_connectable(True)
            await device.set_discoverable(True)
            logger.info('classic: connectable and discoverable')

        logger.info(
            'identity address: %s',
            device.public_address if args.public_identity else device.static_address,
        )
        if args.rpa:
            logger.warning('advertising RPA %s, peer must resolve it', device.random_address)

        if device.keystore is not None:
            for name, _keys in await device.keystore.get_all():
                logger.info('existing bond: %s', name)

        # power_on resets the dongle, which drops any link the controller was still
        # maintaining, so this delay is a genuine outage the peer can observe.
        if args.advertise_after:
            logger.info('radio silent for %d s', args.advertise_after)
            await asyncio.sleep(args.advertise_after)

        await start_advertising(
            device,
            args.advertising,
            args.name,
            dual_mode=args.dual_mode_flags,
            public=args.public_identity,
            appearance=not args.no_appearance,
        )

        typing_tasks: set[asyncio.Task] = set()

        def on_subscription(_connection, notify_enabled, _indicate_enabled):
            logger.info('input report notifications enabled=%s', notify_enabled)
            if not (notify_enabled and args.text):
                return
            task = asyncio.create_task(
                _type_then_log(device, keyboard_input, args.text)
            )
            typing_tasks.add(task)
            task.add_done_callback(typing_tasks.discard)

        keyboard_input.on('subscription', on_subscription)

        stdin_typing = asyncio.create_task(type_from_stdin(device, keyboard_input))

        await hci_transport.source.terminated
        stdin_typing.cancel()


async def _type_then_log(device: Device, characteristic: Characteristic, text: str) -> None:
    await type_text(device, characteristic, text)
    logger.info('done typing')


async def type_from_stdin(device: Device, characteristic: Characteristic) -> None:
    """Each line entered on the console is typed on the virtual keyboard."""
    while True:
        line = await asyncio.to_thread(sys.stdin.readline)
        if not line:
            return
        text = line.rstrip('\r\n')
        if not text:
            continue
        if not device.connections:
            logger.warning('no connection, nothing to type into')
            continue
        logger.info('typing %d characters', len(text))
        try:
            await type_text(device, characteristic, text)
        except Exception:
            logger.exception('typing failed')
        else:
            logger.info('done typing')


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        '--transport',
        default='usb:0',
        help='Bumble transport spec for the dongle (default: usb:0)',
    )
    parser.add_argument(
        '--config',
        default=str(HERE / 'device.json'),
        help='Bumble device config file',
    )
    parser.add_argument(
        '--advertising',
        choices=('open', 'bonded', 'directed'),
        default='open',
        help='Advertising mode (default: open)',
    )
    parser.add_argument('--name', default='Bumble HID', help='Advertised device name')
    parser.add_argument(
        '--text', help='Type this text whenever a host subscribes to input reports'
    )
    # Reproduces the .NET peripheral, which exposes no Device Information Service.
    parser.add_argument(
        '--no-pnp-id',
        action='store_true',
        help='Omit the PnP ID characteristic, to test whether hosts need it to reconnect',
    )
    parser.add_argument(
        '--dual-mode-flags',
        action='store_true',
        help='Advertise the flags Windows uses (0x1a), claiming BR/EDR support',
    )
    # Android files the Windows PC with appearance 0, unlike every keyboard that works.
    parser.add_argument(
        '--no-appearance',
        action='store_true',
        help='Advertise and report no appearance, as the Windows peripheral does',
    )
    parser.add_argument(
        '--no-battery',
        action='store_true',
        help='omit the Battery Service, which HOGP requires a HID device to instantiate',
    )
    parser.add_argument(
        '--classic',
        action='store_true',
        help='Also be a BR/EDR device with a computer class, so Android files it as DUAL',
    )
    parser.add_argument(
        '--public-identity',
        action='store_true',
        help="Use the dongle's public address as the identity, as a dual-mode PC does",
    )
    parser.add_argument(
        '--rpa',
        action='store_true',
        help='Advertise a resolvable private address instead of the static address',
    )
    parser.add_argument(
        '--advertise-after',
        type=int,
        default=0,
        metavar='SECONDS',
        help='Stay off the air this long before advertising, for a visible outage',
    )
    args = parser.parse_args()

    logging.basicConfig(
        level=os.environ.get('BUMBLE_LOGLEVEL', 'INFO').upper(),
        format='%(asctime)s %(levelname)s %(message)s',
    )

    # The keystore path in device.json is relative, so bonds must resolve next to the
    # script rather than wherever it happened to be launched from.
    os.chdir(HERE)

    # Realtek dongles need vendor firmware uploaded before the radio will transmit.
    if (HERE / 'rtk_fw').is_dir():
        os.environ.setdefault('BUMBLE_RTK_FIRMWARE_DIR', str(HERE / 'rtk_fw'))

    asyncio.run(run(args))


if __name__ == '__main__':
    main()
