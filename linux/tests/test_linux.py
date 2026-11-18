import argparse
import math
import os
from pathlib import Path
import re
import struct
import tempfile
import time
import unittest
from unittest.mock import Mock, patch

os.environ.setdefault('QT_QPA_PLATFORM', 'offscreen')

from PyQt5.QtCore import Qt
from PyQt5.QtWidgets import QApplication, QCheckBox, QDoubleSpinBox

from linux.character import Character, Spring
from linux.main import CompanionWindow, SingleInstance
from linux.profile import ROOT, EXACT, POINTS, Profile, TUNING, assignments, catalog, mouse_range
from linux.settings import Settings
from linux.signals import AudioLevel, EVENT, InputSignals

APP = QApplication.instance() or QApplication(['sumikko-tests'])


class ProfileChecks(unittest.TestCase):
    def test_all_bundled_characters(self):
        entries = catalog()
        self.assertTrue({'', 'koji-chan', 'pink-chan'}.issubset({e.id for e in entries}))
        for entry in entries:
            with self.subTest(character=entry.id):
                self.assertFalse(entry.profile.problems)
                self.assertTrue(entry.profile.complete())
                self.assertTrue(entry.profile.name)

    def test_schema_matches_windows(self):
        source = (ROOT / 'app/CharacterProfile.cs').read_text()
        constructor = source[source.index('public CharacterProfile()'):source.index('private void Exact')]
        exact = {key: len(re.findall(r'-?(?:\d*\.)?\d+', values)) for key, values in
                 re.findall(r'Exact\("([^"]+)",([^;]+)\);', constructor)}
        points = {key: (int(count), empty == 'true') for key, count, empty in
                  re.findall(r'Points\("([^"]+)", (\d+), (true|false)', constructor)}
        self.assertEqual(EXACT, exact)
        self.assertEqual(POINTS, points)

    def test_tuning_matches_windows(self):
        source = (ROOT / 'app/MotionTuning.cs').read_text()
        expected = {}
        for key, label, low, high, default, hint in re.findall(
                r'Add\("([^"]+)", \w+, "([^"]+)", ([\d.]+), ([\d.]+), ([\d.]+), "([^"]+)"\)', source):
            expected[key] = dict(label=label, min=float(low), max=float(high), default=float(default), hint=hint)
        for key, label, hint in re.findall(r'Toggle\("([^"]+)", "([^"]+)", "([^"]+)"\)', source):
            expected[key] = dict(label=label, min=0, max=1, default=1, hint=hint)
        self.assertEqual(TUNING, expected)

    def test_invalid_override_keeps_default(self):
        profile = Profile(ROOT / 'assets')
        before = profile['keyHinge'][:]
        profile.read('keyHinge = nan, 10\nunknown = 1\nleftEye = 1,2,3\nname = テスト')
        self.assertEqual(profile['keyHinge'], before)
        self.assertEqual(len(profile.problems), 3)
        self.assertEqual(profile.name, 'テスト')

    def test_empty_and_continued_polygons(self):
        self.assertEqual(list(assignments('neck = # empty\nkeyHinge = 1,\n 2\n')), [('neck', ''), ('keyHinge', '1, 2')])

    def test_image_paths_stay_in_character_directory(self):
        profile = Profile(ROOT / 'assets')
        for name in ('../outside.png', '/tmp/outside.png', '..', 'a\\b.png'):
            profile.read('closedEyesImage = ' + name)
        self.assertEqual(len(profile.problems), 4)
        self.assertEqual(profile.image_name(True), 'face-expression.png')

    def test_separate_eye_and_mouth_images(self):
        profile = Profile(ROOT / 'characters/koji-chan')
        self.assertEqual(profile.image_name(True), 'eyes-closed.png')
        self.assertEqual(profile.image_name(False), 'mouth-open.png')

    def test_character_tuning_overrides(self):
        profile = Profile(ROOT / 'characters/koji-chan')
        self.assertEqual(profile.tuning['hair.ahoge'], 0)
        self.assertEqual(profile.tuning['key.height'], 33)
        self.assertFalse(profile['neck'])

    def test_unsafe_geometry_is_reported(self):
        profile = Profile(ROOT / 'assets')
        profile.read('keyCuffAxis = 1,0\nkeyForearm = 0\nmouseWristY = 10')
        profile.validate_geometry()
        self.assertEqual(len(profile.problems), 3)
        with self.assertRaises(ValueError):
            Character(profile)


class MotionChecks(unittest.TestCase):
    def character(self, id=''):
        entry = next(e for e in catalog() if e.id == id)
        char = Character(entry.profile, seed=17)
        char.wave_start = -100
        return char

    def test_spring_large_dt_stays_finite_and_settles(self):
        spring = Spring()
        for _ in range(1000):
            spring.step(55, 5, 85, .5)
            self.assertTrue(math.isfinite(spring.value))
        self.assertEqual(spring.value, 55)

    def test_keypress_moves_arm_and_returns_home(self):
        char = self.character('koji-chan')
        for frame in range(20):
            char.tick(frame / 60, 1 / 60, key=frame == 0)
        self.assertGreater(abs(char.rx.value) + abs(char.lift.value), 5)
        for frame in range(20, 650):
            char.tick(frame / 60, 1 / 60)
        self.assertAlmostEqual(char.rx.value, 0, places=4)
        self.assertAlmostEqual(char.lift.value, 0, places=4)

    def test_held_key_stays_within_keyboard(self):
        char = self.character('koji-chan')
        char.tick(0, 1 / 60, key=True)
        origin = (char.hold_x, char.hold_y)
        for frame in range(1, 600):
            char.tick(frame / 60, 1 / 60, key=frame % 5 == 0, repeat=True)
            self.assertLessEqual(abs(char.target_x - origin[0]), 7.01)
            self.assertLessEqual(abs(char.target_y - origin[1]), 4.01)
            self.assertGreaterEqual(char.lift.value, -char.tune['key.press'] * 1.3)

    def test_locked_mouse_axis_and_click_bounds(self):
        char = self.character()
        char.range = (0, 0, -10, -10)
        for frame in range(200):
            char.tick(frame / 60, 1 / 60, pointer=(1, -1), mouse=frame % 2 == 0)
            self.assertEqual(char.mouse_dx, 0)
            self.assertEqual(char.mouse_dy, -10)

    def test_mouse_range_sorts_and_clamps(self):
        self.assertEqual(mouse_range((400, -400, 100, -500)), (-220, 220, -160, 8))

    def test_sleep_and_wake(self):
        char = self.character()
        char.tune['act.sleepAfter'] = 1
        char.tick(2, .02)
        self.assertTrue(char.asleep)
        self.assertEqual(char.blink, 1)
        char.tick(2.02, .02, key=True)
        self.assertFalse(char.asleep)
        self.assertTrue(any(s[0] == '!' for s in char.symbols))

    def test_reduced_motion_disables_scenes(self):
        char = self.character()
        char.reduced = True
        char.tune['act.sleepAfter'] = 1
        char.tick(200, .02)
        self.assertFalse(char.asleep)
        self.assertEqual(char.angle.value, 0)
        self.assertEqual(char.hair_sway.value, 0)

    def test_lipsync_opens_and_closes(self):
        char = self.character('pink-chan')
        for frame in range(30):
            char.tick(frame / 60, 1 / 60, audio=.3)
        self.assertGreater(char.mouth_shown, .95)
        for frame in range(30, 120):
            char.tick(frame / 60, 1 / 60)
        self.assertLess(char.mouth_shown, .001)

    def test_pet_requires_reversals(self):
        char = self.character()
        for i, x in enumerate((450, 520, 600)):
            char.time = i * .1
            char.pet(x, 300)
        self.assertLess(char.pet_until, 0)
        for i, x in enumerate((500, 600, 500)):
            char.time = .5 + i * .1
            char.pet(x, 300)
        self.assertGreater(char.pet_until, char.time)

    def test_all_character_frames_are_transparent_and_animated(self):
        for entry in catalog():
            with self.subTest(character=entry.id):
                char = Character(entry.profile, seed=1)
                char.wave_start = -100
                rest = char.frame(320)
                self.assertEqual(rest.pixelColor(319, 0).alpha(), 0)
                self.assertTrue(any(rest.pixelColor(x, y).alpha() for x in range(0, 320, 20) for y in range(0, 230, 20)))
                for frame in range(25):
                    char.tick(frame / 60, 1 / 60, key=frame % 8 == 0, pointer=(1, -1))
                self.assertNotEqual(rest, char.frame(320))

    def test_blinks_change_only_face(self):
        for entry in catalog():
            with self.subTest(character=entry.id):
                char = Character(entry.profile, seed=1)
                char.blink = 0
                opened = char.frame()
                char.blink = .5
                halfway = char.frame()
                char.blink = 1
                closed = char.frame()
                self.assertNotEqual(opened, halfway)
                self.assertNotEqual(halfway, closed)
                self.assertEqual(opened.copy(0, 240, 560, 160), closed.copy(0, 240, 560, 160))


class InputChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.inputs = InputSignals(self.temp.name, x11=False)
        self.device = Mock(keyboard=True, mouse=True, dropped=False, absolute={}, buttons=set())

    def tearDown(self):
        self.inputs.close()
        self.temp.cleanup()

    def test_press_repeat_accent_and_modifier(self):
        self.inputs.consume(self.device, 1, 42, 1)
        self.assertFalse(self.inputs.key)
        self.inputs.consume(self.device, 1, 30, 1)
        self.assertTrue(self.inputs.key)
        self.assertFalse(self.inputs.accent)
        self.inputs.consume(self.device, 1, 28, 1)
        self.assertTrue(self.inputs.accent)
        self.inputs.consume(self.device, 1, 30, 2)
        self.assertTrue(self.inputs.repeat)

    def test_mouse_buttons_and_relative_motion(self):
        self.inputs.consume(self.device, 1, 272, 1)
        self.assertEqual(self.device.buttons, {272})
        self.inputs.consume(self.device, 2, 0, 100000)
        self.inputs.consume(self.device, 2, 1, -100000)
        self.assertEqual(self.inputs.pointer, [1, -1])
        self.inputs.consume(self.device, 1, 272, 0)
        self.assertFalse(self.device.buttons)

    def test_absolute_input_normalization(self):
        self.device.absolute = {0: (100, 900)}
        self.inputs.consume(self.device, 3, 0, 500)
        self.assertEqual(self.inputs.pointer[0], 0)
        self.inputs.consume(self.device, 3, 0, 900)
        self.assertEqual(self.inputs.pointer[0], 1)

    def test_dropped_events_do_not_replay_keys(self):
        self.inputs.consume(self.device, 0, 3, 0)
        self.inputs.consume(self.device, 1, 30, 1)
        self.assertFalse(self.inputs.key)
        self.inputs.consume(self.device, 0, 0, 0)
        self.device.sync_buttons.assert_called_once()
        self.inputs.consume(self.device, 1, 30, 1)
        self.assertTrue(self.inputs.key)

    def test_nonblocking_poll_reads_native_events_and_closes_fd(self):
        read, write = os.pipe2(os.O_NONBLOCK | os.O_CLOEXEC)
        self.device.fd = read
        self.inputs.devices[Path(self.temp.name) / 'event1'] = self.device
        self.inputs.next_scan = time.monotonic() + 100
        try:
            os.write(write, EVENT.pack(0, 0, 1, 28, 1) + EVENT.pack(0, 0, 1, 272, 1))
            self.inputs.poll()
            self.assertTrue(self.inputs.key and self.inputs.accent and self.inputs.mouse_down)
            self.inputs.poll()
            self.assertFalse(self.inputs.key)
            self.assertTrue(self.inputs.mouse_down)
            self.inputs.close()
            with self.assertRaises(OSError):
                os.fstat(read)
        finally:
            os.close(write)

    def test_no_permissions_has_useful_status(self):
        self.assertIn('0台', self.inputs.status)
        self.assertIn('デモ', self.inputs.status)


class AudioChecks(unittest.TestCase):
    def test_rms_level_and_cleanup(self):
        level = AudioLevel()
        read, write = os.pipe2(os.O_NONBLOCK)
        process = Mock()
        process.poll.return_value = None
        process.stdout = os.fdopen(read, 'rb')
        level.process = process
        try:
            os.write(write, struct.pack('<hhhh', 16384, -16384, 16384, -16384))
            self.assertAlmostEqual(level.poll(), .5)
            self.assertAlmostEqual(level.poll(), .5)  # EAGAIN never blocks
            level.close()
            process.terminate.assert_called_once()
            self.assertTrue(process.stdout.closed)
            self.assertEqual(level.level, 0)
        finally:
            os.close(write)

    def test_audio_failure_is_reported_and_defaults_off(self):
        level = AudioLevel()
        self.assertIsNone(level.process)
        with patch('linux.signals.subprocess.Popen', side_effect=FileNotFoundError('parec')):
            level.start('@DEFAULT_SOURCE@')
        self.assertIn('開始できません', level.status)
        self.assertIsNone(level.process)


class SettingsChecks(unittest.TestCase):
    def test_atomic_save_and_roundtrip(self):
        with tempfile.TemporaryDirectory() as directory:
            settings = Settings(directory)
            settings.data = dict(character='工事', position=[10, 20], size=500)
            settings.save()
            loaded = Settings(directory)
            self.assertEqual(loaded.character(), '工事')
            self.assertEqual(loaded.position(), [10, 20])
            self.assertEqual(loaded.number('size', 440, 320, 560), 500)
            self.assertEqual([p.name for p in Path(directory).iterdir()], ['settings.json'])

    def test_bad_settings_are_bounded(self):
        with tempfile.TemporaryDirectory() as directory:
            settings = Settings(directory)
            settings.data = dict(character=[], size=float('nan'), position=[None, 12],
                                 reduced='false', mouseRanges={'': [1, 2, 'bad', 3]}, tuning=[])
            self.assertEqual(settings.character(), '')
            self.assertEqual(settings.number('size', 440, 320, 560), 440)
            self.assertIsNone(settings.position())
            self.assertFalse(settings.flag('reduced'))
            self.assertEqual(settings.range_for('', (-10, 10, -5, 0)), (-10, 10, -5, 0))
            self.assertEqual(settings.tuning_for(''), {})

    def test_damaged_file_is_reported(self):
        with tempfile.TemporaryDirectory() as directory:
            (Path(directory) / 'settings.json').write_text('{bad')
            settings = Settings(directory)
            self.assertTrue(settings.problem)
            self.assertEqual(settings.data, {})


class WindowChecks(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.settings = Settings(Path(self.temp.name) / 'bin/linux')
        self.args = argparse.Namespace(character='', demo=False, smoke_test=False)
        self.inputs = InputSignals(self.temp.name, x11=False)
        self.window = CompanionWindow(self.args, self.settings, input_signals=self.inputs)

    def tearDown(self):
        self.window.close()
        APP.processEvents()
        self.temp.cleanup()

    def test_transparent_topmost_window_and_menu(self):
        self.window.show()
        APP.processEvents()
        self.assertTrue(self.window.testAttribute(Qt.WA_TranslucentBackground))
        self.assertTrue(self.window.windowFlags() & Qt.WindowStaysOnTopHint)
        self.window.rebuild_menu()
        self.assertIn('キャラクター', [a.text() for a in self.window.menu.actions()])
        self.assertEqual(self.window.audio_source, '')
        self.assertIsNone(self.window.audio.process)

    def test_switch_character_restore_size_and_keep_windows_settings(self):
        bin_dir = Path(self.temp.name) / 'bin'
        bin_dir.mkdir(exist_ok=True)
        sentinels = [bin_dir / name for name in ('settings.txt', 'tuning.txt', 'tuning-koji-chan.txt')]
        for path in sentinels:
            path.write_bytes(b'preserve-this-exactly\r\n')
        entry = next(e for e in self.window.entries if e.id == 'koji-chan')
        self.window.choose_character(entry)
        self.window.set_size(500)
        loaded = Settings(self.settings.directory)
        self.assertEqual(loaded.character(), 'koji-chan')
        self.assertEqual(loaded.number('size', 440, 320, 560), 500)
        for path in sentinels:
            self.assertEqual(path.read_bytes(), b'preserve-this-exactly\r\n')

    def test_tuning_and_range_dialogs_and_persistence(self):
        self.window.tuning_dialog()
        dialog = self.window.dialogs[-1]
        check = dialog.findChildren(QCheckBox)[0]
        check.setChecked(False)
        spin = dialog.findChildren(QDoubleSpinBox)[0]
        spin.setValue(.6)
        dialog.close()
        self.window.range_dialog()
        dialog = self.window.dialogs[-1]
        dialog.findChildren(QDoubleSpinBox)[0].setValue(-35)
        dialog.close()
        loaded = Settings(self.settings.directory)
        self.assertAlmostEqual(loaded.number('strength', 1, .2, 1.6), .6)
        self.assertEqual(loaded.range_for('', (-120, 120, -45, 5))[0], -35)

    def test_smoke_mode_does_not_save(self):
        self.args.smoke_test = True
        self.window.save_settings()
        self.assertFalse(self.settings.path.exists())

    def test_pause_drains_input_without_moving(self):
        self.window.show()
        self.window.paused = True
        self.window.local_key = True
        before = self.window.character.time
        self.window.tick()
        self.assertEqual(self.window.character.time, before)
        self.assertFalse(self.window.local_key)

    def test_input_events_reach_the_visible_arm(self):
        read, write = os.pipe2(os.O_NONBLOCK | os.O_CLOEXEC)
        device = Mock(fd=read, keyboard=True, mouse=True, dropped=False,
                      absolute={}, buttons=set())
        self.inputs.devices[Path(self.temp.name) / 'event1'] = device
        self.inputs.next_scan = time.monotonic() + 100
        self.window.character.wave_start = -100
        self.window.show()
        try:
            os.write(write, EVENT.pack(0, 0, 1, 30, 1) + EVENT.pack(0, 0, 2, 0, 200))
            self.window.last_tick = time.monotonic() - 1 / 60
            self.window.tick()
            self.assertEqual(self.window.character.last_key, self.window.elapsed)
            self.assertGreater(abs(self.window.character.rx.value) + self.window.character.lift.value, .1)
            self.assertGreater(self.inputs.pointer[0], 0)
        finally:
            os.close(write)

    def test_pause_and_hide_release_audio_and_resume_reopens_it(self):
        self.window.show()
        with patch.object(self.window.audio, 'start') as start, patch.object(self.window.audio, 'close') as close:
            self.window.set_audio('test-monitor')
            start.assert_called_with('test-monitor')
            self.window.set_paused(True)
            start.assert_called_with('')
            self.window.set_paused(False)
            start.assert_called_with('test-monitor')
            self.window.hide()
            close.assert_called()
            self.window.show()
            start.assert_called_with('test-monitor')

    def test_single_instance_lock_released(self):
        first = SingleInstance(self.temp.name)
        second = SingleInstance(self.temp.name)
        self.assertTrue(first.primary)
        self.assertFalse(second.primary)
        first.close()
        second.close()
        third = SingleInstance(self.temp.name)
        self.assertTrue(third.primary)
        third.close()


if __name__ == '__main__':
    unittest.main()
