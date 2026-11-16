import type { Environment } from './environment.ts';
import { isGuard, readStatementCall } from './syntax.ts';
import type { Unit } from './types.ts';

type Node = Unit['node'];

/** Tells whether a name is a root of a guard or an assertion. */
function isRoot(name: string, environment: Environment): boolean {
	return environment.guardRoots.has(name) || environment.assertionRoots.has(name);
}

/** Reads the condition a narrowing assertion checks: the first argument of `assert(x)` or `assert.ok(x)` for a root of a guard or an assertion. */
export function readNarrowedTest(statement: Node, environment: Environment): Node | undefined {
	if (statement.type !== 'ExpressionStatement' || statement.expression.type !== 'CallExpression') {
		return undefined;
	}

	const {
		callee,
		arguments: [first],
	} = statement.expression;

	const isDirect =
		(callee.type === 'Identifier' && isRoot(callee.name, environment)) ||
		(callee.type === 'MemberExpression' &&
			!callee.computed &&
			callee.object.type === 'Identifier' &&
			isRoot(callee.object.name, environment) &&
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
