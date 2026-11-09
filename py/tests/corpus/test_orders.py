import unittest
from unittest import mock

import pytest

from order_service import Order, OrderService, load_order


@pytest.fixture
def service():
    store = mock.Mock()
    store.load.return_value = Order(1, None)
    return OrderService(store)


def test_close_marks_the_order_closed(service):
    order = service.close(1)
    assert order.state == "closed"
    assert order.is_open is False
    service.store.release.assert_called_once()
    service.store.load.assert_called_with(1)


def test_close_rejects_a_bad_id(service):
    with pytest.raises(ValueError):
        service.close(0)
    with pytest.raises(RuntimeError, match="no store"):
        OrderService(None).close(1)
    assert service.store.load.call_count == 0


@pytest.mark.parametrize("amount", [1, 2, 3])
def test_pay(service, amount):
    order = Order(2, None)
    result = service.pay(order, amount)
    assert result is order
    assert order.state == "paid"


def test_add_line():
    order = Order(3, None)
    order.add("book", 5.0)
    order.add("pen", 1.5)
    assert order.total == 6.5


class OrderTests(unittest.TestCase):
    def setUp(self):
        self.order = Order(4, None)
        self.order.add("book", 2.0)

    def test_total(self):
        self.assertEqual(self.order.total, 2.0)
        self.assertTrue(self.order.is_open)
        self.assertIn("book", self.order.lines)

    def test_missing(self):
        with self.assertRaises(KeyError):
            load_order("missing.json")
        self.assertIsNone(None)
