#!/usr/bin/env python3
"""Generate a playable ScenarioDialogue bank under Assets/Resources/Dialogue/."""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1] / "Assets" / "Resources" / "Dialogue"

SUSPECTS = {
    "jamie": {
        "name": "Jamie Rivera",
        "role": "student volunteer",
        "alibi": "west bus loop",
        "detail": "transit tap at 9:24",
        "herring": "utility knife for decorations",
    },
    "morgan": {
        "name": "Morgan Lee",
        "role": "events assistant",
        "alibi": "residence hall",
        "detail": "hall door at 9:18",
        "herring": "storage key returned before the window",
    },
    "avery": {
        "name": "Avery Patel",
        "role": "facilities worker",
        "alibi": "library wing",
        "detail": "radio check-in at 9:28",
        "herring": "laundry cart for event linens",
    },
    "riley": {
        "name": "Riley Brooks",
        "role": "club treasurer",
        "alibi": "study room B",
        "detail": "room reserved from 9:15",
        "herring": "heavy-duty tape for the club fair",
    },
    "casey": {
        "name": "Casey Nguyen",
        "role": "mascot squad member",
        "alibi": "costume annex",
        "detail": "badge swipe at 9:22",
        "herring": "spare torso shell for the pep rally",
    },
    "drew": {
        "name": "Drew Okonkwo",
        "role": "campus security aide",
        "alibi": "south quad patrol",
        "detail": "checkpoint ping at 9:31",
        "herring": "master key returned at 9:05",
    },
    "quinn": {
        "name": "Quinn Alvarez",
        "role": "student media photographer",
        "alibi": "media lab",
        "detail": "lab login at 9:26",
        "herring": "rolling camera case for lights",
    },
    "sam": {
        "name": "Sam Torvald",
        "role": "dining services closer",
        "alibi": "union cafe",
        "detail": "POS closeout at 9:33",
        "herring": "zip ties for snack bags",
    },
}

STRESS = {
    "calm": (0, 29, "calm"),
    "nervous": (30, 59, "nervous"),
    "cracking": (60, 84, "scared"),
    "broken": (85, 100, "broken"),
}


def entry(eid, suspect, intent, line, emotion, stress_key="calm", **kw):
    lo, hi, default_emotion = STRESS[stress_key]
    return {
        "id": eid,
        "suspectId": suspect,
        "intent": intent,
        "keywords": kw.get("keywords", ""),
        "minStress": kw.get("minStress", lo),
        "maxStress": kw.get("maxStress", hi),
        "emotion": emotion or default_emotion,
        "slip": bool(kw.get("slip", False)),
        "line": line,
        "requiresEvidence": bool(kw.get("requiresEvidence", False)),
        "requiresAggressive": bool(kw.get("requiresAggressive", False)),
        "culpritOnly": bool(kw.get("culpritOnly", False)),
        "innocentOnly": bool(kw.get("innocentOnly", False)),
    }


def build_suspect(sid: str, meta: dict) -> list:
    name = meta["name"].split()[0]
    alibi = meta["alibi"]
    detail = meta["detail"]
    herring = meta["herring"]
    role = meta["role"]
    rows = []

    # Alibi — 4 stress tiers x 3 variants
    alibi_lines = {
        "calm": [
            f"I was at the {alibi}. That's the whole story.",
            f"Check the record — {detail}. I wasn't near storage.",
            f"I left the union and went to the {alibi}. Ask anyone who saw me.",
        ],
        "nervous": [
            f"I already said — {alibi}. Why keep asking?",
            f"The {detail} should settle this. I was nowhere near that room.",
            f"Look, I can walk you through my path to the {alibi} again.",
        ],
        "cracking": [
            f"I keep repeating the {alibi} because it's true.",
            f"If the {detail} is wrong, somebody else's badge is wrong, not mine.",
            f"Please — I was at the {alibi}. Don't twist that.",
        ],
        "broken": [
            f"I can't prove more than the {alibi}. That's all I have.",
            f"I'm tired of this. {detail}. End of list.",
            f"Believe the {alibi} or don't. I have nothing else.",
        ],
    }
    for stress, lines in alibi_lines.items():
        for i, line in enumerate(lines, 1):
            rows.append(
                entry(
                    f"{sid}_alibi_{stress}_{i}",
                    sid,
                    "alibi",
                    line,
                    STRESS[stress][2],
                    stress,
                    keywords="where|alibi|were you|what time|when did|location|bus|home|patrol|study|lab|cafe|annex|library|quad",
                )
            )

    # Evidence present
    evidence_lines = {
        "calm": [
            f"That record? It matches the {herring}. Not a theft.",
            f"You're holding paper, not proof I took the head.",
            f"Fine, look at it. It still puts me at the {alibi}.",
        ],
        "nervous": [
            f"Okay, that looks bad, but the {herring} is the real reason.",
            f"Don't pin a theft on a {role} doing their job.",
            f"If that log is about me, read the timestamp against my alibi.",
        ],
        "cracking": [
            f"You're waving that like a confession. It isn't.",
            f"I can explain the {herring}. Stop acting like I already confessed.",
            f"That evidence doesn't move me off the {alibi}.",
        ],
        "broken": [
            f"I've explained the {herring} as far as I can.",
            "Whatever that paper says, I didn't take the mascot head.",
            f"Stamp whatever you want. My answer stays the {alibi}.",
        ],
    }
    for stress, lines in evidence_lines.items():
        for i, line in enumerate(lines, 1):
            rows.append(
                entry(
                    f"{sid}_evidence_{stress}_{i}",
                    sid,
                    "evidence",
                    line,
                    STRESS[stress][2],
                    stress,
                    keywords="evidence|log|receipt|record|this|prove|access|card|look",
                    requiresEvidence=True,
                )
            )
            # Also allow without requiresEvidence so keyword-only evidence asks work
            rows.append(
                entry(
                    f"{sid}_evidence_soft_{stress}_{i}",
                    sid,
                    "evidence",
                    line,
                    STRESS[stress][2],
                    stress,
                    keywords="evidence|log|receipt|record|prove|access",
                )
            )

    # Pressure
    pressure_lines = {
        "calm": [
            "Pressure doesn't invent a crime. Ask a real question.",
            f"I'm a {role}, not your shortcut to a collar.",
            "Raise your voice all you want. My timeline doesn't change.",
        ],
        "nervous": [
            "Back off. I'm answering, not performing for you.",
            "You want me rattled. That isn't the same as guilty.",
            f"Stop crowding me. I was at the {alibi}.",
        ],
        "cracking": [
            "Enough. You're trying to break me, not find the truth.",
            "I said what happened. Stop cornering me.",
            "This isn't fair. I keep answering and you keep pushing.",
        ],
        "broken": [
            "I want this over. I didn't take it.",
            "Do whatever stamp you came to do.",
            "I can't keep matching your volume.",
        ],
    }
    for stress, lines in pressure_lines.items():
        for i, line in enumerate(lines, 1):
            rows.append(
                entry(
                    f"{sid}_pressure_{stress}_{i}",
                    sid,
                    "pressure",
                    line,
                    "angry" if stress in ("nervous", "cracking") else STRESS[stress][2],
                    stress,
                    keywords="enough|stop lying|cut the|we know|last chance|don't play|pressure",
                    requiresAggressive=stress in ("cracking", "broken"),
                )
            )
            rows.append(
                entry(
                    f"{sid}_pressure_any_{stress}_{i}",
                    sid,
                    "pressure",
                    line,
                    "angry" if stress != "calm" else "defensive",
                    stress,
                    keywords="enough|lying|admit|last chance|we know",
                )
            )

    # Small talk
    small = {
        "calm": [
            f"I'm {name}. Campus {role}. That's it.",
            f"I help around campus as a {role}. Why?",
            "Ask about the mascot, not my resume.",
        ],
        "nervous": [
            f"Why the soft questions? I'm {name}.",
            "Small talk won't make the theft appear in my hands.",
            f"Yes, I'm a {role}. No, I didn't steal a head.",
        ],
        "cracking": [
            "Can we skip the friendly act?",
            f"I'm still {name}. Still not your thief.",
            "You're wasting both our time with this.",
        ],
        "broken": [
            "Just ask what you need.",
            f"{name}. {role}. Done.",
            "I don't have energy for introductions.",
        ],
    }
    for stress, lines in small.items():
        for i, line in enumerate(lines, 1):
            rows.append(
                entry(
                    f"{sid}_small_{stress}_{i}",
                    sid,
                    "small_talk",
                    line,
                    STRESS[stress][2],
                    stress,
                    keywords="name|who are|how are|tell me about|role|job|hello|hi|major",
                )
            )

    # Accusation
    accuse = {
        "calm": [
            "I didn't take the mascot head. Period.",
            f"Accuse me after you check the {alibi}.",
            "Calling me a thief doesn't make it true.",
        ],
        "nervous": [
            "Don't put that on me.",
            f"I was at the {alibi}, not ripping off storage.",
            "You're guessing. Guess better.",
        ],
        "cracking": [
            "Stop saying I stole it.",
            "If you charge me on vibes, that's on you.",
            f"I keep saying {alibi}. Hear it.",
        ],
        "broken": [
            "I didn't steal anything.",
            "Charge me and watch the real culprit walk.",
            "I'm done defending a crime I didn't do.",
        ],
    }
    for stress, lines in accuse.items():
        for i, line in enumerate(lines, 1):
            rows.append(
                entry(
                    f"{sid}_accuse_{stress}_{i}",
                    sid,
                    "accusation",
                    line,
                    "defensive" if stress == "calm" else STRESS[stress][2],
                    stress,
                    keywords="stole|took|lying|admit|confess|guilty|thief|mascot",
                )
            )

    # Generic fillers
    generic = {
        "calm": [
            "Ask something specific.",
            "I'm listening.",
            f"Go ahead. {name} is here.",
        ],
        "nervous": [
            "What now?",
            "Say it clearly.",
            "I'm still answering.",
        ],
        "cracking": [
            "Make it count.",
            "I don't have many answers left in me.",
            "Fine. Ask.",
        ],
        "broken": [
            "...",
            "Whatever you say.",
            "Just finish it.",
        ],
    }
    for stress, lines in generic.items():
        for i, line in enumerate(lines, 1):
            rows.append(
                entry(
                    f"{sid}_generic_{stress}_{i}",
                    sid,
                    "generic",
                    line,
                    STRESS[stress][2],
                    stress,
                )
            )

    # HOLD / RELEASE aftermath
    hold_lines = [
        ("calm", "calm", "You're holding me? Then check the records twice."),
        ("calm", "defensive", f"HOLD me if you want. The {alibi} still stands."),
        ("nervous", "nervous", "Please don't hold me overnight on a maybe."),
        ("nervous", "scared", "A hold means my shift, my classes — everything."),
        ("cracking", "scared", "You're really stamping HOLD on me?"),
        ("broken", "broken", "Fine. Hold. I can't fight the stamp."),
    ]
    for i, (stress, emotion, line) in enumerate(hold_lines, 1):
        rows.append(
            entry(
                f"{sid}_hold_{i}",
                sid,
                "hold",
                line,
                emotion,
                stress,
                keywords="HOLD|hold",
            )
        )

    release_lines = [
        ("calm", "calm", "Release. Good. Don't waste the next person's time either."),
        ("calm", "calm", f"I'm walking. Remember the {alibi} when you write this up."),
        ("nervous", "nervous", "Thank you. I meant what I said."),
        ("cracking", "scared", "You're letting me go? Then find who actually did it."),
        ("broken", "broken", "Release me. I'm empty."),
    ]
    for i, (stress, emotion, line) in enumerate(release_lines, 1):
        rows.append(
            entry(
                f"{sid}_release_{i}",
                sid,
                "release",
                line,
                emotion,
                stress,
                keywords="RELEASE|release",
            )
        )

    # Culprit slip variants (only when CaseCore marks culprit + high stress)
    slips = [
        ("cracking", "I... I went back. Just for a second."),
        ("cracking", "Don't look at the access time. Please."),
        ("broken", "I was there that night. Past my story."),
        ("broken", "The head — I moved it. I panicked."),
        ("broken", "Storage. 9:37. That's me. I'm sorry."),
    ]
    for i, (stress, line) in enumerate(slips, 1):
        rows.append(
            entry(
                f"{sid}_slip_{i}",
                sid,
                "slip",
                line,
                "scared" if stress == "cracking" else "broken",
                stress,
                slip=True,
                culpritOnly=True,
                keywords="where|admit|confess|mascot|storage|access|truth",
            )
        )

    # Shutdown
    for i, line in enumerate(
        [
            "I have nothing more to say.",
            "Lawyer. Now.",
            "We're done talking.",
        ],
        1,
    ):
        rows.append(
            entry(
                f"{sid}_shutdown_{i}",
                sid,
                "shutdown",
                line,
                "angry",
                "broken",
                minStress=0,
                maxStress=100,
                innocentOnly=True,
            )
        )

    return rows


def build_common() -> list:
    rows = []
    common_alibi = [
        ("calm", "I already told you where I was."),
        ("nervous", "My alibi hasn't changed."),
        ("cracking", "Stop making me repeat the same place."),
        ("broken", "I can't invent a better alibi. This is it."),
    ]
    for i, (stress, line) in enumerate(common_alibi, 1):
        rows.append(
            entry(
                f"common_alibi_{i}",
                "*",
                "alibi",
                line,
                STRESS[stress][2],
                stress,
                keywords="where|alibi|were you",
            )
        )

    for i, (stress, line) in enumerate(
        [
            ("calm", "That paper doesn't scare me."),
            ("nervous", "Okay, show me. Still not a confession."),
            ("cracking", "Evidence isn't the same as guilt."),
            ("broken", "I've seen it. My answer is the same."),
        ],
        1,
    ):
        rows.append(
            entry(
                f"common_evidence_{i}",
                "*",
                "evidence",
                line,
                STRESS[stress][2],
                stress,
                keywords="evidence|log|receipt|record",
            )
        )

    for i, line in enumerate(
        [
            "I didn't do it.",
            "Wrong person.",
            "Find someone else to charge.",
        ],
        1,
    ):
        rows.append(
            entry(
                f"common_accuse_{i}",
                "*",
                "accusation",
                line,
                "defensive",
                "calm",
                keywords="stole|guilty|thief|confess",
                minStress=0,
                maxStress=100,
            )
        )

    for i, line in enumerate(
        [
            "Ask your question.",
            "I'm here.",
            "Go on.",
            "Waiting.",
        ],
        1,
    ):
        rows.append(
            entry(
                f"common_generic_{i}",
                "*",
                "generic",
                line,
                "calm",
                "calm",
                minStress=0,
                maxStress=100,
            )
        )

    for decision, lines in {
        "hold": [
            "You're holding me? Make the paperwork honest.",
            "A HOLD stamp isn't a conviction.",
            "Fine. Hold. Check the timestamps again.",
        ],
        "release": [
            "Release. Finally.",
            "I'm leaving. Good luck with the real lead.",
            "Stamp RELEASE and move on.",
        ],
    }.items():
        for i, line in enumerate(lines, 1):
            rows.append(
                entry(
                    f"common_{decision}_{i}",
                    "*",
                    decision,
                    line,
                    "defensive" if decision == "hold" else "calm",
                    "calm",
                    minStress=0,
                    maxStress=100,
                )
            )

    return rows


def write_bank(name: str, entries: list) -> int:
    ROOT.mkdir(parents=True, exist_ok=True)
    path = ROOT / f"{name}.json"
    payload = {"entries": entries}
    path.write_text(json.dumps(payload, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    return len(entries)


def main() -> None:
    totals = {}
    totals["common"] = write_bank("common", build_common())
    for sid, meta in SUSPECTS.items():
        totals[sid] = write_bank(sid, build_suspect(sid, meta))

    index = {"banks": ["common"] + list(SUSPECTS.keys())}
    (ROOT / "bank_index.json").write_text(
        json.dumps(index, indent=2) + "\n", encoding="utf-8"
    )
    grand = sum(totals.values())
    print(json.dumps({"files": totals, "total_lines": grand}, indent=2))


if __name__ == "__main__":
    main()
