"""Khung giao diện và bảng 31 chức năng phải liên kết với API thật."""
from pathlib import Path
from fastapi.testclient import TestClient
from server.app import app,configure_vault,SESSIONS
from server.feature_catalog import FEATURES,STATUS_LABELS,catalog

ROOT=Path(__file__).resolve().parents[1]

def test_catalog_has_exact_unique_31_ids_and_honest_states():
    data=catalog();ids=[x['id'] for x in data['features']]
    assert ids==['F-%03d'%i for i in range(1,32)]
    assert len(ids)==len(set(ids))==31
    assert sum(data['counts'].values())==31
    assert set(data['counts'])==set(STATUS_LABELS)
    assert all(x['status'] in STATUS_LABELS for x in FEATURES)
    assert any(x['status']=='planned' and x['page'] is None for x in FEATURES)

def test_every_catalog_link_points_to_a_real_ui_handler():
    script=(ROOT/'pc/web/framework.js').read_text(encoding='utf-8')
    html=(ROOT/'pc/web/index.html').read_text(encoding='utf-8')
    assert 'data-page="features"' in html and 'data-mobile-page="features"' in html
    assert 'function featurePage()' in script and 'FW_FEATURES' in script
    linked={x['page'] for x in FEATURES if x['page']}
    for page in linked:
        assert ("'"+page+"'") in script or ('function '+page+'(') in (ROOT/'pc/web/app.js').read_text(encoding='utf-8')

def test_planned_modules_are_disabled_not_fake_links():
    html=(ROOT/'pc/web/index.html').read_text(encoding='utf-8')
    assert html.count('planned-link')==3
    assert html.count('planned-link" disabled')==3
