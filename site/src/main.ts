import { catalog, tokenCss } from 'virtual:stanza-catalog';
import { mountApp } from './app.ts';
import './style.css';

const style = document.createElement('style');

style.textContent = tokenCss;
document.head.append(style);

const root = document.getElementById('app');

if (!root) {
	throw new Error('The page has no #app element.');
}

mountApp(root, catalog);
