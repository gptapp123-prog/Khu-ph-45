#!/usr/bin/env python3
"""Repeatable, synthetic benchmark for the PC-only Quản lý Khu phố app.

No real resident data is read. The benchmark creates an isolated temporary
KPHO2 vault, imports a near-10 MB/5,000-row CSV, seeds 500 duplicate pairs,
measures the review scan, and verifies that the encrypted vault can reopen.
"""
from __future__ import annotations

import argparse
import csv
import io
import json
import os
import platform
import sys
import tempfile
import time
from pathlib import Path

try:
    import resource
except ImportError:  # Windows does not provide the POSIX resource module.
    resource=None

ROOT=Path(__file__).resolve().parents[1]
sys.path.insert(0,str(ROOT/'pc'))

from fastapi.testclient import TestClient
from server.app import app,configure_vault
from server.crypto import Vault

VAULT_PASSWORD='Benchmark_KPHO2_2026!'
ADMIN_PASSWORD='Benchmark_Admin_2026!'


def timed(call):
    start=time.perf_counter();value=call()
    return value,round(time.perf_counter()-start,3)


def csv_fixture(rows:int,note_length:int)->bytes:
    output=io.StringIO(newline='')
    writer=csv.writer(output)
    writer.writerow(['Họ tên','Số CCCD','Ngày sinh','Tổ','Ghi chú'])
    note='Dữ liệu hiệu năng tổng hợp. '+('x'*max(0,note_length-28))
    for index in range(1,rows+1):
        writer.writerow([f'Nhân khẩu hiệu năng {index:05d}',f'079{index:09d}',
                         '01-01-1990',1+(index%15),note])
    return output.getvalue().encode('utf-8-sig')


def duplicate_rows(pairs:int):
    rows=[]
    for index in range(1,pairs+1):
        name=f'Ứng viên trùng hiệu năng {index:04d}'
        for _ in range(2):
            rows.append({'Họ tên':name,'Ngày sinh':'02-02-1992','Tổ':1+(index%15)})
    return rows


def run(rows:int,note_length:int,duplicate_pairs:int):
    if rows<1 or rows>5000:raise ValueError('rows phải nằm trong khoảng 1..5000')
    if duplicate_pairs<1 or duplicate_pairs>500:raise ValueError('duplicate-pairs phải nằm trong khoảng 1..500')
    raw=csv_fixture(rows,note_length)
    if len(raw)>10*1024*1024:raise ValueError('Tệp sinh ra vượt giới hạn 10 MB; hãy giảm note-length.')
    with tempfile.TemporaryDirectory(prefix='quanlykp-benchmark-') as directory:
        vault_path=Path(directory)/'benchmark.vault'
        configure_vault(str(vault_path),VAULT_PASSWORD)
        with TestClient(app) as client:
            setup=client.post('/api/setup',json={'username':'benchmark','full_name':'Quản trị benchmark','password':ADMIN_PASSWORD})
            if setup.status_code!=200:raise RuntimeError(setup.text)
            login=client.post('/api/login',json={'username':'benchmark','password':ADMIN_PASSWORD})
            if login.status_code!=200:raise RuntimeError(login.text)
            headers={'Authorization':'Bearer '+login.json()['token']}

            preview,preview_seconds=timed(lambda:client.post('/api/import/preview',headers=headers,
                files={'file':('benchmark-5000.csv',raw,'text/csv')}))
            if preview.status_code!=200:raise RuntimeError(preview.text)
            parsed=preview.json()
            commit,import_seconds=timed(lambda:client.post('/api/import/commit',headers=headers,json={
                'rows':parsed['rows'],'mapping':parsed['mapping'],
                'selected':['full_name','cccd','dob','team_id','note']}))
            if commit.status_code!=200:raise RuntimeError(commit.text)
            imported=commit.json()

            seeds=duplicate_rows(duplicate_pairs)
            duplicate_commit,duplicate_seed_seconds=timed(lambda:client.post('/api/import/commit',headers=headers,json={
                'rows':seeds,'mapping':{'Họ tên':'full_name','Ngày sinh':'dob','Tổ':'team_id'},
                'selected':['full_name','dob','team_id']}))
            if duplicate_commit.status_code!=200:raise RuntimeError(duplicate_commit.text)
            candidates,duplicate_scan_seconds=timed(lambda:client.get('/api/residents/duplicate-candidates',headers=headers))
            if candidates.status_code!=200:raise RuntimeError(candidates.text)
            candidate_data=candidates.json()
            listing,list_200_seconds=timed(lambda:client.get('/api/residents?limit=200',headers=headers))
            if listing.status_code!=200:raise RuntimeError(listing.text)

        reopened,reopen_seconds=timed(lambda:Vault(vault_path,VAULT_PASSWORD))
        try:
            resident_count=reopened.conn.execute('SELECT COUNT(*) FROM residents WHERE deleted_at IS NULL').fetchone()[0]
            integrity=reopened.conn.execute('PRAGMA integrity_check').fetchone()[0]
        finally:reopened.conn.close()
        return {
            'benchmark':'quan-ly-khu-pho-large-dataset-v1',
            'environment':{'python':platform.python_version(),'platform':platform.platform(),'cpu_count':os.cpu_count()},
            'input':{'csv_rows':rows,'csv_bytes':len(raw),'note_length':note_length,'duplicate_pairs':duplicate_pairs},
            'results':{
                'preview_seconds':preview_seconds,'import_seconds':import_seconds,
                'duplicate_seed_seconds':duplicate_seed_seconds,'duplicate_scan_seconds':duplicate_scan_seconds,
                'list_200_seconds':list_200_seconds,'vault_reopen_seconds':reopen_seconds,
                'encrypted_vault_bytes':vault_path.stat().st_size,'resident_count':resident_count,
                'candidate_count':candidate_data['count'],'candidate_limited':candidate_data['limited'],
                'inserted':imported['inserted'],'invalid':imported['invalid'],'integrity_check':integrity,
                'peak_rss_kib':resource.getrusage(resource.RUSAGE_SELF).ru_maxrss if resource else None,
            },
            'acceptance':{
                'all_rows_imported':imported['inserted']==rows and imported['invalid']==0,
                'duplicate_cap_reached':candidate_data['count']==duplicate_pairs,
                'resident_count_correct':resident_count==rows+(duplicate_pairs*2),
                'vault_integrity_ok':integrity=='ok',
            }
        }


def main():
    parser=argparse.ArgumentParser(description='Benchmark kho KPHO2 bằng dữ liệu tổng hợp, không dùng dữ liệu dân cư thật.')
    parser.add_argument('--rows',type=int,default=5000)
    parser.add_argument('--note-length',type=int,default=1800)
    parser.add_argument('--duplicate-pairs',type=int,default=500)
    parser.add_argument('--output',type=Path)
    args=parser.parse_args()
    result=run(args.rows,args.note_length,args.duplicate_pairs)
    rendered=json.dumps(result,ensure_ascii=False,indent=2)
    if args.output:
        args.output.parent.mkdir(parents=True,exist_ok=True)
        args.output.write_text(rendered+'\n',encoding='utf-8')
    print(rendered)
    if not all(result['acceptance'].values()):raise SystemExit(1)


if __name__=='__main__':main()
