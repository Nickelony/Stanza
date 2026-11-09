import type { ImportResult } from '../../editorconfig/import.ts';

/** The file the visitor imported last, and what the import made of it. */
export interface ImportedFile {
	name: string;
	result: ImportResult;
}
