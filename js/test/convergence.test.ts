import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import { Linter } from 'eslint';

import { createConfig, formatWithPrettier, presetRuns, readCorpus, readPresetRules } from './helpers.ts';

type Entries = [string, Linter.RuleEntry][];

/** Creates a repeatable random number generator, so a failing order can be run again. */
function createRandom(seed: number): () => number {
	let state = seed;

	return () => {
		state = (state * 1664525 + 1013904223) % 4294967296;
		return state / 4294967296;
	};
}

function shuffle(entries: Entries, seed: number): Entries {
	const random = createRandom(seed);
	const result = [...entries];

	for (let index = result.length - 1; index > 0; index -= 1) {
		const other = Math.floor(random() * (index + 1));
		[result[index], result[other]] = [result[other] as [string, Linter.RuleEntry], result[index] as [string, Linter.RuleEntry]];
	}

	return result;
}

const orders: Record<string, (entries: Entries) => Entries> = {
	declared: (entries) => entries,
	reversed: (entries) => [...entries].reverse(),
	rotated: (entries) => [...entries.slice(3), ...entries.slice(0, 3)],
	'shuffled once': (entries) => shuffle(entries, 1),
	'shuffled twice': (entries) => shuffle(entries, 2),
	'shuffled three times': (entries) => shuffle(entries, 3),
};

/** Drops about half of the blank lines of a source, so the same file starts from another set of gaps. */
function dropBlankLines(text: string, seed: number): string {
	const random = createRandom(seed);

	return text
		.split('\n')
		.filter((line) => line.trim() !== '' || random() < 0.5)
		.join('\n');
}

for (const [name, level, withOptIn] of presetRuns) {
	describe(`convergence at the ${name}`, () => {
		for (const file of readCorpus()) {
			it(`settles ${file.name} to one text in every rule order, and a second fix changes nothing`, async () => {
				const formatted = await formatWithPrettier(file.text, file.path);
				const inputs = [formatted, dropBlankLines(formatted, 7), dropBlankLines(formatted, 11)];
				const entries = Object.entries(readPresetRules(level, withOptIn));
				const linter = new Linter();
				const filename = file.name;

				for (const input of inputs) {
					const outputs = new Map<string, string>();

					for (const [orderName, order] of Object.entries(orders)) {
						const config = createConfig(Object.fromEntries(order(entries)));
						const first = linter.verifyAndFix(input, config, { filename });
						const second = linter.verifyAndFix(first.output, config, { filename });

						assert.deepEqual(first.messages, [], `${orderName}: the first fix leaves a message`);
						assert.equal(second.fixed, false, `${orderName}: a second fix changes the text`);
						assert.equal(second.output, first.output, `${orderName}: a second fix is not the identity`);
						assert.deepEqual(linter.verify(first.output, config, { filename }), [], `${orderName}: the output does not lint clean`);

						outputs.set(orderName, first.output);
					}

					assert.equal(new Set(outputs.values()).size, 1, 'the rule orders write different texts');
				}
			});
		}
	});
}

describe('the none level', () => {
	it('changes nothing', async () => {
		const linter = new Linter();
		const config = createConfig(readPresetRules('none'));

		for (const file of readCorpus()) {
			assert.equal(linter.verifyAndFix(file.text, config, { filename: file.name }).output, file.text);
		}
	});
});

describe('the corpus', () => {
	it('exercises the rules: the strong level writes blank lines into every file', async () => {
		const linter = new Linter();
		const config = createConfig(readPresetRules('strong'));

		for (const file of readCorpus()) {
			const formatted = await formatWithPrettier(file.text, file.path);
			const fixed = linter.verifyAndFix(formatted, config, { filename: file.name });

			assert.equal(fixed.fixed, true, `${file.name} has nothing to fix`);
		}
	});

	it('formats the corpus with the Prettier settings, imports sorted included', async () => {
		const file = readCorpus().find((candidate) => candidate.name === 'order-service.ts');
		const formatted = await formatWithPrettier(file?.text ?? '', file?.path ?? '');

		assert.ok(formatted.indexOf("'./guards.ts'") < formatted.indexOf("'./logger.ts'"), 'the imports of a file with a decorator are not sorted');
	});
});
