import { extname } from 'node:path';

import ts from 'typescript';

/** The packages whose `keyframes` export is the Emotion factory. */
const keyframesPackages = new Set(['@emotion/react', '@emotion/css']);

/**
 * Parses a source text and binds its names, so a tag can be traced to the import it came from.
 * Nothing else is checked, and no other file is read: the program holds the one file, without the standard library.
 */
export function createBindings(text, fileName) {
	const extension = extname(fileName ?? '') || '.ts';
	const virtualName = `/stanza-input${extension}`;
	const sourceFile = ts.createSourceFile(virtualName, text, ts.ScriptTarget.Latest, true);
	const options = { noLib: true, noResolve: true, allowJs: true, checkJs: false, target: ts.ScriptTarget.Latest, jsx: ts.JsxEmit.Preserve };

	const host = {
		getSourceFile: (name) => (name === virtualName ? sourceFile : undefined),
		getDefaultLibFileName: () => '/lib.d.ts',
		writeFile: () => {},
		getCurrentDirectory: () => '/',
		getCanonicalFileName: (name) => name,
		useCaseSensitiveFileNames: () => true,
		getNewLine: () => '\n',
		fileExists: (name) => name === virtualName,
		readFile: () => undefined,
	};

	const program = ts.createProgram({ rootNames: [virtualName], options, host });

	return { sourceFile, checker: program.getTypeChecker() };
}

/** Recognises an identifier that the import of `keyframes` from Emotion binds, so a shadowing name is skipped. */
export function isEmotionKeyframes(identifier, checker) {
	const declarations = checker.getSymbolAtLocation(identifier)?.declarations ?? [];

	return declarations.some(
		(declaration) =>
			ts.isImportSpecifier(declaration) &&
			(declaration.propertyName ?? declaration.name).text === 'keyframes' &&
			keyframesPackages.has(declaration.parent.parent.parent.moduleSpecifier.text),
	);
}
