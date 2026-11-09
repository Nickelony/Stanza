import functools
import re
import sys


@functools.lru_cache(maxsize=None)
def tokenize(text: str):
    """
    Splits text into tokens.

    The blank line above stays, because it is in a docstring.
    """
    pattern = re.compile(r"\w+")
    return [m.group() for m in pattern.finditer(text) if m]


def classify(value):
    match value:
        case 0:
            return "zero"
        case [x, y]:
            return f"pair {x} {y}"
        case {"kind": kind}:
            print(kind)
            return kind
        case _:
            return "other"


def template(name):
    first = f"""
Dear {name},

this blank line is inside a string.
"""
    second = """
another

string
"""
    return first + second


def continuation(a, b):
    total = a + \
        b
    value = (
        total
        + 1
    )
    items = [
        1,

        2,
    ]
    return total, value, items


def comments(items):
    count = 0  # a trailing comment
    # leading comment of the loop
    for item in items:
        count += 1  # type: ignore
        # noqa leading
        process(item)
    # pragma: no cover
    return count


def comprehension(rows):
    squares = {n: n * n for n in range(10)}
    evens = [n for n in range(10) if n % 2 == 0]
    if (size := len(rows)) > 10:
        print(size)
    pick = lambda row: row[0]  # noqa: E731
    return squares, evens, pick


# fmt: off
MATRIX = [
    1, 0, 0,
    0, 1, 0,
    0, 0, 1,
]
LOOKUP = {   'a':1,
             'b':2 }
# fmt: on
SENTINEL = object()


def generator(n):
    for i in range(n):
        yield i
    yield n


def nested(flag):
    if flag:
        result = 1
        # trailing comment of the branch body
    else:
        result = 2
    return result


try:
    import json
except ImportError:
    json = None

if sys.version_info >= (3, 11):
    import tomllib
else:
    tomllib = None


def clause_comments(flag, name):
    try:
        load(name)

    # fall back to the cache
    except OSError:
        use_cache()
    # otherwise take the default

    else:
        default()
    finally:
        # trails the block
        release()

    if flag:
        first()
    # the other way
    elif name:
        second()

    # directly under the block
    else:
        third()
    return name
