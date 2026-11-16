"""Authored sections inside otherwise compatible statement runs."""

from __future__ import annotations

from collections.abc import Callable


def is_section_break(index: int, tight: Callable[[int], bool], inside: Callable[[int], bool]) -> bool:
    """Tells whether a gap separates tight sections, preserving a lone unit between two such sections too."""
    before, after = tight(index - 1), tight(index + 1)

    if before and after:
        return True

    if before:
        return inside(index + 1) and tight(index + 2)

    return after and inside(index - 1) and tight(index - 2)


def partition_sections(connected: tuple[bool, ...], blank: tuple[bool, ...]) -> tuple[frozenset[int], frozenset[int]]:
    """Splits eligible relationship edges into removable joins and protected authored sections."""

    def connects(index: int) -> bool:
        return 0 <= index < len(connected) and connected[index]

    sections = frozenset(
        index
        for index in range(len(connected))
        if connects(index)
        and blank[index]
        and is_section_break(
            index,
            lambda edge: connects(edge) and not blank[edge],
            lambda edge: connects(edge) and blank[edge],
        )
    )

    return frozenset(index for index, joins in enumerate(connected) if joins and index not in sections), sections
