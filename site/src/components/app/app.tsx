import { useEffect } from 'preact/hooks';

import { onStackedChange } from '../../util/scrolling.ts';
import { Header } from '../header/header.tsx';
import { ListTools } from '../list-tools/list-tools.tsx';
import { OptionList } from '../option-list/option-list.tsx';
import { Output } from '../output/output.tsx';
import { Pickers } from '../pickers/pickers.tsx';
import { Preview } from '../preview/preview.tsx';
import { RuleList } from '../rule-list/rule-list.tsx';
import { Tabs } from '../tabs/tabs.tsx';

import { Layout, LeftPane, RightPane } from './parts.ts';

import { useAddressHash } from './hooks/use-address-hash.ts';
import { useRevealedRow } from './hooks/use-revealed-row.ts';
import { useSelectingLinks } from './hooks/use-selecting-links.ts';

export function App() {
	const followSelectingLinks = useSelectingLinks();

	useAddressHash();
	useRevealedRow();

	// The wide layout clips the page and scrolls each pane alone, so a page scrolled while stacked would stay scrolled.
	useEffect(
		() =>
			onStackedChange((stacked) => {
				if (!stacked) {
					window.scrollTo(0, 0);
				}
			}),

		[],
	);

	return (
		<>
			<Header />

			<Layout onClick={followSelectingLinks}>
				<LeftPane>
					<Pickers />
					<Tabs />
					<ListTools />
					<RuleList />
					<OptionList />
				</LeftPane>

				<RightPane>
					<Preview />
					<Output />
				</RightPane>
			</Layout>
		</>
	);
}
