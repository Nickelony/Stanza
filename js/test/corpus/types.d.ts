/// <reference types="node" />
import type { Readable } from 'node:stream';

export interface Reader {
	name: string;
	size: number;
	read(count: number): Buffer;
	read(count: number, encoding: string): string;
	close(): void;
	(path: string): Reader;
	new (path: string): Reader;
	readonly [key: string]: unknown;
}
export type Options = {
	retries: number;
	delay: number;
	onError(error: Error): void;
	onDone?: () => void;
};
declare module 'plain-reader' {
	export const version: string;
	export const name: string;
	export function open(path: string): Reader;
	export function open(path: string, mode: 'r' | 'w'): Reader;
	export default open;
}
declare global {
	interface Window {
		reader: Reader;
		stream: Readable;
	}
	var debug: boolean;
	var verbose: boolean;
}
export declare class Stream {
	private buffer;
	protected size: number;
	constructor(source: Readable);
	get ready(): boolean;
	set ready(value: boolean);
	pipe(target: Stream): Stream;
	pipe(target: Stream, options: Options): Stream;
	static from(source: Readable): Stream;
}
declare const VERSION: string;
declare const BUILD: number;
export {};
