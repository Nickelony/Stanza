"""A panel that wires its signals and reads events with every clause keyword."""

from __future__ import annotations

import logging

from toolkit import Panel, button, order_closed, order_saved

logger = logging.getLogger(__name__)


class OrdersPanel(Panel):
    """A panel of orders."""

    title: str = "Orders"
    width: int = 640
    height: int = 480
    smooth: bool = True
    label = "orders"
    kind = "panel"
    limit = 10

    def __init__(self, parent, *args, **kwargs):
        super().__init__(parent, *args, **kwargs)
        self.parent = parent
        self.args = args
        self.kwargs = kwargs
        self.orders: dict[str, int] = {}
        self.names: list[str] = []
        self.count: int = 0
        self.total: int = 0
        self.shown: bool = False
        order_saved.connect(self.on_saved)
        order_closed.connect(self.on_closed)
        button.clicked.connect(self.on_click)
        button.pressed.connect(self.on_press)
        self.refresh()

    def close(self):
        order_saved.disconnect(self.on_saved)
        order_closed.disconnect(self.on_closed)
        self.shown = False
        self.count = 0
        super().close()

    def on_click(self, event):
        match event:
            case {"kind": "double"}:
                self.open_all()
            case {"kind": "single", "target": target}:
                target.select()
                target.focus()
            case {"kind": "drag", "target": target}:
                self.drag(target)
                self.refresh()
            case _:
                logger.debug("ignored %s", event)

    def on_press(self, event):
        match event.kind:
            case "left":
                self.left()
            case "right":
                self.right()
            case "middle":
                self.middle()

    def load(self, name):
        try:
            order = self.orders[name]
            total = order.total
            count = order.count
        except KeyError:
            logger.warning("no order %s", name)
            order = None
        except (TypeError, ValueError) as error:
            logger.error("bad order %s", error)
            raise
        else:
            self.shown = True
            self.total += total
        finally:
            self.refresh()

        if order is None:
            self.count = 0
        elif order.closed:
            self.count = 1
        elif order.paid:
            self.count = 2
        else:
            self.count = 3

        for item in self.names:
            if item == name:
                break
        else:
            logger.info("not found")

        while self.shown:
            self.refresh()
            self.shown = False
        else:
            self.count = 0

        return order
