import { resolve } from 'node:path';

import { defineConfig } from 'vitest/config';

import { stanzaCatalog } from './build/plugin.ts';
import { stanzaPreview } from './build/preview.ts';
import { stanzaPages } from './build/render/pages.ts';

// The site is served from https://nickelony.github.io/Stanza/, so every asset path starts with /Stanza/.
export default defineConfig({
	base: '/Stanza/',

	// Emotion is written for React, and preact/compat answers its imports.
	// Without the module condition the test run loads the CommonJS builds of Emotion, which import the React in node_modules instead of the alias.
	resolve: {
		alias: { react: 'preact/compat', 'react-dom': 'preact/compat' },
		conditions: ['module', 'browser', 'development|production'],
	},

	plugins: [stanzaPages(import.meta.dirname), stanzaCatalog(resolve(import.meta.dirname, '..')), stanzaPreview(resolve(import.meta.dirname, '..'))],

	build: {
		target: 'es2022',
		sourcemap: false,

		// The policy of each page loads images from the site alone, so an asset stays a file and never becomes a data: URI.
		assetsInlineLimit: 0,

		// The catalog of the C# page is the largest chunk, about 370 kB, and it loads once on a static page, so splitting it gains nothing; the limit leaves headroom to 700 kB.
		chunkSizeWarningLimit: 700,
	},

	test: {
		include: ['test/**/*.test.{ts,tsx}'],
		environment: 'node',
	},
});
