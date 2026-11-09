import type { Selection } from '../../state/selection.ts';

/** The ID of the tab button that shows the list of the kind, which the panel of that list names as its label. */
export const tabId = (kind: Selection['kind']): string => `tab-${kind === 'rule' ? 'rules' : 'options'}`;

/** The ID of the panel that holds the list of the kind. */
export const panelId = (kind: Selection['kind']): string => `panel-${kind === 'rule' ? 'rules' : 'options'}`;
