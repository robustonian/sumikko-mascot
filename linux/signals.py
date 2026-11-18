"""Nonblocking Linux input and audio levels. Never grab devices or save events."""
import array
import ctypes
import ctypes.util
import fcntl
import json
import math
import os
from pathlib import Path
import struct
import subprocess
import sys
import time

EVENT = struct.Struct('@llHHi')  # struct input_event, native timeval on 32/64 bit
MODIFIERS = {29, 42, 54, 56, 58, 69, 70, 97, 100, 125, 126}


def ioctl_read(number, size):
    return (2 << 30) | (size << 16) | (ord('E') << 8) | number


def capabilities(fd, kind, size=96):
    result = bytearray(size)
    fcntl.ioctl(fd, ioctl_read(0x20 + kind, size), result)
    return {bit for bit in range(size * 8) if result[bit // 8] & (1 << (bit % 8))}


class Device:
    def __init__(self, fd, keyboard, mouse):
        self.fd, self.keyboard, self.mouse = fd, keyboard, mouse
        self.buttons = set()
        self.dropped = False
        self.absolute = {}
        for axis in (0, 1):
            if mouse and axis in capabilities(fd, 3):
                info = bytearray(24)
                fcntl.ioctl(fd, ioctl_read(0x40 + axis, 24), info)
                _, low, high, _, _, _ = struct.unpack('@iiiiii', info)
                if high > low:
                    self.absolute[axis] = (low, high)
        self.sync_buttons()

    def sync_buttons(self):
        bits = bytearray(96)
        fcntl.ioctl(self.fd, ioctl_read(0x18, len(bits)), bits)  # EVIOCGKEY
        self.buttons = {c for c in range(272, 280) if bits[c // 8] & (1 << (c % 8))}


class X11Input:
    """Fallback for X11; on Wayland this can only see XWayland client keys."""
    def __init__(self):
        self.display = None
        self.previous = None
        self.last_key = None
        self.repeat_at = math.inf
        try:
            self.x = ctypes.CDLL(ctypes.util.find_library('X11'))
            self.x.XOpenDisplay.argtypes = [ctypes.c_char_p]
            self.x.XOpenDisplay.restype = ctypes.c_void_p
            self.x.XDefaultRootWindow.argtypes = [ctypes.c_void_p]
            self.x.XDefaultRootWindow.restype = ctypes.c_ulong
            self.x.XQueryKeymap.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
            self.x.XQueryPointer.argtypes = [ctypes.c_void_p, ctypes.c_ulong] + [ctypes.c_void_p] * 7
            self.x.XCloseDisplay.argtypes = [ctypes.c_void_p]
            self.display = self.x.XOpenDisplay(None)
            if self.display:
                self.root = self.x.XDefaultRootWindow(self.display)
        except (OSError, AttributeError, TypeError):
            self.close()

    def poll(self, now):
        if not self.display:
            return False, False, False, False
        keys = ctypes.create_string_buffer(32)
        self.x.XQueryKeymap(self.display, keys)
        held = {i - 8 for i in range(8, 256)
                if keys.raw[i // 8] & (1 << (i % 8)) and i - 8 not in MODIFIERS}
        fresh = held - self.previous if self.previous is not None else set()
        repeated = False
        if fresh:
            self.last_key = min(fresh)
            self.repeat_at = now + .45
        elif self.last_key in held and now >= self.repeat_at:
            repeated = True
            self.repeat_at = now + .07
        if self.last_key not in held:
            self.last_key, self.repeat_at = None, math.inf
        self.previous = held
        root, child = ctypes.c_ulong(), ctypes.c_ulong()
        rx, ry, wx, wy, mask = [ctypes.c_int() for _ in range(5)]
        self.x.XQueryPointer(self.display, self.root, ctypes.byref(root), ctypes.byref(child),
                            ctypes.byref(rx), ctypes.byref(ry), ctypes.byref(wx),
                            ctypes.byref(wy), ctypes.byref(mask))
        return bool(fresh) or repeated, 28 in fresh or 96 in fresh, repeated, bool(mask.value & 0x700)

    def close(self):
        if self.display:
            self.x.XCloseDisplay(self.display)
            self.display = None


class InputSignals:
    def __init__(self, directory='/dev/input', x11=True):
        self.directory = Path(directory)
        self.devices = {}
        self.x11 = X11Input() if x11 else None
        self.next_scan = 0
        self.pointer = [0., 0.]
        self.key = self.accent = self.repeat = self.mouse_down = False
        self.scan()

    @property
    def keyboard_count(self):
        return sum(d.keyboard for d in self.devices.values())

    @property
    def mouse_count(self):
        return sum(d.mouse for d in self.devices.values())

    @property
    def status(self):
        parts = ['直接入力: キーボード%d台 / マウス%d台' % (self.keyboard_count, self.mouse_count)]
        if not self.keyboard_count or not self.mouse_count:
            parts.append('入力機器の読み取り権限が不足、または未接続です。')
            if self.x11 and self.x11.display:
                parts.append('X11入力で補完中（Waylandアプリのキーには反応しません）。')
            else:
                parts.append('デモまたはキャラの上での操作を使えます。')
        return '\n'.join(parts)

    def scan(self):
        paths = set(self.directory.glob('event*'))
        for path in list(self.devices):
            if path not in paths:
                os.close(self.devices.pop(path).fd)
        for path in sorted(paths - self.devices.keys()):
            fd = None
            try:
                fd = os.open(path, os.O_RDONLY | os.O_NONBLOCK | os.O_CLOEXEC)
                keys = capabilities(fd, 1)
                keyboard = {16, 30, 44}.issubset(keys)  # Q, A, Z; skip power buttons
                mouse = 272 in keys and (0 in capabilities(fd, 2) or 0 in capabilities(fd, 3))
                if keyboard or mouse:
                    self.devices[path] = Device(fd, keyboard, mouse)
                    fd = None
            except OSError:
                pass
            finally:
                if fd is not None:
                    os.close(fd)
        self.next_scan = time.monotonic() + 3

    def consume(self, device, kind, code, value):
        if kind == 0 and code == 3:  # SYN_DROPPED: discard through next report
            device.dropped = True
            device.buttons.clear()
            return
        if device.dropped:
            if kind == 0 and code == 0:
                device.dropped = False
                device.sync_buttons()
            return
        if kind == 1:
            if device.keyboard and 1 <= code < 256 and code not in MODIFIERS and value in (1, 2):
                self.key = True
                self.accent |= code in (28, 96) and value == 1
                self.repeat |= value == 2
            elif device.mouse and 272 <= code < 280:
                if value:
                    device.buttons.add(code)
                else:
                    device.buttons.discard(code)
        elif device.mouse and kind == 2 and code in (0, 1):
            # Wayland does not expose the global cursor position. Relative motion
            # follows the physical mouse, bounded to the character's safe range.
            self.pointer[code] = max(-1., min(1., self.pointer[code] + value / 650))
        elif device.mouse and kind == 3 and code in device.absolute:
            low, high = device.absolute[code]
            self.pointer[code] = max(-1., min(1., (value - low) * 2 / (high - low) - 1))

    def poll(self):
        self.key = self.accent = self.repeat = False
        if time.monotonic() >= self.next_scan:
            self.scan()
        for path, device in list(self.devices.items()):
            try:
                # Bounded work per frame even if a device floods its queue.
                for _ in range(8):
                    data = os.read(device.fd, EVENT.size * 128)
                    if not data:
                        raise OSError('device disconnected')
                    for _, _, kind, code, value in EVENT.iter_unpack(data):
                        self.consume(device, kind, code, value)
            except BlockingIOError:
                pass
            except OSError:
                os.close(self.devices.pop(path).fd)
        self.mouse_down = any(d.buttons for d in self.devices.values())
        if self.x11 and (not self.keyboard_count or not self.mouse_count):
            key, accent, repeat, mouse = self.x11.poll(time.monotonic())
            if not self.keyboard_count:
                self.key, self.accent, self.repeat = key, accent, repeat
            if not self.mouse_count:
                self.mouse_down = mouse

    def close(self):
        for device in self.devices.values():
            os.close(device.fd)
        self.devices.clear()
        if self.x11:
            self.x11.close()


def audio_devices():
    try:
        result = subprocess.run(['pactl', '--format=json', 'list', 'sources'],
                                capture_output=True, text=True, timeout=2, check=True)
        return [(d['name'], d.get('description', d['name'])) for d in json.loads(result.stdout)]
    except (OSError, ValueError, KeyError, subprocess.SubprocessError):
        return []


class AudioLevel:
    def __init__(self):
        self.process = None
        self.level = 0.
        self.status = '音声オフ'
        self.pending = b''

    def start(self, source):
        self.close()
        if not source:
            return
        try:
            if source == '@DEFAULT_MONITOR@':
                result = subprocess.run(['pactl', 'get-default-sink'], capture_output=True,
                                        text=True, check=True, timeout=2)
                source = result.stdout.strip() + '.monitor'
            self.process = subprocess.Popen([
                'parec', '--raw', '--format=s16le', '--rate=16000', '--channels=1',
                '--latency-msec=40', '--device=' + source,
            ], stdout=subprocess.PIPE, stderr=subprocess.DEVNULL)
            os.set_blocking(self.process.stdout.fileno(), False)
            self.status = '音量を取得中'
        except (OSError, subprocess.SubprocessError) as exc:
            self.close()
            self.status = '音声を開始できません（pulseaudio-utils と音声サーバーを確認）: ' + str(exc)

    def poll(self):
        if self.process is None:
            return 0.
        if self.process.poll() is not None:
            self.close()
            self.status = '音声機器を開けません。音声メニューから選び直してください。'
            return 0.
        try:
            data = self.pending + os.read(self.process.stdout.fileno(), 16000)
        except BlockingIOError:
            return self.level
        except OSError:
            self.close()
            self.status = '音声の読み取りに失敗しました。'
            return 0.
        end = len(data) - len(data) % 2
        self.pending = data[end:]
        samples = array.array('h')
        samples.frombytes(data[:end])
        if sys.byteorder != 'little':
            samples.byteswap()
        if samples:
            self.level = math.sqrt(sum(v * v for v in samples) / len(samples)) / 32768
        return self.level

    def close(self):
        if self.process is not None:
            self.process.terminate()
            try:
                self.process.wait(timeout=.5)
            except subprocess.TimeoutExpired:
                self.process.kill()
                self.process.wait()
            self.process.stdout.close()
            self.process = None
        self.level, self.pending = 0., b''
        self.status = '音声オフ'
