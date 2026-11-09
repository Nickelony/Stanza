import { fileURLToPath, URL } from 'node:url';

import prettier from 'prettier';
import stylelint from 'stylelint';

import configuration from '../index.mjs';

/** Lints a source text as the file `codeFilename` with the config of the package, and writes the fixes when `fix` is set. */
export async function check(code, fix = false, codeFilename = 'src/example.tsx') {
	return stylelint.lint({ code, codeFilename, config: configuration, fix });
}

/** The Prettier options of the package, which the interplay tests format with. */
export async function resolvePrettierOptions() {
	return prettier.resolveConfig(fileURLToPath(new URL('../package.json', import.meta.url)));
}
