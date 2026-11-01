# Quirks

JavaScript and TypeScript syntax leaves a few places where a blank line would change a meaning, split a unit or touch content.
This page lists each place and the decision the rules take there.

## One owner per gap

A fix writes or removes blank lines and nothing else, and never moves code.
The [shared analysis](https://github.com/Nickelony/Stanza/blob/main/js/src/analysis/session.ts) reads each file once, caches the result by its `SourceCode`, and gives each gap to one rule.

- A rule that adds a blank line asks for a gap from the shape of the syntax tree alone, never from a blank line that is already there.
  Only `separate-jump-statements` reads one, because it ends its stanza at a blank line.
- A rule that removes a blank line reads the blank lines that stand, but removes none that an enabled rule of the owner order would write back.
  So a gap that the rules of the owner order ask for never holds a blank line that a removing rule takes away.
- A blank line another rule's fix writes stands where `separate-jump-statements` already ends its stanza, so any fix order reaches one text.
- The first enabled rule in the [owner order](Rule-map.md#one-owner-per-gap) that asks for a gap reports it, and the others stay silent.
- A rule that is off asks for nothing, so the next rule that asks for the gap reports it.
  A `// eslint-disable-next-line` for the owner of a gap does not hand the gap on,
  because ESLint applies the directive after the rules report, so the gap gets no blank line.
- The one rule that reads other rules, `separate-jump-statements`, ends its stanza at each gap that another enabled rule asks for.
- A container of exactly two single-line units belongs to `keep-two-units-together`, as the [next section](#two-single-line-units) says,
  and the rules that remove a blank line from a run, as [Blank lines that go](#blank-lines-that-go) says, leave it to that rule.
- A gap is satisfied when a blank line stands anywhere between the previous unit and the first line of the next unit's leading comments.

## Two single-line units

`keep-two-units-together` removes the blank line between the only two units of a container when each spans a single line.
The rules that add a blank line ask for nothing in such a container, so the two kinds of rule never meet.

| Quirk | Decision |
| --- | --- |
| A container | A file, a block, a switch case, a class body, a static block, an interface body, a type literal or a `declare module` block. |
| A comment between the two units, a trailing one included | Keeps the blank line. |
| A multi-line unit, including a decorator on its own line | Keeps the blank line, and the multi-line rule asks for it. |
| A function declaration, an import, a directive or a shebang | The container is skipped, because the import, directive and overload rules own the gaps of those units. |
| A guard above one statement, or a declaration above a `return` | Tight. The guard and jump rules judge a stanza of at least two units. |
| Two units on one line | Joined, so the rule leaves them. |

## Blank lines that go

`gather-loose-statements`, `gather-loose-members`, `keep-statement-runs-together`, `keep-member-runs-together`,
`keep-single-line-elements-together` and the keep rules of switch cases and JSX children remove blank lines.
Each removes only a blank line that holds nothing else, so a comment, a trailing comment or a line with a token on it keeps its gap.

| Quirk | Decision |
| --- | --- |
| A blank line some enabled rule asks for | Stays, and ends the run, so the run is counted up to that gap. |
| A multi-line unit, an import, a directive or a shebang | Ends a run, because other rules own the gaps beside it. |
| A unit tight against one neighbor and apart from the other | Out of the loose run, so the blank line that sets that group apart stays. A neighbor that a rule asks to set apart does not count as tight. |
| The blank line above a `return`, `throw`, `break` or `continue` | Stays while `separate-jump-statements` is enabled and the stanza above the jump spans two or more lines once the run is gathered, because that rule would write it back. A jump below one line goes with the run. |
| Several blank lines in a row, a line of whitespace or a CRLF file | All the blank lines go, and the file keeps its line ending. |
| A list that Prettier would join once its blank line is gone | Left for Prettier, which joins a list that fits on one line. |
| A blank line between two sections of a run | `keep-statement-runs-together` and `keep-member-runs-together` keep it when each side holds at least two units tight against each other, and a lone unit between two sections is a section. |
| A run rule that is off | Its runs are not read, so `keep-statement-runs-together` and `keep-member-runs-together` join only the runs of the rules that are enabled. |

## Comments

| Quirk | Decision |
| --- | --- |
| A comment directly above a node, such as `// @ts-expect-error`, `// @ts-ignore`, `// eslint-disable-next-line`, `/* istanbul ignore next */` or a JSDoc block | It belongs to the node. The blank line goes above the comment, so the comment stays adjacent. |
| A comment that trails the previous statement, as in `a(); // note` then `b();` | It belongs to the previous statement. Only a comment that starts on a later line than the statement's last line can lead the next one. |
| A comment separated from the node below by a blank line | It is detached and a unit of nothing; a blank line that stands between it and the node satisfies the gap. |
| A tool directive above a node, such as `// @ts-expect-error` or `// eslint-disable-next-line` | It stays attached to the node, but `separate-commented-siblings` ignores it, because it annotates a line and does not introduce a group. |
| A comment inside a unit | Never read; only the lines between two units count. |

## Gaps no rule owns

A gap is joined, and the rules leave it as written, when a blank line would split a pair or change a meaning.

| Quirk | Decision |
| --- | --- |
| Two statements on one line, as in `a(); b();` | Joined. Prettier owns line breaks, and a single-line block keeps its shape. |
| A semicolon-less file whose next line starts with `(`, `[` or a template literal | The fix inserts a line break only where one already stands, so the meaning cannot change. A line that starts with `;` shares the line of the statement above and stays joined. |
| TypeScript overload signatures, `declare function` overloads and constructor overloads | Joined. A signature without a body and the signature or function after it with the same name stay together. |
| Interface and type literal overloads, call signatures and construct signatures | Joined when two neighbors are the same kind of signature with the same name. |
| A getter and its setter | Joined. A `get x` next to a `set x` of the same name and the same `static` modifier stay together. |
| An empty `case` that falls through to the next one | Joined, so a list of labels stays one stanza. |
| Text, or a `{' '}` space between inline elements, among the children of a JSX element | Content. Every gap of the element is joined, because Prettier removes the blank lines of such an element. |

## Content a rule never touches

- A template literal, a string, a regular expression and the text of a JSX element are inside a unit, so no gap reaches them.
  The blank lines of the CSS in an Emotion template belong to [`stylelint-config-stanza`](https://github.com/Nickelony/Stanza/blob/main/js/stylelint-config/README.md).
- The fix inserts a line break at the start of the line of the next unit, so it never lands inside a template literal or a block comment.
- A line break uses the line ending the file already uses, so a CRLF file stays CRLF.

## Containers

| Container | Decision |
| --- | --- |
| `Program` | Judged. A shebang is the first unit of the file, so `separate-directives` owns the gap below it. |
| `BlockStatement`, `StaticBlock`, `TSModuleBlock` | Judged. This includes `declare module` and `declare global`, so ambient code reads like other code. |
| `SwitchStatement` | The gap between two cases is judged by the switch case rules. |
| `SwitchCase` | Its statements are judged like a block. The `break` that closes a case is not a jump the jump rule judges. |
| `ClassBody` | Judged by the multi-line and member rules. A decorator on its own line makes a member multi-line. |
| `TSInterfaceBody`, `TSTypeLiteral` | Judged by the multi-line and member rules, with the kinds property, method, call, construct and index signature. |
| JSX children | Judged by the JSX child rules, and only between children that a line break separates. A `{/* note */}` comment directly above a child leads that child, and the blank line goes above the comment. |
| Object members, array elements, enum members, arguments, parameters | Judged by the list rules, between two elements that stand on different lines. Elements that share a line, as in `run(a, () => {`, are joined. |
| Patterns and declarators | Not judged. A destructuring pattern is a binding, and Prettier removes the blank lines between the declarators of one declaration. |

## Statements

| Quirk | Decision |
| --- | --- |
| Directive prologue, such as `'use strict'` and `'use client'` | One unit group. The blank line goes below the last directive. |
| Shebang | The blank line goes below it. The rules read it from the text, so the default parser of ESLint and the TypeScript parser agree. |
| `import` statements | Two multi-line imports in a row stay together. A blank line goes below the leading imports, and between two adjacent imports of different kinds. |
| `export default` and `export const` | The statement is judged by what it exports, and an exported declaration is its own declaration kind. |
| Labels | A labeled statement is one unit and is judged by the lines it spans. |
| `try`, `catch` and `finally`, and `if` and `else` | One statement. Prettier owns the keywords between the blocks. |
| An IIFE and any statement that starts with `(` | A call with no callee path. It joins no run and is a unit like any other. |
| `using` and `await using` declarations | Declaration kinds of their own. |
| Assignments, increments and decrements | One assignment run, whatever they write, and a right side that awaits or calls still makes an assignment. |
| `super(...)` and `super.method(...)` | A base call. It opens or closes the body of a constructor, a method, an accessor or an object method, and an arrow function inside it is not that body. |
| `yield` statements | Not a run. A generator's `yield` statements are plain units. |
| Chained promise calls | One call statement. Its callee path joins the whole chain, so only an identical chain repeats a run. |
| `describe`, `it`, `test` and hooks | Test blocks, not invocations, so `separate-test-blocks` owns the gaps between them. A call to a test root is a test block even where that rule is off, and a name that is also an assertion root is a test block. |
