import { useLayoutEffect, useRef } from 'preact/hooks';

import { useAppState } from '../../hooks/use-app-state.ts';
import { useCatalog } from '../../hooks/use-page.ts';
import { optionId } from '../../state/model.ts';

import { OptionPreview } from './option-preview.tsx';
import { RulePreview } from './rule-preview.tsx';

import { Page, Pane, Placeholder } from './parts/layout.ts';

/** The pane of the preview: what the selected rule or option does, and the code before and after. */
export function Preview() {
	const catalog = useCatalog();
	const { selection } = useAppState();
	const pane = useRef<HTMLElement>(null);
	const rule = selection?.kind === 'rule' ? catalog.rules.find((candidate) => candidate.id === selection.id) : undefined;
	const option = selection?.kind === 'option' ? catalog.options.find((candidate) => optionId(candidate) === selection.id) : undefined;
	const chosen = rule ? `rule:${rule.id}` : option ? `option:${optionId(option)}` : 'none';

	// A new page starts at its top, but a new variant of the same page keeps the position the visitor read to.
	useLayoutEffect(() => {
		pane.current?.scrollTo(0, 0);
	}, [chosen]);

	return (
		<Pane ref={pane} aria-label="Preview">
			<Page id="preview">
				{rule ? (
					<RulePreview key={chosen} rule={rule} />
				) : option ? (
					<OptionPreview key={chosen} option={option} />
				) : (
					<Placeholder>Select a rule or an option to see what it does. The preview shows the code before and after the rule runs.</Placeholder>
				)}
			</Page>
		</Pane>
	);
}
