/** The width where the panes sit side by side, and below which they stack; `style.css` repeats it, and a test keeps the two equal. */
const sideBySideAt = 960;

/** The media query of the side by side layout. */
export const sideBySide = `@media (min-width: ${sideBySideAt}px)`;

/** The media query, for `matchMedia`, of the stacked layout. */
export const stacked = `(max-width: ${sideBySideAt - 1}px)`;

/** Below this width the header stacks its title, its links and its buttons. */
export const headerStacks = '@media (max-width: 620px)';

/** At the width of a phone and below, a row stacks its ID above its title and its controls grow to a touch target. */
export const phone = '@media (max-width: 520px)';
