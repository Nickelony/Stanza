import { memo } from 'preact/compat';
import { useMemo } from 'preact/hooks';

import { useAppState } from '../../hooks/use-app-state.ts';
import { usePage, useStore } from '../../hooks/use-page.ts';
import type { Rule, RuleExample as RuleExampleData, Snippet } from '../../state/model.ts';
import { ruleStatus } from '../../state/status.ts';
import { diffLines } from '../../util/diff.ts';
import { groupParts } from '../../util/group-parts.ts';
import { lineText } from '../../util/line-text.ts';
import { CodeBlock } from '../code-block/code-block.tsx';

import { CodeFigure, Description, Notes, PageHead, PageLink, RelatedChips, StatusBox, VariantButtons } from './sections.tsx';
import type { ExampleProps } from './types.ts';

import { Example, Pair } from './parts/examples.ts';

/** The page of a rule: its heading and description, what the form says of it, and its examples and notes in the order the page writes them. */
export function RulePreview({ rule }: { rule: Rule<Snippet> }) {
	const { catalog, language } = usePage();
	const { config, variant } = useAppState();
	const status = ruleStatus(language, catalog, rule, config);

	return (
		<>
			<PageHead id={rule.id} title={rule.title} tier={rule.tierText} />
			<Description markup={rule.descriptionHtml} />
			<StatusBox text={status.text} line={status.line ?? 'No line is written for this rule.'} />
			<RuleBody rule={rule} variant={variant} off={status.off} />
		</>
	);
}

/** The part of the page that only a new selection, a new variant or the rule turning on or off changes. */
const RuleBody = memo(function RuleBody({ rule, variant, off }: { rule: Rule<Snippet>; variant: number; off: boolean }) {
	return (
		<>
			{groupParts(rule.parts).map((part, index) =>
				part.kind === 'notes' ? (
					<Notes key={index} blocks={part.blocks} />
				) : (
					<ExampleOfRule key={index} rule={rule} number={part.index} variant={variant} off={off} />
				),
			)}

			<RelatedChips label="Options" ids={rule.options} kind="option" />
			<PageLink url={rule.pageUrl} name={rule.id} />
		</>
	);
});

/** A page with several examples names each as a group, which is no landmark; one example needs no name. */
function groupProps(rule: Rule<Snippet>, number: number): { role?: 'group'; 'aria-label'?: string } {
	return rule.examples.length > 1 ? { role: 'group', 'aria-label': `Example ${number + 1}` } : {};
}

function ExampleOfRule({ rule, number, variant, off }: ExampleProps) {
	const example = rule.examples[number]!;

	return (
		<Example {...groupProps(rule, number)}>
			{example.unchanged ? <Unchanged example={example} /> : <BeforeAndAfter example={example} variant={variant} off={off} />}
		</Example>
	);
}

/** Code the rule leaves as written has nothing to compare, so it shows once, without marks. */
function Unchanged({ example }: { example: RuleExampleData<Snippet> }) {
	return (
		<CodeFigure caption="Unchanged" detail={example.beforeLabel}>
			<CodeBlock snippet={example.before} label="Code the rule leaves as written" />
		</CodeFigure>
	);
}

function BeforeAndAfter({ example, variant, off }: { example: RuleExampleData<Snippet>; variant: number; off: boolean }) {
	const store = useStore();
	const shown = Math.min(variant, example.afters.length - 1);
	const after = example.afters[shown]!;

	const diff = useMemo(
		() =>
			diffLines(
				example.before.lines.map((line) => lineText(line.html)),
				after.snippet.lines.map((line) => lineText(line.html)),
			),

		[example, after],
	);

	return (
		<>
			{example.afters.length > 1 && (
				<VariantButtons labels={example.afters.map((candidate) => candidate.label)} shown={shown} onPick={(index) => store.set({ variant: index })} />
			)}

			<Pair>
				<CodeFigure caption="Before" detail={example.beforeLabel}>
					<CodeBlock snippet={example.before} label="Code before" marked={diff.removed} mark="del" />
				</CodeFigure>

				<CodeFigure caption="After" detail={after.label}>
					<CodeBlock snippet={after.snippet} label="Code after" marked={diff.added} mark="add" dimmed={off} />
				</CodeFigure>
			</Pair>
		</>
	);
}
