"""Draws shapes onto a canvas, through calls of every shape the call rules name."""

from __future__ import annotations

import os
import textwrap
from dataclasses import dataclass


def draw(width, height, smooth, path, *, scale=1.0):
    """Draws an image of a size."""
    return (width, height, smooth, path, scale)


def clamp(value, low, high, /, step=1):
    """Clamps a value between two bounds."""
    return max(low, min(value, high)) // step


def secret(public, __hidden, tail, other):
    """Takes a parameter that cannot be named."""
    return public, __hidden, tail, other


def retry(times, delay, backoff, jitter):
    """Builds a decorator that retries a function."""

    def wrap(function):
        return function

    return wrap


@dataclass
class Point:
    """A point in space."""

    x: int
    y: int
    z: int
    w: int


class Canvas:
    """A canvas that draws shapes."""

    def __init__(self, width, height, depth, background):
        self.size = draw(width, height, False, "canvas.png")
        self.depth = depth
        self.background = background

    def _line(self, start, end, thickness, color):
        return start, end, thickness, color

    def frame(self, color):
        width = self.size[0]
        height = self.size[1]
        self._line(width, height, 2, color)
        Canvas._line(self, 0, 0, 1, color)
        return self._line(
            0,
            height,  # the bottom edge
            1,
            None,
        )

    @retry(3, 0.5, 2.0, None)
    def save(self, path, overwrite, backup=None):
        os.makedirs(os.path.dirname(path), 0o755, True)
        return draw(640, 480, True, path, scale=2.0)


def render(points=Point(1, 2, 3, 4)):
    """Renders points through a nested call and a lambda."""
    canvas = Canvas(640, 480, 24, "white")
    origin = Point(clamp(0, -1, 1), 2, 3, 4)
    canvas.save("out.png", True, None)
    shade = lambda level: draw(level, level, True, "shade.png")
    label = f"{draw(1, 2, True, 'label.png')}"
    width, height = 3, 4
    draw(width, height, True, "matched.png")
    secret(1, 2, 3, 4)
    print(textwrap.indent("text", "  ", None))
    return canvas, origin, shade, label, points
