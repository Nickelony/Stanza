import { Declaration } from 'postcss';
import styledSyntax from 'postcss-styled-syntax';
import ts from 'typescript';

import { createBindings, isEmotionKeyframes } from './emotion-tag.mjs';

/**
 * The property of a standalone interpolation, which the empty-line rules read as a declaration.
 * The braces keep it out of the standard properties, so a rule that judges a property, a duplicate or a value skips it.
 * A leading `$` would make Stylelint read a Sass variable, and the empty-line rules would lay it out as one.
 */
const interpolationProperty = 'stanza-{interpolation}';

/** Exposes each standalone `${...}` of a template as a node of its own, so a rule sees it as the sibling it is. */
function exposeStandaloneInterpolations(document) {
	const nodes = [];
	document.walk((node) => nodes.push(node));

	function extract(raw, endOffset) {
		if (!raw?.includes('${')) {
			return;
		}

		const first = raw.search(/\S/);
		const last = raw.trimEnd().length;

		if (!raw.slice(first).startsWith('${')) {
			return;
		}

		const start = endOffset - raw.length + first;
		const end = endOffset - raw.length + last;
		const input = document.source.input;

		const position = (offset) => {
			const { line, col } = input.fromOffset(offset);
			return { offset, line, column: col };
		};

		return {
			node: new Declaration({
				prop: interpolationProperty,
				value: raw.slice(first, last),
				raws: { before: raw.slice(0, first), stanzaInterpolationRange: [start, end] },
				source: { input, start: position(start), end: position(end - 1) },
			}),

			after: raw.slice(last),
		};
	}

	for (const node of nodes) {
		const before = node.source?.start && extract(node.raws.before, node.source.start.offset);

		if (before) {
			node.parent.insertBefore(node, before.node);
			node.raws.before = before.after;
		}

		const end = node.type === 'root' ? node.raws.styledSyntaxRangeEnd : node.source?.end && node.source.end.offset - 1;
		const after = node.nodes && end !== undefined && extract(node.raws.after, end);

		if (after) {
			node.append(after.node);
			node.raws.after = after.after;
		}
	}

	return document;
}

/** Finds the tag of each `keyframes` template that Emotion's `keyframes` factory tags. */
function findKeyframesTags(source, fileName) {
	const { sourceFile, checker } = createBindings(source, fileName);
	const ranges = [];

	function visit(node) {
		if (ts.isTaggedTemplateExpression(node) && ts.isIdentifier(node.tag) && node.tag.text === 'keyframes' && isEmotionKeyframes(node.tag, checker)) {
			ranges.push({
				start: node.tag.getStart(sourceFile),
				end: node.tag.end,
				contentStart: node.template.getStart(sourceFile) + 1,
				spelling: node.tag.getText(sourceFile),
			});
		}

		ts.forEachChild(node, visit);
	}

	visit(sourceFile);

	return ranges;
}

/** Adds Emotion `keyframes` to the styled and `css` support of the upstream parser, without changing the saved JavaScript. */
function parse(input, options) {
	const source = String(input);

	if (!source.includes('keyframes')) {
		return exposeStandaloneInterpolations(styledSyntax.parse(source, options));
	}

	let ranges = [];

	try {
		ranges = findKeyframesTags(source, options?.from);
	} catch {
		// A source TypeScript cannot read has no template for the upstream parser to find either.
	}

	// Each spelling of the tag, `keyframes` or an escaped one, gets a marker of its own and is restored as written.
	// A marker is as long as its spelling and ends the identifier css where the tag ended, so every offset stays the source's.
	// A private-use character no other text holds makes the restoration unambiguous, nested interpolations included.
	const markers = new Map();
	let character = 0xe000;

	for (const spelling of new Set(ranges.map((range) => range.spelling))) {
		while (character <= 0xf8ff && source.includes(String.fromCharCode(character))) {
			character++;
		}

		if (character > 0xf8ff) {
			return exposeStandaloneInterpolations(styledSyntax.parse(source, options));
		}

		markers.set(spelling, `/*${String.fromCharCode(character)}${' '.repeat(spelling.length - 9)}*/ css`);
		character++;
	}

	let virtualSource = source;

	for (const { start, end, spelling } of ranges.toSorted((left, right) => right.start - left.start)) {
		virtualSource = virtualSource.slice(0, start) + markers.get(spelling) + virtualSource.slice(end);
	}

	const document = styledSyntax.parse(virtualSource, options);
	document.raws.stanzaKeyframesMarkers = new Map([...markers].map(([spelling, marker]) => [marker, spelling]));

	const prefixes = new Map(ranges.map(({ start, contentStart }) => [contentStart, virtualSource.slice(start, contentStart)]));

	for (const root of document.nodes) {
		const prefix = prefixes.get(root.raws.styledSyntaxRangeStart);

		if (prefix !== undefined) {
			root.raws.stanzaKeyframesPrefix = prefix;
		}
	}

	return exposeStandaloneInterpolations(document);
}

/** Writes the document back, with each interpolation as its JavaScript and each marker as the tag it stands for. */
function stringify(node, builder) {
	const document = node.root();
	const markers = document.raws.stanzaKeyframesMarkers;
	const text = document.source.input.css;
	const interpolations = document.nodes?.filter((item) => !item.raws.styledSyntaxIsComponent) ?? [];

	function render(current) {
		let output = '';

		styledSyntax.stringify(current, (part, cssNode) => {
			if (cssNode?.raws.stanzaInterpolationRange) {
				const [start, end] = cssNode.raws.stanzaInterpolationRange;
				const contained = interpolations.filter((item) => start <= item.raws.styledSyntaxRangeStart && item.raws.styledSyntaxRangeEnd <= end);

				const outermost = contained.filter(
					(item) =>
						!contained.some(
							(other) =>
								other !== item &&
								other.raws.styledSyntaxRangeStart < item.raws.styledSyntaxRangeStart &&
								item.raws.styledSyntaxRangeEnd < other.raws.styledSyntaxRangeEnd,
						),
				);

				// The expression is the source text, never the value, which a rule that fixes a value could have changed.
				let expression = text.slice(start, end);

				for (const child of outermost.toSorted((left, right) => right.raws.styledSyntaxRangeStart - left.raws.styledSyntaxRangeStart)) {
					const from = child.raws.styledSyntaxRangeStart - start;
					const to = child.raws.styledSyntaxRangeEnd - start;

					expression = expression.slice(0, from) + render(child) + expression.slice(to);
				}

				output += expression;
				return;
			}

			// The upstream stringifier restores interpolations in whitespace, but not inside declaration values.
			if (markers && cssNode?.type === 'decl') {
				for (const interpolation of interpolations) {
					const prefix = interpolation.raws.stanzaKeyframesPrefix;

					if (prefix === undefined) {
						continue;
					}

					const original = `${prefix}${interpolation.raws.styledOriginalContent}\``;

					if (part.includes(original)) {
						part = part.replaceAll(original, () => `${prefix}${render(interpolation)}\``);
					}
				}
			}

			output += part;
		});

		return output;
	}

	let output = render(node);

	for (const [marker, spelling] of markers ?? []) {
		output = output.split(marker).join(spelling);
	}

	builder(output);
}

export default { parse, stringify };
