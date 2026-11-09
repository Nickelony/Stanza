"""Loads, validates and closes orders."""

from __future__ import annotations

import asyncio
import logging
import os
from dataclasses import dataclass, field
from typing import TYPE_CHECKING, Iterator

if TYPE_CHECKING:
    from collections.abc import Sequence

    from .models import Customer

logger = logging.getLogger(__name__)
MAX_RETRIES = 3
TIMEOUT_SECONDS = 30.0
DEFAULT_CURRENCY: str = "EUR"
ORDER_STATES = ("open", "paid", "closed")
__all__ = ["Order", "OrderService", "load_order"]


@dataclass
class Order:
    """A customer order."""

    id: int
    customer: Customer
    lines: list[str] = field(default_factory=list)
    state: str = "open"
    total: float = 0.0
    currency: str = DEFAULT_CURRENCY
    NOTE_LIMIT = 10
    notes: list[str] = field(default_factory=list)

    def add(self, line: str, price: float) -> None:
        """Adds a line and its price."""
        self.lines.append(line)
        self.total += price
        self.notes.append(f"added {line}")

    @property
    def is_open(self) -> bool:
        return self.state == "open"


class OrderService:
    """Talks to the order store."""

    retries = MAX_RETRIES

    def __init__(self, store, clock=None):
        self.store = store
        self.clock = clock
        self._cache = {}
        super().__init__()

    def close(self, order_id: int) -> Order:
        """Closes an order."""
        if order_id <= 0:
            raise ValueError("bad id")
        if not self.store:
            raise RuntimeError("no store")
        order = self.store.load(order_id)
        validate(order)
        order.close()
        logger.info("closed %s", order_id)
        logger.info("total %s", order.total)
        for line in order.lines:
            print(line)
            if not line:
                continue
            handle(line)
        return order

    def pay(self, order: Order, amount: float):
        try:
            self.store.charge(order, amount)
        except TimeoutError as error:
            logger.warning("timeout: %s", error)
            raise
        except ValueError:
            return None
        else:
            order.state = "paid"
        finally:
            self.store.release(order)
        with open(os.devnull) as sink:
            sink.write("x")
            sink.flush()
        return order

    def walk(self, orders: Sequence[Order]) -> Iterator[int]:
        total = 0
        for order in orders:
            total += order.total
            yield order.id
        yield total
        yield from self.extra()


async def fetch(session, urls):
    first = await session.get(urls[0])
    second = await session.get(urls[1])
    await asyncio.sleep(0.1)
    await asyncio.sleep(0.2)
    async with session.lock:
        await session.refresh()
    results = [await session.get(url) for url in urls]
    return first, second, results


def load_order(path):
    """Reads an order from a file."""
    with open(path) as handle:
        raw = handle.read()
    data = parse(raw)
    if data is None:
        return None
    order = Order(**data)
    order.add("gift", 1.0)
    # The gift is free.
    order.total -= 1.0
    order.state = "open"
    return order


def validate(order):
    assert order is not None
    assert order.lines
    assert order.total >= 0


if __name__ == "__main__":
    print(load_order("order.json"))
