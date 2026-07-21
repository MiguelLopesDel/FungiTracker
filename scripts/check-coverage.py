#!/usr/bin/env python3
"""Merge Cobertura reports and fail when line coverage drops below a floor.

Both test projects emit their own report, and a line touched by either one is
covered, so the reports are merged by (file, line) before the ratio is taken.
Summing the files instead would count shared lines twice.
"""

import argparse
import glob
import sys
import xml.etree.ElementTree as ElementTree
from collections import defaultdict


def merge(report_paths):
    hits = defaultdict(int)
    for path in report_paths:
        root = ElementTree.parse(path).getroot()
        for class_node in root.iter("class"):
            filename = class_node.get("filename")
            for line in class_node.iter("line"):
                key = (filename, line.get("number"))
                hits[key] = max(hits[key], int(line.get("hits", "0")))
    return hits


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--results", default="TestResults")
    parser.add_argument("--threshold", type=float, default=65.0)
    args = parser.parse_args()

    pattern = f"{args.results}/**/coverage.cobertura.xml"
    reports = glob.glob(pattern, recursive=True)
    if not reports:
        print(f"No coverage report found under '{pattern}'.", file=sys.stderr)
        return 1

    hits = merge(reports)
    total = len(hits)
    if total == 0:
        print("Coverage reports contain no lines.", file=sys.stderr)
        return 1

    covered = sum(1 for value in hits.values() if value > 0)
    percentage = 100.0 * covered / total

    print(f"Merged {len(reports)} report(s)")
    print(f"Line coverage: {covered}/{total} = {percentage:.1f}%")
    print(f"Required floor: {args.threshold:.1f}%")

    if percentage + 1e-9 < args.threshold:
        print("FAIL: coverage is below the floor.", file=sys.stderr)
        return 1

    print("OK")
    return 0


if __name__ == "__main__":
    sys.exit(main())
