import type { TSESTree } from '@typescript-eslint/utils';

import { isImport } from '../analysis/syntax.ts';
import { createGapRule } from '../createGapRule.ts';

export interface ImportGroupOptions {
	/** Separates `import type` declarations from the value imports beside them. */
	typeImports: boolean;
}

type ImportKind = 'value' | 'type' | 'side-effect';

/** Reads the kind of an import: one that binds nothing is a side-effect import, and `import type` is a kind of its own only when asked for. */
function readImportKind(node: TSESTree.Node, separatesTypes: boolean): ImportKind {
	if (node.type !== 'ImportDeclaration' && node.type !== 'TSImportEqualsDeclaration') {
		return 'value';
	}

	if (node.importKind === 'type') {
		return separatesTypes ? 'type' : 'value';
	}

	return node.type === 'ImportDeclaration' && node.specifiers.length === 0 ? 'side-effect' : 'value';
}

export default createGapRule<[ImportGroupOptions]>({
	name: 'separate-import-groups',
	description: 'Separate adjacent imports of different kinds with a blank line.',
	message: 'Add a blank line between these imports of different kinds.',

	schema: [
		{
			type: 'object',
			properties: { typeImports: { type: 'boolean' } },
			additionalProperties: false,
		},
	],

	defaultOptions: [{ typeImports: false }],

	createClaim:
		([options]) =>
		(container, gapIndex) => {
			const previous = container.units[gapIndex]?.node;
			const next = container.units[gapIndex + 1]?.node;

			if ((container.kind !== 'program' && container.kind !== 'module-block') || previous === undefined || next === undefined) {
				return false;
			}

			return isImport(previous) && isImport(next) && readImportKind(previous, options.typeImports) !== readImportKind(next, options.typeImports);
		},
});
