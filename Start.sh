#!/bin/sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
PYTHON=${SUMIKKO_PYTHON:-python3}
if [ -z "${SUMIKKO_PYTHON:-}" ] && ! "$PYTHON" -c 'from PyQt5 import QtWidgets' >/dev/null 2>&1 && [ -x /usr/bin/python3 ]; then
    PYTHON=/usr/bin/python3
fi
if ! "$PYTHON" -c 'from PyQt5 import QtWidgets' >/dev/null 2>&1; then
    echo 'PyQt5 が必要です。Ubuntu / Debian: sudo apt install python3-pyqt5' >&2
    echo '別の Python を使う場合: SUMIKKO_PYTHON=/path/to/python ./Start.sh' >&2
    exit 1
fi
# GNOME Wayland does not allow clients to position/keep a top-level window above
# others. XWayland supplies these desktop-mascot features without changing login.
if [ -n "${DISPLAY:-}" ] && [ -z "${QT_QPA_PLATFORM:-}" ]; then
    export QT_QPA_PLATFORM=xcb
fi
cd "$ROOT"
exec "$PYTHON" -m linux "$@"
