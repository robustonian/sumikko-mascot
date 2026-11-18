#!/bin/sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
cd "$ROOT"
export QT_QPA_PLATFORM=offscreen
PYTHON=${SUMIKKO_PYTHON:-python3}
if [ -z "${SUMIKKO_PYTHON:-}" ] && ! "$PYTHON" -c 'from PyQt5 import QtWidgets' >/dev/null 2>&1 && [ -x /usr/bin/python3 ]; then
    PYTHON=/usr/bin/python3
fi
"$PYTHON" -m unittest discover -s linux/tests -v
# Every character, including user-added ones; actual event loop and isolated settings.
"$PYTHON" - <<'PY'
from pathlib import Path
import subprocess
import sys
import tempfile
from linux.profile import catalog

with tempfile.TemporaryDirectory(prefix='sumikko-verify-') as directory:
    for entry in catalog():
        subprocess.run([
            sys.executable, '-m', 'linux', '--smoke-test', '--demo',
            '--character=' + entry.id, '--settings-dir=' + directory,
            '--preview-dir=' + str(Path('preview/linux').resolve()),
        ], check=True)
PY
