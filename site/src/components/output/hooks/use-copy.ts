import { useRef, useState } from 'preact/hooks';

import { copyText } from '../../../util/clipboard.ts';

/** A copy button's status and action: the word it shows lands beside the block it copied, and goes after a moment. */
export function useCopy(text: string): { message: string; copy(): Promise<void> } {
	const [message, setMessage] = useState('');
	const timer = useRef<number | undefined>(undefined);

	const copy = async (): Promise<void> => {
		setMessage((await copyText(text)) ? 'Copied' : 'Copy failed; select the text instead');

		window.clearTimeout(timer.current);
		timer.current = window.setTimeout(() => setMessage(''), 2500);
	};

	return { message, copy };
}
