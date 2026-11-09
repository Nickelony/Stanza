import { describe, expect, it } from 'vitest';

import { groupParts } from '../../src/util/group-parts.ts';

describe('the parts of a page', () => {
	const p = (html: string) => ({ kind: 'p', html }) as const;
	const ul = (...items: string[]) => ({ kind: 'ul', items }) as const;

	it('gather a run of text into one box and keep an example where the page puts it', () => {
		const grouped = groupParts([p('a'), ul('b'), { kind: 'example', index: 0 }, p('c'), { kind: 'values' }, p('d'), p('e')]);

		expect(grouped).toEqual([
			{ kind: 'notes', blocks: [p('a'), ul('b')] },
			{ kind: 'example', index: 0 },
			{ kind: 'notes', blocks: [p('c')] },
			{ kind: 'values' },
			{ kind: 'notes', blocks: [p('d'), p('e')] },
		]);
	});

	it('hold a table among the text of its run', () => {
		const table = { kind: 'table' as const, head: ['A'], rows: [['1']] };
		expect(groupParts([p('a'), table])).toEqual([{ kind: 'notes', blocks: [p('a'), table] }]);
	});

	it('are empty for a page with none', () => {
		expect(groupParts([])).toEqual([]);
	});
});
