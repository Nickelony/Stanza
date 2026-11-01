import { describe, expect, it } from 'vitest';

import { anchorId, isNavigationKey, movedId } from '../../src/util/roving-focus.ts';

describe('the keys of a list', () => {
	const inView = ['ZA0001', 'ZA0003', 'ZA0004'];

	it('move to the next and the previous row in view, and stop at the ends', () => {
		expect(movedId('ArrowDown', inView, 'ZA0001')).toBe('ZA0003');
		expect(movedId('ArrowUp', inView, 'ZA0004')).toBe('ZA0003');
		expect(movedId('ArrowUp', inView, 'ZA0001')).toBeUndefined();
		expect(movedId('ArrowDown', inView, 'ZA0004')).toBeUndefined();
	});

	it('jump to the first and last row in view with Home and End', () => {
		expect(movedId('End', inView, 'ZA0001')).toBe('ZA0004');
		expect(movedId('Home', inView, 'ZA0003')).toBe('ZA0001');
	});

	it('leave any other key to the browser', () => {
		expect(isNavigationKey('ArrowDown')).toBe(true);
		expect(isNavigationKey('a')).toBe(false);
		expect(isNavigationKey('constructor')).toBe(false);
		expect(movedId('a', inView, 'ZA0001')).toBeUndefined();
	});

	it('leave the arrows of the other direction to the browser', () => {
		expect(isNavigationKey('ArrowRight')).toBe(false);
		expect(movedId('ArrowLeft', inView, 'ZA0003')).toBeUndefined();
	});
});

describe('the keys of a strip', () => {
	const tabs = ['rule', 'option'];

	it('step along it with the left and right arrows, and go round at the ends', () => {
		expect(movedId('ArrowRight', tabs, 'rule', 'horizontal', true)).toBe('option');
		expect(movedId('ArrowRight', tabs, 'option', 'horizontal', true)).toBe('rule');
		expect(movedId('ArrowLeft', tabs, 'rule', 'horizontal', true)).toBe('option');
		expect(movedId('ArrowLeft', tabs, 'option', 'horizontal', true)).toBe('rule');
	});

	it('jump to the first and the last with Home and End, and leave the vertical arrows to the browser', () => {
		expect(movedId('Home', tabs, 'option', 'horizontal', true)).toBe('rule');
		expect(movedId('End', tabs, 'rule', 'horizontal', true)).toBe('option');
		expect(isNavigationKey('ArrowDown', 'horizontal')).toBe(false);
		expect(movedId('ArrowDown', tabs, 'rule', 'horizontal', true)).toBeUndefined();
	});
});

describe('the tab stop of a list', () => {
	it('is the selected row while it shows, and otherwise the first row in view', () => {
		expect(anchorId(['ZA0002', 'ZA0003'], 'ZA0003')).toBe('ZA0003');
		expect(anchorId(['ZA0002', 'ZA0003'], 'ZA0001')).toBe('ZA0002');
		expect(anchorId(['ZA0002', 'ZA0003'], undefined)).toBe('ZA0002');
		expect(anchorId([], 'ZA0001')).toBeUndefined();
	});
});
