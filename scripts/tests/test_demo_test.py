import importlib.util
from pathlib import Path
import unittest


spec = importlib.util.spec_from_file_location(
    "demo_test", Path(__file__).parents[1] / "demo_test.py"
)
demo = importlib.util.module_from_spec(spec)
spec.loader.exec_module(demo)


class DemoTests(unittest.TestCase):
    def test_decodes_target_diagnostics_around_json(self):
        self.assertEqual(
            demo.decode('target diagnostic\n{"success": true}\nmore diagnostics'),
            {"success": True},
        )

    def test_rejects_non_json_response(self):
        with self.assertRaisesRegex(ValueError, "did not return JSON"):
            demo.decode("Cannot connect")

    def test_uses_top_page_of_active_section_not_hidden_shell_page(self):
        hidden = {"type": "ContentPageWithBackButtonPressedOverriden", "id": "hidden"}
        shown = {"type": "ContentPageWithBackButtonPressedOverriden", "id": "shown"}
        tree = [{"type": "Shell", "children": [
            {"type": "ShellSection", "text": "New Drink", "children": [hidden]},
            {"type": "ShellSection", "text": "Settings", "children": [
                {"type": "ShellContent", "children": [hidden]}, shown,
            ]},
        ]}]
        self.assertEqual(demo.active_page(tree, "//settings/beans"), shown)

    def test_rejects_missing_active_page(self):
        with self.assertRaisesRegex(RuntimeError, "Cannot identify"):
            demo.active_page([], "//shots")

if __name__ == "__main__":
    unittest.main()
