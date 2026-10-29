import { resolve } from 'node:path';
import { defineConfig } from 'vitest/config';
import { stanzaCatalog } from './build/plugin.ts';
import { stanzaPreview } from './build/preview.ts';

// The site is served from https://nickelony.github.io/Stanza/, so every asset path starts with /Stanza/.
export default defineConfig({
	base: '/Stanza/',
	plugins: [stanzaCatalog(resolve(import.meta.dirname, '..')), stanzaPreview(resolve(import.meta.dirname, '..'))],

	build: {
		target: 'es2022',
		sourcemap: false,
	},

	test: {
		include: ['test/**/*.test.ts'],
		environment: 'node',
	},
});
