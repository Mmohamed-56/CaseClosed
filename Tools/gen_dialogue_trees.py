#!/usr/bin/env python3
"""Generate evidence-driven dialogue trees under Assets/Resources/Dialogue/Trees/."""
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
    if path.suffix == ".cs":
        body = (
            "fileFormatVersion: 2\n"
            f"guid: {new_guid()}\n"
            "MonoImporter:\n"
            "  externalObjects: {}\n"
            "  serializedVersion: 2\n"
            "  defaultReferences: []\n"
            "  executionOrder: 0\n"
            "  icon: {instanceID: 0}\n"
            "  userData: \n"
            "  assetBundleName: \n"
            "  assetBundleVariant: \n"
        )
    else:
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
    statement = (a.get("statement") or "").strip()
    alibi = (a.get("alibi") or "").strip()
    alibi_title = a.get("alibiSupportTitle") or "Alibi support"
    alibi_desc = a.get("alibiSupportDescription") or alibi
    roster_title = a.get("contextTitleA") or "Roster"
    roster_desc = a.get("contextDescriptionA") or a.get("bio") or ""
    herring = a.get("redHerringRecord") or "A suspicious purchase shows up under your name."
    herring_explain = a.get("redHerringExplanation") or "That was for event work."

    access_qs = [
        question(
            f"{sid}_access_q1",
            f"Storage access at 9:37 — you said you left before 9:15. Explain.",
            stress_pack(
                f"That card ping is wrong. I was already gone.",
                f"Nine thirty-seven? That can't be my card. I left earlier.",
                f"Okay — I went back. Just for a second. Not for what went missing.",
                f"Fine. I opened that door after I said I left. I did it. I went back.",
                slip_at_scared=True,
            ),
            aggressive=True,
        ),
        question(
            f"{sid}_access_q2",
            f"Your campus card opened a restricted door inside the incident window.",
            stress_pack(
                f"I never went back in. Check the cameras — not me.",
                f"Someone could have borrowed my card. It happens.",
                f"I… might have swiped back in. I needed something from the cart.",
                f"It was me. I did it. I went back.",
                slip_at_scared=True,
            ),
            aggressive=True,
        ),
        question(
            f"{sid}_access_q3",
            f"You claimed: \"{statement}\" How does that access fit?",
            stress_pack(
                f"It doesn't — because I wasn't there. The log is dirty.",
                f"I stick by that statement. The access log has to be a glitch.",
                f"I left, then panicked and came back. Stupid.",
                f"The statement was a lie. I went back during the window.",
                slip_at_scared=True,
            ),
        ),
        question(
            f"{sid}_access_q4",
            f"If you left when you said, why is your card on that door inside the window?",
            stress_pack(
                f"Ask security about false reads. I was not in that room.",
                f"I don't have a clean answer — but I didn't do it.",
                f"Because I went back. I shouldn't have.",
                f"Because I did it. That's why.",
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
                f"I bought / borrowed that for the event — not for theft.",
                f"Believe what you want. It still doesn't put me in storage at 9:37.",
            ),
        ),
        question(
            f"{sid}_herring_q2",
            f"Looks like prep for cutting or moving something bulky. Was it for what went missing?",
            stress_pack(
                f"No. Event work only.",
                f"People always jump to that. It was event gear.",
                f"I get why you'd ask. Still not me on what went missing.",
                f"I didn't do it. That record is a red herring and you know it.",
            ),
            aggressive=True,
        ),
        question(
            f"{sid}_herring_q3",
            f"Walk me through this suspicious purchase against your alibi: {alibi}",
            stress_pack(
                f"Easy — {alibi}. The purchase was earlier, for setup.",
                f"Alibi first: {alibi}. The purchase is unrelated.",
                f"I'm rattled, but the timeline still clears me.",
                f"Pin the head on someone with a storage swipe. Not this.",
            ),
        ),
        question(
            f"{sid}_herring_q4",
            f"Why should we trust you after this shows up under your name?",
            stress_pack(
                f"Because I'm on camera / logs elsewhere during the window.",
                f"Trust the other records — not one ugly receipt.",
                f"I'm nervous because it looks bad. That doesn't make me guilty.",
                f"I've answered. Stamp someone else.",
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
                f"It should. Unless the clock is off, I was there.",
                f"I need you to believe that log. I wasn't in storage.",
            ),
        ),
        question(
            f"{sid}_alibi_q2",
            f"You said you left before 9:15. Prove {alibi}.",
            stress_pack(
                f"The support record does. Read {alibi_title}.",
                f"I left early. Then {alibi}.",
                f"I'm telling the same story. Check the timestamp again.",
                f"If that support log is real, I'm clear.",
            ),
        ),
        question(
            f"{sid}_alibi_q3",
            f"Could you have doubled back to storage after this alibi stamp?",
            stress_pack(
                f"No. Once I left, I stayed put.",
                f"Not without showing up on something else — and I don't.",
                f"I… don't think so. I was tired. I went straight there.",
                f"Doubling back would be insane. I didn't.",
            ),
            aggressive=True,
        ),
        question(
            f"{sid}_alibi_q4",
            f"Who saw you after 9:15 if {alibi_title} is wrong?",
            stress_pack(
                f"It's not wrong. Ask around the area.",
                f"People were there. I wasn't hiding.",
                f"I can't name everyone. The record should be enough.",
                f"I don't have a crowd of witnesses. I have that log.",
            ),
        ),
    ]

    roster_qs = [
        question(
            f"{sid}_roster_q1",
            f"{roster_title} puts you on site that evening. When did you actually leave?",
            stress_pack(
                f"Before 9:15. Same as my statement.",
                f"Shift / duty ended; I walked out before the window.",
                f"I might've lingered a minute — not until 9:37.",
                f"I left when I said I left. Don't twist the roster.",
            ),
        ),
        question(
            f"{sid}_roster_q2",
            f"This roster listing: {roster_desc} Did that give you storage access?",
            stress_pack(
                f"Being listed isn't a key to the head.",
                f"Lots of people were listed. Only one thief.",
                f"I had a job to do. I finished and left.",
                f"Roster presence ≠ theft. Move on.",
            ),
        ),
        question(
            f"{sid}_roster_q3",
            f"Why does the roster still matter if you claim {alibi}?",
            stress_pack(
                f"It just shows I was helping earlier. Then I left.",
                f"Context only. The alibi is the point.",
                f"You're stacking paper. My exit time is what counts.",
                f"Stamp me or don't — the roster isn't a confession.",
            ),
            aggressive=True,
        ),
        question(
            f"{sid}_roster_q4",
            f"Anyone on this roster could've stayed late. Did you?",
            stress_pack(
                f"No. Out before 9:15.",
                f"I didn't stay for the theft window.",
                f"I wanted to go. I went.",
                f"Late stayers exist. I wasn't one.",
            ),
        ),
    ]

    sign_qs = [
        question(
            f"{sid}_sign_q1",
            f"Event sign-in shows {first} helping setup. When did you sign out?",
            stress_pack(
                f"I was done and gone before 9:15.",
                f"Sign-in isn't a life sentence. I left.",
                f"I signed in earlier. I did not hang around for the head.",
                f"Setup, then exit. That's the whole sheet.",
            ),
        ),
        question(
            f"{sid}_sign_q2",
            f"Being on the sign-in sheet put you near storage. Convenient?",
            stress_pack(
                f"Convenient for volunteering. Not for stealing.",
                f"Near storage earlier ≠ inside at 9:37.",
                f"I don't like how that sounds, but it's still not proof.",
                f"Stop fishing. Sign-in is daytime/evening setup only.",
            ),
            aggressive=True,
        ),
        question(
            f"{sid}_sign_q3",
            f"Did signing in get you a storage key or card?",
            stress_pack(
                f"No special access from a name on a sheet.",
                f"Volunteers don't get free storage raids.",
                f"If keys moved, it wasn't because of my signature.",
                f"Ask who held keys. My name on paper isn't that.",
            ),
        ),
        question(
            f"{sid}_sign_q4",
            f"Reconcile the sign-in with: \"{statement}\"",
            stress_pack(
                f"Signed in, worked, left before 9:15. Consistent.",
                f"Same story every time.",
                f"I keep repeating it because it's true.",
                f"Sign-in earlier, departure as stated. Done.",
            ),
        ),
    ]

    hold_qs = [
        question(
            f"{sid}_hold",
            "HOLD",
            [
                reply(0, 100, "scared", f"You're holding me? I didn't take the head.", False),
                reply(0, 100, "angry", f"This is a mistake. I want that on the record.", False),
                reply(60, 100, "broken", f"…I can't keep this up. Check storage for my prints.", True),
            ],
        )
    ]
    release_qs = [
        question(
            f"{sid}_release",
            "RELEASE",
            [
                reply(0, 100, "calm", f"Thank you. I told you I wasn't the one.", False),
                reply(0, 100, "nervous", f"Okay. I'm leaving. Don't call me back unless you have proof.", False),
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
    write_meta(OUT)  # folder meta optional; Unity makes DefaultAsset — skip if dir

    index = {"trees": []}
    for a in interviewees:
        tree = build_tree(a)
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
