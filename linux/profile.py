"""Read the same artboard measurements as the WPF version, without editing PNGs."""
import json
import math
import re
from dataclasses import dataclass
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent
WIDTH, HEIGHT = 1469, 1071

# Exact-length values and point lists from CharacterProfile. Keep defaults in the
# existing assets/character.txt, which Windows verifies against its own defaults.
EXACT = {
    'nearLid': 10, 'farLid': 10, 'lidWidth': 2, 'mouthRegion': 4,
    'mouthPivot': 2, 'mouthCenter': 2, 'pursedMouth': 2, 'mouthCover': 2,
    'skinColor': 3, 'yawnTear': 2, 'headPivot': 2, 'headRise': 1,
    'keyHinge': 2, 'keyCuffAxis': 2, 'keyForearm': 1, 'keyPalm': 2,
    'keyKnuckle': 2, 'keyArmBox': 4, 'keyRows': 4, 'keyDepth': 4,
    'keyReachX': 2, 'waveHand': 3, 'triumphLift': 1,
    'mouseShoulderY': 1, 'mouseWristY': 1, 'clickOrigin': 2,
    'headArea': 4, 'symbolAnchor': 2, 'trayIcon': 3, 'mouseRange': 4,
    'outlineColor': 3,
}
# minimum point count, whether an empty list is allowed
POINTS = {
    'leftEye': (3, False), 'rightEye': (3, False),
    'nearLidCurve': (6, True), 'farLidCurve': (6, True),
    'nearLidLower': (3, True), 'farLidLower': (3, True),
    'neck': (3, True), 'ahogeBase': (3, True), 'ahogeTip': (3, True),
    'keyHandEdge': (2, False), 'collar': (3, True),
    'mouseShoulderLine': (2, True), 'mouseShoulderCut': (3, True),
}
TUNING = json.loads((Path(__file__).parent / 'tuning.json').read_text())


def assignments(text):
    """Yield key/value pairs, including multiline polygons and empty values."""
    key, parts = None, []
    for raw in text.splitlines():
        line = raw.split('#', 1)[0].strip()
        if not line:
            continue
        if '=' in line:
            if key is not None:
                yield key, ' '.join(parts)
            key, rest = line.split('=', 1)
            key, parts = key.strip(), [rest.strip()]
        elif key is not None:
            parts.append(line)
        else:
            yield '', line
    if key is not None:
        yield key, ' '.join(parts)


class Profile:
    def __init__(self, directory, root=ROOT):
        self.directory = Path(directory)
        self.values = {}
        self.problems = []
        self.name = self.directory.name
        self.images = {}
        self.tuning = {key: spec['default'] for key, spec in TUNING.items()}
        self.read((Path(root) / 'assets/character.txt').read_text(encoding='utf-8-sig'))
        self.name = self.directory.name
        self.images = {}
        path = self.directory / 'character.txt'
        if path.is_file():
            self.read(path.read_text(encoding='utf-8-sig'))
        self.validate_geometry()

    def read(self, text):
        for key, raw in assignments(text):
            try:
                if key == 'name':
                    self.name = raw
                    continue
                if key in ('closedEyesImage', 'openMouthImage'):
                    if not raw or raw in ('.', '..') or '/' in raw or '\\' in raw:
                        raise ValueError('画像はフォルダ内のファイル名で指定します')
                    self.images[key] = raw
                    continue
                values = [float(v) for v in re.split(r'[,\s]+', raw) if v]
                if not all(math.isfinite(v) for v in values):
                    raise ValueError('有限の数値を指定します')
                if key.startswith('tune.') and key[5:] in TUNING:
                    if len(values) != 1:
                        raise ValueError('数値を1個指定します')
                    spec = TUNING[key[5:]]
                    self.tuning[key[5:]] = max(spec['min'], min(spec['max'], values[0]))
                    continue
                if key in EXACT:
                    if len(values) != EXACT[key]:
                        raise ValueError('数値を%d個指定します' % EXACT[key])
                elif key in POINTS:
                    count, empty = POINTS[key]
                    if len(values) % 2 or (len(values) < count * 2 and not (empty and not values)):
                        raise ValueError('点の数が正しくありません')
                elif key != 'clickRays':
                    raise ValueError('知らない項目です')
                self.values[key] = values
            except ValueError as exc:
                self.problems.append('%s: %s' % (key or raw, exc))

    def validate_geometry(self):
        for valid, message in (
            (abs(self['keyCuffAxis'][1]) > .001, 'keyCuffAxis の y は0にできません'),
            (self['keyForearm'][0] > 0, 'keyForearm は正の値にします'),
            (self['mouseWristY'][0] > self['mouseShoulderY'][0], '手首は肩より下に置きます'),
        ):
            if not valid:
                self.problems.append(message)

    def __getitem__(self, key):
        return self.values[key]

    def image_name(self, closed):
        key = 'closedEyesImage' if closed else 'openMouthImage'
        fallback = 'eyes-closed.png' if closed else 'mouth-open.png'
        return self.images.get(key, fallback if (self.directory / fallback).is_file() else 'face-expression.png')

    def complete(self):
        names = ['base.png', 'keyboard-arm.png', 'mouse-arm.png', 'head.png',
                 self.image_name(True), self.image_name(False)]
        return all((self.directory / name).is_file() for name in names)


@dataclass
class CharacterEntry:
    id: str
    profile: Profile


def catalog(root=ROOT):
    root = Path(root)
    entries = [CharacterEntry('', Profile(root / 'assets', root))]
    directory = root / 'characters'
    if directory.is_dir():
        for path in sorted(directory.iterdir(), key=lambda p: p.name.lower()):
            if path.is_dir() and '=' not in path.name:
                try:
                    profile = Profile(path, root)
                    if profile.complete():
                        entries.append(CharacterEntry(path.name, profile))
                except (OSError, ValueError):
                    continue
    return entries


def clamp(value, low, high):
    return max(low, min(high, value)) if math.isfinite(value) else low


def mouse_range(values):
    left, right, top, bottom = values
    return (*sorted((clamp(left, -220, 220), clamp(right, -220, 220))),
            *sorted((clamp(top, -160, 8), clamp(bottom, -160, 8))))
