import csharp from '../../icons/csharp.svg';
import js from '../../icons/js.svg';
import markup from '../../icons/markup.svg';
import python from '../../icons/python.svg';
import type { LanguageId } from '../../languages/language.ts';

/** The logo of each page, which the tab shows too, from the copy `src/icons` keeps of `icon/`. */
export const logos: Record<LanguageId, string> = { csharp, markup, js, python };
