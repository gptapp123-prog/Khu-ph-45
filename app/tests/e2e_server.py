"""Ephemeral E2E server using generated data only."""
import atexit
import shutil
import tempfile
from pathlib import Path
import uvicorn
from server.app import app,configure_vault

ROOT=Path(tempfile.mkdtemp(prefix="quanlykp-e2e-"))
atexit.register(lambda: shutil.rmtree(ROOT,ignore_errors=True))
configure_vault(str(ROOT/"e2e.vault"),"E2E_Vault_Passphrase_2026!")
uvicorn.run(app,host="127.0.0.1",port=8765,access_log=False,server_header=False)
