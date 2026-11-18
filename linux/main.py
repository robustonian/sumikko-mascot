"""Transparent desktop window, menus, tray, lifecycle and preview exports."""
import argparse
import ctypes
import fcntl
import hashlib
import os
from pathlib import Path
import signal
import sys
import tempfile
import time

from PyQt5.QtCore import QPoint, Qt, QTimer
from PyQt5.QtGui import QCursor, QIcon, QPainter, QPixmap
from PyQt5.QtNetwork import QLocalServer, QLocalSocket
from PyQt5.QtWidgets import (QActionGroup, QApplication, QCheckBox, QDialog,
                            QDialogButtonBox, QDoubleSpinBox, QFormLayout,
                            QMenu, QMessageBox, QOpenGLWidget, QScrollArea, QSystemTrayIcon,
                            QVBoxLayout, QWidget)

from .character import Character
from .profile import ROOT, WIDTH, HEIGHT, TUNING, catalog, clamp, mouse_range
from .settings import Settings
from .signals import AudioLevel, InputSignals, audio_devices


class SingleInstance:
    def __init__(self, directory):
        directory = Path(directory)
        directory.mkdir(parents=True, exist_ok=True)
        self.file = (directory / 'instance.lock').open('a')
        self.name = 'sumikko-' + hashlib.sha256(str(directory.resolve()).encode()).hexdigest()[:24]
        self.server = None
        try:
            fcntl.flock(self.file, fcntl.LOCK_EX | fcntl.LOCK_NB)
            self.primary = True
        except BlockingIOError:
            self.primary = False

    def listen(self, callback):
        QLocalServer.removeServer(self.name)  # only the lock owner removes stale sockets
        self.server = QLocalServer()
        self.server.setSocketOptions(QLocalServer.UserAccessOption)
        if not self.server.listen(self.name):
            raise OSError(self.server.errorString())

        def connected():
            while self.server.hasPendingConnections():
                socket = self.server.nextPendingConnection()
                callback()
                socket.disconnectFromServer()
                socket.deleteLater()
        self.server.newConnection.connect(connected)

    def show_existing(self):
        socket = QLocalSocket()
        socket.connectToServer(self.name)
        connected = socket.waitForConnected(1500)
        socket.close()
        return connected

    def close(self):
        if self.server:
            self.server.close()
        self.file.close()


class CompanionControls:
    renderer = 'CPU (Qt Raster)'

    def __init__(self, args, settings, entries=None, input_signals=None):
        super().__init__(None, Qt.Tool | Qt.FramelessWindowHint | Qt.WindowStaysOnTopHint | Qt.WindowDoesNotAcceptFocus)
        self.args, self.settings = args, settings
        self.entries = entries if entries is not None else catalog()
        self.inputs = input_signals if input_signals is not None else InputSignals()
        self.audio = AudioLevel()
        self.character_id = ''
        self.character = None
        self.size = round(settings.number('size', 440, 320, 560))
        self.strength = settings.number('strength', 1., .2, 1.6)
        self.reduced = settings.flag('reduced')
        self.paused = False
        self.demo = args.demo
        self.click_through = False
        self.audio_source = ''  # audio always starts off
        self.drag_at = None
        self.dialogs = []
        self.started = self.last_tick = time.monotonic()
        self.last_draw = 0.
        self.elapsed = 0.
        self.local_key = self.local_accent = self.local_repeat = False
        self.local_mouse = False
        self.cleaned = False
        self.setWindowTitle('隅っこキャラ (Linux)')
        self.setAttribute(Qt.WA_TranslucentBackground)
        self.setAttribute(Qt.WA_ShowWithoutActivating)
        self.setMouseTracking(True)
        self.setFocusPolicy(Qt.NoFocus)
        self.tray = QSystemTrayIcon(self)
        wanted = settings.character() if args.character is None else args.character
        chosen = next((e for e in self.entries if e.id == wanted), self.entries[0])
        try:
            self.select_character(chosen)
        except (ValueError, OSError):
            if chosen is self.entries[0]:
                raise
            self.select_character(self.entries[0])
        self.set_size(self.size, save=False)
        saved_position = settings.position()
        if saved_position:
            self.move(*saved_position)
        if not saved_position or not any(s.availableGeometry().intersects(self.frameGeometry()) for s in QApplication.screens()):
            self.return_to_screen()
        self.menu = QMenu(self)
        self.menu.aboutToShow.connect(self.rebuild_menu)
        self.tray.setContextMenu(self.menu)
        self.tray.setToolTip('隅っこキャラ — 右クリックで設定')
        self.tray.activated.connect(self.tray_activated)
        if QSystemTrayIcon.isSystemTrayAvailable():
            self.tray.show()
        self.timer = QTimer(self)
        self.timer.setTimerType(Qt.PreciseTimer)
        self.timer.setInterval(16)
        self.timer.timeout.connect(self.tick)
        self.timer.start()

    def select_character(self, entry):
        # Load first; a bad character must not replace a working one.
        character = Character(entry.profile)
        character.time = self.elapsed
        character.last_activity = self.elapsed
        character.greet()
        for key, value in self.settings.tuning_for(entry.id).items():
            if key in TUNING:
                spec = TUNING[key]
                character.tune[key] = clamp(value, spec['min'], spec['max'])
        character.range = self.settings.range_for(entry.id, entry.profile['mouseRange'])
        character.strength, character.reduced, character.demo = self.strength, self.reduced, self.demo
        character.gain = self.settings.number('gain', 1., .2, 8)
        character.floor = self.settings.number('floor', .015, 0, .15)
        self.character, self.character_id = character, entry.id
        icon = QIcon(QPixmap.fromImage(character.tray_image))
        self.setWindowIcon(icon)
        self.tray.setIcon(icon)
        self.update()

    def choose_character(self, entry):
        try:
            for dialog in self.dialogs[:]:
                dialog.close()
            self.select_character(entry)
            self.save_settings()
        except (ValueError, OSError) as exc:
            QMessageBox.warning(self, 'キャラクターを開けません', str(exc))

    def set_size(self, size, save=True):
        bottom_right = self.geometry().bottomRight()
        self.size = round(clamp(size, 320, 560))
        self.setFixedSize(self.size, round(self.size * HEIGHT / WIDTH))
        if self.isVisible():
            self.move(bottom_right - QPoint(self.width() - 1, self.height() - 1))
        self.update()
        if save:
            self.save_settings()

    def return_to_screen(self):
        screen = QApplication.screenAt(QCursor.pos()) or QApplication.primaryScreen()
        rect = screen.availableGeometry()
        self.move(rect.right() - self.width() - 16, rect.bottom() - self.height() - 16)

    def show_companion(self):
        if self.click_through:
            self.set_click_through(False)
        if not any(s.availableGeometry().intersects(self.frameGeometry()) for s in QApplication.screens()):
            self.return_to_screen()
        self.show()
        self.raise_()
        self.character.greet()

    def tray_activated(self, reason):
        if reason in (QSystemTrayIcon.Trigger, QSystemTrayIcon.DoubleClick):
            self.show_companion()

    def set_click_through(self, enabled):
        # Only offer click-through with a visible tray, so there is a way back.
        self.click_through = enabled
        self.setWindowFlag(Qt.WindowTransparentForInput, enabled)
        self.show()

    def tick(self):
        now = time.monotonic()
        dt = min(.05, max(0., now - self.last_tick))
        self.last_tick = now
        self.inputs.poll()  # keep draining while paused; never replay old input
        audio = self.audio.poll()
        if not self.paused and self.isVisible():
            self.elapsed += dt
            pointer = tuple(self.inputs.pointer)
            if os.environ.get('XDG_SESSION_TYPE') != 'wayland' or not self.inputs.mouse_count:
                pos = QCursor.pos()
                screen = QApplication.screenAt(pos) or QApplication.primaryScreen()
                rect = screen.geometry()
                pointer = (clamp((pos.x() - rect.left()) * 2 / max(1, rect.width()) - 1, -1, 1),
                           clamp((pos.y() - rect.top()) * 2 / max(1, rect.height()) - 1, -1, 1))
            self.character.demo = self.demo
            self.character.tick(self.elapsed, dt, self.inputs.key or self.local_key,
                                self.inputs.accent or self.local_accent,
                                self.inputs.repeat or self.local_repeat,
                                self.inputs.mouse_down or self.local_mouse, pointer, audio)
            if self.character.needs_full_frame_rate or now - self.last_draw >= 1 / 30:
                self.last_draw = now
                self.update()
        self.local_key = self.local_accent = self.local_repeat = False

    def draw_character(self):
        painter = QPainter(self)
        painter.setCompositionMode(QPainter.CompositionMode_Source)
        painter.fillRect(self.rect(), Qt.transparent)
        painter.setCompositionMode(QPainter.CompositionMode_SourceOver)
        painter.scale(self.width() / WIDTH, self.width() / WIDTH)
        self.character.draw(painter)
        painter.end()

    def mousePressEvent(self, event):
        if event.button() == Qt.LeftButton:
            self.drag_at = event.globalPos() - self.pos()
            self.local_mouse = True

    def mouseMoveEvent(self, event):
        if self.drag_at is not None and event.buttons() & Qt.LeftButton:
            self.move(event.globalPos() - self.drag_at)
        self.character.pet(event.pos().x() * WIDTH / self.width(), event.pos().y() * WIDTH / self.width())

    def leaveEvent(self, event):
        self.character.pet_extreme = None

    def mouseReleaseEvent(self, event):
        if event.button() == Qt.LeftButton:
            self.drag_at = None
            self.local_mouse = False
            self.save_settings()

    def keyPressEvent(self, event):
        if event.key() == Qt.Key_Escape:
            self.show_companion()
        else:
            self.local_key = True
            self.local_accent = event.key() in (Qt.Key_Return, Qt.Key_Enter)
            self.local_repeat = event.isAutoRepeat()

    def contextMenuEvent(self, event):
        self.menu.popup(event.globalPos())

    def add_check(self, menu, text, checked, callback):
        action = menu.addAction(text)
        action.setCheckable(True)
        action.setChecked(checked)
        action.toggled.connect(callback)
        return action

    def rebuild_menu(self):
        self.menu.clear()
        title = self.menu.addAction(self.character.profile.name + ' (Linux)')
        title.setEnabled(False)
        characters = self.menu.addMenu('キャラクター')
        group = QActionGroup(characters)
        for entry in self.entries:
            action = characters.addAction(entry.profile.name)
            action.setCheckable(True)
            action.setChecked(entry.id == self.character_id)
            group.addAction(action)
            action.triggered.connect(lambda checked, e=entry: self.choose_character(e))
        sizes = self.menu.addMenu('表示サイズ')
        group = QActionGroup(sizes)
        for size in (320, 380, 440, 500, 560):
            action = sizes.addAction(str(size))
            action.setCheckable(True)
            action.setChecked(self.size == size)
            group.addAction(action)
            action.triggered.connect(lambda checked, s=size: self.set_size(s))
        self.menu.addAction('画面内に戻す', self.return_to_screen)
        if self.tray.isVisible():
            self.menu.addAction('隠す' if self.isVisible() else '表示する', self.hide if self.isVisible() else self.show_companion)
            self.add_check(self.menu, 'クリックを背面に通す', self.click_through, self.set_click_through)
        self.menu.addSeparator()
        self.add_check(self.menu, '一時停止', self.paused, self.set_paused)
        self.add_check(self.menu, 'デモ（自動で動く）', self.demo, lambda value: setattr(self, 'demo', value))
        self.add_check(self.menu, '動きを控えめにする', self.reduced, self.set_reduced)
        self.menu.addAction('動きの調整…', self.tuning_dialog)
        self.menu.addAction('マウスの範囲…', self.range_dialog)
        audio = self.menu.addMenu('音声（口パク）')
        group = QActionGroup(audio)
        for device, label in [('', 'オフ'), ('@DEFAULT_SOURCE@', '既定のマイク'),
                              ('@DEFAULT_MONITOR@', 'PCの再生音')] + audio_devices():
            action = audio.addAction(label)
            action.setCheckable(True)
            action.setChecked(device == self.audio_source)
            group.addAction(action)
            action.triggered.connect(lambda checked, d=device: self.set_audio(d))
        audio.addSeparator()
        audio.addAction('感度の調整…', self.audio_dialog)
        self.menu.addAction('入力・音声の状態…', self.status_dialog)
        self.menu.addSeparator()
        self.menu.addAction('終了', self.close)

    def set_reduced(self, value):
        self.reduced = self.character.reduced = value
        self.save_settings()

    def set_audio(self, source):
        self.audio_source = source
        self.audio.start(source if not self.paused and self.isVisible() else '')

    def set_paused(self, paused):
        self.paused = paused
        self.audio.start(self.audio_source if not paused and self.isVisible() else '')

    def hideEvent(self, event):
        self.audio.close()
        super().hideEvent(event)

    def showEvent(self, event):
        if self.audio_source and not self.paused:
            self.audio.start(self.audio_source)
        super().showEvent(event)

    def status_dialog(self):
        QMessageBox.information(self, '入力・音声の状態', self.inputs.status + '\n\n' + self.audio.status +
                                '\n\n表示: Qt / ' + QApplication.platformName() + ' / ' + self.renderer +
                                '\n設定: ' + str(self.settings.path) +
                                '\n\n再度 Start.sh を実行すると、隠したキャラを表示できます。')

    def make_dialog(self, title):
        # Nonmodal so motion and audio remain visible while sliders are adjusted.
        dialog = QDialog(self, Qt.Window)
        dialog.setWindowTitle(title)
        dialog.setAttribute(Qt.WA_DeleteOnClose)
        dialog.setWindowFlag(Qt.WindowStaysOnTopHint)
        layout = QVBoxLayout(dialog)
        scroll = QScrollArea(dialog)
        scroll.setWidgetResizable(True)
        content = QWidget()
        form = QFormLayout(content)
        scroll.setWidget(content)
        layout.addWidget(scroll)
        buttons = QDialogButtonBox(QDialogButtonBox.Close | QDialogButtonBox.RestoreDefaults)
        buttons.rejected.connect(dialog.close)
        layout.addWidget(buttons)
        dialog.finished.connect(lambda: (self.save_settings(), self.dialogs.remove(dialog)))
        self.dialogs.append(dialog)
        dialog.resize(560, 540)
        return dialog, form, buttons

    @staticmethod
    def spin(form, label, value, low, high, callback, hint=''):
        widget = QDoubleSpinBox()
        widget.setRange(low, high)
        widget.setDecimals(4 if high <= .1 else (2 if high <= 10 else 1))
        widget.setSingleStep((high - low) / 100)
        widget.setValue(value)
        widget.setToolTip(hint)
        widget.valueChanged.connect(callback)
        form.addRow(label, widget)
        return widget

    def tuning_dialog(self):
        dialog, form, buttons = self.make_dialog('動きの調整')
        widgets = {}
        strength = self.spin(form, '動き全体の大きさ', self.strength, .2, 1.6, self.set_strength)
        for key, spec in TUNING.items():
            if key.startswith('act.') and spec['max'] == 1:
                widget = QCheckBox()
                widget.setChecked(self.character.tune[key] >= .5)
                widget.toggled.connect(lambda value, k=key: self.character.tune.__setitem__(k, int(value)))
                widget.setToolTip(spec['hint'])
                form.addRow(spec['label'], widget)
            else:
                widget = self.spin(form, spec['label'], self.character.tune[key], spec['min'], spec['max'],
                                   lambda value, k=key: self.character.tune.__setitem__(k, value), spec['hint'])
            widgets[key] = widget

        def reset():
            strength.setValue(1.)
            for key, widget in widgets.items():
                value = self.character.profile.tuning[key]
                if isinstance(widget, QCheckBox):
                    widget.setChecked(value >= .5)
                else:
                    widget.setValue(value)
        buttons.button(QDialogButtonBox.RestoreDefaults).clicked.connect(reset)
        dialog.show()

    def set_strength(self, value):
        self.strength = self.character.strength = value

    def range_dialog(self):
        dialog, form, buttons = self.make_dialog('マウスの範囲（原寸のpx）')
        controls = []

        def changed():
            self.character.range = mouse_range([w.value() for w in controls])
        for i, label in enumerate(('左端', '右端', '上端', '下端')):
            controls.append(self.spin(form, label, self.character.range[i], -220 if i < 2 else -160,
                                      220 if i < 2 else 8, lambda value: changed()))

        def reset():
            for widget, value in zip(controls, self.character.profile['mouseRange']):
                widget.setValue(value)
        buttons.button(QDialogButtonBox.RestoreDefaults).clicked.connect(reset)
        dialog.show()

    def audio_dialog(self):
        dialog, form, buttons = self.make_dialog('口パクの感度')
        gain = self.spin(form, '感度', self.character.gain, .2, 8,
                         lambda value: setattr(self.character, 'gain', value))
        floor = self.spin(form, '小さい音を無視するしきい値', self.character.floor, 0, .15,
                          lambda value: setattr(self.character, 'floor', value))
        buttons.button(QDialogButtonBox.RestoreDefaults).clicked.connect(lambda: (gain.setValue(1.), floor.setValue(.015)))
        dialog.show()

    def save_settings(self):
        if self.args.smoke_test:
            return
        data = self.settings.data
        data.update(character=self.character_id, size=self.size, strength=self.strength,
                    reduced=self.reduced, position=[self.x(), self.y()],
                    gain=self.character.gain, floor=self.character.floor)
        for key in ('mouseRanges', 'tuning'):
            if not isinstance(data.get(key), dict):
                data[key] = {}
        data['mouseRanges'][self.character_id] = list(self.character.range)
        data['tuning'][self.character_id] = {k: v for k, v in self.character.tune.items() if v != self.character.profile.tuning[k]}
        try:
            self.settings.save()
        except OSError as exc:
            print('設定を保存できません: ' + str(exc), file=sys.stderr)

    def release_resources(self, save):
        if not self.cleaned:
            for dialog in self.dialogs[:]:
                dialog.close()
            self.timer.stop()
            if save:
                self.save_settings()
            self.audio.close()
            self.inputs.close()
            self.tray.hide()
            self.cleaned = True

    def closeEvent(self, event):
        self.release_resources(save=True)
        event.accept()
        QApplication.instance().quit()


class CompanionWindow(CompanionControls, QWidget):
    def paintEvent(self, event):
        self.draw_character()


class OpenGLCompanionWindow(CompanionControls, QOpenGLWidget):
    renderer = 'GPU (Qt OpenGL)'

    def paintGL(self):
        self.draw_character()

    def hardware_available(self):
        # Exercise the real widget's context and framebuffer before showing it.
        frame = self.grabFramebuffer()
        if frame.isNull() or not frame.hasAlphaChannel() or not self.isValid():
            return False
        self.makeCurrent()
        try:
            address = self.context().getProcAddress(b'glGetString')
            if not address:
                return False
            get_string = ctypes.CFUNCTYPE(ctypes.c_char_p, ctypes.c_uint)(int(address))
            renderer = (get_string(0x1F01) or b'').lower()  # GL_RENDERER
            software = (b'llvmpipe', b'softpipe', b'swrast', b'software', b'swiftshader', b'lavapipe')
            return bool(renderer) and not any(name in renderer for name in software)
        finally:
            self.doneCurrent()


def create_window(args, settings):
    if args.renderer == 'auto' and QApplication.platformName() not in ('minimal', 'offscreen'):
        window = OpenGLCompanionWindow(args, settings)
        fmt = window.format()
        fmt.setAlphaBufferSize(8)
        window.setFormat(fmt)
        if window.hardware_available():
            return window
        window.release_resources(save=False)
        window.hide()
        window.deleteLater()
    return CompanionWindow(args, settings)


def export_previews(directory, entry):
    directory = Path(directory)
    directory.mkdir(parents=True, exist_ok=True)
    name = entry.id or 'sample'
    char = Character(entry.profile, seed=42)
    char.wave_start = -100
    for state in ('rest', 'typing', 'mouse', 'wave', 'sleep', 'mouth'):
        char = Character(entry.profile, seed=42)
        char.wave_start = 0 if state == 'wave' else -100
        if state == 'sleep':
            char.tune['act.sleepAfter'] = .1
        for frame in range(90):
            char.tick(frame / 60, 1 / 60, key=state == 'typing' and frame % 7 == 0,
                      pointer=(.8, -.8) if state == 'mouse' else (0, 0),
                      mouse=state == 'mouse', audio=.21 if state == 'mouth' else 0)
        if not char.frame().save(str(directory / ('%s-%s.png' % (name, state)))):
            raise OSError('プレビューを書き出せません')
    # All closing stages, including characters with curved eyelashes.
    for i in range(11):
        char.blink = i / 10
        char.mouth_shown = 0
        char.frame().save(str(directory / ('%s-blink-%02d.png' % (name, i))))
    for i in range(0, 11, 2):
        char.blink = 0
        char.mouth_shown = i / 10
        char.frame().save(str(directory / ('%s-mouth-%02d.png' % (name, i))))


def main(argv=None):
    parser = argparse.ArgumentParser(description='隅っこキャラ — Linuxデスクトップ版')
    parser.add_argument('--renderer', choices=('auto', 'cpu'), default='auto',
                        help='描画方式（自動選択 / CPU固定）')
    parser.add_argument('--demo', action='store_true', help='入力なしで動きを試す')
    parser.add_argument('--smoke-test', action='store_true', help='3秒表示して終了（設定は保存しない）')
    parser.add_argument('--character', help='characters/ のフォルダ名。空文字は見本')
    parser.add_argument('--settings-dir', type=Path, help='Linux設定の保存先（通常 bin/linux/）')
    parser.add_argument('--preview-dir', type=Path, help='検証用PNGを書き出す')
    parser.add_argument('--diagnose', action='store_true', help='入力機器の利用状況を表示して終了')
    args = parser.parse_args(argv)
    if args.diagnose:
        inputs = InputSignals()
        print(inputs.status)
        inputs.close()
        return 0
    if not os.environ.get('DISPLAY') and not os.environ.get('WAYLAND_DISPLAY') and os.environ.get('QT_QPA_PLATFORM') not in ('offscreen', 'minimal'):
        print('デスクトップ画面が見つかりません。GUIセッションから Start.sh を起動してください。', file=sys.stderr)
        return 1
    QApplication.setAttribute(Qt.AA_EnableHighDpiScaling)
    app = QApplication([sys.argv[0]])
    app.setApplicationName('SumikkoCompanion')
    app.setQuitOnLastWindowClosed(False)
    scratch = tempfile.TemporaryDirectory(prefix='sumikko-smoke-') if args.smoke_test and args.settings_dir is None else None
    settings_dir = args.settings_dir or (Path(scratch.name) if scratch else ROOT / 'bin/linux')
    instance = None
    window = None
    try:
        instance = SingleInstance(settings_dir)
        if not instance.primary:
            restored = instance.show_existing()
            print('起動中のキャラを表示しました。' if restored else '起動中です。少し待ってから再実行してください。')
            return 1 if args.smoke_test or not restored else 0
        settings = Settings(settings_dir)
        if settings.problem:
            print(settings.problem, file=sys.stderr)
        window = create_window(args, settings)
        instance.listen(window.show_companion)
        window.show()
        print('描画: ' + window.renderer, flush=True)
        print(window.inputs.status, flush=True)
        if args.preview_dir:
            entry = next(e for e in window.entries if e.id == window.character_id)
            export_previews(args.preview_dir, entry)
        if args.smoke_test:
            # A timer is checked against the monotonic clock; never report a
            # successful early exit because of Qt's coarse timer rounding.
            smoke_started = time.monotonic()

            def smoke_tick():
                if time.monotonic() - smoke_started >= 3:
                    print('Smoke test passed: %.2f s' % (time.monotonic() - smoke_started), flush=True)
                    window.close()
            smoke_timer = QTimer()
            smoke_timer.timeout.connect(smoke_tick)
            smoke_timer.start(100)
        signal.signal(signal.SIGINT, lambda *_: window.close())
        signal.signal(signal.SIGTERM, lambda *_: window.close())
        return app.exec_()
    except (OSError, ValueError) as exc:
        print('起動できません: ' + str(exc), file=sys.stderr)
        return 1
    finally:
        if window and not window.cleaned:
            window.close()
        if instance:
            instance.close()
        if scratch:
            scratch.cleanup()


if __name__ == '__main__':
    raise SystemExit(main())
