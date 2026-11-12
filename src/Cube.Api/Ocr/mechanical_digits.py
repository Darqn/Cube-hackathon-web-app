#!/usr/bin/env python3
"""OCR for five-integer-wheel mechanical electricity registers.
Returns JSON on stdout; optional --debug emits diagnostics to stderr.
Requires Python, opencv-python-headless, numpy, and locally installed Tesseract.
"""
import argparse
import json
import subprocess
import sys
import tempfile
from collections import Counter
from pathlib import Path

import cv2
import numpy as np

CROP_W, CROP_H = 830, 214
CENTER_RATIOS = [89 / 830, 212 / 830, 337 / 830, 466 / 830, 591 / 830]
Y_WINDOWS = [(26, 155), (30, 165), (38, 155)]


def locate_register_candidates(image):
    """Return distinct dark register regions, including alternative contrast thresholds.

    Never infer the value from the whole photo. Reject non-horizontal or tiny areas.
    """
    height, width = image.shape[:2]
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    candidates = []
    for threshold in (35, 40, 50, 55, 65, 75):
        dark = cv2.inRange(gray, 0, threshold)
        for kernel_size in ((3, 3), (9, 3)):
            kernel = cv2.getStructuringElement(cv2.MORPH_RECT, kernel_size)
            joined = cv2.morphologyEx(dark, cv2.MORPH_CLOSE, kernel)
            contours, _ = cv2.findContours(joined, cv2.RETR_LIST, cv2.CHAIN_APPROX_SIMPLE)
            for contour in contours:
                x, y, w, h = cv2.boundingRect(contour)
                aspect = w / max(h, 1)
                if not (3.4 < aspect < 8.5 and w > 0.24 * width and h > 0.045 * height):
                    continue
                fill = cv2.contourArea(contour) / max(w * h, 1)
                if fill < 0.42:
                    continue
                # Penalize low-fill rectangles and unlikely aspect ratios.
                score = (w * h) * fill * fill / (1 + 0.3 * abs(aspect - 4.8))
                # Similar rectangles from neighboring thresholds are represented once.
                rect = (x, y, w, h)
                if any(_iou(rect, item[1]) > 0.85 for item in candidates):
                    continue
                candidates.append((score, rect))
    candidates.sort(key=lambda item: item[0], reverse=True)
    for _, (x, y, w, h) in candidates[:6]:
        # Expands dark rectangle to retain the upper numerals even when
        # the contour includes only the center of the number window.
        px = int(w * 0.023)
        x0, x1 = max(0, x - px), min(width, x + w + px)
        y0, y1 = max(0, y - int(h * 0.19)), min(height, y + h + int(h * 0.11))
        crop = image[y0:y1, x0:x1]
        if crop.size:
            yield (x0, y0, x1, y1), cv2.resize(crop, (CROP_W, CROP_H), interpolation=cv2.INTER_AREA)


def _iou(a, b):
    ax, ay, aw, ah = a
    bx, by, bw, bh = b
    x0, y0 = max(ax, bx), max(ay, by)
    x1, y1 = min(ax + aw, bx + bw), min(ay + ah, by + bh)
    area = max(0, x1 - x0) * max(0, y1 - y0)
    return area / max(1, aw * ah + bw * bh - area)


def ocr_digit(gray, center, y0, y1, tesseract):
    half = 43
    tile = gray[y0:y1, max(0, int(center - half)):min(gray.shape[1], int(center + half))]
    if tile.size == 0:
        return []
    _, bitmap = cv2.threshold(tile, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    bitmap = 255 - bitmap
    bitmap = cv2.resize(bitmap, None, fx=3, fy=3, interpolation=cv2.INTER_NEAREST)
    bitmap = cv2.copyMakeBorder(bitmap, 45, 45, 45, 45, cv2.BORDER_CONSTANT, value=255)
    with tempfile.NamedTemporaryFile(prefix='cube-digit-', suffix='.png', delete=False) as tmp:
        path = Path(tmp.name)
    try:
        if not cv2.imwrite(str(path), bitmap):
            return []
        found = []
        for mode in ('6', '8', '10', '13'):
            p = subprocess.run(
                [tesseract, str(path), 'stdout', '--psm', mode,
                 '-c', 'tessedit_char_whitelist=0123456789'],
                capture_output=True, text=True, timeout=5, check=False,
            )
            result = p.stdout.strip()
            if p.returncode == 0 and len(result) == 1 and result in '0123456789':
                found.append(result)
        return found
    finally:
        path.unlink(missing_ok=True)


def read_register(roi, tesseract):
    gray = cv2.cvtColor(roi, cv2.COLOR_BGR2GRAY)
    digits = []
    for ratio in CENTER_RATIOS:
        center = int(CROP_W * ratio)
        votes = []
        for y0, y1 in Y_WINDOWS:
            votes.extend(ocr_digit(gray, center, y0, y1, tesseract))
        if len(votes) < 2:
            return None
        top = Counter(votes).most_common(2)
        if len(top) > 1 and top[0][1] <= top[1][1]:
            return None
        digits.append(top[0][0])
    return ''.join(digits)


def recognize(path, tesseract='tesseract', debug=False):
    image = cv2.imread(str(path), cv2.IMREAD_COLOR)
    if image is None:
        if debug:
            print('Image could not be loaded', file=sys.stderr)
        return None
    if debug:
        print(f'Image dimensions: {image.shape[1]} x {image.shape[0]}', file=sys.stderr)
    found_candidates = 0
    for rect, roi in locate_register_candidates(image):
        found_candidates += 1
        value = read_register(roi, tesseract)
        if debug:
            print(f'Candidate {found_candidates}: bbox={rect}, OCR={value}', file=sys.stderr)
        if value is not None:
            return value
    if debug and found_candidates == 0:
        print('No suitable horizontal register detected', file=sys.stderr)
    return None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('image')
    parser.add_argument('--tesseract', default='tesseract')
    parser.add_argument('--debug', action='store_true')
    args = parser.parse_args()
    try:
        result = recognize(args.image, args.tesseract, args.debug)
        print(json.dumps({'value': result}, ensure_ascii=False))
    except (OSError, subprocess.TimeoutExpired) as exc:
        print(json.dumps({'value': None, 'error': type(exc).__name__}))
        raise SystemExit(1)


if __name__ == '__main__':
    main()
