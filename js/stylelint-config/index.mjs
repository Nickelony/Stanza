import scssSyntax from 'postcss-scss';

import syntax from './syntax.mjs';

/** The blank lines of a stylesheet, as the core rules of Stylelint set them. */
export default {
	rules: {
		'at-rule-empty-line-before': [
			'always',

			{
				except: ['first-nested'],
				ignore: ['after-comment', 'blockless-after-blockless'],
				ignoreAtRules: ['else'],
			},
		],

		'rule-empty-line-before': [
			'always-multi-line',

			{
				except: ['first-nested'],
				ignore: ['after-comment'],
			},
		],

		'declaration-empty-line-before': [
			'always',

			{
				except: ['first-nested'],
				ignore: ['after-comment', 'after-declaration', 'inside-single-line-block'],
			},
		],
	},

	overrides: [
		{ files: ['**/*.{js,jsx,mjs,cjs,ts,tsx,mts,cts}'], customSyntax: syntax },
		{ files: ['**/*.scss'], customSyntax: scssSyntax },
	],
};
