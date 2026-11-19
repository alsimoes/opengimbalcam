"""Generates src/OpenGimbalCam.Studio/Styles/Icons.axaml from Bootstrap Icons 1.13.2.

Standard library only. Run from the repository root:  python -I tools/ui/gen_icons.py
"""
import re
import sys
import urllib.request
from pathlib import Path

VERSION = "1.13.2"
URL = "https://cdn.jsdelivr.net/npm/bootstrap-icons@" + VERSION + "/icons/{}.svg"
OUT = Path(__file__).resolve().parents[2] / "src" / "OpenGimbalCam.Studio" / "Styles" / "Icons.axaml"

# resource key -> bootstrap icon name
ICONS = {
    "IconCameraVideo": "camera-video",
    "IconCameraVideoFill": "camera-video-fill",
    "IconCameraVideoOff": "camera-video-off",
    "IconArrowClockwise": "arrow-clockwise",
    "IconInfoCircle": "info-circle",
    "IconKeyboard": "keyboard",
    "IconBroadcast": "broadcast",
    "IconActivity": "activity",
    "IconSliders": "sliders",
    "IconChevronUp": "chevron-up",
    "IconChevronDown": "chevron-down",
    "IconChevronLeft": "chevron-left",
    "IconChevronRight": "chevron-right",
    "IconBullseye": "bullseye",
    "IconArrowsMove": "arrows-move",
    "IconSpeedometer": "speedometer2",
    "IconZoomIn": "zoom-in",
    "IconZoomOut": "zoom-out",
    "IconAspectRatio": "aspect-ratio",
    "IconSymmetryVertical": "symmetry-vertical",
    "IconBookmarkStar": "bookmark-star",
    "IconCamera": "camera",
    "IconRecordFill": "record-fill",
    "IconStopFill": "stop-fill",
    "IconPlayFill": "play-fill",
    "IconGrid3x3": "grid-3x3",
    "IconFolder2Open": "folder2-open",
    "IconFullscreen": "fullscreen",
    "IconFullscreenExit": "fullscreen-exit",
    "IconArrowCounterclockwise": "arrow-counterclockwise",
    "IconStopwatch": "stopwatch",
    "IconGraphUpArrow": "graph-up-arrow",
    "IconBrightnessAltHigh": "brightness-alt-high",
    "IconLightbulb": "lightbulb",
    "IconVignette": "vignette",
    "IconThermometerHalf": "thermometer-half",
    "IconPalette": "palette",
    "IconDropletHalf": "droplet-half",
    "IconBrightnessHigh": "brightness-high",
    "IconCircleHalf": "circle-half",
    "IconGraphUp": "graph-up",
    "IconTriangleHalf": "triangle-half",
    "IconCrosshair": "crosshair",
    "IconArrowRepeat": "arrow-repeat",
    "IconClipboard": "clipboard",
    "IconTrash": "trash",
    "IconCheckCircleFill": "check-circle-fill",
    "IconExclamationTriangleFill": "exclamation-triangle-fill",
}

NUM = r"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?"
ARITY = {"M": 2, "L": 2, "T": 2, "H": 1, "V": 1, "C": 6, "S": 4, "Q": 4, "A": 7, "Z": 0}


def normalize(d: str, name: str) -> str:
    """Rewrites path data with explicit separators; arc flags are split into single characters."""
    out = []
    i, n = 0, len(d)
    cmd = None
    while i < n:
        c = d[i]
        if c in " ,\t\r\n":
            i += 1
            continue
        if c.isalpha():
            if c.upper() not in ARITY:
                sys.exit(f"{name}: unknown path command {c!r}")
            cmd = c
            i += 1
            if ARITY[c.upper()] == 0:
                out.append(c)
                cmd = None
                continue
        elif cmd is None:
            sys.exit(f"{name}: path data without a command near {d[i:i+10]!r}")
        arity = ARITY[cmd.upper()]
        params = []
        while len(params) < arity:
            while i < n and d[i] in " ,\t\r\n":
                i += 1
            if cmd.upper() == "A" and len(params) in (3, 4):
                if i >= n or d[i] not in "01":
                    sys.exit(f"{name}: bad arc flag near {d[i:i+10]!r}")
                params.append(d[i])
                i += 1
                continue
            m = re.compile(NUM).match(d, i)
            if not m:
                sys.exit(f"{name}: bad number near {d[i:i+10]!r}")
            params.append(m.group(0))
            i = m.end()
        out.append(cmd + " " + " ".join(params))
        if cmd == "M":
            cmd = "L"
        elif cmd == "m":
            cmd = "l"
    return " ".join(out)


def fetch(icon: str) -> str:
    with urllib.request.urlopen(URL.format(icon), timeout=30) as r:
        return r.read().decode("utf-8")


def parse(icon: str, svg: str):
    if 'viewBox="0 0 16 16"' not in svg:
        sys.exit(f"{icon}: viewBox is not 0 0 16 16")
    body = re.sub(r"<svg[^>]*>|</svg>", "", svg).strip()
    tags = re.findall(r"<(\w+)\b([^>]*?)/?>", body)
    paths = []
    for tag, attrs in tags:
        if tag != "path":
            sys.exit(f"{icon}: unsupported element <{tag}>")
        if "transform" in attrs or "clip-path" in attrs:
            sys.exit(f"{icon}: transform/clip-path not supported")
        d = re.search(r'\sd="([^"]+)"', " " + attrs)
        if not d:
            sys.exit(f"{icon}: path without d")
        rule = "F0" if 'fill-rule="evenodd"' in attrs else "F1"
        paths.append(f"{rule} {normalize(d.group(1), icon)}")
    if not paths:
        sys.exit(f"{icon}: no paths")
    return paths


def geometry(key: str, paths: list[str]) -> str:
    if len(paths) == 1:
        return f'  <StreamGeometry x:Key="{key}">{paths[0]}</StreamGeometry>'
    # left-nested union: ((p0 U p1) U p2) ...
    def sg(p):
        return f"<StreamGeometry>{p}</StreamGeometry>"

    inner = sg(paths[0])
    for p in paths[1:-1]:
        inner = (
            '<CombinedGeometry GeometryCombineMode="Union">'
            f"<CombinedGeometry.Geometry1>{inner}</CombinedGeometry.Geometry1>"
            f"<CombinedGeometry.Geometry2>{sg(p)}</CombinedGeometry.Geometry2>"
            "</CombinedGeometry>"
        )
    return (
        f'  <CombinedGeometry x:Key="{key}" GeometryCombineMode="Union">\n'
        f"    <CombinedGeometry.Geometry1>{inner}</CombinedGeometry.Geometry1>\n"
        f"    <CombinedGeometry.Geometry2>{sg(paths[-1])}</CombinedGeometry.Geometry2>\n"
        "  </CombinedGeometry>"
    )


def main():
    parts = []
    for key, icon in ICONS.items():
        parts.append(f"  <!-- bi-{icon} -->\n" + geometry(key, parse(icon, fetch(icon))))
    header = (
        '<ResourceDictionary xmlns="https://github.com/avaloniaui"\n'
        '                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">\n'
        "  <!-- Generated by tools/ui/gen_icons.py from Bootstrap Icons v" + VERSION + " (MIT License, "
        "Copyright (c) 2019-2024 The Bootstrap Authors). Do not edit by hand. -->\n"
    )
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(header + "\n".join(parts) + "\n</ResourceDictionary>\n", encoding="utf-8", newline="\n")
    print(f"wrote {OUT} ({len(ICONS)} icons)")


if __name__ == "__main__":
    main()
