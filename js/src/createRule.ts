import type { TSESLint } from '@typescript-eslint/utils';
import type { Rule } from 'eslint';

import type { StanzaRule } from './stanzaRule.ts';

import type { RuleName } from './analysis/ruleNames.ts';
import type { OwnedGap, Session } from './analysis/session.ts';

interface RuleDefinition<Options extends readonly unknown[], MessageId extends string> {
	readonly name: string;
	readonly meta: Omit<TSESLint.RuleMetaData<MessageId, unknown, Options>, 'docs'> & { readonly docs: { readonly description: string } };
	readonly create: (context: Readonly<TSESLint.RuleContext<MessageId, Options>>, options: Readonly<Options>) => TSESLint.RuleListener;
}

function isObjectNotArray(value: unknown): value is Record<string, unknown> {
	return typeof value === 'object' && value !== null && !Array.isArray(value);
}

/** Merges two objects deeply into a new one, the second winning where both hold a value. */
function deepMerge(first: Record<string, unknown>, second: Record<string, unknown>): Record<string, unknown> {
	const merged: Record<string, unknown> = {};

	for (const key of new Set([...Object.keys(first), ...Object.keys(second)])) {
		const firstValue = first[key];
		const secondValue = second[key];

		if (!(key in second)) {
			merged[key] = firstValue;
		} else if (key in first && isObjectNotArray(firstValue) && isObjectNotArray(secondValue)) {
			merged[key] = deepMerge(firstValue, secondValue);
		} else {
			merged[key] = secondValue;
		}
	}

	return merged;
}

/** Overlays the options a user gave on the defaults, one by one, and gives back one option for each default. */
function applyDefaults<Options extends readonly unknown[]>(defaults: Readonly<Options>, given: readonly unknown[] | undefined): Options {
	const options: unknown[] = structuredClone([...defaults]);

	if (given === undefined) {
		return options as unknown as Options;
	}

	for (const [index, option] of options.entries()) {
		const userOption = given[index];

		if (userOption === undefined) {
			continue;
		}

		options[index] = isObjectNotArray(option) && isObjectNotArray(userOption) ? deepMerge(option, userOption) : userOption;
	}

	return options as unknown as Options;
}

/** Creates a rule whose options are merged into the defaults of its meta and whose meta links the page of the rule. */
export function createRule<Options extends readonly unknown[], MessageId extends string>(definition: RuleDefinition<Options, MessageId>): StanzaRule {
	const { name, meta, create } = definition;
	const defaults = (meta.defaultOptions ?? []) as Readonly<Options>;

	return {
		meta: { ...meta, docs: { ...meta.docs, url: `https://github.com/Nickelony/Stanza/blob/main/js/docs/rules/${name}.md` } } as Rule.RuleMetaData,

		create(context) {
			const typedContext = context as unknown as Readonly<TSESLint.RuleContext<MessageId, Options>>;
			return create(typedContext, applyDefaults(defaults, context.options)) as Rule.RuleListener;
		},
	};
}

/** Creates a listener that reports each gap the shared analysis gives the rule, at the unit below the gap. */
export function createOwnedGapListener<MessageId extends string, Options extends readonly unknown[]>(
	context: Readonly<TSESLint.RuleContext<MessageId, Options>>,
	session: Session,
	name: RuleName,
	messageId: MessageId,
	createFix: (owned: OwnedGap) => TSESLint.ReportFixFunction | null,
): TSESLint.RuleListener {
	return {
		Program() {
			for (const owned of session.getOwnedGaps(name)) {
				context.report({ node: owned.next.node, messageId, fix: createFix(owned) });
			}
		},
	};
}
