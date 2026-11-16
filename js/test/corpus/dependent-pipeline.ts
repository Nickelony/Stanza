async function readStream(fileHandle: FileHandle) {
	const file = await fileHandle.getFileAsync();

	const bytes = await file.arrayBufferAsync();

	const stream = new MemoryStream(bytes);
	return stream;
}

function useBindings() {
	const { catalog, store } = usePage();
	const { config } = useAppState();
	const preference = pairPreference(pair, config);
	const owner = pairOwner(catalog, pair, config);
	consume(store, preference, owner);

	const source = read();
	const first = transform(source);

	let second = transform(first);
	var third = finish(second);
	consume(third);
}
