#!/usr/bin/env python3
"""Sign and verify QLKP release manifests offline with OpenSSL 3 and Ed25519."""
import argparse
import getpass
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import tempfile

FORMAT = "QLKP-UPDATE-1"
TARGETS = {"windows-x64": ".exe", "android": ".apk", "android-test": ".apk"}
LIMIT = 16384


def openssl(*args, password=None):
    command = ["openssl", *map(str, args)]
    if password is not None:
        command += ["-passin", "stdin"]
    return subprocess.run(command, check=True, capture_output=True,
                          input=None if password is None else (password + "\n").encode()).stdout


def version(value):
    if not re.fullmatch(r"(?:0|[1-9][0-9]{0,8})(?:\.(?:0|[1-9][0-9]{0,8})){2,3}", value):
        raise ValueError("Phiên bản cần có dạng 0.21.5 hoặc 0.21.5.0.")
    parts = tuple(map(int, value.split(".")))
    return parts + (0,) * (4 - len(parts))


def package_details(path, target, release):
    version(release)
    if path.suffix.lower() != TARGETS[target] or not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]{0,127}", path.name):
        raise ValueError("Tên bộ cài hoặc phần mở rộng không phù hợp nền tảng.")
    with path.open("rb") as file:
        magic = file.read(4)
        if (target == "windows-x64" and not magic.startswith(b"MZ")) or (target != "windows-x64" and magic != b"PK\x03\x04"):
            raise ValueError("Định dạng bộ cài không hợp lệ.")
        file.seek(0)
        size = 0
        digest = hashlib.sha256()
        for chunk in iter(lambda: file.read(1024 * 1024), b""):
            digest.update(chunk)
            size += len(chunk)
    return {"format": FORMAT, "target": target, "version": release, "file": path.name,
            "size": size, "sha256": digest.hexdigest()}


def fingerprint(key):
    return hashlib.sha256(openssl("pkey", "-pubin", "-in", key, "-outform", "DER")).hexdigest()


def init(args):
    if args.directory.exists():
        raise ValueError("Chọn thư mục mới cho cặp khóa. Thư mục hiện có sẽ không bị ghi đè.")
    password = getpass.getpass("Mật khẩu bảo vệ khóa riêng (tối thiểu 16 ký tự): ")
    if len(password) < 16 or password != getpass.getpass("Nhập lại mật khẩu: "):
        raise ValueError("Mật khẩu quá ngắn hoặc hai lần nhập chưa trùng khớp.")
    args.directory.mkdir(mode=0o700)
    private = args.directory / "update-private.pem"
    public = args.directory / "update-public.pem"
    try:
        with private.open("xb") as file:
            if os.name != "nt":
                os.fchmod(file.fileno(), 0o600)
            result = subprocess.run(["openssl", "genpkey", "-algorithm", "ED25519", "-aes-256-cbc",
                                     "-pass", "stdin"], input=(password + "\n").encode(),
                                    capture_output=True, check=True)
            file.write(result.stdout)
        with public.open("xb") as file:
            file.write(openssl("pkey", "-in", private, "-pubout", password=password))
        key_id = fingerprint(public)
    except BaseException:
        private.unlink(missing_ok=True)
        public.unlink(missing_ok=True)
        args.directory.rmdir()
        raise
    print("SHA-256 khóa công khai:", key_id)


def sign(args):
    manifest = package_details(args.package, args.target, args.version)
    raw = (json.dumps(manifest, sort_keys=True, separators=(",", ":")) + "\n").encode()
    signature = args.manifest.with_suffix(args.manifest.suffix + ".sig")
    if args.manifest.exists() or signature.exists():
        raise ValueError("Chọn tên tệp manifest mới.")
    password = getpass.getpass("Mật khẩu khóa riêng: ")
    with tempfile.TemporaryDirectory() as root:
        data = Path(root) / "manifest"
        data.write_bytes(raw)
        signed = openssl("pkeyutl", "-sign", "-rawin", "-in", data, "-inkey", args.key, password=password)
    if len(signed) != 64:
        raise ValueError("Khóa ký phải là Ed25519.")
    with args.manifest.open("xb") as file:
        file.write(raw)
    try:
        with signature.open("xb") as file:
            file.write(signed)
    except BaseException:
        args.manifest.unlink()
        raise
    print("Đã ký:", args.manifest.name, signature.name)


def verify(args):
    trust = args.fingerprint.lower()
    if not re.fullmatch(r"[a-f0-9]{64}", trust):
        raise ValueError("Khóa công khai không khớp dấu vân tay tin cậy.")
    with args.manifest.open("rb") as file:
        raw = file.read(LIMIT + 1)
    with args.manifest.with_suffix(args.manifest.suffix + ".sig").open("rb") as file:
        sig = file.read(65)
    if len(raw) > LIMIT or len(sig) != 64:
        raise ValueError("Manifest hoặc chữ ký sai kích thước.")
    with tempfile.TemporaryDirectory() as root:
        data, signature = Path(root) / "manifest", Path(root) / "signature"
        key = Path(root) / "trusted-public.pem"
        with args.key.open("rb") as file:
            public = file.read(LIMIT + 1)
        if len(public) > LIMIT:
            raise ValueError("Tệp khóa công khai quá lớn.")
        key.write_bytes(public)
        if fingerprint(key) != trust:
            raise ValueError("Khóa công khai không khớp dấu vân tay tin cậy.")
        data.write_bytes(raw)
        signature.write_bytes(sig)
        openssl("pkeyutl", "-verify", "-rawin", "-in", data, "-pubin", "-inkey", key, "-sigfile", signature)
    manifest = json.loads(raw)
    if not isinstance(manifest, dict) or not isinstance(manifest.get("version"), str):
        raise ValueError("Cấu trúc manifest không hợp lệ.")
    release = manifest["version"]
    if version(release) <= version(args.installed):
        raise ValueError("Gói có phiên bản bằng hoặc thấp hơn bản đang cài.")
    if manifest != package_details(args.package, args.target, release):
        raise ValueError("Tên, nền tảng, kích thước hoặc SHA-256 bộ cài không khớp.")
    print("Đã xác minh gói", release, "cho", args.target)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    sub = parser.add_subparsers(dest="action", required=True)
    create = sub.add_parser("init")
    create.add_argument("directory", type=Path)
    for action in ("sign", "verify"):
        command = sub.add_parser(action)
        command.add_argument("--target", choices=TARGETS, required=True)
        for field in ("key", "manifest", "package"):
            command.add_argument("--" + field, type=Path, required=True)
        if action == "sign":
            command.add_argument("--version", required=True)
        else:
            command.add_argument("--installed", required=True)
            command.add_argument("--fingerprint", required=True)
    args = parser.parse_args()
    try:
        {"init": init, "sign": sign, "verify": verify}[args.action](args)
    except subprocess.CalledProcessError:
        print("OpenSSL từ chối thao tác. Kiểm tra khóa, mật khẩu hoặc chữ ký.", file=sys.stderr)
        return 2
    except (OSError, ValueError) as exc:
        print("Thao tác thất bại:", exc, file=sys.stderr)
        return 2
    return 0


if __name__ == "__main__":
    sys.exit(main())
