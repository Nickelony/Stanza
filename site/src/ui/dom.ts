type Child = Node | string | null | undefined | false;

type Props<K extends keyof HTMLElementTagNameMap> = Partial<Omit<HTMLElementTagNameMap[K], 'style' | 'dataset' | 'children'>> & {
	class?: string;
	dataset?: Record<string, string>;
	aria?: Record<string, string>;
};

/** Creates an element with its properties, ARIA attributes and children, so the views read as a tree. */
export function h<K extends keyof HTMLElementTagNameMap>(tag: K, props: Props<K> = {}, ...children: Child[]): HTMLElementTagNameMap[K] {
	const element = document.createElement(tag);
	const { class: className, dataset, aria, ...rest } = props;

	if (className) {
		element.className = className;
	}

	Object.assign(element, rest);

	for (const [key, value] of Object.entries(dataset ?? {})) {
		element.dataset[key] = value;
	}

	for (const [key, value] of Object.entries(aria ?? {})) {
		element.setAttribute(`aria-${key}`, value);
	}

	element.append(...children.filter((child): child is Node | string => child !== null && child !== undefined && child !== false));

	return element;
}

/** Creates an element from trusted HTML, which the build produced from the pages and escaped. */
export function html<K extends keyof HTMLElementTagNameMap>(tag: K, markup: string, className?: string): HTMLElementTagNameMap[K] {
	const element = document.createElement(tag);

	element.innerHTML = markup;

	if (className) {
		element.className = className;
	}

	return element;
}
