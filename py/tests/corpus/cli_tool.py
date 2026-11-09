#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Command-line entry point."""
import argparse
import sys
from pathlib import Path

import order_service


def build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(description="Close orders.")
    parser.add_argument("path", type=Path)
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument("--verbose", action="store_true")
    return parser


def main(argv=None) -> int:
    args = build_parser().parse_args(argv)
    if not args.path.exists():
        print(f"missing: {args.path}", file=sys.stderr)
        return 2
    order = order_service.load_order(args.path)
    if args.dry_run:
        print(order)
        return 0
    service = order_service.OrderService(None)
    result = service.close(order.id)
    print(result)
    return 0


def helper(): return 1


def other(): ...


x = 1; y = 2


if __name__ == "__main__":
    sys.exit(main())
