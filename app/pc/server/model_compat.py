"""Pydantic 1 (Win7 legacy) and Pydantic 2 compatibility helpers."""


def fields_set(instance):
    if hasattr(instance,'model_fields_set'):
        return instance.model_fields_set
    return instance.__fields_set__


def copy_with(instance, update):
    if hasattr(instance,'model_copy'):
        return instance.model_copy(update=update)
    return instance.copy(update=update)


def declared_fields(model_type):
    if hasattr(model_type,'model_fields'):
        return set(model_type.model_fields)
    return set(model_type.__fields__)


def validate_model(model_type,values):
    if hasattr(model_type,'model_validate'):
        return model_type.model_validate(values)
    return model_type.parse_obj(values)
