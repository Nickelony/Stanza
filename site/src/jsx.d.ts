import type { JSX as PreactJSX } from 'preact';

declare global {
	// Emotion types its tags from the global JSX namespace, which TypeScript leaves empty under jsxImportSource.
	namespace JSX {
		interface IntrinsicElements extends PreactJSX.IntrinsicElements {}
	}
}
