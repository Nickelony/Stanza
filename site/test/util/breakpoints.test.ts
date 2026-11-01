import { readFileSync } from 'node:fs';
import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

import { sideBySide } from '../../src/util/breakpoints.ts';

describe('the width where the panes sit side by side', () => {
	it('is the one style.css uses', () => {
		const css = readFileSync(join(import.meta.dirname, '..', '..', 'src', 'style.css'), 'utf8');
		expect(css).toContain(`${sideBySide} {`);
	});
});
