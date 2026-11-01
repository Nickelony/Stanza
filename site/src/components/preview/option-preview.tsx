import { memo } from 'preact/compat';

import { useAppState } from '../../hooks/use-app-state.ts';
import { usePage } from '../../hooks/use-page.ts';
import { optionValueAt } from '../../state/config.ts';
import { optionId, type Option, type Snippet } from '../../state/model.ts';
import { statusSentence } from '../../state/status.ts';
import { groupParts } from '../../util/group-parts.ts';
import { CodeBlock } from '../code-block/code-block.tsx';

import { CodeFigure, Description, Notes, PageHead, PageLink, RelatedChips, StatusBox, ValueTable } from './sections.tsx';
import type { BodyProps } from './types.ts';

import { Example, Segments } from './parts/examples.ts';

/** The page of an option: its heading and description, what the form says of it, and its examples, value table and notes in the order the page writes them. */
export function OptionPreview({ option }: { option: Option<Snippet> }) {
	const { catalog, language } = usePage();
	const { config } = useAppState();
	const value = config.options[optionId(option)];
	const line = language.optionLines(catalog, option, config).join('\n') || null;

	return (
		<>
			<PageHead id={optionId(option)} title={null} tier={`Default: ${option.defaultText}`} />
			<Description markup={option.descriptionHtml} />
			<Description markup={option.readByHtml} />
			<StatusBox text={statusSentence(option, config, line)} line={line ?? 'No line is written for this key.'} />
			<OptionBody option={option} effective={optionValueAt(option, config)} explicit={value !== undefined} />
		</>
	);
}

const OptionBody = memo(function OptionBody({ option, effective, explicit }: BodyProps) {
	return (
		<>
			{groupParts(option.parts).map((part, index) => {
				if (part.kind === 'notes') {
					return <Notes key={index} blocks={part.blocks} />;
				}

				if (part.kind === 'values') {
					return <ValueTable key={index} option={option} effective={effective} />;
				}

				return (
					<Example key={index}>
						<Segments>
							{option.examples[part.index]!.segments.map((segment, segmentIndex) => (
								<CodeFigure key={segmentIndex} caption={segment.label || 'Example'} detail="">
									<CodeBlock
										snippet={segment.snippet}
										label={segment.label || 'Example'}
										plain
										current={(effective !== null && segment.value === effective) || (!explicit && segment.isDefault)}
									/>
								</CodeFigure>
							))}
						</Segments>
					</Example>
				);
			})}

			<RelatedChips label="Rules" ids={option.rules} kind="rule" />
			<PageLink url={option.pageUrl} name={optionId(option)} />
		</>
	);
});
