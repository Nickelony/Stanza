import { memo } from 'preact/compat';
import { useMemo } from 'preact/hooks';

import type { Syntax } from '../../languages/language.ts';
import { colorize } from '../../writers/colorize.ts';

import { Colored } from './parts.ts';

/** The generated text with its comments, sections and values colored. */
export const ColoredText = memo(function ColoredText({ syntax, text }: { syntax: Syntax; text: string }) {
	const lines = useMemo(() => colorize(syntax, text), [syntax, text]);

	return (
		<>
			{lines.map((line, index) =>
				syntax === 'plain' ? (
					line.tokens[0]!.text
				) : (
					<Colored key={index} $kind={line.kind}>
						{line.tokens.map((token, tokenIndex) =>
							token.kind === null ? (
								token.text
							) : (
								<Colored key={tokenIndex} $kind={token.kind}>
									{token.text}
								</Colored>
							),
						)}

						{'\n'}
					</Colored>
				),
			)}
		</>
	);
});
