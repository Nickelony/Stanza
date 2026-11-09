import { catalog, tokenCss } from 'virtual:stanza-catalog/js';

import { javascript } from '../languages/javascript.ts';

import { startPage } from './start.tsx';

startPage(javascript, catalog, tokenCss);
