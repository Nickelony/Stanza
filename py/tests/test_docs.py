"""The documentation stays true: every example runs against its rule, and the tables list the rules that exist."""

from __future__ import annotations

import json
import re
from dataclasses import asdict
from pathlib import Path

import pytest

from nickelony_stanza import Config, Options, fix
from nickelony_stanza.cli import run
from nickelony_stanza.config import _KEYS
from nickelony_stanza.rules import ALL_RULES, OWNER_ORDER, rules_of_level
from nickelony_stanza.rules.base import LEVELS, Rule
from tests.helpers import fix_with

ROOT = Path(__file__).parent.parent
PAGES = ROOT / "docs" / "rules"
FENCE = "```"

# The rules that keep a run read the runs of the rules that group it, so their examples run with one of those enabled.
COMPANIONS = {"ZA3054": ("ZA3004",), "ZA3055": ("ZA3013",)}

# The keys that hold a long list of patterns, which the page states in a sentence of its own.
PATTERN_KEYS = ("assertion-roots", "subscription-roots")


def _page(rule: Rule) -> str:
    return (PAGES / f"{rule.code}-{rule.name}.md").read_text(encoding="utf-8")


def _examples(page: str) -> list[tuple[str, str]]:
    examples = []

    for match in re.finditer(rf"{FENCE}python\n(.*?)\n{FENCE}", page, flags=re.DOTALL):
        before, rest = match.group(1).split("\n\n# After\n")
        after, *unchanged = re.split(r"\n\n# Unchanged[^\n]*\n", rest)

        assert all(text.strip() for text in unchanged), f"An Unchanged example is empty in:\n{match.group(1)}"

        examples.append((before.removeprefix("# Before\n") + "\n", after + "\n"))
        examples.extend((text + "\n", text + "\n") for text in unchanged)

    return examples


def test_there_is_one_page_for_each_rule_and_a_rule_for_each_page() -> None:
    pages = sorted(path.stem for path in PAGES.glob("*.md"))
    assert pages == sorted(f"{rule.code}-{rule.name}" for rule in ALL_RULES)


@pytest.mark.parametrize("rule", ALL_RULES, ids=lambda rule: rule.code)
def test_every_example_of_a_page_is_the_real_fix_of_its_rule(rule: Rule) -> None:
    examples = _examples(_page(rule))

    assert examples

    for before, after in examples:
        rules = (rule.code, *COMPANIONS.get(rule.code, ()))

        assert fix_with(before, *rules) == after
        assert fix_with(after, *rules) == after


@pytest.mark.parametrize("rule", ALL_RULES, ids=lambda rule: rule.code)
def test_a_page_names_its_title_its_level_and_its_counterparts(rule: Rule) -> None:
    page = _page(rule)
    csharp = f"ZA{int(rule.code[2:]) - 3000:04d}"

    assert page.startswith(f"# {rule.code} - {rule.name}\n\n**Level: {rule.level}.**\n")
    assert f"## C# counterpart\n\n[{csharp} - " in page
    assert f"https://github.com/Nickelony/Stanza/wiki/{csharp}-" in page
    assert "## JS counterpart\n\n" in page


def test_a_code_is_a_csharp_code_plus_3000_with_a_page_or_is_python_only() -> None:
    csharp_pages = ROOT.parent / "docs" / "rules"

    for rule in ALL_RULES:
        number = int(rule.code[2:])

        if 3800 <= number <= 3899:
            continue

        assert list(csharp_pages.glob(f"ZA{number - 3000:04d}-*.md")), f"{rule.code} has no C# counterpart page"

    assert not list(csharp_pages.glob("ZA08*.md")), "ZA08xx stays unused so that ZA38xx can hold the Python-only rules"


def test_the_js_counterparts_point_at_rules_that_exist() -> None:
    js_pages = {path.stem for path in (ROOT.parent / "js" / "docs" / "rules").glob("*.md")}

    for rule in ALL_RULES:
        for name in re.findall(r"/js/docs/rules/([\w-]+)\.md", _page(rule)):
            assert name in js_pages


def test_a_js_rule_of_the_same_name_is_linked_and_a_page_without_a_link_says_none() -> None:
    js_pages = {path.stem for path in (ROOT.parent / "js" / "docs" / "rules").glob("*.md")}

    for rule in ALL_RULES:
        section = _page(rule).partition("## JS counterpart")[2].strip()
        linked = re.findall(r"/js/docs/rules/([\w-]+)\.md", section)

        if rule.name in js_pages:
            assert rule.name in linked, f"{rule.code} does not link the JS rule of its name"

        if not linked:
            assert section.startswith("None."), f"{rule.code} names no JS rule and does not say none"


def test_the_readme_lists_each_rule_with_its_level_and_its_csharp_code() -> None:
    readme = (ROOT / "README.md").read_text(encoding="utf-8")

    for rule in ALL_RULES:
        csharp = f"ZA{int(rule.code[2:]) - 3000:04d}"
        row = next(line for line in readme.splitlines() if line.startswith(f"| [`{rule.code}`]"))

        assert f"`{rule.name}`" in row
        assert f"| {rule.level} |" in row
        assert f"| `{csharp}` |" in row
        assert f"docs/rules/{rule.code}-{rule.name}.md" in row

    assert sum(1 for line in readme.splitlines() if re.match(r"\| \[`ZA3\d+`\]", line)) == len(ALL_RULES)


def test_the_rule_map_lists_each_rule_and_the_owner_order() -> None:
    rule_map = (ROOT / "docs" / "Rule-map.md").read_text(encoding="utf-8")

    for rule in ALL_RULES:
        assert f"rules/{rule.code}-{rule.name}.md" in rule_map
        assert f"| {rule.level} |" in next(line for line in rule_map.splitlines() if f"[`{rule.code}`]" in line)

    order = re.findall(r"^\d+\. `(ZA\d+)` `([\w-]+)`$", rule_map, flags=re.MULTILINE)

    assert order == [(rule.code, rule.name) for rule in OWNER_ORDER]


def _defaults() -> dict[str, object]:
    """Reads the default of every key of `[tool.stanza]` from the code that applies it."""
    config = Config.create()

    values: dict[str, object] = {
        "level": config.level,
        "enable": list(config.enable),
        "disable": list(config.disable),
        "exclude": list(config.exclude),
        "formatter": config.formatter,
    }

    for name, value in asdict(Options()).items():
        values[name.replace("_", "-")] = list(value) if isinstance(value, tuple) else value

    return values


def _documented_default(row: str) -> object:
    return json.loads(row.split("|")[2].strip().strip("`"))


def test_the_configuration_page_documents_every_key_with_its_default() -> None:
    page = (ROOT / "docs" / "Configuration.md").read_text(encoding="utf-8")
    defaults = _defaults()

    assert set(defaults) == _KEYS

    for key in _KEYS - set(PATTERN_KEYS):
        row = next(line for line in page.splitlines() if line.startswith(f"| `{key}` |"))
        assert _documented_default(row) == defaults[key], key

    for key in PATTERN_KEYS:
        sentence = re.search(rf"The default `{key}` are ([^\n]+?)\.\n", page)

        assert sentence is not None, key
        assert re.findall(r"`([^`]+)`", sentence.group(1)) == defaults[key], key


@pytest.mark.parametrize("rule", ALL_RULES, ids=lambda rule: rule.code)
def test_the_option_tables_of_a_page_state_the_defaults_of_the_code(rule: Rule) -> None:
    section = _page(rule).partition("## Options\n")[2].partition("\n## ")[0]
    defaults = _defaults()

    for row in re.findall(r"^\| `[\w-]+` \|.*$", section, flags=re.MULTILINE):
        key = row.split("|")[1].strip().strip("`")
        assert _documented_default(row) == defaults[key], key


def test_the_readme_example_is_the_fix_of_the_default_level() -> None:
    readme = (ROOT / "README.md").read_text(encoding="utf-8")
    examples = _examples(readme)
    config = Config.create()

    assert len(examples) == 1

    for before, after in examples:
        assert fix(before, config).text == after
        assert fix(after, config).text == after


def test_the_pre_commit_file_defines_a_hook_for_each_command() -> None:
    hooks = (ROOT / ".pre-commit-hooks.yaml").read_text(encoding="utf-8")

    for command in ("check", "fix", "format"):
        assert f"id: stanza-py-{command}" in hooks
        assert f"entry: stanza-py {command}\n" in hooks


# A text for each half of a pair: one the separating half would change and one the tight half would.
PAIR_SAMPLES: dict[tuple[str, ...], list[str]] = {
    ("ZA3201", "ZA3202"): [
        "if a:\n    x()\nelse:\n    y()\n",
        "if a:\n    x()\n\nelse:\n    y()\n",
        "if a:\n    x()\n\n# otherwise\nelse:\n    y()\n",
    ],
    ("ZA3111", "ZA3112"): [
        "match v:\n    case 1:\n        a()\n    case 2:\n        b()\n",
        "match v:\n    case 1:\n        a()\n\n    case 2:\n        b()\n",
    ],
}


def _pair_rows() -> list[tuple[tuple[str, ...], str, str]]:
    """Reads the pair table of the rule map: the halves, the default half and the owner when both are enabled."""
    rule_map = (ROOT / "docs" / "Rule-map.md").read_text(encoding="utf-8")
    section = rule_map.partition("## Pairs and their default halves\n")[2].partition("\n## ")[0]
    rows = []

    for line in section.splitlines():
        if not line.startswith("| ") or line.startswith(("| Gap", "| ---")):
            continue

        cells = [cell.strip() for cell in line.strip("|").split("|")]
        halves, default, owner = (tuple(re.findall(r"`(ZA\d{4})`", cell)) for cell in cells[1:])

        rows.append((halves, default[0], owner[0]))

    return rows


def test_the_pair_table_lists_each_pair_the_code_links_and_no_other() -> None:
    opposites = {tuple(sorted((rule.code, rule.opposite))) for rule in ALL_RULES if rule.opposite}
    documented = {halves for halves, _, _ in _pair_rows()}

    assert opposites <= documented
    assert documented == set(PAIR_SAMPLES)


def test_every_opposite_is_mutual() -> None:
    by_code = {rule.code: rule for rule in ALL_RULES}

    for rule in ALL_RULES:
        if rule.opposite:
            assert by_code[rule.opposite].opposite == rule.code


@pytest.mark.parametrize("row", _pair_rows(), ids=lambda row: "-".join(row[0]))
def test_the_pair_table_names_the_owner_the_code_picks(row: tuple[tuple[str, ...], str, str]) -> None:
    halves, _, owner = row
    other = next(code for code in halves if code != owner)

    for sample in PAIR_SAMPLES[halves]:
        assert fix_with(sample, *halves) == fix_with(sample, owner)

    assert any(fix_with(sample, other) != fix_with(sample, owner) for sample in PAIR_SAMPLES[halves])


@pytest.mark.parametrize("row", _pair_rows(), ids=lambda row: "-".join(row[0]))
def test_the_default_half_is_the_one_a_level_enables(row: tuple[tuple[str, ...], str, str]) -> None:
    halves, default, _ = row
    by_code = {rule.code: rule for rule in ALL_RULES}

    assert [code for code in halves if by_code[code].level != "opt-in"] == [default]


@pytest.mark.parametrize("row", _pair_rows(), ids=lambda row: "-".join(row[0]))
def test_the_pages_of_both_halves_link_the_pair_table(row: tuple[tuple[str, ...], str, str]) -> None:
    for rule in ALL_RULES:
        if rule.code in row[0]:
            assert "](../Rule-map.md#pairs-and-their-default-halves)" in _page(rule)


def test_a_command_reports_no_conflict_when_both_halves_are_enabled(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    path = tmp_path / "zoo.py"
    path.write_bytes((ROOT / "tests" / "corpus" / "syntax_zoo.py").read_bytes())

    assert run(["check", "--level", "none", "--enable", "ZA3201,ZA3202", str(path)]) == 1

    output = capsys.readouterr()
    codes = set(re.findall(r"\bZA\d{4}\b", output.out))

    assert codes == {"ZA3202"}
    assert "conflict" not in (output.out + output.err).lower()


def test_a_noqa_over_the_owner_does_not_hand_the_gap_to_the_other_half() -> None:
    text = "if a:\n    x()\n\nelse:  # noqa: ZA3202\n    y()\n"
    assert fix_with(text, "ZA3201", "ZA3202") == text


def test_no_level_holds_both_halves_of_a_pair() -> None:
    for level in LEVELS:
        codes = {rule.code for rule in rules_of_level(level)}

        for halves, _, _ in _pair_rows():
            assert not set(halves) <= codes
