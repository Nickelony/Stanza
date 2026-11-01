declare module 'virtual:stanza-catalog/*' {
	import type { SiteCatalog } from './state/model.ts';

	export const catalog: SiteCatalog;
	export const tokenCss: string;
}
