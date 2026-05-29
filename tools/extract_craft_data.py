#!/usr/bin/env python3
"""Extract AtlasLootClassic_Crafting/data.lua into Craft.json for SpecialAzerothService."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

CONTENT_TYPE_MAP = {
    "PROF_CONTENT": "Professions",
    "PROF_GATH_CONTENT": "Gathering",
    "PROF_SEC_CONTENT": "Secondary",
    "PROF_CLASS_CONTENT": "Class",
}

PROF_DISPLAY_FR = {
    "Alchemy": "Alchimie",
    "Blacksmithing": "Forge",
    "Enchanting": "Enchantement",
    "Engineering": "Ingénierie",
    "Tailoring": "Couture",
    "Leatherworking": "Travail du cuir",
    "Mining": "Minage",
    "Herbalism": "Herboristerie",
    "Cooking": "Cuisine",
    "FirstAid": "Secourisme",
    "Fishing": "Pêche",
    "RoguePoisons": "Poisons (voleur)",
}

ENTRY_RE = re.compile(
    r"\{\s*(\d+)\s*,\s*([\d\s,]+)\s*\}\s*,?\s*(?:--\s*(.+))?",
    re.MULTILINE,
)
CATEGORY_NAME_RE = re.compile(r'name\s*=\s*(?:AL(?:IL)?\["([^"]+)"\]|"([^"]+)")')
PROF_NAME_RE = re.compile(r'name\s*=\s*ALIL\["([^"]+)"\]')
CONTENT_TYPE_RE = re.compile(r"ContentType\s*=\s*(\w+)")


def find_matching_brace(text: str, open_index: int) -> int:
    depth = 0
    i = open_index
    while i < len(text):
        ch = text[i]
        if ch == "{":
            depth += 1
        elif ch == "}":
            depth -= 1
            if depth == 0:
                return i
        elif ch in ("'", '"'):
            quote = ch
            i += 1
            while i < len(text) and text[i] != quote:
                if text[i] == "\\":
                    i += 1
                i += 1
        elif ch == "-" and i + 1 < len(text) and text[i + 1] == "-":
            while i < len(text) and text[i] not in "\r\n":
                i += 1
            continue
        i += 1
    raise ValueError("Unbalanced braces")


def split_profession_blocks(lua: str) -> list[tuple[str, str]]:
    pattern = re.compile(r'data\["([^"]+)"\]\s*=\s*\{')
    blocks: list[tuple[str, str]] = []
    for match in pattern.finditer(lua):
        key = match.group(1)
        start = match.end() - 1
        end = find_matching_brace(lua, start)
        blocks.append((key, lua[start + 1 : end]))
    return blocks


def parse_category_entries(diff_body: str) -> list[dict]:
    entries: list[dict] = []
    for match in ENTRY_RE.finditer(diff_body):
        slot = int(match.group(1))
        ids = [int(x) for x in match.group(2).replace(" ", "").split(",") if x]
        label = (match.group(3) or "").strip()
        # Strip inline skill hints: "Name / 50 / 250"
        if label:
            label = label.split("/")[0].strip()
        spell_id = 0
        item_ids: list[int] = []
        if len(ids) == 1:
            # Crafting: usually spell; herbalism gathering: item
            if ids[0] > 100000:
                item_ids = ids
            else:
                spell_id = ids[0]
        else:
            item_ids = ids
        entries.append(
            {
                "slot": slot,
                "spellId": spell_id,
                "itemIds": item_ids,
                "label": label,
            }
        )
    return entries


def parse_categories(body: str) -> list[dict]:
    categories: list[dict] = []
    items_idx = body.find("items = {")
    if items_idx < 0:
        return categories
    items_start = body.index("{", items_idx)
    items_end = find_matching_brace(body, items_start)
    items_body = body[items_start + 1 : items_end]

    cursor = 0
    while cursor < len(items_body):
        name_match = CATEGORY_NAME_RE.search(items_body, cursor)
        if not name_match:
            break
        name = name_match.group(1) or name_match.group(2) or ""
        diff_match = re.search(r"\[NORMAL_DIFF\]\s*=\s*\{", items_body, name_match.end())
        if not diff_match:
            cursor = name_match.end()
            continue
        diff_start = items_body.index("{", diff_match.start())
        diff_end = find_matching_brace(items_body, diff_start)
        entries = parse_category_entries(items_body[diff_start + 1 : diff_end])
        categories.append({"name": name, "entries": entries})
        cursor = diff_end + 1
    return categories


def parse_profession(key: str, body: str) -> dict:
    content_match = CONTENT_TYPE_RE.search(body)
    content_key = content_match.group(1) if content_match else "PROF_CONTENT"
    content_type = CONTENT_TYPE_MAP.get(content_key, content_key)

    name = key
    name_match = PROF_NAME_RE.search(body)
    if name_match:
        name = name_match.group(1)

    return {
        "id": key,
        "name": name,
        "nameFr": PROF_DISPLAY_FR.get(key, name),
        "contentType": content_type,
        "categories": parse_categories(body),
    }


def main() -> int:
    default_lua = Path(
        r"D:\Programmes\World of Warcraft\_classic_era_\Interface\AddOns"
        r"\AtlasLootClassic_Crafting\data.lua"
    )
    lua_path = Path(sys.argv[1]) if len(sys.argv) > 1 else default_lua
    out_path = (
        Path(sys.argv[2])
        if len(sys.argv) > 2
        else Path(__file__).resolve().parents[1]
        / "WindowsOrganiserApp"
        / "Assets"
        / "Craft.json"
    )

    if not lua_path.is_file():
        print(f"ERROR: data.lua not found: {lua_path}", file=sys.stderr)
        return 1

    lua = lua_path.read_text(encoding="utf-8", errors="replace")
    professions = [parse_profession(k, b) for k, b in split_profession_blocks(lua)]

    payload = {
        "version": 1,
        "game": "classic-era",
        "source": "AtlasLootClassic_Crafting/data.lua",
        "extractedFrom": str(lua_path),
        "contentTypes": list(CONTENT_TYPE_MAP.values()),
        "professions": professions,
    }

    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(payload, ensure_ascii=False, indent=2), encoding="utf-8")

    cat_count = sum(len(p["categories"]) for p in professions)
    entry_count = sum(
        len(e)
        for p in professions
        for c in p["categories"]
        for e in [c["entries"]]
    )
    print(f"Wrote {out_path}")
    print(f"  {len(professions)} professions, {cat_count} categories, {entry_count} entries")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
