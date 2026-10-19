"""Regression checks for RFC-specific interpretation of raw h2spec reports."""
import hashlib
from pathlib import Path
import sys
import subprocess
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[3] / "tools"))
from summarize_h2spec import summarize


class H2SpecSummaryTests(unittest.TestCase):
    """Only the two exact deprecated dependency checks can be classified N/A."""

    def report(self, contents, rfc=9113):
        """Build a diagnostic fixture and verify the source bytes remain intact."""
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "raw.xml"
            original = b"<testsuites><testsuite>" + contents + b"</testsuite></testsuites>"
            path.write_bytes(original)
            result = summarize(path, rfc)
            self.assertEqual(original, path.read_bytes())
            self.assertEqual(hashlib.sha256(original).hexdigest(), result["source_sha256"])
            return result

    def test_9113_separates_legacy_failures_without_hiding_real_failure(self):
        result = self.report(
            b'<testcase package="http2/5.3.1" classname="Sends PRIORITY frame that depend on itself"><error>rejected</error></testcase>'
            b'<testcase package="http2/5.3.1" classname="Sends HEADERS frame that depends on itself"><failure/></testcase>'
            b'<testcase package="http2/6.1" classname="Invalid DATA"><error/></testcase>'
            b'<testcase package="generic/1" classname="Handshake"/>')
        self.assertEqual(dict(total=4, passed=1, failed=3, skipped=0), result["raw"])
        self.assertEqual(dict(total=2, passed=1, failed=1, skipped=0), result["applicable"])
        self.assertEqual(2, result["not_applicable"])
        self.assertEqual("failed", result["cases"][0]["raw_status"])
        self.assertIn("rfc9113", result["cases"][0]["reference"])

    def test_7540_retains_dependency_rejection_expectations(self):
        result = self.report(
            b'<testcase package="http2/5.3.1" classname="Sends PRIORITY frame that depend on itself"><error/></testcase>'
            b'<testcase package="http2/5.3.1" classname="Sends HEADERS frame that depends on itself"><error/></testcase>', 7540)
        self.assertEqual(0, result["not_applicable"])
        self.assertEqual(result["raw"], result["applicable"])
        self.assertEqual(2, result["applicable"]["failed"])

    def test_similar_names_and_other_sections_remain_applicable(self):
        result = self.report(
            b'<testcase package="http2/6.3" classname="Sends PRIORITY frame that depend on itself"><error/></testcase>'
            b'<testcase package="http2/5.3.1" classname="Sends malformed PRIORITY frame that depend on itself"><error/></testcase>')
        self.assertEqual(0, result["not_applicable"])
        self.assertEqual(2, result["applicable"]["failed"])

    def test_diagnostic_control_octets_do_not_change_original_file(self):
        result = self.report(b'<testcase package="generic/1" classname="Diagnostic"><error>wire:\x00</error></testcase>')
        self.assertEqual(1, result["diagnostic_control_bytes_removed_for_parsing"])
        self.assertEqual(1, result["applicable"]["failed"])

    def test_legacy_passes_are_not_counted_as_applicable_passes(self):
        result = self.report(b'<testcase package="http2/5.3.1" classname="Sends PRIORITY frame that depend on itself"/>')
        self.assertEqual(1, result["raw"]["passed"])
        self.assertEqual(0, result["applicable"]["passed"])
        self.assertEqual(1, result["not_applicable"])

    def test_skipped_cases_remain_visible(self):
        result = self.report(b'<testcase package="generic/1" classname="Unavailable"><skipped/></testcase>')
        self.assertEqual(1, result["raw"]["skipped"])
        self.assertEqual(1, result["applicable"]["skipped"])

    def test_cli_refuses_to_overwrite_the_input_report(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / "raw.xml"
            original = b'<testsuite><testcase package="generic/1" classname="Handshake"/></testsuite>'
            path.write_bytes(original)
            script = Path(__file__).resolve().parents[3] / "tools" / "summarize_h2spec.py"
            run = subprocess.run([sys.executable, str(script), str(path), "--rfc", "9113", "--output", str(path)],
                                 capture_output=True, text=True)
            self.assertEqual(2, run.returncode)
            self.assertIn("must not overwrite", run.stderr)
            self.assertEqual(original, path.read_bytes())

    def test_empty_reports_are_rejected(self):
        with self.assertRaises(ValueError):
            self.report(b"")

    def test_unknown_rfc_is_rejected(self):
        with self.assertRaises(ValueError):
            self.report(b'<testcase classname="Handshake"/>', 9999)


if __name__ == "__main__":
    unittest.main()
