import { isAssertion, readNarrowedTest } from './assertions.ts';
import { narrowsAnyName, type ValueReferences } from './bindings.ts';
import type { Environment } from './environment.ts';
import { readAskingJumps } from './jumps.ts';
import { guardKinds } from './kinds.ts';
import { countLeadingGuards } from './leading.ts';
import type { ClaimContext } from './session.ts';
import { isGuard, isJump, readStatementCall } from './syntax.ts';
import type { Container, Unit } from './types.ts';

interface CheckedStanza {
	readonly start: number;
	readonly firstCheck: number;
	readonly lastCheck: number;
	readonly onlyAssertions: boolean;
}

interface CheckedGeometry {
	readonly stanzas: readonly CheckedStanza[];
	readonly guardBindings: ReadonlySet<number>;
	readonly assertionBindings: ReadonlySet<number>;
}

/** Tells whether a unit is a single-line assertion whose first argument narrows a name, in the forms of `keep-assertion-with-binding`. */
function isNarrowing(unit: Unit, environment: Environment): boolean {
	const test = readNarrowedTest(unit.node, environment);
	return !unit.isMultiLine && test !== undefined && narrowsAnyName(test);
}

/**
 * Reads a check as a guard or a narrowing assertion, as the C# rule does: a call of an `assertionRoots` name is an assertion,
 * which counts only when it narrows, and any other guard, a `guardRoots` call included, is a guard.
 */
function readCheck(unit: Unit, environment: Environment): 'assertion' | 'guard' | undefined {
	const call = readStatementCall(unit.node);

	if (typeof call === 'object' && call.names.some((name) => environment.assertionRoots.has(name))) {
		return isNarrowing(unit, environment) ? 'assertion' : undefined;
	}

	return isGuard(unit.node, environment.guardRoots) ? 'guard' : undefined;
}

/** Tells whether a narrowing assertion is no lead of an assert phase, so `keep-assertion-with-binding` keeps the bindings it checks. */
function isEligibleAssertion(container: Container, index: number, environment: Environment): boolean {
	const unit = container.units[index] as Unit;
	const after = container.units[index + 1];

	if (!isNarrowing(unit, environment)) {
		return false;
	}

	return after === undefined || after.isMultiLine || container.gaps[index]?.hasComment !== false || !isAssertion(after.node, environment);
}

/** Reads the bindings and checks of each stanza once, so its separating and keeping rules share their boundaries. */
function readGeometry(container: Container, environment: Environment, references: ValueReferences): CheckedGeometry {
	const stanzas: CheckedStanza[] = [];
	const guardBindings = new Set<number>();
	const assertionBindings = new Set<number>();

	if (!guardKinds.has(container.kind)) {
		return { stanzas, guardBindings, assertionBindings };
	}

	const checks = container.units.map((unit) => (unit.isShebang ? undefined : readCheck(unit, environment)));

	const assertions = container.units.map((unit, index) => {
		const call = readStatementCall(unit.node);

		return (
			!unit.isMultiLine && !unit.isShebang && (checks[index] === 'assertion' || (typeof call === 'object' && isAssertion(unit.node, environment)))
		);
	});

	for (let start = 0; start < assertions.length;) {
		if (assertions[start] !== true) {
			start += 1;
			continue;
		}

		let end = start + 1;

		while (assertions[end] === true && container.gaps[end - 1]?.hasComment === false) {
			end += 1;
		}

		// A run that holds a plain result assertion is the assert phase of a test, so none of it is a check.
		if (checks.slice(start, end).some((check) => check === undefined)) {
			checks.fill(undefined, start, end);
		}

		start = end;
	}

	for (let index = 0; index < checks.length; index += 1) {
		if (checks[index] === undefined) {
			continue;
		}

		const first = index;
		const reads = new Set<object | string>();

		let onlyAssertions = true;

		do {
			const unit = container.units[index] as Unit;
			const test = unit.node.type === 'IfStatement' ? unit.node.test : unit.node;

			for (const binding of references.readsOf(test)) {
				reads.add(binding);
			}

			onlyAssertions &&= checks[index] === 'assertion';
			index += 1;
		} while (checks[index] !== undefined && container.gaps[index - 1]?.hasComment === false);

		const last = index - 1;
		let start = first;

		while (start > 0 && container.gaps[start - 1]?.hasComment === false) {
			const above = container.units[start - 1] as Unit;
			const bindings = above.isShebang ? new Set<object | string>() : references.boundOf(above.node);

			if (bindings.size === 0 || ![...bindings].some((binding) => reads.has(binding))) {
				break;
			}

			start -= 1;
		}

		stanzas.push({ start, firstCheck: first, lastCheck: last, onlyAssertions });

		// The keeper of the bindings follows the first check: a narrowing assertion, or an `if` guard; a guard call has none.
		const firstNode = (container.units[first] as Unit).node;
		const isAsserted = onlyAssertions || isEligibleAssertion(container, first, environment);
		const bindings = isAsserted ? assertionBindings : firstNode.type === 'IfStatement' ? guardBindings : undefined;

		for (let gap = start; gap < first; gap += 1) {
			bindings?.add(gap);
		}

		index = last;
	}

	return { stanzas, guardBindings, assertionBindings };
}

const geometries = new WeakMap<Container, CheckedGeometry>();

function getGeometry(container: Container, environment: Environment, references: ValueReferences): CheckedGeometry {
	let geometry = geometries.get(container);

	if (geometry === undefined) {
		geometry = readGeometry(container, environment, references);
		geometries.set(container, geometry);
	}

	return geometry;
}

export function readCheckedStanzas(container: Container, environment: Environment, references: ValueReferences): readonly CheckedStanza[] {
	return getGeometry(container, environment, references).stanzas;
}

export function isCheckedBindingGap(
	container: Container,
	index: number,
	environment: Environment,
	references: ValueReferences,
	kind: 'guard' | 'assertion',
): boolean {
	const geometry = getGeometry(container, environment, references);
	return (kind === 'guard' ? geometry.guardBindings : geometry.assertionBindings).has(index);
}

/** Reads the outer gaps of each guard chain and the contiguous bindings the chain checks. */
export function readCheckedEdges(container: Container, context: ClaimContext): ReadonlySet<number> {
	const edges = new Set<number>();
	const jumpEdges = new Set<number>();

	if (!guardKinds.has(container.kind)) {
		return edges;
	}

	const leading = countLeadingGuards(container, context.environment.guardRoots);

	for (const stanza of readCheckedStanzas(container, context.environment, context.valueReferences)) {
		if (stanza.start > 0 && container.gaps[stanza.start - 1]?.hasComment === false) {
			edges.add(stanza.start - 1);
		}

		const next = container.units[stanza.lastCheck + 1];
		const openingOwner = container.kind === 'block' && context.isEnabled('separate-guard-clauses') && stanza.lastCheck + 1 === leading;

		if (!stanza.onlyAssertions && next !== undefined && !openingOwner) {
			edges.add(stanza.lastCheck);

			if (isJump(next.node) && context.isEnabled('separate-jump-statements')) {
				jumpEdges.add(stanza.lastCheck);
			}
		}
	}

	const kept = new Set(container.gaps.flatMap((gap, index) => (gap.isBlank && context.isKept(index) ? [index] : [])));

	const jumpAsks = readAskingJumps(
		container,

		(index) =>
			!jumpEdges.has(index) &&
			((container.gaps[index]?.isBlank === true && !kept.has(index)) || context.isClaimedByOthers(index) || edges.has(index)),

		kept,
	);

	for (const index of jumpEdges) {
		if (jumpAsks[index + 1] === true) {
			edges.delete(index);
		}
	}

	return edges;
}
