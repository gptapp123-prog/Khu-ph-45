import tempfile
import unittest
from pathlib import Path
from vault_prompt import request_vault_password


class VaultPromptTests(unittest.TestCase):
    def test_creation_checks_password_and_confirmation(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder)/'new.vault'
            for answers, expected in [(['short'], False),
                    (['NewPassword_2026!', 'Mismatch_2026!'], False),
                    (['NewPassword_2026!']*2, True)]:
                with self.subTest(answers_count=len(answers)):
                    entries=iter(answers)
                    if expected:
                        self.assertEqual(request_vault_password(path,lambda _:next(entries)), 'NewPassword_2026!')
                    else:
                        with self.assertRaises(ValueError):
                            request_vault_password(path,lambda _:next(entries))
                    self.assertFalse(path.exists())

    def test_existing_vault_only_unlocks_and_keeps_bytes(self):
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/'existing.vault';path.write_bytes(b'encrypted original')
            calls=[]
            self.assertEqual(request_vault_password(path,lambda label:calls.append(label) or 'Password_2026!'), 'Password_2026!')
            self.assertEqual(len(calls),1)
            self.assertEqual(path.read_bytes(),b'encrypted original')

    def test_cancel_does_not_create_vault(self):
        with tempfile.TemporaryDirectory() as folder:
            path=Path(folder)/'new.vault'
            def cancel(_): raise KeyboardInterrupt()
            with self.assertRaises(KeyboardInterrupt):request_vault_password(path,cancel)
            self.assertFalse(path.exists())
