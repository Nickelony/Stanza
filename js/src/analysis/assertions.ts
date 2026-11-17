import type { Environment } from './environment.ts';
import { isGuard, readStatementCall } from './syntax.ts';
import type { Unit } from './types.ts';

type Node = Unit['node'];

/** Reads the condition a narrowing assertion checks: the first argument of `assert(x)` or `assert.ok(x)` for a root of `assertionRoots`, as the C# rule reads it. */
export function readNarrowedTest(statement: Node, environment: Environment): Node | undefined {
	if (statement.type !== 'ExpressionStatement' || statement.expression.type !== 'CallExpression') {
		return undefined;
	}

	const {
		callee,
		arguments: [first],
	} = statement.expression;

	const isDirect =
		(callee.type === 'Identifier' && environment.assertionRoots.has(callee.name)) ||
		(callee.type === 'MemberExpression' &&
			!callee.computed &&
			callee.object.type === 'Identifier' &&
			environment.assertionRoots.has(callee.object.name) &&
			callee.property.type === 'Identifier' &&
			callee.property.name === 'ok');

	return isDirect && first !== undefined && first.type !== 'SpreadElement' ? first : undefined;
}

/** Tells whether a statement is an assertion or a guard call, as `separate-assertion-runs` and `separate-guard-clauses` read one. */
export function isAssertion(statement: Node, environment: Environment): boolean {
	const call = readStatementCall(statement);

	if (typeof call === 'object' && call.names.some((name) => environment.assertionRoots.has(name))) {
		return true;
	}

	return statement.type !== 'IfStatement' && isGuard(statement, environment.guardRoots);
}
