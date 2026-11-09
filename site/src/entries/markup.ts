import { catalog, tokenCss } from 'virtual:stanza-catalog/markup';

import { markup } from '../languages/markup.ts';

import { startPage } from './start.tsx';

startPage(markup, catalog, tokenCss);
