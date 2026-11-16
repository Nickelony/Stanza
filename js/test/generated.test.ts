import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { Linter } from 'eslint';

import { createConfig, fixOneByOne, readPresetRules, settingVariants } from './helpers.ts';

/** The rules that keep or separate checked stanzas, accumulators, initializations, jumps, receiver groups and dependent bindings. */
const pairRules = [
	'separate-checked-stanzas',
	'keep-guard-clauses-together',
	'keep-guard-with-binding',
	'keep-accumulator-with-loop',
	'keep-jump-with-binding',
	'keep-assertion-with-binding',
	'keep-initialization-with-calls',
	'separate-receiver-groups',
	'keep-receiver-groups-together',
	'keep-dependent-bindings-together',
	'keep-statement-runs-together',
];

/** The settings a body is fixed with: the defaults, the variants of the corpus suites, and the roots and limit only the bodies vary. */
const bodySettings: Record<string, unknown>[] = [
	{},
	...settingVariants.map(([, settings]) => settings),
	{ stanza: { guardRoots: ['invariant', 'check'] } },
	{ stanza: { maxLooseRunSize: 2, declarationShapes: 'split' } },
];

const names = ['a', 'b', 'order', 'item', 'total', 'names'];

/** Creates a repeatable random number generator, so a failing body can be built again from its seed. */
function createRandom(seed: number): () => number {
	let state = seed >>> 0 || 1;

	return () => {
		state = (Math.imul(state, 1664525) + 1013904223) >>> 0;
		return state / 4294967296;
	};
}

/** Builds the lines of one statement, or of a pair that a keep rule judges, around a name the body has bound lately. */
function writeStatement(random: () => number, name: string): string[] {
	const other = names[Math.floor(random() * names.length)] ?? 'a';
	const gap = (): string[] => (random() < 0.5 ? [''] : []);

	if (random() < 0.25) {
		const pairs = [
			[`let ${name} = 0;`, ...gap(), `const ${other} = [];`, ...gap(), 'for (const x of list) {', `\t${name} += x;`, `\t${other}.push(x);`, '}'],
			[`const ${name} = new Item();`, ...gap(), `${name}.call();`, `${name}.call2();`],
			[`const ${name} = load(id);`, ...gap(), `if (!${name}) return;`],
			[`const ${name} = load(id);`, ...gap(), `return ${name};`],
			[`db.add(${name});`, ...gap(), 'await db.flush();', ...gap(), 'db.count += 1;'],
			[`const ${name} = load(id);`, ...gap(), `const ${other} = build(${name});`, ...gap(), `assert(${other});`],
			[`yield ${name};`, ...gap(), 'yield* list;', ...gap(), `yield ${other};`],
			[`switch (${name}) {`, '\tcase 1:', ...gap(), '\tcase 2:', ...gap(), '\tdefault:', '\t\trun();', '}'],
		];

		return pairs[Math.floor(random() * pairs.length)] ?? [];
	}

	const statements = [
		[`const ${name} = load(${other});`],
		[`if (!${name}) return;`],
		[`if (!${name}) {`, '\treturn null;', '}'],
		[`invariant(${name});`],
		[`check(${name} > 0);`],
		[`assert(${name});`],
		[`assert.equal(${name}, 1);`],
		[`db.add(${name});`],
		[`logger.info(${name});`],
		['await db.flush();'],
		[`${name} = compute(${other});`],
		[`for (const x of list) {`, `\t${name} += x;`, '}'],
		[`return ${name};`],
		[`${name}.call();`],
		[`const ${name} = await open(${other});`],
		['run(', `\t${name},`, ');'],
		[`if (${name}.done) break;`],
		[`print(${name});`],
		[`yield ${name};`],
	];

	return statements[Math.floor(random() * statements.length)] ?? [];
}

/** Builds a function body of two to ten statements with a blank line in about half of the gaps, sometimes inside a loop. */
function writeBody(random: () => number): string {
	const body: string[] = [];
	const count = 2 + Math.floor(random() * 9);

	let name = names[Math.floor(random() * names.length)] ?? 'a';

	for (let index = 0; index < count; index += 1) {
		if (index > 0 && random() < 0.5) {
			body.push('');
		}

		body.push(...writeStatement(random, name));

		if (random() < 0.25) {
			name = names[Math.floor(random() * names.length)] ?? 'a';
		}
	}

	const inLoop = random() < 0.2;
	const indented = body.map((line) => (line === '' ? line : `${inLoop ? '\t\t' : '\t'}${line}`));

	return ['async function* f(id, list, data) {', ...(inLoop ? ['\tfor (const y of list) {', ...indented, '\t}'] : indented), '}', ''].join('\n');
}

/** Reads the strong preset with a random subset of the pair rules, and sometimes without `keep-two-units-together`. */
function readRules(random: () => number): Record<string, Linter.RuleEntry> {
	const rules: Record<string, Linter.RuleEntry> = { ...readPresetRules('strong') };

	for (const name of pairRules) {
		if (random() < 0.5) {
			rules[name] = 'error';
		}
	}

	if (random() < 0.3) {
		delete rules['keep-two-units-together'];
	}

	return rules;
}

describe('generated bodies at the strong level with the pair rules', () => {
	it('settle in one pass to one text in any fix order, with each gap reported by one rule', () => {
		const linter = new Linter();
		const filename = 'file.ts';

		for (let seed = 0; seed < 150; seed += 1) {
			const random = createRandom(seed * 7919 + 17);
			const source = writeBody(random);
			const config = createConfig(readRules(random), bodySettings[Math.floor(random() * bodySettings.length)]);
			const label = `seed ${seed}:\n${source}`;
			const places = linter.verify(source, config, { filename }).map((message) => `${message.line}:${message.column}`);

			assert.equal(new Set(places).size, places.length, `two rules report one gap, ${label}`);

			const options = { filename, stats: true };
			const first = linter.verifyAndFix(source, config, options);

			assert.deepEqual(first.messages, [], label);
			assert.ok(linter.getFixPassCount() <= 1, `needed ${linter.getFixPassCount()} fix passes, ${label}`);
			assert.equal(linter.verifyAndFix(first.output, config, { filename }).output, first.output, `a second fix changes the text, ${label}`);
			assert.equal(fixOneByOne(source, config, 'first'), first.output, `fixing from the top writes another text, ${label}`);
			assert.equal(fixOneByOne(source, config, 'last'), first.output, `fixing from the bottom writes another text, ${label}`);
		}
	});
});
