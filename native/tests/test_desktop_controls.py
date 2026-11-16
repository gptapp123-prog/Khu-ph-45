from pathlib import Path
import re
import unittest
from xml.etree import ElementTree


DESKTOP = Path(__file__).resolve().parents[1] / "src/QLKP.Desktop"


class DesktopControlTests(unittest.TestCase):
    def test_every_xaml_button_has_a_routed_handler_or_built_in_action(self):
        for xaml in sorted(DESKTOP.rglob("*.xaml")):
            code_path = xaml.with_suffix(".xaml.cs")
            source = code_path.read_text(encoding="utf-8-sig") if code_path.exists() else ""
            for node in ElementTree.parse(xaml).iter():
                if node.tag.rsplit("}", 1)[-1] != "Button":
                    continue
                with self.subTest(view=xaml.name, button=node.attrib):
                    click = node.get("Click")
                    if click:
                        self.assertRegex(source, r"\bvoid\s+" + re.escape(click) + r"\s*\(")
                    else:
                        self.assertTrue(node.get("Command") or node.get("IsCancel") == "True")

    def test_account_creation_confirms_password_before_writing(self):
        source = (DESKTOP / "Views/AccountsView.xaml.cs").read_text(encoding="utf-8")
        self.assertLess(source.index("Password.Password!=ConfirmNewPassword.Password"),
                        source.index("DataStore.CreateAccount("))
        smoke = (DESKTOP / "Core/NativeUiSelfTest.cs").read_text(encoding="utf-8")
        for name in ("UI_SETUP_ENTER_AND_AUTO_LOGIN", "UI_CREATE_ADMIN",
                     "UI_CREATE_DEFAULT_ORDINARY_ACCOUNT", "UI_DISPLAY_MODE_2D_3D"):
            self.assertIn('BeginStep("' + name + '")', smoke)
