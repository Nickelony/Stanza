import assertionRuns from '../../src/rules/separate-assertion-runs.ts';
import assignmentRuns from '../../src/rules/separate-assignment-runs.ts';
import awaitRuns from '../../src/rules/separate-await-runs.ts';
import declarationRuns from '../../src/rules/separate-declaration-runs.ts';
import invocationRuns from '../../src/rules/separate-invocation-runs.ts';
import { createRuleTester, lines } from '../helpers.ts';

const tester = createRuleTester();

tester.run('separate-declaration-runs', declarationRuns as never, {
	valid: [
		{ code: lines('const a = 1;', 'foo(a);') },
		{ code: lines('const a = 1;', '', 'const b = 2;', '', 'foo(a);') },
		{ code: lines('const a = f();', 'const b = g(a);', '', 'foo(a, b);') },
		{ code: lines('foo();', 'const a = 1;', 'bar(a);') },
		{ code: lines('const a = 1;', 'let b = 2;', 'var c = 3;', 'using d = open();', 'await using e = open();', 'foo();') },
	],

	invalid: [
		{
			code: lines('const limit = 3;', 'const retries = 5;', 'foo(limit);', 'return retries;'),
			output: lines('const limit = 3;', 'const retries = 5;', '', 'foo(limit);', 'return retries;'),
			errors: 1,
		},

		{
			code: lines('foo();', 'const a = 1;', 'const b = 2;', 'bar();'),
			output: lines('foo();', '', 'const a = 1;', 'const b = 2;', '', 'bar();'),
			errors: 2,
		},

		{
			code: lines('const a = 1;', 'const b = 2;', 'let c = 3;', 'let d = 4;'),
			output: lines('const a = 1;', 'const b = 2;', '', 'let c = 3;', 'let d = 4;'),
			errors: 1,
		},

		{
			code: lines('export const a = 1;', 'export const b = 2;', 'const c = 3;', 'const d = 4;'),
			output: lines('export const a = 1;', 'export const b = 2;', '', 'const c = 3;', 'const d = 4;'),
			errors: 1,
		},

		{
			code: lines('type A = string;', 'type B = number;', 'const a: A = "x";'),
			output: lines('type A = string;', 'type B = number;', '', 'const a: A = "x";'),
			errors: 1,
		},

		{
			code: lines('using a = open();', 'using b = open();', 'foo();'),
			output: lines('using a = open();', 'using b = open();', '', 'foo();'),
			errors: 1,
		},
	],
});

tester.run('separate-invocation-runs', invocationRuns as never, {
	valid: [
		{ code: lines('foo();', 'bar();', 'baz();') },
		{ code: lines('log(1);', 'log(2);', '', 'foo();') },
		{ code: lines('const a = 1;', 'foo(a);') },
		{ code: lines('expect(a).toBe(1);', 'expect(b).toBe(2);', 'foo();') },
		{ code: lines('describe("a", () => {});', 'describe("b", () => {});', 'foo();') },
		{ code: lines('(function () {})();', '(function () {})();') },
	],

	invalid: [
		{
			code: lines('const a = 1;', 'log(a);', 'log(2);', 'done();'),
			output: lines('const a = 1;', '', 'log(a);', 'log(2);', '', 'done();'),
			errors: 2,
		},

		{
			code: lines('start();', 'console.log(1);', 'console.log(2);', 'console.error(3);'),
			output: lines('start();', '', 'console.log(1);', 'console.log(2);', '', 'console.error(3);'),
			errors: 2,
		},

		{
			code: lines('start();', 'await save();', 'await save();', 'stop();'),
			output: lines('start();', '', 'await save();', 'await save();', '', 'stop();'),
			errors: 2,
		},

		{
			code: lines('start();', 'fetch(url).then(parse).catch(fail);', 'fetch(url).then(parse).catch(fail);', 'stop();'),
			output: lines('start();', '', 'fetch(url).then(parse).catch(fail);', 'fetch(url).then(parse).catch(fail);', '', 'stop();'),
			errors: 2,
		},

		{
			code: lines('this.a.run();', 'this.a.run();', 'x = 1;'),
			output: lines('this.a.run();', 'this.a.run();', '', 'x = 1;'),
			errors: 1,
		},
	],
});

tester.run('separate-assertion-runs', assertionRuns as never, {
	valid: [
		{ code: lines('const r = parse(text);', 'expect(r).toBe(1);', 'cleanup();') },
		{ code: lines('const r = parse(text);', '', 'expect(r).toBe(1);', 'expect(r.x).toBe(2);', '', 'cleanup();') },
		{ code: lines('foo();', 'foo();', 'bar();') },
		{ code: lines('check(a);', 'check(b);', 'bar();'), name: 'a helper outside the roots is no assertion' },
		{ code: lines('run();', 'expect(a).toBe(1);', 'assert.ok(b);', 'done();'), name: 'a lone assertion of each root stays tight' },
	],

	invalid: [
		{
			code: lines('const result = parse(text);', 'expect(result.count).toBe(2);', 'expect(result.first).not.toBeNull();', 'cleanup();'),
			output: lines('const result = parse(text);', '', 'expect(result.count).toBe(2);', 'expect(result.first).not.toBeNull();', '', 'cleanup();'),
			errors: 2,
		},

		{
			code: lines('const r = run();', 'assert.equal(r, 1);', 'assert.ok(r);'),
			output: lines('const r = run();', '', 'assert.equal(r, 1);', 'assert.ok(r);'),
			errors: 1,
		},

		{
			code: lines('const r = run();', 't.is(r, 1);', 't.truthy(r);', 'done();'),
			output: lines('const r = run();', '', 't.is(r, 1);', 't.truthy(r);', '', 'done();'),
			errors: 2,
		},

		{
			code: lines('run();', 'await expect(a).resolves.toBe(1);', 'await expect(b).rejects.toThrow();', 'done();'),
			output: lines('run();', '', 'await expect(a).resolves.toBe(1);', 'await expect(b).rejects.toThrow();', '', 'done();'),
			errors: 2,
		},

		{
			code: lines('run();', 'check(a);', 'check.that(b);', 'done();'),
			output: lines('run();', '', 'check(a);', 'check.that(b);', '', 'done();'),
			settings: { stanza: { assertionRoots: ['check'] } },
			errors: 2,
		},

		{
			code: lines('run();', 'chai.expect(a).to.equal(1);', 'expect(b).toBe(2);', 'done();'),
			output: lines('run();', '', 'chai.expect(a).to.equal(1);', 'expect(b).toBe(2);', '', 'done();'),
			errors: 2,
		},

		{
			code: lines('run();', 'expect(a).toBe(1);', 'expect(b).toBe(2);', 'assert.ok(c);', 'assert.ok(d);', 'done();'),
			output: lines('run();', '', 'expect(a).toBe(1);', 'expect(b).toBe(2);', '', 'assert.ok(c);', 'assert.ok(d);', '', 'done();'),
			errors: 3,
			name: 'assertions of another root are another run',
		},
	],
});

tester.run('separate-await-runs', awaitRuns as never, {
	valid: [
		{ code: lines('const c = create();', 'await c.connect();', 'log();') },
		{ code: lines('const a = await load();', 'const b = await read(a);', 'log(b);') },
		{ code: lines('a = await load();', 'b = await read(a);', 'log(b);') },

		{
			code: lines('run();', 'await expect(a).resolves.toBe(1);', 'await expect(b).resolves.toBe(2);', 'done();'),
			name: 'an awaited assertion stays an assertion',
		},
	],

	invalid: [
		{
			code: lines('const client = create();', 'await client.connect();', 'await client.send(request);', 'log("sent");'),
			output: lines('const client = create();', '', 'await client.connect();', 'await client.send(request);', '', 'log("sent");'),
			errors: 2,
		},

		{
			code: lines('start();', 'await a;', 'await b;', 'stop();'),
			output: lines('start();', '', 'await a;', 'await b;', '', 'stop();'),
			errors: 2,
		},
	],
});

tester.run('separate-assignment-runs', assignmentRuns as never, {
	valid: [
		{ code: lines('start();', 'a = 1;', 'stop();') },
		{ code: lines('start();', '', 'this.a = 1;', 'this.b = 2;', '', 'stop();') },
		{ code: lines('const a = 1;', 'const b = 2;', 'start();') },
		{ code: lines('start();', 'start();', 'stop();') },
		{ code: lines('start();', 'run(a = 1);', 'run(b = 2);', 'stop();'), name: 'an assignment inside a call is no assignment statement' },
		{ code: lines('start();', 'a = 1, b = 2;', 'stop();'), name: 'a sequence is one statement' },
	],

	invalid: [
		{
			code: lines('validate(input);', 'this.name = input.name;', 'this.size = input.size;', 'initialize();'),
			output: lines('validate(input);', '', 'this.name = input.name;', 'this.size = input.size;', '', 'initialize();'),
			errors: 2,
		},

		{
			code: lines('start();', 'total += 1;', 'count++;', '--left;', 'stop();'),
			output: lines('start();', '', 'total += 1;', 'count++;', '--left;', '', 'stop();'),
			errors: 2,
			name: 'compound assignments, increments and decrements join the run',
		},

		{
			code: lines('start();', 'a ??= 1;', 'b ||= 2;', 'c &&= 3;', 'stop();'),
			output: lines('start();', '', 'a ??= 1;', 'b ||= 2;', 'c &&= 3;', '', 'stop();'),
			errors: 2,
		},

		{
			code: lines('start();', 'a.b = 1;', 'c[0] = 2;', 'd = 3;', 'stop();'),
			output: lines('start();', '', 'a.b = 1;', 'c[0] = 2;', 'd = 3;', '', 'stop();'),
			errors: 2,
			name: 'a run holds any target',
		},

		{
			code: lines('start();', 'a = await load();', 'b = await read(a);', 'stop(b);'),
			output: lines('start();', '', 'a = await load();', 'b = await read(a);', '', 'stop(b);'),
			errors: 2,
			name: 'an assignment that awaits stays an assignment',
		},

		{
			code: lines('start();', '(a as any).x = 1;', 'b!.y = 2;', 'stop();'),
			output: lines('start();', '', '(a as any).x = 1;', 'b!.y = 2;', '', 'stop();'),
			errors: 2,
		},

		{
			code: lines('const a = 1;', 'b = 2;', 'c = 3;'),
			output: lines('const a = 1;', '', 'b = 2;', 'c = 3;'),
			errors: 1,
			name: 'a lone declaration above a run is set apart by this rule',
		},

		{
			code: lines('a = b = 0;', 'c = 1;', 'stop();'),
			output: lines('a = b = 0;', 'c = 1;', '', 'stop();'),
			errors: 1,
		},

		{
			code: lines('log(a);', 'log(b);', 'x = 1;', 'y = 2;'),
			output: lines('log(a);', 'log(b);', '', 'x = 1;', 'y = 2;'),
			errors: 1,
			name: 'a gap where two runs meet is one gap',
		},

		{
			code: lines('start();', '// the sizes', 'this.a = 1;', 'this.b = 2;'),
			output: lines('start();', '', '// the sizes', 'this.a = 1;', 'this.b = 2;'),
			errors: 1,
			name: 'a comment directly above the run moves with it',
		},
	],
});
