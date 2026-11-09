"""Domain models."""

import enum
from dataclasses import dataclass
from typing import ClassVar, Protocol, overload


class Color(enum.Enum):
    RED = 1
    GREEN = 2
    BLUE = 3

    @classmethod
    def parse(cls, text):
        return cls[text.upper()]


class Shape(Protocol):
    sides: int
    name: str

    def area(self) -> float: ...

    def perimeter(self) -> float: ...


@dataclass(frozen=True)
class Customer:
    """A customer of the shop."""

    name: str
    email: str
    age: int = 0
    tags: ClassVar[list] = []
    LIMIT = 5
    EXTRA = 6

    def greeting(self):
        greeting = f"Hello {self.name}"
        return greeting

    def __post_init__(self):
        if self.age < 0:
            raise ValueError("age")


class Registry:
    items = {}
    default = None
    count = 0

    @overload
    def get(self, key: int) -> int: ...
    @overload
    def get(self, key: str) -> str: ...
    def get(self, key):
        return self.items.get(key, self.default)

    class Meta:
        ordering = ("name",)

    def clear(self):
        self.items.clear()
        self.count = 0
        self.default = None


def counter():
    total = 0

    def bump():
        nonlocal total
        total += 1
        return total

    return bump


def configure():
    global SETTINGS
    SETTINGS = {"a": 1}
    result = SETTINGS
    return result
