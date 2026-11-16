"""A value-producing pipeline with type and syntax boundaries."""

from io import BytesIO


async def read_pipeline(handle):
    file = await handle.get_file()
    raw = await file.array_buffer()
    data: bytes = bytes(raw)
    stream = BytesIO(data)
    return stream


def separate_shapes(page, state):
    catalog, store = page
    config, flags = state
    preference = choose(config)
    owner = find_owner(catalog, config)
    return preference, owner, store, flags
