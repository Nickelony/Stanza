import { readCheckedEdges } from '../analysis/checked.ts';
import { createAddRule } from '../createAddRule.ts';

export default createAddRule({
	name: 'separate-checked-stanzas',
	description: 'Separate a guard chain and the bindings it checks from neighboring statements.',
	message: 'Add a blank line beside this checked stanza.',
	readsOthers: true,
	judgesTwoUnits: true,
	plan: readCheckedEdges,
});
