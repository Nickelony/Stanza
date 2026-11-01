# Relation to Prettier

Prettier owns braces, indentation, spaces and line breaks, and the rules never write them.
It keeps a blank line the author wrote between statements, members, the elements of a list, switch cases and JSX children,
so the rules that add a blank line agree with it.
The rules that remove one leave a list that fits on one line for Prettier to join.

[`@ianvs/prettier-plugin-sort-imports`](https://github.com/IanVS/prettier-plugin-sort-imports) writes the blank lines between import groups and removes the others.
It puts a blank line around every side-effect import, as [`separate-import-groups`](rules/separate-import-groups.md) does.
It keeps `import type` with the value imports of its group unless `importOrder` holds a `<TYPES>` group between two `""` entries.
So the `typeImports` option of that rule is off by default, and it settles with the plugin only beside such a group.

The convergence suite fixes a corpus in several rule orders, and the round trip suite checks that Prettier leaves each fixed file as it is.
