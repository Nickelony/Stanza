import { existsSync, readFileSync, statSync } from 'node:fs';
import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

import { previewImages } from '../build/preview.ts';
import { jsSourcePaths, pythonSourcePaths, sourcePaths } from '../build/readers/source.ts';

import { repositoryRoot } from './fixtures.ts';

/** The preview images are read by the preview plugin rather than by the extraction, so `sourcePaths` does not list them. */
const previewPaths = Object.values(previewImages).map((image) => `icon/${image.source}`);

/** The logo of each page, which `src/icons` copies under the ID of its language. */
const pageLogos = { csharp: 'icon/Stanza.svg', markup: 'icon/Stanza-Mu.svg', js: 'icon/Stanza-JS.svg', python: 'icon/Stanza-Py.svg' };
const workflowPath = '.github/workflows/pages.yml';

/** The lint loads JS Stanza from its build and links the package from `js/`, so the source and the package files of `js/` count. */
const lintPaths = ['js/src/**', 'js/package.json', 'js/package-lock.json', 'js/tsconfig.json', 'js/tsconfig.build.json'];
const read = (file: string): string => readFileSync(join(repositoryRoot, file), 'utf8');

/** A file the site reads is named once, and a folder of them by its files: `folder/**`, as the workflow writes it. */
const asWorkflowPath = (path: string): string => (statSync(join(repositoryRoot, path)).isDirectory() ? `${path}/**` : path);

/**
 * The `paths` list of each event in the `on:` block of the workflow, read as the workflow is written:
 * `on:` at the left edge, an event two spaces in, `paths:` four in, and one quoted entry per line six in.
 * The site has no YAML parser, so a line of any other shape in that block fails here rather than being skipped.
 */
function readPathFilters(text: string): Map<string, string[]> {
	const filters = new Map<string, string[]>();
	const lines = text.split(/\r?\n/);
	const start = lines.indexOf('on:');

	expect(start, 'the workflow has an on: block').toBeGreaterThanOrEqual(0);

	let event = '';
	let inPaths = false;

	for (const line of lines.slice(start + 1)) {
		if (line.trim() === '' || line.trimStart().startsWith('#')) {
			continue;
		}

		if (!line.startsWith(' ')) {
			break;
		}

		const eventMatch = /^ {2}([a-z_]+):/.exec(line);
		const entry = /^ {6}- '([^']+)'$/.exec(line);

		if (eventMatch) {
			event = eventMatch[1]!;
			inPaths = false;
		} else if (/^ {4}paths:$/.test(line)) {
			inPaths = true;
			filters.set(event, []);
		} else if (inPaths && entry) {
			filters.get(event)!.push(entry[1]!);
		} else if (!/^ {4}\S/.test(line)) {
			throw new Error(`Unexpected line in the on: block of ${workflowPath}: ${line}`);
		} else {
			inPaths = false;
		}
	}

	return filters;
}

/** The first link target of each row of the table under "What the build reads", as a path from the repository root. */
function readTablePaths(text: string): string[] {
	const section = /^## What the build reads\r?\n([\s\S]*?)(?=^## )/m.exec(text)?.[1];

	expect(section, 'the README has a "What the build reads" section').toBeDefined();

	const rows = section!.split(/\r?\n/).filter((line) => line.startsWith('|') && !/^\|\s*(?:File|-+)\s*\|/.test(line));

	return rows.map((row) => {
		const target = /\]\(\.\.\/([^)]+)\)/.exec(row)?.[1];

		expect(target, `the table row has a link to a file: ${row}`).toBeDefined();

		return target!;
	});
}

/** The files the JS reader takes from `js/src` are already named by the glob of the lint. */
const isInLintGlob = (path: string): boolean => path.startsWith('js/src/');

const sourceFiles = [
	...Object.values(sourcePaths),
	...Object.values(jsSourcePaths),
	...Object.values(pythonSourcePaths),
	...previewPaths,
	...Object.values(pageLogos),
];

describe('the Pages workflow', () => {
	const filters = readPathFilters(read(workflowPath));
	const expected = ['site/**', ...sourceFiles.filter((path) => !isInLintGlob(path)).map(asWorkflowPath), ...lintPaths, workflowPath].sort();

	it.each(['push', 'pull_request'])('filters a %s on the files the site reads and lints with, its own folder and the workflow itself', (event) => {
		expect(filters.get(event)?.toSorted()).toEqual(expected);
	});

	it('filters only the push and the pull request', () => {
		expect([...filters.keys()].sort()).toEqual(['pull_request', 'push']);
	});

	it('names only paths that exist', () => {
		for (const path of expected) {
			expect(existsSync(join(repositoryRoot, path.replace(/\/\*\*$/, ''))), path).toBe(true);
		}
	});
});

describe('the table of what the build reads in the site README', () => {
	it('lists the files the site reads and the logo, and nothing else', () => {
		expect(readTablePaths(read('site/README.md')).toSorted()).toEqual([...sourceFiles].sort());
	});
});

describe('the logos of the pages', () => {
	it.each(Object.entries(pageLogos))('show the %s page a byte-for-byte copy of %s', (id, logo) => {
		expect(readFileSync(join(repositoryRoot, 'site/src/icons', `${id}.svg`))).toEqual(readFileSync(join(repositoryRoot, logo)));
	});
});
