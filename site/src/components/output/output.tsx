import { useLanguage } from '../../hooks/use-page.ts';

import { OutputSection } from './output-section.tsx';
import { Box } from './parts.ts';

/** The output of the language: the blocks of text its configuration writes, each with its Copy button. */
export function Output() {
	const language = useLanguage();

	return (
		<Box aria-label="Output">
			{language.outputs.map((block, index) => (
				<OutputSection key={block.title} block={block} index={index} />
			))}
		</Box>
	);
}
