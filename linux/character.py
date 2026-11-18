"""Qt painter implementation of the registered PNG parts and pinned-line motion.

Coordinates, part ordering and affine sleeve transforms follow CharacterView.cs.
The Linux animation clock is independent; PNG files and character.txt are shared.
"""
import math
import random

from PyQt5.QtCore import QPointF, QRectF, Qt
from PyQt5.QtGui import (QBitmap, QColor, QFont, QImage, QPainter, QPainterPath,
                         QPen, QPixmap, QPolygonF, QRadialGradient, QRegion, QTransform)

from .profile import WIDTH, HEIGHT, clamp, mouse_range

ART = QRectF(0, 0, WIDTH, HEIGHT)


def point(values):
    return QPointF(*values)


def polygon(values):
    path = QPainterPath()
    if values:
        path.addPolygon(QPolygonF([QPointF(*values[i:i + 2]) for i in range(0, len(values), 2)]))
        path.closeSubpath()
    return path


def rectangle(x, y, w, h):
    path = QPainterPath()
    path.addRect(QRectF(x, y, w, h))
    return path


def ellipse(x, y, rx, ry):
    path = QPainterPath()
    path.addEllipse(QPointF(x, y), rx, ry)
    return path


def pen(color, width):
    return QPen(color, width, Qt.SolidLine, Qt.RoundCap, Qt.RoundJoin)


def smooth(x):
    x = clamp(x, 0, 1)
    return x * x * (3 - 2 * x)


class Spring:
    def __init__(self):
        self.value = self.velocity = 0.

    def step(self, target, dt, frequency=22, damping=.7):
        dt = clamp(dt, 0, .05)
        steps = max(1, math.ceil(dt / .006))
        h = dt / steps
        for _ in range(steps):
            self.velocity += ((target - self.value) * frequency ** 2 - 2 * damping * frequency * self.velocity) * h
            self.value += self.velocity * h
        if abs(self.value - target) < .00001 and abs(self.velocity) < .00001:
            self.value, self.velocity = target, 0.

    def constrain(self, low, high):
        value = clamp(self.value, low, high)
        if value != self.value or low == high:
            self.velocity = 0.
        self.value = value


class Character:
    def __init__(self, profile, seed=None):
        if profile.problems:
            raise ValueError('\n'.join(profile.problems))
        self.profile = profile
        self.tune = profile.tuning.copy()
        self.random = random.Random(seed)
        self.images = {}
        names = {'base': 'base.png', 'head': 'head.png', 'key': 'keyboard-arm.png',
                 'mouse': 'mouse-arm.png', 'closed': profile.image_name(True),
                 'mouth': profile.image_name(False)}
        for key, name in names.items():
            image = QImage(str(profile.directory / name))
            if image.isNull():
                raise ValueError('画像を開けません: ' + str(profile.directory / name))
            # The artboard is shared even when a generated PNG is a few px off.
            self.images[key] = image.scaled(WIDTH, HEIGHT, Qt.IgnoreAspectRatio, Qt.SmoothTransformation)
        self.ink = QColor(*[round(clamp(v, 0, 255)) for v in profile['outlineColor']])
        self.regions = {k: polygon(profile[k]) for k in (
            'leftEye', 'rightEye', 'collar', 'neck', 'ahogeBase', 'ahogeTip', 'mouseShoulderCut')}
        self.make_key_clips()
        shoulder, wrist = profile['mouseShoulderY'][0], profile['mouseWristY'][0]
        cut = self.regions['mouseShoulderCut']
        self.shoulder_clip = rectangle(0, 0, WIDTH, shoulder + 1).subtracted(cut)
        self.sleeve_clip = rectangle(0, shoulder, WIDTH, wrist - shoulder + 1.5).subtracted(cut)
        self.hand_clip = rectangle(0, wrist, WIDTH, HEIGHT - wrist)
        self.head_clip = rectangle(0, 0, WIDTH, HEIGHT).subtracted(self.regions['neck'])
        self.hair_head_clip = self.head_clip.subtracted(self.regions['ahogeBase'].united(self.regions['ahogeTip']))
        self.mouth_region = ellipse(*profile['mouthRegion'])
        self.pursed_region = ellipse(*profile['mouthCenter'], *profile['pursedMouth'])
        self.hair_base_clip = self.regions['ahogeBase']
        self.hair_tip_clip = self.regions['ahogeTip']
        base, tip = profile['ahogeBase'], profile['ahogeTip']
        if base:
            a, b = point(base[:2]), point(base[2:4])
            delta = b - a
            length = math.hypot(delta.x(), delta.y()) or 1
            up = QPointF(delta.y() / length, -delta.x() / length)
            adjusted = base.copy()
            adjusted[:4] = [a.x() - up.x() * 1.5, a.y() - up.y() * 1.5,
                            b.x() - up.x() * 1.5, b.y() - up.y() * 1.5]
            self.hair_base_clip = polygon(adjusted)
        if tip:
            adjusted = tip.copy()
            under = 1.5 if sum(tip[::2]) / len(tip[::2]) < tip[0] else -1.5
            adjusted[0] += under
            adjusted[-2] += under
            self.hair_tip_clip = polygon(adjusted)
        self.prepare_pieces()
        self.closed_patches = {key: self.eye_patch(self.regions[key]) for key in ('leftEye', 'rightEye')}
        x, y, size = map(round, profile['trayIcon'])
        self.tray_image = self.images['head'].copy(x, y, size, size).scaled(
            64, 64, Qt.KeepAspectRatio, Qt.SmoothTransformation)
        self.images.clear()  # patches and full-size pixmaps now own their pixels
        self.backgrounds = {}
        self.background_scale = None
        for name in ('rx', 'ry', 'lift', 'flick', 'nod', 'mx', 'my', 'press', 'lean',
                     'hx', 'hy', 'angle', 'purse', 'thump', 'hair_sway', 'hair_stretch', 'hair_tip'):
            setattr(self, name, Spring())
        self.time = 0.
        self.last_key = self.stroke_start = self.click_start = -100.
        self.stroke_peak = self.stroke_wind = 0.
        self.stroke_accent = False
        self.target_x = self.target_y = self.hold_x = self.hold_y = 0.
        self.blink = self.mouth = self.mouth_shown = 0.
        self.blink_start, self.next_blink = -100., 2.7
        self.blink_pace = 1.
        self.next_tilt, self.head_tilt = 5., 0.
        self.purse_until, self.next_purse = -100., 8.
        self.mouse_was_down = False
        self.mouse_dx = self.mouse_dy = self.squash = 0.
        self.strength = 1.
        self.range = mouse_range(profile['mouseRange'])
        self.reduced = self.demo = False
        self.gain, self.floor = 1., .015
        self.last_activity = 0.
        self.last_pointer = (0., 0.)
        self.asleep = False
        self.sleep_level = 0.
        self.wave_start, self.triumph_start, self.startle_start = -100., -100., -100.
        self.think_start = -100.
        self.phrase_keys = 0
        self.typing_rate = 0.
        self.pet_until = self.pet_reversed = -100.
        self.pet_extreme, self.pet_direction = None, 0
        self.next_symbol = 0.
        self.symbols = []
        self.hand_calm = 0.
        self.demo_beat = -1
        self.greet()

    def prepare_pieces(self):
        # Clip registered parts only once, at native resolution. Re-rasterizing
        # large polygon clips on each desktop frame was the main idle CPU cost.
        self.cached_pieces = {}
        self.pixmaps = {name: QPixmap.fromImage(image) for name, image in self.images.items()
                        if name in ('base', 'key', 'head')}
        bounds = {name: QRegion(QBitmap.fromImage(image.createAlphaMask())).boundingRect()
                  for name, image in self.images.items()}
        parts = [('base', self.regions['collar']), ('key', self.key_root),
                 ('key', self.key_sleeve), ('key', self.key_rim), ('key', self.key_hand),
                 ('mouse', self.shoulder_clip), ('mouse', self.sleeve_clip),
                 ('mouse', self.hand_clip), ('head', self.head_clip),
                 ('head', self.hair_head_clip), ('head', self.regions['neck']),
                 ('head', self.hair_base_clip), ('head', self.hair_tip_clip),
                 ('head', self.pursed_region), ('mouth', self.mouth_region)]
        for name, clip in parts:
            if clip.isEmpty():
                continue
            box = clip.boundingRect().toAlignedRect().intersected(bounds[name])
            if box.isEmpty():
                # A faint custom layer can disappear from the binary alpha mask.
                # Preserve the original clipped rendering in that case.
                if name not in self.pixmaps:
                    self.pixmaps[name] = QPixmap.fromImage(self.images[name])
                continue
            image = QImage(box.size(), QImage.Format_ARGB32_Premultiplied)
            image.fill(Qt.transparent)
            painter = QPainter(image)
            painter.translate(-box.x(), -box.y())
            painter.setClipPath(clip)
            painter.drawImage(ART, self.images[name])
            painter.end()
            # Retain the path itself: ids cannot be reused by temporary eye clips.
            self.cached_pieces[(name, id(clip))] = (clip, box, QPixmap.fromImage(image))

    @property
    def needs_full_frame_rate(self):
        return (self.time - self.last_key < .9 or
                abs(self.mx.velocity) + abs(self.my.velocity) > 30 or
                abs(self.press.velocity) > .1 or self.time - self.click_start < .24 or
                self.mouth_shown > .02 or 0 <= self.time - self.wave_start < 1.9 or
                0 <= self.time - self.triumph_start < 1.2 or self.time < self.pet_until)

    def make_key_clips(self):
        p = self.profile
        hx, hy = p['keyHinge']
        ax, ay = p['keyCuffAxis']
        self.reach = (ay, -ax)
        forearm = p['keyForearm'][0]
        rim = (hx + ay * forearm, hy - ax * forearm)
        left, top, right, bottom = p['keyArmBox']

        def along(origin, y, shift=0):
            return (origin[0] + (y - origin[1]) * ax / ay + shift, y)

        def poly(pairs):
            return polygon([v for pair in pairs for v in pair])

        self.key_root = poly([(left, top), along((hx, hy), top, 1), along((hx, hy), bottom, 1), (left, bottom)])
        self.key_sleeve = poly([along((hx, hy), top), along(rim, top, 1), along(rim, bottom, 1), along((hx, hy), bottom)])
        edge = p['keyHandEdge']
        pairs = [tuple(edge[i:i + 2]) for i in range(0, len(edge), 2)]
        palm = point(p['keyPalm'])
        inside = []
        for pair in pairs:
            delta = palm - point(pair)
            length = math.hypot(delta.x(), delta.y()) or 1
            inside.append((pair[0] + delta.x() * 7 / length, pair[1] + delta.y() * 7 / length))
        self.key_hand = poly(pairs + [(right, edge[-1]), (right, edge[1])])
        self.key_rim = poly(inside + [along(rim, edge[-1] + 10), along(rim, edge[1])])

    def eye_patch(self, region):
        # Feathered, registered expression patches. The closed image never
        # replaces the head outline; this avoids flicker between generated PNGs.
        box = region.boundingRect().adjusted(-15, -15, 15, 15).toAlignedRect()
        mask = QImage(box.size(), QImage.Format_ARGB32_Premultiplied)
        mask.fill(Qt.transparent)
        painter = QPainter(mask)
        painter.setRenderHint(QPainter.Antialiasing)
        painter.translate(-box.x(), -box.y())
        for width in range(20, 0, -2):
            painter.setPen(pen(QColor(0, 0, 0, 25), width))
            painter.setBrush(Qt.NoBrush)
            painter.drawPath(region)
        painter.fillPath(region, Qt.black)
        painter.end()
        closed = self.images['closed'].copy(box)
        painter = QPainter(closed)
        painter.setCompositionMode(QPainter.CompositionMode_DestinationIn)
        painter.drawImage(0, 0, mask)
        painter.end()
        composed = self.images['head'].copy(box)
        painter = QPainter(composed)
        painter.drawImage(0, 0, closed)
        painter.end()
        return box, composed

    def greet(self):
        if self.tune['act.greet']:
            self.wave_start = self.time + .45

    def add_symbol(self, kind, life=1.5):
        if self.tune['act.symbols'] and len(self.symbols) < 12:
            self.symbols.append((kind, self.time, life, self.random.uniform(-20, 50)))

    def pet(self, x, y):
        cx, cy, rx, ry = self.profile['headArea']
        if not self.tune['act.pet'] or self.reduced or ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 > 1:
            self.pet_extreme, self.pet_direction = None, 0
            return
        if self.pet_extreme is None:
            self.pet_extreme = x
        delta = x - self.pet_extreme
        if abs(delta) > 36:
            direction = 1 if delta > 0 else -1
            if self.pet_direction and direction != self.pet_direction:
                if self.time - self.pet_reversed < 1.2:
                    self.pet_until = self.time + 1.2
                self.pet_reversed = self.time
            self.pet_direction, self.pet_extreme = direction, x
        elif delta * self.pet_direction > 0:
            self.pet_extreme = x

    def begin_stroke(self, accent, repeat, amount):
        t, p = self.tune, self.profile
        interval = self.time - self.last_key
        self.last_key = self.time
        spread = min(1., amount)
        rows, depth = p['keyRows'], p['keyDepth']

        def spot(along, across):
            return (clamp(spread * (along * rows[2] + across * depth[2]), *p['keyReachX']),
                    spread * (along * rows[3] + across * depth[3]))

        if repeat:
            area = [spot(a, b) for a in rows[:2] for b in depth[:2]]
            self.target_x = clamp(self.hold_x + self.random.uniform(-7, 7) * spread, min(a[0] for a in area), max(a[0] for a in area))
            self.target_y = clamp(self.hold_y + self.random.uniform(-4, 4) * spread, min(a[1] for a in area), max(a[1] for a in area))
            self.stroke_peak = (t['key.holdHeight'] + self.random.random() * 10) * amount
            self.stroke_wind = 0 if self.lift.value > 7 * amount else .03
        else:
            for _ in range(8):
                x, y = spot(self.random.uniform(*rows[:2]), self.random.uniform(*depth[:2]))
                if math.hypot(x - self.target_x, y - self.target_y) >= t['key.apart'] * spread:
                    break
            self.target_x = self.hold_x = x
            self.target_y = self.hold_y = y
            hop = math.hypot(x - self.rx.value, y - self.ry.value)
            slow = smooth((interval - .09) / .3)
            self.stroke_peak = min(t['key.maxHeight'], ((t['key.height'] + t['key.heightSlow'] * slow) * amount + t['key.hopLift'] * hop) * self.random.uniform(.85, 1.15) * (t['key.accent'] if accent else 1))
            self.stroke_wind = (0 if self.lift.value > 7 * amount else .04) + min(.04, hop * .0004) + .01 * slow + (.025 if accent else 0)
        self.stroke_accent, self.stroke_start = accent and not repeat, self.time

    def tick(self, elapsed, dt, key=False, accent=False, repeat=False, mouse=False, pointer=(0., 0.), audio=0.):
        if not math.isfinite(elapsed):
            return
        self.time, dt = elapsed, clamp(dt, 0, .05)
        t, p, now = self.tune, self.profile, elapsed
        if self.demo:
            pointer = (math.sin(now * .83) * .8, math.sin(now * .57) * .65)
            phrase = now % 7
            beat = int(phrase * 9) if phrase < 2.2 else (100 + int((phrase - 2.2) * 3.6) if phrase < 3.8 else (200 if phrase < 4.4 else -1))
            key, accent = beat >= 0 and beat != self.demo_beat, beat == 200
            repeat, self.demo_beat = False, beat
            mouse = now % 4.3 > 3.9
            audio = .035 + .14 * max(0, math.sin(now * 8.3)) ** 2 if now % 6 < 3.5 else 0
        amount = (.32 if self.reduced else 1) * self.strength
        activity = key or mouse or math.hypot(pointer[0] - self.last_pointer[0], pointer[1] - self.last_pointer[1]) > .015
        self.last_pointer = pointer
        if activity:
            if self.asleep and (key or mouse):
                self.startle_start = now
                self.add_symbol('!')
            self.last_activity = now
        idle = now - self.last_activity
        self.asleep = bool(t['act.sleep'] and not self.reduced and idle > t['act.sleepAfter'])
        self.sleep_level += ((1 if self.asleep else 0) - self.sleep_level) * (1 - math.exp(-dt * 3))
        if key:
            self.begin_stroke(accent, repeat, amount)
            self.typing_rate += 1
            if not repeat:
                self.phrase_keys += 1
            if accent and self.phrase_keys >= t['act.triumphKeys'] and t['act.triumph'] and not self.reduced:
                self.triumph_start, self.phrase_keys = now, 0
                self.add_symbol('star')
        self.typing_rate *= math.exp(-dt)
        if not self.reduced and self.phrase_keys >= 6 and 2.4 < now - self.last_key < 2.5 and t['act.think']:
            self.think_start = now
            self.phrase_keys = 0
            self.add_symbol('...')
        self.symbols = [s for s in self.symbols if now < s[1] + s[2]]
        petting = now < self.pet_until and not self.reduced
        if now > self.next_symbol:
            if self.asleep:
                self.add_symbol('z', 2.6)
            elif petting:
                self.add_symbol('heart')
            elif self.typing_rate > 7 and t['act.sweat'] and not self.reduced:
                self.add_symbol('sweat', .7)
            self.next_symbol = now + (.5 if petting else 1.1)

        left, right, top, bottom = self.range
        if self.reduced:
            cx, cy = (left + right) / 2, (top + bottom) / 2
            left, right = cx + (left - cx) * .32, cx + (right - cx) * .32
            top, bottom = cy + (top - cy) * .32, cy + (bottom - cy) * .32
        self.mx.step(left + (right - left) * (1 - clamp(pointer[0], -1, 1)) / 2, dt, t['mouse.follow'], t['mouse.damping'])
        self.my.step(top + (bottom - top) * (1 - clamp(pointer[1], -1, 1)) / 2, dt, t['mouse.follow'], t['mouse.damping'])
        self.mx.constrain(left, right)
        self.my.constrain(top, bottom)
        self.press.step(1 if mouse else 0, dt, 38, .45)
        self.squash = t['mouse.squash'] * self.press.value * (.32 if self.reduced else 1)
        self.lean.step(clamp(-self.mx.velocity * t['mouse.lean'], -t['mouse.leanMax'], t['mouse.leanMax']) * (.32 if self.reduced else 1), dt, 30, .45)
        drift_x = (4 * math.sin(now * .33 + 2.1) + 2 * math.sin(now * .83 + .4)) * t['mouse.drift'] if not self.reduced else 0
        drift_y = (2.2 * math.sin(now * .47 + 1.1) + 1.2 * math.sin(now * 1.65)) * t['mouse.drift'] if not self.reduced else 0
        self.mouse_dx = clamp(self.mx.value + drift_x + self.nod.value * .8, left, right)
        self.mouse_dy = clamp(self.my.value + drift_y + self.press.value * t['mouse.clickSink'], top, bottom)
        if mouse and not self.mouse_was_down:
            self.click_start = now
        self.mouse_was_down = mouse

        age, end = now - self.stroke_start, self.stroke_wind + .055
        typing = age < end + t['key.linger']
        if age < self.stroke_wind:
            target, frequency, damping = self.stroke_peak, 58, .6
        elif age < end:
            target, frequency, damping = -t['key.press'] * amount, 85, .5
        elif age < end + .07:
            target, frequency, damping = t['key.hover'] * amount + self.stroke_peak * t['key.rebound'], 55, .5
        elif typing:
            target, frequency, damping = t['key.hover'] * amount, 30, .55
        else:
            target, frequency, damping = 0, 13, 1
        goal_x, goal_y = (self.target_x, self.target_y) if typing else (0, 0)
        wave = now - self.wave_start
        triumph = now - self.triumph_start
        waving = 0 <= wave < 1.9 and not self.reduced and t['act.greet']
        beaming = 0 <= triumph < 1.2 and not self.reduced
        self.hand_calm = 1 if waving or beaming else 0
        wrist = None
        if waving:
            envelope = smooth(wave / .25) * smooth((1.9 - wave) / .3)
            height, x, y = p['waveHand']
            target, goal_x, goal_y = height * envelope, x * envelope, y * envelope
            frequency, damping = 16, .8
            wrist = math.sin(wave * 17) * 16 * envelope
        elif beaming:
            target = p['triumphLift'][0] * smooth((1.2 - triumph) / .3)
            frequency, damping = 25, .6
            goal_x, goal_y = p['waveHand'][1], p['waveHand'][2]
        elif 0 <= now - self.think_start < 2.6 and not self.reduced:
            target = 10 + max(0, math.sin((now - self.think_start) * 13)) * 12
        self.lift.step(target, dt, frequency, damping)
        floor = -t['key.press'] * amount * 1.3
        if self.lift.value < floor:
            self.lift.value, self.lift.velocity = floor, abs(self.lift.velocity) * .25
        self.rx.step(goal_x, dt, 50 if typing else 11, .9 if typing else 1)
        self.ry.step(goal_y, dt, 50 if typing else 11, .9 if typing else 1)
        self.flick.step(wrist if wrist is not None else clamp(self.lift.velocity * t['key.flick'], -t['key.flickMax'], t['key.flickMax']), dt, 44, .45)
        striking = self.stroke_wind <= age < end + .03
        self.nod.step((t['head.accent'] if self.stroke_accent else 1) * amount if striking else 0, dt, 30, .5)
        self.thump.step(t['act.thump'] if striking and self.stroke_accent and not self.reduced else 0, dt, 46 if striking else 26, .42)

        if now >= self.next_tilt:
            self.head_tilt = self.random.uniform(-2.5, 2.5) * t['head.tiltSize'] if self.head_tilt == 0 else 0
            self.next_tilt = now + self.random.uniform(2, 8) * t['head.tiltEvery']
        sway = t['head.sway']
        angle = -t['head.pointerLean'] * pointer[0] + .55 * math.sin(now * .71) * sway + self.head_tilt
        angle += 4 * self.sleep_level - (2.8 if waving else 0)
        if 0 <= now - self.think_start < 2.6:
            angle += 3 * t['head.tiltSize']
        self.angle.step(0 if self.reduced else angle, dt, 5, .85)
        self.hx.step(0 if self.reduced else 2.2 * math.sin(now * .37 + .6) * sway - 2.5 * pointer[0], dt, 5, 1)
        self.hy.step(0 if self.reduced else max(4.5, t['head.breath'] + 1.3) + math.sin(now * 1.65) * t['head.breath'] + 14 * self.sleep_level, dt, 8, 1)
        if now >= self.next_purse:
            self.purse_until = now + self.random.uniform(1, 1.8)
            self.next_purse = now + self.random.uniform(9, 20) * t['mouth.purseEvery']
        purse = t['mouth.purseTyping'] if now - self.last_key < .4 else (.8 if now < self.purse_until else 0)
        self.purse.step(0 if self.asleep or waving or beaming or petting else purse, dt, 12, .75)
        if now >= self.next_blink:
            self.blink_start = now
            self.blink_pace = self.random.uniform(.8, 1.2) * t['blink.slowness']
            self.next_blink = now + (.33 if self.random.random() < t['blink.double'] else self.random.uniform(1.8, 4.5) * t['blink.every'])
        b = (now - self.blink_start) / self.blink_pace
        self.blink = smooth(b / .085) if b < .085 else (1 if b < .125 else (1 - smooth((b - .125) / .125) if b < .25 else 0))
        if self.asleep or waving and .2 < wave < 1.6 or beaming or petting:
            self.blink = 1
        level = clamp(audio, 0, 1) * self.gain
        mouth_target = clamp(math.sqrt(max(0, (level - self.floor) / .21)), 0, 1)
        self.mouth += (mouth_target - self.mouth) * (1 - math.exp(-dt / (.035 if mouth_target > self.mouth else .11)))
        self.mouth_shown = max(self.mouth, .8 if waving or beaming or petting else 0)
        if not self.reduced and t['act.sleep'] and t['act.sleepAfter'] - 3 < idle < t['act.sleepAfter']:
            self.mouth_shown = max(self.mouth_shown, 1.25 * math.sin(math.pi * (idle - t['act.sleepAfter'] + 3) / 3))
        hair = 0 if self.reduced else t['hair.ahoge']
        self.hair_sway.step(hair * (.011 * math.sin(now * 1.37) - self.hx.velocity * .0011), dt, 18, .22)
        self.hair_stretch.step(hair * clamp(-self.hy.velocity * .0009, -.12, .18), dt, 23, .26)
        self.hair_tip.step(hair * clamp(-self.hy.velocity * .0011, -.16, .16), dt, 15, .2)

    def piece(self, painter, name, clip=None, transform=None, opacity=1.):
        painter.save()
        if transform is not None:
            painter.setTransform(transform, True)
        painter.setOpacity(painter.opacity() * opacity)
        cached = self.cached_pieces.get((name, id(clip))) if clip is not None else None
        if clip is not None and clip.isEmpty():
            painter.restore()
            return
        if cached:
            _, box, pixmap = cached
            painter.drawPixmap(QPointF(box.x(), box.y()), pixmap)
        else:
            if clip is not None:
                painter.setClipPath(clip, Qt.IntersectClip)
            painter.drawPixmap(ART, self.pixmaps[name], ART)
        painter.restore()

    def draw_key(self, painter):
        x, y, turn = self.rx.value, self.ry.value - self.lift.value, self.flick.value
        p, t = self.profile, self.tune
        self.piece(painter, 'key', self.key_root)
        gx, gy = x / p['keyForearm'][0], y / p['keyForearm'][0]
        ux, uy = self.reach
        hinge = ux * p['keyHinge'][0] + uy * p['keyHinge'][1]
        self.piece(painter, 'key', self.key_sleeve, QTransform(1 + gx * ux, gy * ux, gx * uy, 1 + gy * uy, -gx * hinge, -gy * hinge))
        self.piece(painter, 'key', self.key_rim, QTransform.fromTranslate(x, y))
        fx, fy = ux * p['keyForearm'][0], uy * p['keyForearm'][0]
        angle = math.degrees(math.atan2(fx * (fy + y) - fy * (fx + x), fx * (fx + x) + fy * (fy + y)))
        calm = self.hand_calm
        lean = clamp(angle * (.3 + .6 * calm), -9 - 26 * calm, 9 + 26 * calm)
        swell = 1 + t['key.swell'] * (1 - .7 * calm) * clamp(self.lift.value / 90, 0, 1)
        splat = t['key.splat'] * clamp(self.nod.value, 0, 2.6)
        transform = QTransform.fromTranslate(x, y)
        px, py = p['keyPalm']
        transform.translate(px, py)
        transform.rotate(clamp(turn + lean, -26 - 24 * calm, 26 + 24 * calm))
        transform.translate(-px, -py)
        kx, ky = p['keyKnuckle']
        transform.translate(kx, ky)
        transform.scale(swell + splat, swell)
        transform.translate(-kx, -ky)
        self.piece(painter, 'key', self.key_hand, transform)

    def draw_eye(self, painter, key, far):
        region = self.regions[key]
        prefix = 'far' if far else 'near'
        curve, lower = self.profile[prefix + 'LidCurve'], self.profile[prefix + 'LidLower']
        box, patch = self.closed_patches[key]
        painter.save()
        if len(curve) == 12 and len(lower) == 6 and self.blink < .96:
            upper_pts = [QPointF(curve[i] + (curve[i + 6] - curve[i]) * self.blink,
                                curve[i + 1] + (curve[i + 7] - curve[i + 1]) * self.blink) for i in (0, 2, 4)]
            low_pts = [QPointF(lower[i] + (curve[i + 6] - lower[i]) * self.blink,
                              lower[i + 1] + (curve[i + 7] - lower[i + 1]) * self.blink) for i in (0, 2, 4)]
            all_box = region.boundingRect().adjusted(-20, -20, 20, 20)
            covered = QPainterPath()
            covered.setFillRule(Qt.WindingFill)
            for pts, corners in ((upper_pts, (all_box.topRight(), all_box.topLeft())),
                                 (low_pts, (all_box.bottomRight(), all_box.bottomLeft()))):
                covered.moveTo(all_box.left(), pts[0].y())
                covered.lineTo(pts[0])
                covered.quadTo(pts[1], pts[2])
                covered.lineTo(all_box.right(), pts[2].y())
                covered.lineTo(corners[0])
                covered.lineTo(corners[1])
                covered.closeSubpath()
            painter.setClipPath(region, Qt.IntersectClip)
            painter.save()
            painter.setClipPath(covered, Qt.IntersectClip)
            painter.drawImage(QPointF(box.x(), box.y()), patch)
            painter.restore()
            start, control, end = upper_pts
        else:
            painter.drawImage(QPointF(box.x(), box.y()), patch)
            if self.blink >= .96:
                painter.restore()
                return
            line = self.profile[prefix + 'Lid']
            top, bottom = line[0] + line[1] * self.blink, line[2] - line[3] * self.blink
            start, control, end = QPointF(line[4], top + line[5]), QPointF(line[6], top + line[7]), QPointF(line[8], top + line[9])
            opening = QPainterPath(start)
            opening.quadTo(control, end)
            opening.lineTo(end.x(), bottom)
            opening.lineTo(start.x(), bottom)
            opening.closeSubpath()
            painter.setClipPath(region, Qt.IntersectClip)
            self.piece(painter, 'head', opening)
        lid = QPainterPath(start)
        lid.quadTo(control, end)
        painter.setPen(pen(self.ink, self.profile['lidWidth'][1 if far else 0]))
        painter.setBrush(Qt.NoBrush)
        painter.drawPath(lid)
        painter.restore()

    def draw_hair(self, painter):
        base, tip = self.profile['ahogeBase'], self.profile['ahogeTip']
        a, b = point(base[:2]), point(base[2:4])
        length = math.hypot(b.x() - a.x(), b.y() - a.y()) or 1
        along = (b - a) / length
        up = QPointF(along.y(), -along.x())
        push = along * self.hair_sway.value + up * self.hair_stretch.value
        line = a.x() * up.x() + a.y() * up.y()
        lean = QTransform(1 + up.x() * push.x(), up.x() * push.y(), up.y() * push.x(), 1 + up.y() * push.y(), -line * push.x(), -line * push.y())
        self.piece(painter, 'head', self.hair_base_clip, lean)
        if tip:
            flap = QTransform(1, -self.hair_tip.value, 0, 1, 0, tip[0] * self.hair_tip.value)
            self.piece(painter, 'head', self.hair_tip_clip, flap * lean)

    def draw_background(self, painter, resting):
        self.piece(painter, 'base')
        if resting:
            self.piece(painter, 'key')
            if self.profile['collar']:
                self.piece(painter, 'base', self.regions['collar'])
            self.piece(painter, 'mouse', self.shoulder_clip)

    def paint_background(self, painter, resting):
        # Cache only at the exact device-pixel scale. Fractional body motion
        # and other transforms must sample the original art on each frame.
        transform = painter.deviceTransform()
        scale = transform.m11()
        if (transform.type() > QTransform.TxScale or scale <= 0 or
                transform.m22() != scale or transform.dx() or transform.dy()):
            self.draw_background(painter, resting)
            return
        if scale != self.background_scale:
            self.backgrounds.clear()
            self.background_scale = scale
        if resting not in self.backgrounds:
            layer = QPixmap(math.ceil(WIDTH * scale), math.ceil(HEIGHT * scale))
            layer.fill(Qt.transparent)
            brush = QPainter(layer)
            brush.setRenderHints(painter.renderHints())
            brush.scale(scale, scale)
            self.draw_background(brush, resting)
            brush.end()
            self.backgrounds[resting] = layer
        painter.save()
        painter.scale(1 / scale, 1 / scale)
        painter.drawPixmap(0, 0, self.backgrounds[resting])
        painter.restore()

    def draw(self, painter):
        p, t = self.profile, self.tune
        painter.save()
        painter.setRenderHints(QPainter.Antialiasing | QPainter.SmoothPixmapTransform)
        painter.setClipRect(ART, Qt.IntersectClip)
        painter.translate(0, max(0, self.thump.value))
        resting = abs(self.rx.value) + abs(self.ry.value - self.lift.value) + abs(self.flick.value) < .00001
        self.paint_background(painter, resting)
        if not resting:
            self.draw_key(painter)
            if p['collar']:
                self.piece(painter, 'base', self.regions['collar'])
            self.piece(painter, 'mouse', self.shoulder_clip)
        dx, dy = self.mouse_dx, self.mouse_dy
        shoulder, wrist = p['mouseShoulderY'][0], p['mouseWristY'][0]
        reach = wrist - shoulder
        deform = QTransform(1, 0, dx / reach, 1 + dy / reach, -shoulder * dx / reach, -shoulder * dy / reach)
        self.piece(painter, 'mouse', self.sleeve_clip, deform)
        points = p['mouseShoulderLine']
        if points:
            path = QPainterPath()
            for i in range(0, len(points), 2):
                at = point(points[i:i + 2])
                if at.y() > shoulder:
                    at = deform.map(at)
                path.moveTo(at) if i == 0 else path.lineTo(at)
            painter.setPen(pen(self.ink, 9.5))
            painter.drawPath(path)
        self.piece(painter, 'mouse', self.hand_clip, QTransform(1, 0, self.lean.value, 1 - self.squash, dx - wrist * self.lean.value, dy + wrist * self.squash))
        pop = (self.time - self.click_start) / .24
        if 0 <= pop < 1 and t['mouse.clickLines']:
            painter.save()
            painter.setOpacity(min(1, (1 - pop) / .35))
            painter.setPen(pen(self.ink, 10))
            spread = 1 - (1 - pop) ** 2
            inner, outer = 84 + 48 * spread, 104 + 50 * min(1, spread * 1.6)
            origin = point(p['clickOrigin']) + QPointF(dx, dy)
            for ray in p['clickRays']:
                direction = QPointF(math.cos(math.radians(ray)), math.sin(math.radians(ray)))
                painter.drawLine(origin + direction * inner, origin + direction * (inner + (outer - inner) * t['mouse.clickLines']))
            painter.restore()
        if p['neck']:
            self.piece(painter, 'head', self.regions['neck'])
        painter.save()
        pivot = point(p['headPivot'])
        painter.translate(self.hx.value, max(-p['headRise'][0], self.hy.value + self.nod.value * t['head.dip'] + self.press.value * t['head.clickDip']))
        painter.translate(pivot)
        painter.rotate(self.angle.value + self.nod.value * t['head.dipTilt'] - self.press.value * .5)
        painter.translate(-pivot)
        hair = bool(p['ahogeBase'] and t['hair.ahoge'] and not self.reduced)
        self.piece(painter, 'head', self.hair_head_clip if hair else self.head_clip)
        if hair:
            self.draw_hair(painter)
        if self.blink > .001:
            self.draw_eye(painter, 'leftEye', False)
            self.draw_eye(painter, 'rightEye', True)
        if self.purse.value > .01 and self.mouth_shown < .02:
            pressed = clamp(self.purse.value, 0, 1.15)
            center = point(p['mouthCenter'])
            painter.save()
            painter.translate(center)
            painter.scale(*p['mouthCover'])
            gradient = QRadialGradient(QPointF(), 1)
            skin = QColor(*[round(v) for v in p['skinColor']])
            gradient.setColorAt(0, skin)
            gradient.setColorAt(.8, skin)
            skin.setAlpha(0)
            gradient.setColorAt(1, skin)
            painter.setOpacity(min(1, pressed * 6))
            painter.setPen(Qt.NoPen)
            painter.setBrush(gradient)
            painter.drawEllipse(QPointF(), 1, 1)
            painter.restore()
            scale = QTransform().translate(center.x(), center.y() - 8).scale(1 - .24 * pressed, 1 + .07 * pressed).translate(-center.x(), -center.y() + 8)
            self.piece(painter, 'head', self.pursed_region, scale)
        if self.mouth_shown > .018:
            pivot = point(p['mouthPivot'])
            yawn = max(0, self.mouth_shown - 1)
            transform = QTransform().translate(pivot.x(), pivot.y()).scale(1 - .14 * yawn, .43 + self.mouth_shown * .57 + .3 * yawn).translate(-pivot.x(), -pivot.y())
            self.piece(painter, 'mouth', self.mouth_region, transform, min(1, self.mouth_shown * 5))
        painter.restore()
        painter.restore()
        self.draw_symbols(painter)

    def draw_symbols(self, painter):
        painter.save()
        painter.setRenderHint(QPainter.Antialiasing)
        for kind, born, life, sideways in self.symbols:
            age = self.time - born
            painter.save()
            x, y = self.profile['symbolAnchor']
            painter.translate(x + sideways, max(90, y - age * 35))
            size = self.tune['act.symbols'] * min(1, age / .12)
            painter.scale(size, size)
            painter.setOpacity(min(1, (life - age) / .3))
            painter.setPen(pen(self.ink, 8))
            painter.setBrush(self.ink)
            if kind == 'heart':
                path = QPainterPath(QPointF(0, 22))
                path.cubicTo(-110, -38, -35, -102, 0, -48)
                path.cubicTo(35, -102, 110, -38, 0, 22)
                painter.setBrush(QColor(247, 115, 149))
                painter.drawPath(path)
            elif kind == 'star':
                coords = []
                for i in range(10):
                    angle = math.radians(i * 36 - 90)
                    radius = 65 if i % 2 == 0 else 28
                    coords.extend((math.cos(angle) * radius, math.sin(angle) * radius))
                painter.setBrush(QColor(255, 222, 100))
                painter.drawPath(polygon(coords))
            elif kind == 'sweat':
                path = QPainterPath(QPointF(0, -70))
                path.cubicTo(-60, 0, -40, 48, 0, 48)
                path.cubicTo(40, 48, 60, 0, 0, -70)
                painter.setBrush(QColor(138, 215, 247))
                painter.drawPath(path)
            else:
                painter.setFont(QFont('Sans Serif', 85 if kind != '...' else 65, QFont.Bold))
                painter.drawText(QPointF(0, 0), kind)
            painter.restore()
        painter.restore()

    def frame(self, width=560):
        image = QImage(width, round(width * HEIGHT / WIDTH), QImage.Format_ARGB32_Premultiplied)
        image.fill(Qt.transparent)
        painter = QPainter(image)
        painter.scale(width / WIDTH, width / WIDTH)
        self.draw(painter)
        painter.end()
        return image
