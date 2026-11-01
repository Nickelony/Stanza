import type { TSESLint } from '@typescript-eslint/utils';

import { readRuns, type Runs } from './classify.ts';
import { collectContainers } from './containers.ts';
import { readSharedSettings, type Environment } from './environment.ts';
import { readOncePerContainer } from './memo.ts';
import { addOrder, pairRuleName, removeOrder, type AddRuleName, type RemoveRuleName, type RuleName } from './ruleNames.ts';
import { holdsTwoSingleLineUnits } from './twoUnits.ts';
import type { Container, Gap, Unit } from './types.ts';

type SourceCode = Readonly<TSESLint.SourceCode>;

/** What the analysis reads about a file once, for every rule to share. */
interface Reading {
	readonly environment: Environment;
	readonly runsOf: (container: Container) => Runs;

	/** The runs of a container whose members are read with their modifiers, as `separate-modifier-groups` reads them. */
	readonly modifierRunsOf: (container: Container) => Runs;
}

export interface ClaimContext extends Reading {
	/** Tells whether another enabled rule asks for a blank line at the gap, which only a rule that `readsOthers` may call. */
	readonly isClaimedByOthers: (gapIndex: number) => boolean;

	/** Tells whether a rule is enabled, so the separating half of a pair can yield the gaps to the tight half. */
	readonly isEnabled: (name: RuleName) => boolean;
}

/** Tells whether a rule asks for a blank line at the gap between `units[gapIndex]` and `units[gapIndex + 1]`. */
export type Claim = (container: Container, gapIndex: number, context: ClaimContext) => boolean;

/** What a rule that removes blank lines reads about the container and about the rules that add them. */
export interface RemovalContext extends Reading {
	/** Tells whether an enabled rule asks for a blank line at the gap, so removing the one that stands there would only be undone. */
	readonly isClaimed: (gapIndex: number) => boolean;

	/** Tells whether a rule is enabled, so a rule that removes blank lines reads the runs of the rules that are on and no others. */
	readonly isEnabled: (name: RuleName) => boolean;
}

/** Reads the gaps whose blank line a rule removes, as groups of gap indexes that one fix removes together. */
export type Removal = (container: Container, context: RemovalContext) => readonly (readonly number[])[];

export interface Claimant {
	readonly name: RuleName;
	readonly claim?: Claim;

	/** The reading of a rule that removes blank lines instead of adding them. */
	readonly removal?: Removal;

	/** True when the rule reads the claims of the other rules, so it is evaluated after them. */
	readonly readsOthers?: boolean;
}

/** A gap a rule owns and reports. */
export interface OwnedGap {
	readonly container: Container;
	readonly gap: Gap;
	readonly next: Unit;

	/** The gaps whose blank lines one fix removes together, for a rule that removes the blank lines of a whole run. */
	readonly group?: readonly Gap[];
}

/** Every rule of one lint run of a file registers here, so the analysis reads the file once and each gap has one owner. */
class Session {
	private readonly claimants = new Map<RuleName, Claimant>();
	private readonly ruleIds = new Set<string>();
	private readonly sharedSettings: ReturnType<typeof readSharedSettings>;

	private owned: Map<RuleName, OwnedGap[]> | undefined;

	private readonly sourceCode: SourceCode;

	public constructor(sourceCode: SourceCode, settings: unknown) {
		this.sourceCode = sourceCode;
		this.sharedSettings = readSharedSettings(settings);
	}

	public hasRule(ruleId: string): boolean {
		return this.ruleIds.has(ruleId);
	}

	public register(ruleId: string, claimant: Claimant): void {
		this.ruleIds.add(ruleId);
		this.claimants.set(claimant.name, claimant);
	}

	public getOwnedGaps(name: RuleName): readonly OwnedGap[] {
		this.owned ??= this.analyze();
		return this.owned.get(name) ?? [];
	}

	private analyze(): Map<RuleName, OwnedGap[]> {
		const environment: Environment = {
			...this.sharedSettings,
			awaitEnabled: this.claimants.has('separate-await-runs'),
		};

		const reading: Reading = {
			environment,
			runsOf: readOncePerContainer((container: Container) => readRuns(container, environment)),
			modifierRunsOf: readOncePerContainer((container: Container) => readRuns(container, environment, true)),
		};

		const enabled = addOrder.filter((name) => this.claimants.has(name));
		const removers = removeOrder.filter((name) => this.claimants.has(name));
		const owned = new Map<RuleName, OwnedGap[]>();

		const add = (name: RuleName, container: Container, gap: Gap, next: Unit, group?: readonly Gap[]): void => {
			const list = owned.get(name) ?? [];

			list.push({ container, gap, next, ...(group === undefined ? {} : { group }) });
			owned.set(name, list);
		};

		const includeLists = this.claimants.has('separate-multi-line-elements') || this.claimants.has('keep-single-line-elements-together');

		for (const container of collectContainers(this.sourceCode, includeLists)) {
			if (holdsTwoSingleLineUnits(container)) {
				const [gap] = container.gaps;
				const [, second] = container.units;

				if (this.claimants.has(pairRuleName) && gap?.isBlank && second !== undefined) {
					add(pairRuleName, container, gap, second);
				}

				continue;
			}

			const claimed = this.readClaims(container, enabled, reading);

			container.gaps.forEach((gap, index) => {
				const owner = enabled.find((name) => claimed.get(name)?.[index]);
				const next = container.units[index + 1];

				if (owner !== undefined && next !== undefined && !gap.isBlank && !gap.isJoined) {
					add(owner, container, gap, next);
				}
			});

			this.readRemovals(container, removers, reading, (gapIndex) => enabled.some((name) => claimed.get(name)?.[gapIndex] === true), add);
		}

		return owned;
	}

	/** Gives each blank line a rule removes to the first rule of the remove order that asks for it, with the gaps its fix removes together. */
	private readRemovals(
		container: Container,
		removers: readonly RemoveRuleName[],
		reading: Reading,
		isClaimed: (gapIndex: number) => boolean,
		add: (name: RuleName, container: Container, gap: Gap, next: Unit, group?: readonly Gap[]) => void,
	): void {
		const context: RemovalContext = { ...reading, isClaimed, isEnabled: (name) => this.claimants.has(name) };
		const reported = new Set<Gap>();

		for (const name of removers) {
			for (const group of this.claimants.get(name)?.removal?.(container, context) ?? []) {
				const gaps = group.flatMap((index) => container.gaps[index] ?? []);

				for (const index of group) {
					const gap = container.gaps[index];
					const next = container.units[index + 1];

					if (gap !== undefined && next !== undefined && !reported.has(gap)) {
						reported.add(gap);
						add(name, container, gap, next, gaps);
					}
				}
			}
		}
	}

	private readClaims(container: Container, enabled: readonly AddRuleName[], reading: Reading): Map<AddRuleName, boolean[]> {
		const claimed = new Map<AddRuleName, boolean[]>();
		const union = container.gaps.map(() => false);
		const independent = enabled.filter((name) => !this.claimants.get(name)?.readsOthers);
		const dependent = enabled.filter((name) => this.claimants.get(name)?.readsOthers);

		for (const name of [...independent, ...dependent]) {
			const claim = this.claimants.get(name)?.claim;

			const context: ClaimContext = {
				...reading,
				isClaimedByOthers: (gapIndex) => union[gapIndex] === true,
				isEnabled: (rule) => this.claimants.has(rule),
			};

			const flags = container.gaps.map((_, index) => claim?.(container, index, context) === true);

			claimed.set(name, flags);

			if (!this.claimants.get(name)?.readsOthers) {
				flags.forEach((flag, index) => {
					union[index] = union[index] === true || flag;
				});
			}
		}

		return claimed;
	}
}

const sessions = new WeakMap<object, Session>();

/** Registers a rule for the file being linted, and starts a new session when the rule has registered for this file before. */
export function registerClaimant(sourceCode: SourceCode, ruleId: string, settings: unknown, claimant: Claimant): Session {
	let session = sessions.get(sourceCode);

	if (session === undefined || session.hasRule(ruleId)) {
		session = new Session(sourceCode, settings);
		sessions.set(sourceCode, session);
	}

	session.register(ruleId, claimant);

	return session;
}

export type { Session };
