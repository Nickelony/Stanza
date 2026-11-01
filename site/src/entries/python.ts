import { catalog, tokenCss } from 'virtual:stanza-catalog/python';

import { python } from '../languages/python.ts';

import { startPage } from './start.tsx';

startPage(python, catalog, tokenCss);
