import { readFile } from 'node:fs/promises';

export const PI = 3.14159;
export const E = 2.71828;
export let mutable = 0;
export function clamp(value: number, min: number, max: number): number {
	if (value < min) return min;
	if (value > max) return max;
	return value;
}
export function sum(values: number[]): number {
	let total = 0;
	for (const value of values) {
		total += value;
	}
	return total;
}
export async function loadAll(paths: string[]): Promise<string[]> {
	const results: string[] = [];
	const failures: string[] = [];
	for (const path of paths) {
		try {
			results.push(await readFile(path, 'utf8'));
		} catch {
			failures.push(path);
		}
	}
	console.log(results.length);
	console.log(failures.length);
	return results;
}
export function* range(from: number, to: number): Generator<number> {
	let current = from;
	while (current < to) {
		yield current;
		current += 1;
	}
	yield to;
}
export function parse(text: string): number {
	'use strict';
	const trimmed = text.trim();
	const value = Number(trimmed);
	if (Number.isNaN(value)) {
		throw new TypeError(`Not a number: ${trimmed}`);
	}
	return value;
}
const template = `first line

third line`;
export const config = { template, retries: 3 };
outer: for (const row of [[1], [2]]) {
	for (const cell of row) {
		if (cell > 1) break outer;
		mutable += cell;
	}
}
export default function main(): void {
	// Starts the run.
	mutable = 1;
	mutable = 2;
	void loadAll([]);
	void loadAll([]);
}
export enum Mode {
	Fast = 1,

	Slow = 2,
	Custom = compute(
		3,
	),
	Off = 0,
}
export const settings = {
	name: 'utils',

	retries: 3,
	nested: {
		deep: true,
	},
	label: 'x',
};
export const matrix = [
	[1, 2],

	[3, 4],
	[
		5,
		6,
	],
];
export function configure(
	name: string,

	options: {
		verbose: boolean;
	},
	level: number,
): void {
	register(
		name,

		options,
		() => {
			run(level);
		},
		level,
	);
}
