import type { ContainerKind } from './types.ts';

/** The options of the rules about lists, each switching the lists of one kind on or off. */
export interface ListOptions {
	/** Judges the members of an object literal. */
	objectMembers: boolean;

	/** Judges the elements of an array literal. */
	arrayElements: boolean;

	/** Judges the members of an enum. */
	enumMembers: boolean;

	/** Judges the arguments of a call and of `new`. */
	callArguments: boolean;

	/** Judges the parameters of a function, a method, a constructor and a function type. */
	parameters: boolean;
}

/** The container kind each list option switches. */
const listOptionKinds: Readonly<Record<keyof ListOptions, ContainerKind>> = {
	objectMembers: 'object',
	arrayElements: 'array',
	enumMembers: 'enum',
	callArguments: 'arguments',
	parameters: 'parameters',
};

/** Tells whether the options switch on the lists of a container kind. */
export function judgesList(options: ListOptions, kind: ContainerKind): boolean {
	return Object.entries(listOptionKinds).some(([option, listKind]) => listKind === kind && options[option as keyof ListOptions]);
}

export const listOptionsSchema = {
	type: 'object',

	properties: {
		objectMembers: { type: 'boolean' },
		arrayElements: { type: 'boolean' },
		enumMembers: { type: 'boolean' },
		callArguments: { type: 'boolean' },
		parameters: { type: 'boolean' },
	},

	additionalProperties: false,
} as const;
