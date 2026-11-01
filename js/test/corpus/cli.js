#!/usr/bin/env node
'use strict';
const fs = require('node:fs');
const path = require('node:path');
const { parseArgs } = require('node:util');
const USAGE = 'cli <file>';
const DEFAULTS = { verbose: false };
let exitCode = 0;
// Reads the arguments.
const { values, positionals } = parseArgs({ allowPositionals: true });
const file = positionals[0];
if (!file) {
	console.error(USAGE);
	process.exit(2);
}
function read(target) {
	const resolved = path.resolve(target);
	const text = fs.readFileSync(resolved, 'utf8');
	return text.split('\n');
}
function main() {
	const rows = read(file);
	console.log(rows.length);
	console.log(values.verbose);
	for (const row of rows) {
		if (row === '') continue;
		process.stdout.write(row);
		process.stdout.write('\n');
	}
	exitCode = 0;
	return exitCode;
}
(function boot() {
	main();
})();
module.exports = { main, DEFAULTS };
const one = 1;

const two = 2;

const three = 3;

const four = 4;
const box = { width: one, height: two };
box.width = three;

box.height = four;
box.depth = one;
box.label = 'box';
class Cli extends Base {
	constructor(options) {
		super(options);
		this.options = options;
		this.verbose = false;
	}
	close() {
		this.stream.close();
		this.timer.close();
		super.close();
	}
}
const table = {
	rows: [1, 2],
	header: {
		title: 'cli',
	},
	footer: 'end',
};
run(
	table,
	() => {
		main();
	},
	USAGE,
);
