from server.parsers import automap


def test_resident_template_recognizes_exact_headers():
    mapped = automap(['Họ và tên', 'Số thẻ BHYT', 'CCCD', 'Địa chỉ', 'Ngày sinh'])
    assert mapped == {'Họ và tên': 'full_name', 'Số thẻ BHYT': 'health_insurance',
                      'CCCD': 'cccd', 'Địa chỉ': 'permanent_address', 'Ngày sinh': 'dob'}


def test_distinct_template_fields_are_not_guessed():
    assert automap(['Năm sinh', 'Mã hộ', 'Quan hệ với chủ hộ', 'Là chủ hộ',
                    'Dân tộc', 'Tình trạng cư trú']) == {}
