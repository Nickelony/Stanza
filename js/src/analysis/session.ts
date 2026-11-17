import type { TSESLint, TSESTree } from '@typescript-eslint/utils';

import { readSeedBuilder, readValueReferences, type ValueReferences } from './bindings.ts';
import { readRuns, type Runs } from './classify.ts';
import { collectContainers } from './containers.ts';
import { readDependentBindingGroups, type DependentBindingReader } from './dependentBindings.ts';
import { readSharedSettings, type Environment } from './environment.ts';
import { readJumpAsks } from './jumps.ts';
import { readOncePerContainer } from './memo.ts';
import { readReceiverGroups, type ReceiverGroupReader } from './receivers.ts';
import {
	addOrder,
	keepOrder,
	pairRuleName,
	removeOrder,
	type AddRuleName,
	type KeepRuleName,
	type RemoveRuleName,
	type RuleName,
} from './ruleNames.ts';
import { readSilencing, type Silencing } from './silencing.ts';
import { holdsTwoSingleLineUnits } from './twoUnits.ts';
import type { Container, Gap, Unit } from './types.ts';

type SourceCode = Readonly<TSESLint.SourceCode>;

/** The readers a keeper's protected sections come from. */
type SectionReading = Pick<Reading, 'receiverGroupsOf' | 'dependentBindingsOf'>;

/** What the analysis reads about a file once, for every rule to share. */
interface Reading {
	readonly environment: Environment;

	/** Tells whether a comment is an ESLint directive, a disable or enable comment, which a run of blank lines reads through. */
	readonly isEslintDirective: (comment: TSESTree.Comment) => boolean;
	readonly receiverGroupsOf: ReceiverGroupReader;
	readonly dependentBindingsOf: DependentBindingReader;

	/** The gaps whose authored blank line an enabled keeper protects as a section boundary, so no run reads across them. */
	readonly protectedSectionsOf: (container: Container) => ReadonlySet<number>;
	readonly buildsSeed: ReturnType<typeof readSeedBuilder>;
	readonly valueReferences: ValueReferences;
	readonly runsOf: (container: Container) => Runs;

	/** The runs of a container whose members are read with their modifiers, as `separate-modifier-groups` reads them. */
	readonly modifierRunsOf: (container: Container) => Runs;
}

export interface ClaimContext extends Reading {
	/** Tells whether a rule of the add order asks for the gap before any keeper that reads others moves it, so a keeper sees the stanza that rule opens. */
	readonly isPlannedSeparation: (gapIndex: number) => boolean;

	/** Tells whether another enabled rule asks for a blank line at the gap, which only a rule that `readsOthers` may call. */
	readonly isClaimedByOthers: (gapIndex: number) => boolean;

	/** Tells whether an enabled rule of the keep order keeps the units around the gap together, so no blank line stays there. */
	readonly isKept: (gapIndex: number) => boolean;

	/** Tells whether an enabled rule that removes the blank lines of a run holds the gap inside that run. */
	readonly isHeldByRun: (gapIndex: number) => boolean;

	/** Tells whether a rule is enabled, so the separating half of a pair can yield the gaps to the tight half. */
	readonly isEnabled: (name: RuleName) => boolean;
}

/** Tells whether a rule asks for a blank line at the gap between `units[gapIndex]` and `units[gapIndex + 1]`. */
export type Claim = (container: Container, gapIndex: number, context: ClaimContext) => boolean;

/** Reads, once for each container, the gaps a rule of the add order asks to separate. */
export type Plan = (container: Container, context: ClaimContext) => ReadonlySet<number>;

/** What a rule that removes blank lines reads about the container and about the rules that add them. */
export interface RemovalContext extends Reading {
	/** Tells whether an enabled rule asks for a blank line at the gap, so removing the one that stands there would only be undone. */
	readonly isClaimed: (gapIndex: number) => boolean;

	/** Tells whether an enabled rule of the keep order keeps the units around the gap together, so its blank line goes whatever the rule reads. */
	readonly isKept: (gapIndex: number) => boolean;

	/** Tells whether a rule is enabled, so a rule that removes blank lines reads the runs of the rules that are on and no others. */
	readonly isEnabled: (name: RuleName) => boolean;

	/** Tells whether a directive silences the rule being read at the gap, so a run ends there and no fix removes the blank line. */
	readonly isSilenced: (gapIndex: number) => boolean;
}

/** Reads the gaps whose blank line a rule removes, as groups of gap indexes that one fix removes together. */
export type Removal = (container: Container, context: RemovalContext) => readonly (readonly number[])[];

/** The gaps a rule that removes blank lines holds inside its runs, read before the keepers that read others so they know which gaps a run takes. */
export interface RunHold {
	/** Reads the held gaps; the context reports the planned separations as claimed and the gaps of the keepers that read no others as kept. */
	readonly read: (container: Container, context: RemovalContext) => Iterable<number>;

	/** True when a separation that a keeper displaces may not move onto a held gap. */
	readonly pinsBoundary: boolean;
}

export interface Claimant {
	readonly name: RuleName;

	/** For a rule of the add order, the gaps it asks to separate; for a rule of the keep order, the gaps it keeps together. */
	readonly claim?: Claim;

	/** For a rule of the add order whose gaps keepers read as planned separations, the gaps it asks to separate, read in place of `claim`. */
	readonly plan?: Plan;

	/** The reading of a rule that removes blank lines instead of adding them. */
	readonly removal?: Removal;
	readonly hold?: RunHold;

	/** For a rule that removes the blank lines of runs, the gaps it could remove were a run long enough, so it reports each one a directive silences and the directive stays used. */
	readonly reach?: (container: Container, context: RemovalContext) => Iterable<number>;

	/** True when the rule reads the claims of the other rules, so it is evaluated after them. */
	readonly readsOthers?: boolean;

	/**
	 * For a keeper whose claim also reads what stands around its pair, tells whether the pair stands at the gap.
	 * A silenced keeper reports wherever its pair stands, because the gap it hands back may change what stands around the pair.
	 */
	readonly pairs?: Claim;

	/** True when one fix removes the blank lines of a whole stretch of kept gaps, because removing one alone could leave a section boundary. */
	readonly fixTogether?: boolean;

	/** True when the rule judges a container of two single-line units while `keep-two-units-together` is off; every other rule leaves it alone. */
	readonly judgesTwoUnits?: boolean;

	/** The gaps whose authored blank line the rule protects as a section boundary while it is enabled. */
	readonly protects?: (container: Container, reading: SectionReading) => ReadonlySet<number>;
}

/** A gap a rule owns and reports. */
export interface OwnedGap {
	readonly container: Container;
	readonly gap: Gap;
	readonly next: Unit;

	/** The gaps whose blank lines one fix removes together, for a rule that removes the blank lines of a whole run. */
	readonly group?: readonly Gap[];

	/** True on the first report of a group, which carries the fix of the group at any size; `carriesGroupFix` says which of the others carry it too. */
	readonly fixesGroup?: boolean;

	/** Where a directive silences the rule at the gap, so the report stands there, only uses the directive and carries no fix. */
	readonly silencedAt?: TSESTree.Position;
}

/** The rules of one reading, each list in the order that decides which rule reports a gap. */
interface Order {
	readonly enabled: readonly AddRuleName[];
	readonly keepers: readonly KeepRuleName[];
	readonly removers: readonly RemoveRuleName[];
}

/** What the claims of one container read from the session. */
interface ClaimSource {
	readonly claimantOf: (name: RuleName) => Claimant | undefined;
	readonly isEnabled: (name: RuleName) => boolean;
	readonly silencedAt: (name: RuleName, container: Container, gapIndex: number) => TSESTree.Position | undefined;

	/** Reads the gaps where a directive silences a rule that removes blank lines. */
	readonly silencedGapsOf: (name: RuleName, container: Container) => ReadonlySet<number>;

	/** Tells whether a comment is an ESLint directive, which a silenced keeper reads through. */
	readonly isEslintDirective: (comment: TSESTree.Comment) => boolean;
}

/**
 * Reads the claims of one container in the order the rules read each other:
 * the gaps runs hold, the keepers, the planned separations, the keepers that read them, then every separation.
 * A kept gap wins: no rule of the add order sees it as asked for, so the two kinds of rule never fight over it.
 */
class ContainerClaims {
	public readonly claimed = new Map<AddRuleName, boolean[]>();
	public readonly kept = new Map<KeepRuleName, boolean[]>();

	/** Where a directive silences each keeper at a gap it claims, which the others judge as if that keeper were off. */
	public readonly silenced = new Map<KeepRuleName, (TSESTree.Position | undefined)[]>();

	private readonly keptGaps = new Set<number>();
	private readonly claimedGaps = new Set<number>();
	private readonly heldGaps = new Set<number>();
	private readonly pinnedGaps = new Set<number>();
	private readonly plannedGaps = new Set<number>();

	/** The flags each rule of the add order asked for while planning, reused so each claim is read once. */
	private readonly planned = new Map<AddRuleName, boolean[]>();
	private readonly context: ClaimContext;
	private readonly container: Container;
	private readonly source: ClaimSource;
	private readonly reading: Reading;
	private readonly order: Order;

	public constructor(container: Container, source: ClaimSource, reading: Reading, order: Order) {
		this.container = container;
		this.source = source;
		this.reading = reading;
		this.order = order;

		this.context = {
			...reading,
			isPlannedSeparation: (gapIndex) => this.plannedGaps.has(gapIndex),
			isClaimedByOthers: (gapIndex) => this.claimedGaps.has(gapIndex),
			isKept: (gapIndex) => this.keptGaps.has(gapIndex),
			isHeldByRun: (gapIndex) => this.heldGaps.has(gapIndex),
			isEnabled: source.isEnabled,
		};
	}

	public read(): this {
		this.readKeepers(false);
		this.readPlannedSeparations();
		this.readAnchoredKeepers();
		this.readSeparations();

		return this;
	}

	public isKept(gapIndex: number): boolean {
		return this.keptGaps.has(gapIndex);
	}

	public isClaimed(gapIndex: number): boolean {
		return this.order.enabled.some((name) => this.claimed.get(name)?.[gapIndex] === true);
	}

	/**
	 * Reads the keepers that read others together with the gaps runs hold, starting from every gap such a keeper could anchor on.
	 * Each round reads the holds with the anchors of the round before kept, and keeps the anchors whose stanza no run then takes.
	 * Dropping an anchor only lengthens the runs, so the anchors shrink until a round reads the same ones; the cap of one round for each gap only guards the loop.
	 */
	private readAnchoredKeepers(): void {
		const settledKept = new Set(this.keptGaps);

		// Without a keeper that reads others, the holds only pin the gaps a displaced separation may not move onto, and only a kept gap displaces one.
		if (!this.order.keepers.some((name) => this.source.claimantOf(name)?.readsOthers === true)) {
			if (settledKept.size > 0) {
				this.readHolds(settledKept);
			}

			return;
		}

		let anchored: ReadonlySet<number> | undefined;

		for (let round = 0; round <= this.container.gaps.length; round += 1) {
			this.keptGaps.clear();
			this.heldGaps.clear();
			this.pinnedGaps.clear();
			settledKept.forEach((index) => this.keptGaps.add(index));

			// The first round reads no holds, so every gap a keeper could anchor on counts.
			if (anchored !== undefined) {
				this.readHolds(new Set([...settledKept, ...anchored]));
			}

			this.readKeepers(true);

			const next = new Set([...this.keptGaps].filter((index) => !settledKept.has(index)));
			const previous = anchored;

			if (previous !== undefined && next.size === previous.size && [...next].every((index) => previous.has(index))) {
				return;
			}

			anchored = next;
		}
	}

	/**
	 * Reads the gaps runs hold where the planned separations, the jumps that ask above them and the given kept gaps leave them,
	 * as the removals will read them, so a keeper that reads others never anchors on a gap a run takes.
	 */
	private readHolds(keptGaps: ReadonlySet<number>): void {
		const jumpAsks = this.source.isEnabled('separate-jump-statements')
			? readJumpAsks(
					this.container,
					(index) => keptGaps.has(index),
					(index) => this.plannedGaps.has(index),
				)
			: [];

		const context: RemovalContext = {
			...this.reading,
			isClaimed: (gapIndex) => this.plannedGaps.has(gapIndex) || jumpAsks[gapIndex + 1] === true,
			isKept: (gapIndex) => keptGaps.has(gapIndex),
			isEnabled: this.source.isEnabled,
			isSilenced: () => false,
		};

		for (const name of this.order.removers) {
			const hold = this.source.claimantOf(name)?.hold;
			const silenced = this.source.silencedGapsOf(name, this.container);

			for (const index of hold?.read(this.container, { ...context, isSilenced: (gapIndex) => silenced.has(gapIndex) }) ?? []) {
				this.heldGaps.add(index);

				if (hold?.pinsBoundary === true) {
					this.pinnedGaps.add(index);
				}
			}
		}
	}

	private readKeepers(readsOthers: boolean): void {
		for (const name of this.order.keepers) {
			const claimant = this.source.claimantOf(name);

			if ((claimant?.readsOthers ?? false) !== readsOthers) {
				continue;
			}

			const flags = this.container.gaps.map((_, index) => claimant?.claim?.(this.container, index, this.context) === true);
			const silenced = flags.map((flag, index) => (claimant === undefined ? undefined : this.readSilencedAt(claimant, name, index, flag)));
			const kept = flags.map((flag, index) => flag && silenced[index] === undefined);

			this.kept.set(name, kept);
			this.silenced.set(name, silenced);

			kept.forEach((flag, index) => {
				if (flag) {
					this.keptGaps.add(index);
				}
			});
		}
	}

	/**
	 * Reads where a directive silences a keeper at a gap it claims, or, for a keeper that reads its pairs apart, at a gap where a pair stands.
	 * A gap that holds directives and nothing else is read as if they were not there,
	 * so the keeper still reports it and ESLint counts a directive in its own gap as used, as it counts one anywhere else.
	 */
	private readSilencedAt(claimant: Claimant, name: KeepRuleName, gapIndex: number, claimed: boolean): TSESTree.Position | undefined {
		const gap = this.container.gaps[gapIndex];
		const throughDirectives = gap?.hasComment === true && gap.comments.every(this.source.isEslintDirective);

		if (gap === undefined || (!claimed && claimant.pairs === undefined && !throughDirectives)) {
			return undefined;
		}

		const at = this.source.silencedAt(name, this.container, gapIndex);
		const container = throughDirectives ? withoutComments(this.container, gapIndex) : this.container;

		return at !== undefined && (claimed || (claimant.pairs ?? claimant.claim)?.(container, gapIndex, this.context) === true) ? at : undefined;
	}

	/** Reads where the rules that read no others ask for a gap, then the plans built on them, before a keeper that reads others moves any. */
	private readPlannedSeparations(): void {
		const planContext: ClaimContext = { ...this.context, isClaimedByOthers: (index) => this.plannedGaps.has(index) };

		for (const name of this.order.enabled) {
			const claimant = this.source.claimantOf(name);

			if (claimant?.plan === undefined && claimant?.readsOthers !== true) {
				const flags = this.container.gaps.map((_, index) => claimant?.claim?.(this.container, index, planContext) === true);

				this.planned.set(name, flags);
				this.addPlannedGaps(flags.flatMap((flag, index) => (flag ? [index] : [])));
			}
		}

		for (const name of this.order.enabled) {
			const gaps = this.source.claimantOf(name)?.plan?.(this.container, planContext);

			if (gaps !== undefined) {
				this.planned.set(
					name,
					this.container.gaps.map((_, index) => gaps.has(index)),
				);

				this.addPlannedGaps(gaps);
			}
		}
	}

	private addPlannedGaps(gaps: Iterable<number>): void {
		for (const index of gaps) {
			if (this.container.gaps[index]?.isJoined === false && !this.keptGaps.has(index)) {
				this.plannedGaps.add(index);
			}
		}
	}

	/** Reads each separation, moving one that a keeper displaces to the free gap above the kept stretch. */
	private readSeparations(): void {
		const upperEdges: number[] = [];
		let upper = -1;

		for (let index = 0; index < this.container.gaps.length; index += 1) {
			if (!this.keptGaps.has(index)) {
				upper = index;
			}

			upperEdges[index] = upper;
		}

		const readsOthers = (name: AddRuleName): boolean => this.source.claimantOf(name)?.readsOthers === true;

		for (const name of [...this.order.enabled.filter((rule) => !readsOthers(rule)), ...this.order.enabled.filter(readsOthers)]) {
			const claimant = this.source.claimantOf(name);
			const asked = this.planned.get(name) ?? this.container.gaps.map((_, index) => claimant?.claim?.(this.container, index, this.context) === true);
			const flags = this.container.gaps.map(() => false);

			asked.forEach((ask, index) => {
				const target = this.keptGaps.has(index) ? (upperEdges[index] ?? -1) : index;
				const upperGap = this.container.gaps[target];

				if (ask && target >= 0 && (target === index || (upperGap?.hasComment === false && !upperGap.isJoined && !this.pinnedGaps.has(target)))) {
					flags[target] = true;
				}
			});

			this.claimed.set(name, flags);

			// A planned rule settled its gaps before the rules that read others, so `separate-jump-statements` reads them too.
			if (!readsOthers(name) || this.planned.has(name)) {
				flags.forEach((flag, index) => {
					if (flag) {
						this.claimedGaps.add(index);
					}
				});
			}
		}
	}
}

/** Every rule of one lint run of a file registers here, so the analysis reads the file once and each gap has one owner. */
class Session {
	private readonly claimants = new Map<RuleName, Claimant>();
	private readonly ruleIds = new Map<RuleName, string>();
	private readonly sharedSettings: ReturnType<typeof readSharedSettings>;

	private owned: Map<RuleName, OwnedGap[]> | undefined;
	private silencing: Silencing | undefined;

	private readonly silencedGaps = new WeakMap<Container, Map<RuleName, ReadonlySet<number>>>();
	private readonly sourceCode: SourceCode;

	public constructor(sourceCode: SourceCode, settings: unknown) {
		this.sourceCode = sourceCode;
		this.sharedSettings = readSharedSettings(settings);
	}

	public hasRule(ruleId: string): boolean {
		return [...this.ruleIds.values()].includes(ruleId);
	}

	public register(ruleId: string, claimant: Claimant): void {
		this.ruleIds.set(claimant.name, ruleId);
		this.claimants.set(claimant.name, claimant);
	}

	public getOwnedGaps(name: RuleName): readonly OwnedGap[] {
		this.owned ??= this.analyze();
		return this.owned.get(name) ?? [];
	}

	private analyze(): Map<RuleName, OwnedGap[]> {
		const reading = this.readReading();
		const owned = new Map<RuleName, OwnedGap[]>();
		const order = this.readOrder(() => true);
		const twoUnitOrder = this.readOrder((name) => this.claimants.get(name)?.judgesTwoUnits === true);
		const includeLists = this.claimants.has('separate-multi-line-elements') || this.claimants.has('keep-single-line-elements-together');

		for (const container of collectContainers(this.sourceCode, includeLists)) {
			if (!holdsTwoSingleLineUnits(container)) {
				this.ownGaps(container, reading, order, owned);
			} else if (this.claimants.has(pairRuleName)) {
				this.ownPair(container, owned);
			} else {
				this.ownGaps(container, reading, twoUnitOrder, owned);
			}
		}

		return owned;
	}

	private readReading(): Reading {
		const environment: Environment = {
			...this.sharedSettings,
			awaitEnabled: this.claimants.has('separate-await-runs'),

			receiverEnabled:
				this.claimants.has('separate-invocation-runs') ||
				this.claimants.has('separate-receiver-groups') ||
				this.claimants.has('keep-receiver-groups-together'),

			receiverSeparationEnabled: this.claimants.has('separate-receiver-groups'),
		};

		const valueReferences = readValueReferences(this.sourceCode);
		const receiverGroupsOf = readOncePerContainer((container: Container) => readReceiverGroups(container, environment));
		const dependentBindingsOf = readOncePerContainer((container: Container) => readDependentBindingGroups(container, valueReferences));

		const protectedSectionsOf = readOncePerContainer((container: Container) =>
			this.readProtectedSections(container, { receiverGroupsOf, dependentBindingsOf }),
		);

		return {
			environment,
			isEslintDirective: (comment) => this.readSilencing().isEslintDirective(comment),
			receiverGroupsOf,
			dependentBindingsOf,
			protectedSectionsOf,
			buildsSeed: readSeedBuilder(valueReferences),
			valueReferences,
			runsOf: readOncePerContainer((container: Container) => readRuns(container, environment, receiverGroupsOf, false, protectedSectionsOf)),
			modifierRunsOf: readOncePerContainer((container: Container) => readRuns(container, environment, receiverGroupsOf, true, protectedSectionsOf)),
		};
	}

	/** Reads the sections the enabled keepers protect, leaving out a gap where a directive silences the keeper. */
	private readProtectedSections(container: Container, reading: SectionReading): ReadonlySet<number> {
		const sections = new Set<number>();

		for (const claimant of this.claimants.values()) {
			for (const index of claimant.protects?.(container, reading) ?? []) {
				if (this.silencedAt(claimant.name, container, index) === undefined) {
					sections.add(index);
				}
			}
		}

		return sections;
	}

	private readOrder(includes: (name: RuleName) => boolean): Order {
		const isIncluded = (name: RuleName): boolean => this.claimants.has(name) && includes(name);
		return { enabled: addOrder.filter(isIncluded), keepers: keepOrder.filter(isIncluded), removers: removeOrder.filter(isIncluded) };
	}

	/**
	 * Reads where a directive silences the rule's report at a gap: at the unit below the gap, where the report stands,
	 * or at a directive in the gap itself, such as an `eslint-disable-line` comment that trails the unit above.
	 */
	private silencedAt(name: RuleName, container: Container, gapIndex: number): TSESTree.Position | undefined {
		const ruleId = this.ruleIds.get(name);
		const next = container.units[gapIndex + 1];

		if (ruleId === undefined || next === undefined) {
			return undefined;
		}

		const silencing = this.readSilencing();

		if (silencing.silences(ruleId, next.node.loc.start)) {
			return next.node.loc.start;
		}

		return container.gaps[gapIndex]?.comments.find((comment) => silencing.isEslintDirective(comment) && silencing.silences(ruleId, comment.loc.start))
			?.loc.start;
	}

	/** Reads the gaps with a blank line where a directive silences a rule, which a rule that removes blank lines neither removes nor reads a run across. */
	private silencedGapsOf(name: RuleName, container: Container): ReadonlySet<number> {
		const byRule = this.silencedGaps.get(container) ?? new Map<RuleName, ReadonlySet<number>>();
		let gaps = byRule.get(name);

		if (gaps === undefined) {
			gaps = new Set(
				container.gaps.flatMap((gap, index) =>
					gap.blankRange !== undefined && this.silencedAt(name, container, index) !== undefined ? [index] : [],
				),
			);

			byRule.set(name, gaps);
			this.silencedGaps.set(container, byRule);
		}

		return gaps;
	}

	private readSilencing(): Silencing {
		this.silencing ??= readSilencing(this.sourceCode);
		return this.silencing;
	}

	private ownPair(container: Container, owned: Map<RuleName, OwnedGap[]>): void {
		const [gap] = container.gaps;
		const [, second] = container.units;

		if (gap?.isBlank === true && second !== undefined) {
			addOwnedGap(owned, pairRuleName, { container, gap, next: second });
		}
	}

	/** Gives each gap of a container to its owner: a blank line to the keeper or the remover that removes it, a tight gap to the rule that writes one. */
	private ownGaps(container: Container, reading: Reading, order: Order, owned: Map<RuleName, OwnedGap[]>): void {
		// A rule the order leaves out judges nothing here, so the rules that yield to it read it as off.
		const judging = new Set<RuleName>([...order.enabled, ...order.keepers, ...order.removers]);

		const source: ClaimSource = {
			claimantOf: (name) => this.claimants.get(name),
			isEnabled: (name) => judging.has(name),
			silencedAt: (name, target, gapIndex) => this.silencedAt(name, target, gapIndex),
			silencedGapsOf: (name, target) => this.silencedGapsOf(name, target),
			isEslintDirective: (comment) => this.readSilencing().isEslintDirective(comment),
		};

		const claims = new ContainerClaims(container, source, reading, order).read();
		const keepGroups = this.readKeepGroups(container, order.keepers, claims.kept);
		const reported = new Set<Gap>();

		container.gaps.forEach((gap, index) => {
			const next = container.units[index + 1];

			if (next === undefined || gap.isJoined) {
				return;
			}

			// A silenced keeper still reports, tight gap or blank, so ESLint counts its directive as used, while the others judge the gap as if it were off.
			for (const name of order.keepers) {
				const silencedAt = claims.silenced.get(name)?.[index];

				if (silencedAt !== undefined) {
					addOwnedGap(owned, name, { container, gap, next, silencedAt });
				}
			}

			if (!gap.isBlank) {
				const owner = order.enabled.find((name) => claims.claimed.get(name)?.[index]);

				if (owner !== undefined) {
					addOwnedGap(owned, owner, { container, gap, next });
				}

				return;
			}

			if (gap.blankRange === undefined) {
				return;
			}

			const keeper = order.keepers.find((name) => claims.kept.get(name)?.[index]);

			if (keeper !== undefined) {
				const group = keepGroups.get(gap);

				reported.add(gap);
				addOwnedGap(owned, keeper, { container, gap, next, group, fixesGroup: group?.[0] === gap });
			}
		});

		const context: RemovalContext = {
			...reading,
			isClaimed: (gapIndex) => claims.isClaimed(gapIndex),
			isKept: (gapIndex) => claims.isKept(gapIndex),
			isEnabled: source.isEnabled,
			isSilenced: () => false,
		};

		this.readRemovals(container, order.removers, context, reported, owned);
	}

	/** Groups the stretches of kept gaps a rule that fixes together owns, so one fix never leaves a lone blank line that reads as a section boundary. */
	private readKeepGroups(container: Container, keepers: readonly KeepRuleName[], kept: Map<KeepRuleName, boolean[]>): Map<Gap, readonly Gap[]> {
		const groups = new Map<Gap, readonly Gap[]>();
		const owners = container.gaps.map((_, index) => keepers.find((name) => kept.get(name)?.[index] === true));

		for (const name of keepers) {
			if (this.claimants.get(name)?.fixTogether !== true) {
				continue;
			}

			for (let start = 0; start < owners.length;) {
				if (owners[start] !== name) {
					start += 1;
					continue;
				}

				let end = start + 1;

				while (owners[end] === name) {
					end += 1;
				}

				const gaps = container.gaps.slice(start, end).filter((gap) => gap.isBlank && gap.blankRange !== undefined);

				for (const gap of gaps) {
					groups.set(gap, gaps);
				}

				start = end;
			}
		}

		return groups;
	}

	/**
	 * Gives each blank line a rule removes to the first rule of the remove order that asks for it, with the gaps its fix removes together.
	 * A gap where a directive silences the rule ends a run and loses its blank line to no fix, so the directive keeps what it covers.
	 * The rule still reports that gap, silenced and without a fix, wherever a run could take it, so ESLint counts the directive as used.
	 */
	private readRemovals(
		container: Container,
		removers: readonly RemoveRuleName[],
		context: RemovalContext,
		reported: Set<Gap>,
		owned: Map<RuleName, OwnedGap[]>,
	): void {
		for (const name of removers) {
			const claimant = this.claimants.get(name);
			const removal = claimant?.removal;

			if (removal === undefined) {
				continue;
			}

			const silenced = this.silencedGapsOf(name, container);
			const groups = removal(container, { ...context, isSilenced: (gapIndex) => silenced.has(gapIndex) });
			const reach = silenced.size === 0 ? [] : [...removal(container, context).flat(), ...(claimant?.reach?.(container, context) ?? [])];

			for (const index of new Set(reach)) {
				const gap = container.gaps[index];
				const next = container.units[index + 1];
				const silencedAt = silenced.has(index) ? this.silencedAt(name, container, index) : undefined;

				if (gap !== undefined && next !== undefined && silencedAt !== undefined) {
					addOwnedGap(owned, name, { container, gap, next, silencedAt });
				}
			}

			for (const group of groups) {
				const gaps = group.flatMap((index) => (silenced.has(index) ? [] : (container.gaps[index] ?? [])));
				let hasFix = false;

				for (const index of group) {
					const gap = container.gaps[index];
					const next = container.units[index + 1];

					if (gap !== undefined && next !== undefined && !silenced.has(index) && !reported.has(gap)) {
						const fixesGroup = !hasFix;

						hasFix = true;
						reported.add(gap);
						addOwnedGap(owned, name, { container, gap, next, group: gaps, fixesGroup });
					}
				}
			}
		}
	}
}

/** Reads a container as if the comments of one gap were not there. */
function withoutComments(container: Container, gapIndex: number): Container {
	return {
		...container,
		gaps: container.gaps.map((gap, index) => (index === gapIndex ? { ...gap, comments: [], leadingComments: [], hasComment: false } : gap)),
	};
}

function addOwnedGap(owned: Map<RuleName, OwnedGap[]>, name: RuleName, gap: OwnedGap): void {
	const list = owned.get(name) ?? [];

	list.push(gap);
	owned.set(name, list);
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
