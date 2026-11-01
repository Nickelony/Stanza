import type { Rule } from 'eslint';

/** A rule as ESLint reads it, with its meta always present. */
export type StanzaRule = Rule.RuleModule & { readonly meta: Rule.RuleMetaData };
