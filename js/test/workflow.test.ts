import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';

const workflow = readFileSync(fileURLToPath(new URL('../../.github/workflows/js.yml', import.meta.url)), 'utf8').split(/\r?\n/);

const scripts = Object.keys(
	(JSON.parse(readFileSync(fileURLToPath(new URL('../package.json', import.meta.url)), 'utf8')) as { scripts: object }).scripts,
);

/** The quoted entries under `paths:` of an event in the `on:` block, which the workflow writes two spaces in and six deep. */
function pathFilter(event: string): string[] {
	const start = workflow.indexOf(`  ${event}:`);

	assert.notEqual(start, -1, `the workflow has a ${event} trigger`);

	const block = workflow.slice(start + 1);
	const end = block.findIndex((line) => /^ {0,2}\S/.test(line));

	return block
		.slice(0, end)
		.map((line) => /^ {6}- '([^']+)'$/.exec(line)?.[1])
		.filter((path) => path !== undefined);
}

describe('the JS workflow', () => {
	for (const event of ['push', 'pull_request']) {
		it(`filters a ${event} on the directory and on the workflow itself`, () => {
			assert.deepEqual(pathFilter(event), ['js/**', '.github/workflows/js.yml']);
		});
	}

	it('runs every script of the package but the formatter that writes', () => {
		for (const script of scripts.filter((name) => name !== 'format')) {
			const command = script === 'test' ? 'npm test' : `npm run ${script}`;

			assert.ok(
				workflow.some((line) => line.trim() === `run: ${command}`),
				`the workflow runs ${command}`,
			);
		}
	});
});
