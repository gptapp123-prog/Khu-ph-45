import contextlib
import importlib.util
import io
from pathlib import Path
import shutil
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch


TOOL = Path(__file__).resolve().parents[1] / "tools/qlkp_update.py"
spec = importlib.util.spec_from_file_location("qlkp_update", TOOL)
update = importlib.util.module_from_spec(spec)
spec.loader.exec_module(update)


@unittest.skipUnless(shutil.which("openssl"), "OpenSSL is required")
class UpdateTests(unittest.TestCase):
    def test_signed_release_accepts_original_and_rejects_mismatches(self):
        with tempfile.TemporaryDirectory() as directory, contextlib.redirect_stdout(io.StringIO()):
            root = Path(directory)
            keys = root / "keys"
            password = "isolated_test_password_2026"
            with patch.object(update.getpass, "getpass", return_value=password):
                update.init(SimpleNamespace(directory=keys))
                with self.assertRaises(ValueError):
                    update.init(SimpleNamespace(directory=keys))
                exe = root / "QLKP-Test.exe"
                exe.write_bytes(b"MZ fixture only")
                manifest = root / "test.json"
                update.sign(SimpleNamespace(key=keys / "update-private.pem", package=exe, target="windows-x64",
                                            manifest=manifest, version="0.21.5"))
            self.assertIn(b"ENCRYPTED PRIVATE KEY", (keys / "update-private.pem").read_bytes())
            options = SimpleNamespace(key=keys / "update-public.pem", package=exe, target="windows-x64",
                                      manifest=manifest, installed="0.21.4", fingerprint=update.fingerprint(keys / "update-public.pem"))
            update.verify(options)
            with self.assertRaisesRegex(ValueError, "dấu vân tay"):
                update.verify(SimpleNamespace(**{**vars(options), "fingerprint": "0" * 64}))
            with self.assertRaisesRegex(ValueError, "phiên bản"):
                update.verify(SimpleNamespace(**{**vars(options), "installed": "0.21.5.0"}))
            with self.assertRaisesRegex(ValueError, "nền tảng"):
                update.verify(SimpleNamespace(**{**vars(options), "target": "android"}))
            signature = manifest.with_suffix(".json.sig")
            original = signature.read_bytes()
            signature.write_bytes(bytes([original[0] ^ 1]) + original[1:])
            with self.assertRaises(update.subprocess.CalledProcessError):
                update.verify(options)
            signature.write_bytes(original)
            exe.write_bytes(b"MZ altered bytes")
            with self.assertRaisesRegex(ValueError, "SHA-256"):
                update.verify(options)
