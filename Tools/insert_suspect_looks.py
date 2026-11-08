#!/usr/bin/env python3
"""Insert mix-and-match look slots on the 12 interviewees. Does not reformat the rest of the file."""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
PATH = ROOT / "Assets" / "Resources" / "Campus" / "affiliates.json"

LOOKS = {
    "jamie": {
        "body": "normal",
        "hair": "short wavy dark brown, tucked behind one ear",
        "skinTone": "warm medium",
        "top": "faded blue volunteer polo",
        "bottoms": "dark jeans",
        "outfitColor": "faded blue and indigo",
        "genderPresentation": "woman",
        "accessories": "yellow lanyard, canvas tote strap",
        "summary": "Normal build, woman presentation, same side as voice Female. Mix short dark wavy hair, warm-medium skin, faded-blue polo, dark jeans, yellow lanyard. Do not lock one prefab; normal + woman is only a starting family.",
    },
    "morgan": {
        "body": "normal",
        "hair": "neat side-part black",
        "skinTone": "light olive",
        "top": "charcoal button-down, sleeves rolled",
        "bottoms": "black slacks",
        "outfitColor": "charcoal and black",
        "genderPresentation": "man",
        "accessories": "thin metal watch, visitor badge reel",
        "summary": "Normal build, man presentation, same side as voice Male. Side-part black hair, light-olive skin, charcoal shirt, black slacks. Mix slots; do not lock a single mesh.",
    },
    "avery": {
        "body": "strong",
        "hair": "buzzed dark, no part",
        "skinTone": "deep brown",
        "top": "grey work shirt with a reflective strip",
        "bottoms": "dark cargo pants",
        "outfitColor": "grey and safety yellow",
        "genderPresentation": "androgynous",
        "accessories": "radio clip, work gloves in a pocket",
        "summary": "Strong build, androgynous, same side as voice Neutral. Buzz cut, deep-brown skin, grey work shirt. Man or woman mesh is fine if those slots read. Do not lock a gendered prefab.",
    },
    "riley": {
        "body": "normal",
        "hair": "long straight black, low clip",
        "skinTone": "light brown",
        "top": "cream cardigan over a rust tee",
        "bottoms": "brown trousers",
        "outfitColor": "cream, rust, and brown",
        "genderPresentation": "woman",
        "accessories": "round glasses, slim ledger folder",
        "summary": "Normal build, woman presentation, voice Female. Long black hair in a low clip, light-brown skin, cream cardigan, brown trousers, round glasses. Mix; do not lock one prefab.",
    },
    "casey": {
        "body": "normal",
        "hair": "high ponytail, dark with a blue streak",
        "skinTone": "light",
        "top": "royal-blue squad hoodie",
        "bottoms": "black joggers",
        "outfitColor": "royal blue and black",
        "genderPresentation": "woman",
        "accessories": "white sneakers, small bull pin",
        "summary": "Normal build, woman presentation, voice Female. High ponytail with a blue streak, light skin, royal-blue hoodie, black joggers, small bull pin. The pin is not a mascot mesh. Mix slots.",
    },
    "drew": {
        "body": "strong",
        "hair": "short cropped black",
        "skinTone": "deep brown",
        "top": "navy aide jacket over a white tee",
        "bottoms": "khaki duty pants",
        "outfitColor": "navy and khaki",
        "genderPresentation": "man",
        "accessories": "earpiece, flashlight loop",
        "summary": "Strong build, man presentation, voice Male. Cropped black hair, deep-brown skin, navy aide jacket, khaki pants, earpiece. Mix; do not lock one prefab.",
    },
    "quinn": {
        "body": "normal",
        "hair": "curly auburn, shoulder length",
        "skinTone": "fair with freckles",
        "top": "olive overshirt",
        "bottoms": "black jeans",
        "outfitColor": "olive and black",
        "genderPresentation": "androgynous",
        "accessories": "camera strap, silver ear cuff",
        "summary": "Normal build, androgynous, voice Neutral. Curly auburn hair, freckles, olive overshirt, camera strap. Man or woman mesh both work. Do not lock a gender.",
    },
    "sam": {
        "body": "stout",
        "hair": "short sandy, slightly messy",
        "skinTone": "fair",
        "top": "white kitchen tee under an open red cafe apron",
        "bottoms": "dark work pants",
        "outfitColor": "white, red, and dark navy",
        "genderPresentation": "man",
        "accessories": "dish towel over one shoulder",
        "summary": "Stout build, man presentation, voice Male. Sandy hair, fair skin, red apron over a white tee. The apron is the read. Mix slots; do not lock one prefab.",
    },
    "taylor": {
        "body": "normal",
        "hair": "dark shoulder-length, center part",
        "skinTone": "light olive",
        "top": "maroon RA pullover",
        "bottoms": "black pants",
        "outfitColor": "maroon and black",
        "genderPresentation": "woman",
        "accessories": "radio, RA lanyard, pen behind one ear",
        "summary": "Normal build, woman presentation, voice Female. Center-part dark hair, light-olive skin, maroon RA pullover, radio and lanyard. Mix; do not lock one prefab.",
    },
    "jordan": {
        "body": "normal",
        "hair": "straight black, a bit long at the collar",
        "skinTone": "medium tan",
        "top": "heather-grey hoodie",
        "bottoms": "dark jeans",
        "outfitColor": "heather grey and indigo",
        "genderPresentation": "man",
        "accessories": "headphones around the neck, retractable badge reel",
        "summary": "Normal build, man presentation, voice Male. Collar-length black hair, medium-tan skin, grey hoodie, headphones on the neck. Mix slots.",
    },
    "harper": {
        "body": "normal",
        "hair": "black braid over one shoulder",
        "skinTone": "medium brown",
        "top": "white lab coat over a teal shirt",
        "bottoms": "black trousers",
        "outfitColor": "white, teal, and black",
        "genderPresentation": "woman",
        "accessories": "safety glasses pushed up, nitrile gloves in a coat pocket",
        "summary": "Normal build, woman presentation, voice Female. Black braid, medium-brown skin, lab coat over teal, glasses on the head. Mix; do not lock one prefab.",
    },
    "reese": {
        "body": "strong",
        "hair": "undercut, dark top longer and swept",
        "skinTone": "olive",
        "top": "green rec tank under an open grey zip jacket",
        "bottoms": "black track pants",
        "outfitColor": "green, grey, and black",
        "genderPresentation": "androgynous",
        "accessories": "wrist sweatband, gym bag strap",
        "summary": "Strong build, androgynous, voice Neutral. Undercut, olive skin, green tank, grey jacket, black track pants. Man or woman mesh both work. Do not lock a gender.",
    },
}


def main() -> None:
    text = PATH.read_text(encoding="utf-8")
    for sid, look in LOOKS.items():
        marker = f'"id": "{sid}"'
        start = text.find(marker)
        if start < 0:
            raise SystemExit(f"missing {sid}")
        hooks = text.find('"recordHooks"', start)
        if hooks < 0:
            raise SystemExit(f"no recordHooks after {sid}")
        line_start = text.rfind("\n", 0, hooks) + 1
        if '"look"' in text[start:hooks]:
            raise SystemExit(f"look already present for {sid}")
        blob = json.dumps(look, ensure_ascii=False, indent=2)
        indented = "\n".join(("      " + line) if line else line for line in blob.splitlines())
        insertion = f'      "look": {indented.strip()},\n'
        text = text[:line_start] + insertion + text[line_start:]
    data = json.loads(text)
    found = [a["id"] for a in data["affiliates"] if a.get("tier") == "interviewee"]
    missing = [sid for sid in LOOKS if sid not in found]
    if missing:
        raise SystemExit(f"tier mismatch {missing}")
    for affiliate in data["affiliates"]:
        if affiliate["id"] in LOOKS:
            look = affiliate.get("look") or {}
            for slot in ("body", "hair", "skinTone", "top", "bottoms", "outfitColor", "genderPresentation", "accessories", "summary"):
                if not look.get(slot):
                    raise SystemExit(f"{affiliate['id']} missing {slot}")
            if look["body"] not in ("normal", "stout", "strong"):
                raise SystemExit(f"bad body {affiliate['id']}")
            if ".prefab" in json.dumps(look).lower() or "normal man" in json.dumps(look).lower():
                raise SystemExit(f"locked mesh name in {affiliate['id']}")
    PATH.write_text(text, encoding="utf-8")
    print(f"looks inserted for {len(LOOKS)} interviewees")


if __name__ == "__main__":
    main()
