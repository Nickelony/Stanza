import type { ComponentChildren } from 'preact';

import { useLanguage, usePage } from '../../hooks/use-page.ts';
import { followLink } from '../../state/follow-link.ts';
import { optionId, type Block, type Option, type Snippet } from '../../state/model.ts';
import type { Selection } from '../../state/selection.ts';
import { trusted } from '../../util/trusted-html.ts';

import type { VariantButtonsProps } from './types.ts';

import { Cap, Caption, Figure, Variant, Variants } from './parts/examples.ts';
import { Box, Head, Heading, Id, Paragraph, StatusLine, StatusText, Tier } from './parts/header.ts';
import { Chip, OpenPage, Related, RelatedLabel } from './parts/related.ts';
import { NotesBox, Table, TableWrap, ValueRow } from './parts/table.ts';

/** The heading of a page in the preview: its ID or key, its title when it has one, and the line under them. */
export function PageHead({ id, title, tier }: { id: string; title: string | null; tier: string }) {
	return (
		<Head>
			<Heading tabIndex={-1}>
				<Id>{id}</Id>
				{title !== null && [' ', title]}
			</Heading>

			<Tier>{tier}</Tier>
		</Head>
	);
}

/** A paragraph of the page as trusted HTML. */
export function Description({ markup }: { markup: string }) {
	return <Paragraph dangerouslySetInnerHTML={trusted(markup)} />;
}

/** What the form says of the selection, and the line it writes. It is no live region, because a move down the list rewrites the text too and the control of a row announces its own change. */
export function StatusBox({ text, line }: { text: string; line: string }) {
	return (
		<Box>
			<StatusText>{text}</StatusText>
			<StatusLine>{line}</StatusLine>
		</Box>
	);
}

/** A code block with its caption: the name of the side, and what sets it apart. */
export function CodeFigure({ caption, detail, children }: { caption: string; detail: string; children: ComponentChildren }) {
	return (
		<Figure>
			<Caption>
				<Cap>{caption}</Cap>
				{detail !== '' && ` - ${detail}`}
			</Caption>

			{children}
		</Figure>
	);
}

/** The buttons that pick which After code the example shows. */
export function VariantButtons({ labels, shown, onPick }: VariantButtonsProps) {
	return (
		<Variants role="group" aria-label="Variant of the After code">
			{labels.map((label, index) => (
				<Variant key={index} type="button" title={label} aria-pressed={index === shown} onClick={() => onPick(index)}>
					{`Variant ${index + 1}`}
				</Variant>
			))}
		</Variants>
	);
}

/** The paragraphs, lists and tables a page writes between its examples, as one run. */
export function Notes({ blocks }: { blocks: readonly Block[] }) {
	return (
		<NotesBox>
			{blocks.map((block, index) => {
				if (block.kind === 'p') {
					return <p key={index} dangerouslySetInnerHTML={trusted(block.html)} />;
				}

				if (block.kind === 'ul') {
					return (
						<ul key={index}>
							{block.items.map((item, itemIndex) => (
								<li key={itemIndex} dangerouslySetInnerHTML={trusted(item)} />
							))}
						</ul>
					);
				}

				return (
					<TableWrap key={index}>
						<Table>
							<thead>
								<tr>
									{block.head.map((cell, cellIndex) => (
										<th key={cellIndex} dangerouslySetInnerHTML={trusted(cell)} />
									))}
								</tr>
							</thead>

							<tbody>
								{block.rows.map((row, rowIndex) => (
									<tr key={rowIndex}>
										{row.map((cell, cellIndex) => (
											<td key={cellIndex} dangerouslySetInnerHTML={trusted(cell)} />
										))}
									</tr>
								))}
							</tbody>
						</Table>
					</TableWrap>
				);
			})}
		</NotesBox>
	);
}

/** The table of the values an option takes, with the row in force marked. */
export function ValueTable({ option, effective }: { option: Option<Snippet>; effective: string | null }) {
	return (
		<TableWrap>
			<Table>
				<thead>
					<tr>
						<th>Value</th>
						<th>Effect</th>
					</tr>
				</thead>

				<tbody>
					{(option.csharp?.values ?? []).map((value, index) => (
						<ValueRow key={index} $active={effective !== null && value.literals.includes(effective)}>
							<td dangerouslySetInnerHTML={trusted(value.valueHtml)} />
							<td dangerouslySetInnerHTML={trusted(value.effectHtml)} />
						</ValueRow>
					))}
				</tbody>
			</Table>
		</TableWrap>
	);
}

/** The rules or the options a page names, as buttons that select them; an option reads as its key, because the page that names it is its rule's. */
export function RelatedChips({ label, ids, kind }: { label: string; ids: readonly string[]; kind: Selection['kind'] }) {
	const { catalog, store } = usePage();

	if (ids.length === 0) {
		return null;
	}

	const nameOf = (id: string): string => (kind === 'option' ? (catalog.options.find((option) => optionId(option) === id)?.key ?? id) : id);

	return (
		<Related>
			<RelatedLabel>{`${label} `}</RelatedLabel>

			{ids.map((id) => (
				<Chip key={id} type="button" onClick={() => followLink(catalog, store, { kind, id })}>
					{nameOf(id)}
				</Chip>
			))}
		</Related>
	);
}

/** The link to the full page of the rule or the option, on the host the language keeps its pages on. */
export function PageLink({ url, name }: { url: string; name: string }) {
	const language = useLanguage();

	return (
		<OpenPage>
			<a href={url} target="_blank" rel="noopener">{`Open ${name} ${language.pageHost}`}</a>
		</OpenPage>
	);
}
