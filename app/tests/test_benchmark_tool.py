"""Smoke checks for the repeatable large-dataset benchmark tool."""
from tools.benchmark_large_dataset import csv_fixture,duplicate_rows,run


def test_benchmark_fixture_is_bounded_and_contains_no_real_data():
    raw=csv_fixture(5000,1800)
    assert 8*1024*1024<len(raw)<10*1024*1024
    assert b'079000000001' in raw
    assert len(duplicate_rows(500))==1000


def test_benchmark_smoke_verifies_import_duplicate_scan_and_reopen():
    result=run(rows=20,note_length=80,duplicate_pairs=3)
    assert all(result['acceptance'].values())
    assert result['results']['candidate_count']==3
    assert result['results']['resident_count']==26
