import assert from 'node:assert/strict';
import { describe, it } from 'node:test';

import ts from 'typescript';

import { createBindings, isEmotionKeyframes } from '../emotion-tag.mjs';

/** Reads whether the tag of the last tagged template of a source is the Emotion `keyframes`. */
function isKeyframes(source) {
	const { sourceFile, checker } = createBindings(source, 'example.tsx');
	let tag;

	function visit(node) {
		if (ts.isTaggedTemplateExpression(node)) {
			tag = node.tag;
		}

		ts.forEachChild(node, visit);
	}

	visit(sourceFile);

	return isEmotionKeyframes(tag, checker);
}

describe('isEmotionKeyframes', () => {
	it('recognizes the keyframes export of the react and css packages', () => {
		assert.equal(isKeyframes('import { keyframes } from "@emotion/react";\nkeyframes``;'), true);
		assert.equal(isKeyframes('import { keyframes } from "@emotion/css";\nkeyframes``;'), true);
		assert.equal(isKeyframes('import { keyframes as animate } from "@emotion/react";\nanimate``;'), true);
	});

	it('rejects the other exports of the package and a name from another package', () => {
		assert.equal(isKeyframes('import { css } from "@emotion/react";\ncss``;'), false);
		assert.equal(isKeyframes('import { keyframes } from "styled-components";\nkeyframes``;'), false);
		assert.equal(isKeyframes('import keyframes from "@emotion/react";\nkeyframes``;'), false);
	});

	it('rejects a name with no import, a local declaration and a shadowing parameter', () => {
		assert.equal(isKeyframes('keyframes``;'), false);
		assert.equal(isKeyframes('const keyframes = (text) => text;\nkeyframes``;'), false);
		assert.equal(isKeyframes('import { keyframes } from "@emotion/react";\nfunction label(keyframes) {\n\treturn keyframes``;\n}'), false);
		assert.equal(isKeyframes('import { keyframes } from "@emotion/react";\n{\n\tconst keyframes = text;\n\tkeyframes``;\n}'), false);
	});
});
