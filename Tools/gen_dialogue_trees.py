#!/usr/bin/env python3
"""Generate evidence-driven dialogue trees under Assets/Resources/Dialogue/Trees/.

Prompts stay case-agnostic (no shared storage / 9:37 / mascot-head boilerplate) so
rotating case_templates.json titles and clue bodies remain the unique flavor per shift.
"""
from __future__ import annotations

import json
import uuid
from pathlib import Path

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


def reply(lo, hi, emotion, line, slip=False):
    return {
        "minStress": lo,
        "maxStress": hi,
        "emotion": emotion,
        "slip": bool(slip),
        "line": line,
    }


def stress_pack(calm, nervous, scared, broken, slip_at_scared=False):
    return [
        reply(0, 29, "calm", calm, False),
        reply(30, 59, "nervous", nervous, False),
        reply(60, 84, "scared", scared, slip_at_scared),
        reply(85, 100, "broken", broken, True),
    ]


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
    name = a["displayName"]
    first = name.split()[0]
    statement = (a.get("statement") or "I left before the incident window.").strip()
    alibi = (a.get("alibi") or "Elsewhere during the window.").strip()
    alibi_title = a.get("alibiSupportTitle") or "Alibi support"
    alibi_desc = a.get("alibiSupportDescription") or alibi
    roster_title = a.get("contextTitleA") or "Roster"
    roster_desc = a.get("contextDescriptionA") or a.get("bio") or ""
    herring = a.get("redHerringRecord") or "A suspicious purchase shows up under your name."
    herring_explain = a.get("redHerringExplanation") or "That was for ordinary campus work."

    access_qs = [
        question(
            f"{sid}_access_q1",
            "This access record puts you inside the incident window after you said you left. Explain.",
            stress_pack(
                "That record is wrong. I was already gone.",
                "That timestamp can't be me. I left earlier.",
                "Okay — I went back. Just for a second. Not for what went missing.",
                "Fine. I opened that door after I said I left. I did it. I went back.",
                slip_at_scared=True,
            ),
            aggressive=True,
        ),
        question(
            f"{sid}_access_q2",
            "Your campus credential hit a restricted reader inside the incident window.",
            stress_pack(
                "I never went back in. Check the cameras — not me.",
                "Someone could have borrowed my card. It happens.",
                "I… might have swiped back in. I needed something from the cart.",
                "It was me. I did it. I went back.",
                slip_at_scared=True,
            ),
            aggressive=True,
        ),
        question(
            f"{sid}_access_q3",
            f'You claimed: "{statement}" How does that access fit?',
            stress_pack(
                "It doesn't — because I wasn't there. The log is dirty.",
                "I stick by that statement. The access log has to be a glitch.",
                "I left, then panicked and came back. Stupid.",
                "The statement was a lie. I went back during the window.",
                slip_at_scared=True,
            ),
        ),
        question(
            f"{sid}_access_q4",
            "If you left when you said, why is your credential on that door inside the window?",
            stress_pack(
                "Ask security about false reads. I was not in that room.",
                "I don't have a clean answer — but I didn't do it.",
                "Because I went back. I shouldn't have.",
                "Because I did it. That's why.",
                slip_at_scared=True,
            ),
            aggressive=True,
        ),
    ]

    herring_qs = [
        question(
            f"{sid}_herring_q1",
            f"This record: {herring} Explain it.",
            stress_pack(
                f"{herring_explain}",
                f"That looks bad, I know. {herring_explain}",
                "I bought / borrowed that for ordinary work — not for the incident.",
                "Believe what you want. It still doesn't put me at the scene during the window.",
            ),
        ),
        question(
            f"{sid}_herring_q2",
            "Looks like prep for moving or cutting something. Was it for what went missing?",
            stress_pack(
                "No. Ordinary campus work only.",
                "People always jump to that. It was unrelated gear.",
                "I get why you'd ask. Still not me on what went missing.",
                "I didn't do it. That record is a red herring and you know it.",
            ),
            aggressive=True,
        ),
        question(
            f"{sid}_herring_q3",
            f"Walk me through this suspicious record against your alibi: {alibi}",
            stress_pack(
                f"Easy — {alibi}. The purchase / checkout was earlier, for setup.",
                f"Alibi first: {alibi}. The record is unrelated.",
                "I'm rattled, but the timeline still clears me.",
                "Pin the incident on someone with a hard access hit. Not this.",
            ),
        ),
        question(
            f"{sid}_herring_q4",
            "Why should we trust you after this shows up under your name?",
            stress_pack(
                "Because I'm on camera / logs elsewhere during the window.",
                "Trust the other records — not one ugly receipt.",
                "I'm nervous because it looks bad. That doesn't make me guilty.",
                "I've answered. Stamp someone else.",
            ),
            aggressive=True,
        ),
    ]

    alibi_qs = [
        question(
            f"{sid}_alibi_q1",
            f"{alibi_title}: {alibi_desc} Does that match your story?",
            stress_pack(
                f"Yes. {alibi}",
                f"That's exactly where I was. {alibi}",
                "It should. Unless the clock is off, I was there.",
                "I need you to believe that log. I wasn't at the scene.",
            ),
        ),
        question(
            f"{sid}_alibi_q2",
            f"You said you left before the incident. Prove {alibi}.",
            stress_pack(
                f"The support record does. Read {alibi_title}.",
                f"I left early. Then {alibi}.",
                "I'm telling the same story. Check the timestamp again.",
                "If that support log is real, I'm clear.",
            ),
        ),
        question(
            f"{sid}_alibi_q3",
            "Could you have doubled back to the scene after this alibi stamp?",
            stress_pack(
                "No. Once I left, I stayed put.",
                "Not without showing up on something else — and I don't.",
                "I… don't think so. I was tired. I went straight there.",
                "Doubling back would be insane. I didn't.",
            ),
            aggressive=True,
        ),
        question(
            f"{sid}_alibi_q4",
            f"Who saw you during the window if {alibi_title} is wrong?",
            stress_pack(
                "It's not wrong. Ask around the area.",
                "People were there. I wasn't hiding.",
                "I can't name everyone. The record should be enough.",
                "I don't have a crowd of witnesses. I have that log.",
            ),
        ),
    ]

    roster_qs = [
        question(
            f"{sid}_roster_q1",
            f"{roster_title} puts you on site that evening. When did you actually leave?",
            stress_pack(
                "Before the incident window. Same as my statement.",
                "Shift / duty ended; I walked out before the window.",
                "I might've lingered a minute — not through the whole window.",
                "I left when I said I left. Don't twist the roster.",
            ),
        ),
        question(
            f"{sid}_roster_q2",
            f"This roster listing: {roster_desc} Did that give you special access?",
            stress_pack(
                "Being listed isn't a key to the scene.",
                "Lots of people were listed. Only one culprit.",
                "I had a job to do. I finished and left.",
                "Roster presence ≠ guilt. Move on.",
            ),
        ),
        question(
            f"{sid}_roster_q3",
            f"Why does the roster still matter if you claim {alibi}?",
            stress_pack(
                "It just shows I was helping earlier. Then I left.",
                "Context only. The alibi is the point.",
                "You're stacking paper. My exit time is what counts.",
                "Stamp me or don't — the roster isn't a confession.",
            ),
            aggressive=True,
        ),
        question(
            f"{sid}_roster_q4",
            "Anyone on this roster could've stayed late. Did you?",
            stress_pack(
                "No. Out before the window.",
                "I didn't stay for the incident window.",
                "I wanted to go. I went.",
                "Late stayers exist. I wasn't one.",
            ),
        ),
    ]

    sign_qs = [
        question(
            f"{sid}_sign_q1",
            f"Sign-in shows {first} on site earlier. When did you sign out?",
            stress_pack(
                "I was done and gone before the incident window.",
                "Sign-in isn't a life sentence. I left.",
                "I signed in earlier. I did not hang around for the incident.",
                "Setup, then exit. That's the whole sheet.",
            ),
        ),
        question(
            f"{sid}_sign_q2",
            "Being on the sign-in sheet put you near the scene. Convenient?",
            stress_pack(
                "Convenient for volunteering. Not for the incident.",
                "Near the scene earlier ≠ inside during the window.",
                "I don't like how that sounds, but it's still not proof.",
                "Stop fishing. Sign-in is earlier activity only.",
            ),
            aggressive=True,
        ),
        question(
            f"{sid}_sign_q3",
            "Did signing in get you a key or special credential?",
            stress_pack(
                "No special access from a name on a sheet.",
                "Volunteers don't get free raids on restricted rooms.",
                "If keys moved, it wasn't because of my signature.",
                "Ask who held keys. My name on paper isn't that.",
            ),
        ),
        question(
            f"{sid}_sign_q4",
            f'Reconcile the sign-in with: "{statement}"',
            stress_pack(
                "Signed in, worked, left before the window. Consistent.",
                "Same story every time.",
                "I keep repeating it because it's true.",
                "Sign-in earlier, departure as stated. Done.",
            ),
        ),
    ]

    hold_qs = [
        question(
            f"{sid}_hold",
            "HOLD",
            [
                reply(0, 100, "scared", "You're holding me? I didn't do this.", False),
                reply(0, 100, "angry", "This is a mistake. I want that on the record.", False),
                reply(60, 100, "broken", "…I can't keep this up. Check the access trail for my prints.", True),
            ],
        )
    ]
    release_qs = [
        question(
            f"{sid}_release",
            "RELEASE",
            [
                reply(0, 100, "calm", "Thank you. I told you I wasn't the one.", False),
                reply(0, 100, "nervous", "Okay. I'm leaving. Don't call me back unless you have proof.", False),
            ],
        )
    ]

    return {
        "suspectId": sid,
        "displayName": name,
        "branches": [
            branch("access", access_qs),
            branch("red_herring", herring_qs),
            branch("alibi_support", alibi_qs),
            branch("roster", roster_qs),
            branch("sign_in", sign_qs),
            branch("hold", hold_qs),
            branch("release", release_qs),
        ],
    }


def main() -> None:
    data = json.loads(AFFILIATES.read_text(encoding="utf-8"))
    interviewees = [a for a in data.get("affiliates", []) if a.get("tier") == "interviewee"]
    if len(interviewees) < 8:
        raise SystemExit(f"Expected ≥8 interviewees, got {len(interviewees)}")

    OUT.mkdir(parents=True, exist_ok=True)

    index = {"trees": []}
    for a in interviewees:
        tree = build_tree(a)
        blob = json.dumps(tree).lower()
        for banned in ("9:37", "9:15", "storage-room", "event-storage", "mascot head", "took the head", "the head"):
            if banned in blob:
                raise SystemExit(f"Banned phrase {banned!r} in tree {a['id']}")
        path = OUT / f"{a['id']}.json"
        path.write_text(json.dumps(tree, indent=2) + "\n", encoding="utf-8")
        write_meta(path)
        index["trees"].append(a["id"])
        print("wrote", path.name, "branches", len(tree["branches"]))

    index_path = OUT / "tree_index.json"
    index_path.write_text(json.dumps(index, indent=2) + "\n", encoding="utf-8")
    write_meta(index_path)
    print("index", index["trees"])


if __name__ == "__main__":
    main()
