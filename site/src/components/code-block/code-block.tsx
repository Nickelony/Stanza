import { memo } from 'preact/compat';

import { lineText } from '../../util/line-text.ts';
import { trusted } from '../../util/trusted-html.ts';

import { BlankNote, Gutter, Line, MarkNote, Region, Source } from './parts.ts';
import type { CodeBlockProps } from './types.ts';

const none: ReadonlySet<number> = new Set();

/** Draws a snippet with a gutter, and marks the lines the other side does not have. */
export const CodeBlock = memo(function CodeBlock({
	snippet,
	label,
	marked = none,
	mark = 'add',
	plain = false,
	dimmed = false,
	current = false,
}: CodeBlockProps) {
	const word = mark === 'add' ? 'added' : 'removed';

	return (
		<Region tabIndex={0} role="region" aria-label={label} $dimmed={dimmed} $current={current}>
			{snippet.lines.map((line, index) => {
				const changed = marked.has(index);
				const blank = changed && lineText(line.html).trim() === '';

				return (
					<Line key={index} $mark={changed ? mark : null} $plain={plain}>
						{!plain && (
							<Gutter aria-hidden="true" $mark={changed ? mark : null}>
								{changed ? (mark === 'add' ? '+' : '−') : ''}
							</Gutter>
						)}

						{changed && <MarkNote>{word}: </MarkNote>}

						{blank ? (
							<Source>
								<BlankNote>blank line</BlankNote>
							</Source>
						) : (
							<Source dangerouslySetInnerHTML={trusted(line.html)} />
						)}
					</Line>
				);
			})}
		</Region>
	);
});
