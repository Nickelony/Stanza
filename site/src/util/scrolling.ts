import { stacked } from './breakpoints.ts';

/** Below the width where the panes stack, the preview sits under the list, so a tap on a row brings it into view. */
export function scrollToPreview(): void {
	if (!window.matchMedia(stacked).matches) {
		return;
	}

	const reduced = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

	document.getElementById('preview')?.scrollIntoView({ block: 'start', behavior: reduced ? 'auto' : 'smooth' });
}

/** Runs the callback when the layout changes between stacked and side by side. */
export function onStackedChange(callback: (stackedNow: boolean) => void): () => void {
	const query = window.matchMedia(stacked);
	const listener = (): void => callback(query.matches);

	query.addEventListener('change', listener);

	return () => query.removeEventListener('change', listener);
}
