import { csharp } from './csharp.ts';
import { javascript } from './javascript.ts';
import type { Language } from './language.ts';
import { markup } from './markup.ts';
import { python } from './python.ts';

/** Every language with a page, in the order the header lists them. */
export const languages: readonly Language[] = [csharp, markup, javascript, python];
