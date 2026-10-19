#!/usr/bin/env python3
"""Interpret unmodified h2spec JUnit results against an explicit RFC profile."""
import argparse
import hashlib
import json
from pathlib import Path
import xml.etree.ElementTree as ET


PRIORITY_SECTION = "http2/5.3.1"
LEGACY_DEPENDENCY_CHECKS = frozenset([
    "Sends PRIORITY frame that depend on itself",
    "Sends HEADERS frame that depends on itself",
])
RFC_REFERENCE = "https://www.rfc-editor.org/rfc/rfc9113.html#section-5.3.2"
INVALID_XML_CONTROLS = bytes(i for i in range(32) if i not in [9, 10, 13])


def summarize(report_path, rfc):
    """Keep raw statuses separate from RFC-specific applicability and counts."""
    if rfc not in [7540, 9113]:
        raise ValueError("RFC must be 7540 or 9113")
    original = Path(report_path).read_bytes()
    # h2spec embeds control octets in diagnostic text. Sanitize a parsing copy only.
    sanitized = original.translate(None, INVALID_XML_CONTROLS)
    root = ET.fromstring(sanitized)
    raw = dict(total=0, passed=0, failed=0, skipped=0)
    applicable = dict(total=0, passed=0, failed=0, skipped=0)
    cases = []
    excluded = 0
    for case in root.iter("testcase"):
        section = case.get("package", "")
        name = case.get("classname", case.get("name", ""))
        status = "passed"
        if case.find("error") is not None or case.find("failure") is not None:
            status = "failed"
        elif case.find("skipped") is not None:
            status = "skipped"
        legacy = rfc == 9113 and section == PRIORITY_SECTION and name in LEGACY_DEPENDENCY_CHECKS
        raw["total"] += 1
        raw[status] += 1
        entry = dict(section=section, name=name, raw_status=status,
                     applicability="not_applicable" if legacy else "applicable")
        if legacy:
            excluded += 1
            entry["reason"] = (
                "RFC 7540 self-dependency rejection expectation; RFC 9113 does not "
                "retain that priority dependency requirement. Watson ignores valid legacy priority signals."
            )
            entry["reference"] = RFC_REFERENCE
        else:
            applicable["total"] += 1
            applicable[status] += 1
        cases.append(entry)
    if not cases:
        raise ValueError("Report contains no test cases")
    return dict(rfc=rfc, source=str(report_path), source_sha256=hashlib.sha256(original).hexdigest(),
                diagnostic_control_bytes_removed_for_parsing=len(original) - len(sanitized),
                raw=raw, applicable=applicable, not_applicable=excluded, cases=cases)


def main():
    """Exit nonzero while applicable failures remain; never rewrite input XML."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("report", type=Path)
    parser.add_argument("--rfc", type=int, choices=[7540, 9113], required=True)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    if args.output and (args.output.resolve() == args.report.resolve()
                        or (args.output.exists() and args.output.samefile(args.report))):
        parser.error("--output must not overwrite the raw input report")
    result = summarize(args.report, args.rfc)
    rendered = json.dumps(result, indent=2) + "\n"
    if args.output:
        args.output.write_text(rendered)
    else:
        print(rendered, end="")
    return 1 if result["applicable"]["failed"] else 0


if __name__ == "__main__":
    raise SystemExit(main())
