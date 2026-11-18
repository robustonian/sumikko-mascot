"""Linux-only settings, written atomically; never read/write Windows settings."""
import json
import math
import os
from pathlib import Path
import tempfile

from .profile import clamp, mouse_range


class Settings:
    def __init__(self, directory):
        self.directory = Path(directory)
        self.path = self.directory / 'settings.json'
        self.data = {}
        self.problem = ''
        try:
            data = json.loads(self.path.read_text(encoding='utf-8'))
            if isinstance(data, dict):
                self.data = data
        except FileNotFoundError:
            pass
        except (OSError, ValueError) as exc:
            self.problem = 'Linuxの設定を読めませんでした: ' + str(exc)

    def number(self, key, default, low, high):
        value = self.data.get(key, default)
        if not isinstance(value, (int, float)) or isinstance(value, bool) or not math.isfinite(value):
            return default
        return clamp(value, low, high)

    def flag(self, key):
        return self.data.get(key) is True

    def character(self):
        value = self.data.get('character', '')
        return value if isinstance(value, str) else ''

    def position(self):
        value = self.data.get('position')
        if isinstance(value, list) and len(value) == 2 and all(
                isinstance(v, (int, float)) and math.isfinite(v) and abs(v) < 100000 for v in value):
            return [int(v) for v in value]
        return None

    def range_for(self, character, default):
        ranges = self.data.get('mouseRanges', {})
        value = ranges.get(character) if isinstance(ranges, dict) else None
        if isinstance(value, list) and len(value) == 4 and all(
                isinstance(v, (int, float)) and math.isfinite(v) for v in value):
            return mouse_range(value)
        return mouse_range(default)

    def tuning_for(self, character):
        tuning = self.data.get('tuning', {})
        value = tuning.get(character) if isinstance(tuning, dict) else None
        if isinstance(value, dict):
            return {k: v for k, v in value.items() if isinstance(v, (float, int)) and math.isfinite(v)}
        return {}

    def save(self):
        self.directory.mkdir(parents=True, exist_ok=True)
        fd, name = tempfile.mkstemp(prefix='.settings-', dir=self.directory)
        try:
            with os.fdopen(fd, 'w', encoding='utf-8') as file:
                json.dump(self.data, file, ensure_ascii=False, indent=2, allow_nan=False)
                file.write('\n')
                file.flush()
                os.fsync(file.fileno())
            os.replace(name, self.path)
        finally:
            if os.path.exists(name):
                os.unlink(name)
