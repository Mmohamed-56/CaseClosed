#!/usr/bin/env python3
"""Generate evidence-driven dialogue trees under Assets/Resources/Dialogue/Trees/.

Prompts stay case-agnostic so rotating case_templates.json stays the flavor per shift.
Replies come from dialogue_voice_lines.py — one voice per interviewee, innocent and
culprit separated on access / HOLD / RELEASE. No casual confession before stress 85.
"""
from __future__ import annotations

import json
import uuid
from pathlib import Path

from dialogue_voice_lines import PEOPLE, PROMPTS, replies_for

ROOT = Path(__file__).resolve().parents[1]
AFFILIATES = ROOT / "Assets" / "Resources" / "Campus" / "affiliates.json"
OUT = ROOT / "Assets" / "Resources" / "Dialogue" / "Trees"


def new_guid() -> str:
    return uuid.uuid4().hex


def write_meta(path: Path) -> None:
    meta = path.with_suffix(path.suffix + ".meta")
    if meta.exists():
        return
    body = (
        "fileFormatVersion: 2\n"
        f"guid: {new_guid()}\n"
        "TextScriptImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n"
    )
    meta.write_text(body, encoding="utf-8")


def question(qid, prompt, replies, aggressive=False):
    return {
        "id": qid,
        "prompt": prompt,
        "aggressive": bool(aggressive),
        "replies": replies,
    }


def branch(evidence_key, questions):
    return {"evidenceKey": evidence_key, "questions": questions}


def build_tree(a: dict) -> dict:
    sid = a["id"]
    if sid not in PEOPLE:
        raise SystemExit(f"no voice lines for interviewee {sid}")
    name = a["displayName"]

    branches = []
    for key in ("access", "red_herring", "alibi_support", "roster", "sign_in"):
        questions = []
        for index, (suffix, prompt, aggressive) in enumerate(PROMPTS[key]):
            replies = replies_for(sid, key, index)
            for reply in replies:
                length = len(reply["line"])
                if length < 50 or length > 320:
                    raise SystemExit(f"{sid} {key} {suffix} line length {length}: {reply['line'][:60]}")
                if reply.get("slip") and reply["minStress"] < 85:
                    raise SystemExit(f"{sid} casual slip before 85 on {key}")
            questions.append(question(f"{sid}_{suffix}", prompt, replies, aggressive))
        branches.append(branch(key, questions))

    for key, suffix in (("hold", "hold"), ("release", "release")):
        replies = replies_for(sid, key, 0)
        for reply in replies:
            length = len(reply["line"])
            if length < 40 or length > 300:
                raise SystemExit(f"{sid} {key} line length {length}")
            if reply.get("slip") and reply["minStress"] < 85:
                raise SystemExit(f"{sid} casual slip on {key}")
        branches.append(branch(key, [question(f"{sid}_{suffix}", suffix.upper(), replies, False)]))

    return {"suspectId": sid, "displayName": name, "branches": branches}


def main() -> None:
    data = json.loads(AFFILIATES.read_text(encoding="utf-8"))
    interviewees = [a for a in data.get("affiliates", []) if a.get("tier") == "interviewee"]
    if len(interviewees) < 8:
        raise SystemExit(f"Expected ≥8 interviewees, got {len(interviewees)}")
    missing = [a["id"] for a in interviewees if a["id"] not in PEOPLE]
    if missing:
        raise SystemExit(f"missing voices: {missing}")

    OUT.mkdir(parents=True, exist_ok=True)
    seen = set()
    index = {"trees": []}
    for a in interviewees:
        tree = build_tree(a)
        blob = json.dumps(tree).lower()
        for banned in (
            "9:37", "9:15", "storage-room", "event-storage", "mascot head", "took the head", "nordy",
            "incident window", "the trap", "checklist", "upstairs", "the page", "motive on the table",
            "the quiet", "better version", "final version", "second version", "first version",
            "blank check", "hall pass", "all-access", "all-campus pass", "new location",
            "spot i'm giving", "coordinate i'm giving", "path i'm giving", "different crop",
            "that's the cut", "covered my slice", "monitor in your head", "dressing it up",
            "neater sentence", "stop circling", "treat a ", "treat the ",
        ):
            if banned in blob:
                raise SystemExit(f"Banned phrase {banned!r} in tree {a['id']}")
        for branch_obj in tree["branches"]:
            for q in branch_obj["questions"]:
                for reply in q["replies"]:
                    line = reply["line"]
                    if reply.get("slip"):
                        if "{act}" not in line:
                            raise SystemExit(f"{a['id']} slip line has no act: {line[:80]}")
                        low = line.lower()
                        for banned_slip in (" cover", "version", "the file", "the release"):
                            if banned_slip in low:
                                raise SystemExit(f"{a['id']} slip still has {banned_slip!r}: {line[:80]}")
                    elif "{act}" in line:
                        raise SystemExit(f"{a['id']} non-slip line names the act: {line[:80]}")
                    if line in seen:
                        raise SystemExit(f"duplicate line in {a['id']}: {line[:80]}")
                    seen.add(line)
        path = OUT / f"{a['id']}.json"
        path.write_text(json.dumps(tree, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
        write_meta(path)
        index["trees"].append(a["id"])
        print("wrote", path.name, "branches", len(tree["branches"]))

    index_path = OUT / "tree_index.json"
    index_path.write_text(json.dumps(index, indent=2) + "\n", encoding="utf-8")
    write_meta(index_path)
    print("index", index["trees"], "unique lines", len(seen))


if __name__ == "__main__":
    main()
