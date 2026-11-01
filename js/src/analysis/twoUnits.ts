import { memberKinds, statementKinds } from './kinds.ts';
import { isDirective, isImport, unwrapExport } from './syntax.ts';
import type { Container, Unit } from './types.ts';

/** Tells whether a container holds exactly two single-line units, with nothing between them but whitespace. */
export function holdsTwoSingleLineUnits(container: Container): boolean {
	const [first, second] = container.units;
	const [gap] = container.gaps;

	if (
		!(statementKinds.has(container.kind) || memberKinds.has(container.kind)) ||
		container.units.length !== 2 ||
		first === undefined ||
		second === undefined ||
		gap === undefined
	) {
		return false;
	}

	if (first.isMultiLine || second.isMultiLine || gap.hasComment || gap.isJoined) {
		return false;
	}

	return !container.units.some((unit) => unit.isShebang || isDirective(unit.node) || isImport(unit.node) || isFunctionDeclaration(unit));
}

function isFunctionDeclaration(unit: Unit): boolean {
	const target = unwrapExport(unit.node);
	return target.type === 'FunctionDeclaration' || target.type === 'TSDeclareFunction';
}
