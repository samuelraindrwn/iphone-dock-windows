"""HID report map and report builders.

The report map is byte-identical to src/BleHid.Core/HidDescriptors.cs so that a host
sees the same device from either implementation.
"""

KEYBOARD_REPORT_ID = 1
MOUSE_REPORT_ID = 2

REPORT_TYPE_INPUT = 1
REPORT_TYPE_OUTPUT = 2

PROTOCOL_MODE_REPORT = 0x01

# bcdHID 1.11, country code 0, flags = RemoteWake | NormallyConnectable.
HID_INFORMATION = bytes([0x11, 0x01, 0x00, 0x03])

KEYBOARD_REPORT_LENGTH = 8
MOUSE_REPORT_LENGTH = 6

REPORT_MAP = bytes(
    [
        # ---- Keyboard (Report ID 1) ----
        0x05, 0x01,        # Usage Page (Generic Desktop)
        0x09, 0x06,        # Usage (Keyboard)
        0xA1, 0x01,        # Collection (Application)
        0x85, KEYBOARD_REPORT_ID,
        0x05, 0x07,        #   Usage Page (Keyboard/Keypad)
        0x19, 0xE0,        #   Usage Minimum (LeftControl)
        0x29, 0xE7,        #   Usage Maximum (RightGUI)
        0x15, 0x00,        #   Logical Minimum (0)
        0x25, 0x01,        #   Logical Maximum (1)
        0x75, 0x01,        #   Report Size (1)
        0x95, 0x08,        #   Report Count (8)
        0x81, 0x02,        #   Input (Data,Var,Abs)  - modifier byte
        0x95, 0x01,        #   Report Count (1)
        0x75, 0x08,        #   Report Size (8)
        0x81, 0x01,        #   Input (Const)         - reserved byte
        0x95, 0x06,        #   Report Count (6)
        0x75, 0x08,        #   Report Size (8)
        0x15, 0x00,        #   Logical Minimum (0)
        0x25, 0x65,        #   Logical Maximum (101)
        0x05, 0x07,        #   Usage Page (Keyboard/Keypad)
        0x19, 0x00,        #   Usage Minimum (0)
        0x29, 0x65,        #   Usage Maximum (101)
        0x81, 0x00,        #   Input (Data,Array)    - 6 key slots
        0xC0,              # End Collection

        # ---- Mouse (Report ID 2) ----
        0x05, 0x01,        # Usage Page (Generic Desktop)
        0x09, 0x02,        # Usage (Mouse)
        0xA1, 0x01,        # Collection (Application)
        0x85, MOUSE_REPORT_ID,
        0x09, 0x01,        #   Usage (Pointer)
        0xA1, 0x00,        #   Collection (Physical)
        0x05, 0x09,        #     Usage Page (Button)
        0x19, 0x01,        #     Usage Minimum (1)
        0x29, 0x03,        #     Usage Maximum (3)
        0x15, 0x00,        #     Logical Minimum (0)
        0x25, 0x01,        #     Logical Maximum (1)
        0x95, 0x03,        #     Report Count (3)
        0x75, 0x01,        #     Report Size (1)
        0x81, 0x02,        #     Input (Data,Var,Abs) - buttons
        0x95, 0x01,        #     Report Count (1)
        0x75, 0x05,        #     Report Size (5)
        0x81, 0x01,        #     Input (Const)        - padding
        0x05, 0x01,        #     Usage Page (Generic Desktop)
        0x09, 0x30,        #     Usage (X)
        0x09, 0x31,        #     Usage (Y)
        0x16, 0x01, 0x80,  #     Logical Minimum (-32767)
        0x26, 0xFF, 0x7F,  #     Logical Maximum (32767)
        0x75, 0x10,        #     Report Size (16)
        0x95, 0x02,        #     Report Count (2)
        0x81, 0x06,        #     Input (Data,Var,Rel) - dx, dy
        0x09, 0x38,        #     Usage (Wheel)
        0x15, 0x81,        #     Logical Minimum (-127)
        0x25, 0x7F,        #     Logical Maximum (127)
        0x75, 0x08,        #     Report Size (8)
        0x95, 0x01,        #     Report Count (1)
        0x81, 0x06,        #     Input (Data,Var,Rel) - wheel
        0xC0,              #   End Collection
        0xC0,              # End Collection
    ]
)


def keyboard_report(modifiers: int = 0, *usages: int) -> bytes:
    report = bytearray(KEYBOARD_REPORT_LENGTH)
    report[0] = modifiers & 0xFF
    for i, usage in enumerate(usages[:6]):
        report[2 + i] = usage & 0xFF
    return bytes(report)


def keyboard_release() -> bytes:
    return bytes(KEYBOARD_REPORT_LENGTH)


def mouse_report(buttons: int = 0, dx: int = 0, dy: int = 0, wheel: int = 0) -> bytes:
    x = max(-32767, min(32767, dx))
    y = max(-32767, min(32767, dy))
    w = max(-127, min(127, wheel))
    return bytes(
        [
            buttons & 0xFF,
            x & 0xFF,
            (x >> 8) & 0xFF,
            y & 0xFF,
            (y >> 8) & 0xFF,
            w & 0xFF,
        ]
    )


_SHIFT_LEFT = 0x02
_UNSHIFTED_SYMBOLS = "-=[]\\;'`,./"
_SHIFTED_SYMBOLS = '_+{}|:"~<>?'
_UNSHIFTED_USAGES = [0x2D, 0x2E, 0x2F, 0x30, 0x31, 0x33, 0x34, 0x35, 0x36, 0x37, 0x38]
_SHIFTED_DIGITS = "!@#$%^&*()"


def map_char(char: str) -> tuple[int, int] | None:
    """Map a printable ASCII character to (usage id, modifiers)."""
    if "a" <= char <= "z":
        return 0x04 + ord(char) - ord("a"), 0
    if "A" <= char <= "Z":
        return 0x04 + ord(char) - ord("A"), _SHIFT_LEFT
    if "1" <= char <= "9":
        return 0x1E + ord(char) - ord("1"), 0
    if char == "0":
        return 0x27, 0
    if char == "\n":
        return 0x28, 0
    if char == "\t":
        return 0x2B, 0
    if char == " ":
        return 0x2C, 0

    index = _UNSHIFTED_SYMBOLS.find(char)
    if index >= 0:
        return _UNSHIFTED_USAGES[index], 0

    index = _SHIFTED_SYMBOLS.find(char)
    if index >= 0:
        return _UNSHIFTED_USAGES[index], _SHIFT_LEFT

    index = _SHIFTED_DIGITS.find(char)
    if index >= 0:
        return (0x27 if index == 9 else 0x1E + index), _SHIFT_LEFT

    return None
