/** The values several rules share, so every rule reads a statement the same way. */
export interface Environment {
	readonly assertionRoots: ReadonlySet<string>;
	readonly testRoots: ReadonlySet<string>;

	/** The names whose bare call is a guard, which `separate-guard-clauses` and `separate-base-call` both read. */
	readonly guardRoots: ReadonlySet<string>;

	/** The number of units a loose run may hold before `gather-loose-statements` and `gather-loose-members` gather it; 0 turns both off. */
	readonly maxLooseRunSize: number;

	/** True when `separate-await-runs` is enabled, which makes an awaited call an await statement whatever it calls. */
	readonly awaitEnabled: boolean;
}

export const defaultGuardRoots: readonly string[] = ['assert', 'invariant'];
export const defaultAssertionRoots: readonly string[] = ['assert', 'expect', 'expectTypeOf', 't'];

export const defaultTestRoots: readonly string[] = [
	'describe',
	'it',
	'test',
	'suite',
	'context',
	'specify',
	'before',
	'beforeAll',
	'beforeEach',
	'after',
	'afterAll',
	'afterEach',
];

export const defaultMaxLooseRunSize = 3;
export const leastMaxLooseRunSize = 0;
export const greatestMaxLooseRunSize = 1000;

/** The settings that `settings.stanza` holds, which `readSharedSettings` returns once they are read. */
export type SharedSettings = Pick<Environment, 'assertionRoots' | 'testRoots' | 'guardRoots' | 'maxLooseRunSize'>;

/** Reads one setting from its configured value, which is never `undefined`, and throws when the value is invalid. */
type SettingReader<Value> = (key: string, value: unknown) => Value;

/** Tells a value in a message, with a string quoted. */
function describeValue(value: unknown): string {
	if (typeof value === 'string') {
		return JSON.stringify(value);
	}

	if (typeof value !== 'object' || value === null) {
		return String(value);
	}

	try {
		return JSON.stringify(value);
	} catch {
		return Object.prototype.toString.call(value);
	}
}

/** Lists words as `a, b and c`. */
function listWords(words: readonly string[]): string {
	return words.length < 2 ? words.join('') : `${words.slice(0, -1).join(', ')} and ${words.at(-1)}`;
}

/** Reads a list of names; an empty list is no names, and a value that is no array of strings is an error. */
function readNames(): SettingReader<ReadonlySet<string>> {
	return (key, value) => {
		if (!Array.isArray(value)) {
			throw new Error(`Stanza: setting "${key}" must be an array of strings, but is ${describeValue(value)}.`);
		}

		const index = value.findIndex((item) => typeof item !== 'string');

		if (index >= 0) {
			throw new Error(`Stanza: setting "${key}" must be an array of strings, but item ${index} is ${describeValue(value[index])}.`);
		}

		return new Set(value as string[]);
	};
}

/** Reads the size of a loose run; a value that is no whole number from the least to the greatest is an error. */
const readLooseRunSize: SettingReader<number> = (key, value) => {
	if (typeof value === 'number' && Number.isInteger(value) && value >= leastMaxLooseRunSize && value <= greatestMaxLooseRunSize) {
		return value;
	}

	throw new Error(
		`Stanza: setting "${key}" must be a whole number from ${leastMaxLooseRunSize} to ${greatestMaxLooseRunSize}, but is ${describeValue(value)}.`,
	);
};

/** The one table of the settings: each key `settings.stanza` accepts, with the default of an absent key and how a present value is read. */
const settingTable = {
	assertionRoots: { defaultValue: new Set(defaultAssertionRoots), read: readNames() },
	guardRoots: { defaultValue: new Set(defaultGuardRoots), read: readNames() },
	maxLooseRunSize: { defaultValue: defaultMaxLooseRunSize, read: readLooseRunSize },
	testRoots: { defaultValue: new Set(defaultTestRoots), read: readNames() },
} satisfies {
	readonly [Key in keyof SharedSettings]: { readonly defaultValue: SharedSettings[Key]; readonly read: SettingReader<SharedSettings[Key]> };
};

const settingKeys = Object.keys(settingTable) as (keyof SharedSettings)[];
const sortedSettingKeys = [...settingKeys].sort();

/** Validates `settings.stanza` and reads each key, with the default for a key it leaves out; an invalid setting throws an error that says what to change. */
function readStanzaSettings(stanza: unknown): SharedSettings {
	if (stanza !== undefined && (typeof stanza !== 'object' || stanza === null || Array.isArray(stanza))) {
		throw new Error(
			`Stanza: settings.stanza must be an object of settings, but is ${describeValue(stanza)}; the settings are ${listWords(sortedSettingKeys)}.`,
		);
	}

	const values = (stanza ?? {}) as Record<string, unknown>;

	for (const key of Object.keys(values)) {
		if (!Object.hasOwn(settingTable, key)) {
			throw new Error(`Stanza: unknown setting "${key}" in settings.stanza; the settings are ${listWords(sortedSettingKeys)}.`);
		}
	}

	const read: Record<string, unknown> = {};

	for (const key of settingKeys) {
		const value = values[key];
		read[key] = value === undefined ? settingTable[key].defaultValue : settingTable[key].read(key, value);
	}

	return read as unknown as SharedSettings;
}

const noSettings = readStanzaSettings(undefined);
const readSettings = new WeakMap<object, SharedSettings>();

/**
 * Reads `settings.stanza`, and throws an error for an unknown key or a value of the wrong type or range, so no mistake is silently ignored.
 * A settings object is validated once, so every rule and file of one configuration shares the reading.
 */
export function readSharedSettings(settings: unknown): SharedSettings {
	if (typeof settings !== 'object' || settings === null) {
		return noSettings;
	}

	let shared = readSettings.get(settings);

	if (shared === undefined) {
		shared = readStanzaSettings((settings as Record<string, unknown>)['stanza']);
		readSettings.set(settings, shared);
	}

	return shared;
}
