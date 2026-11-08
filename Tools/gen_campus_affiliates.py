#!/usr/bin/env python3
"""Generate campus affiliate pool + light witness dialogue. Batch-friendly toward ~100."""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
CAMPUS = ROOT / "Assets" / "Resources" / "Campus"
DIALOGUE = ROOT / "Assets" / "Resources" / "Dialogue"

# tier: interviewee = dense interrogation pool; witness = records/bio + light lines
# First 12 interviewees (original 8 + 4). Rest witnesses for batch 1 (40 total).
AFFILIATES = [
    # --- playable interviewees (dense) ---
    dict(id="jamie", displayName="Jamie Rivera", campusRole="Student volunteer", tier="interviewee",
         bio="Evening setup volunteer who lives off the west bus loop.",
         statement="I left the student union before 9:15 PM and went straight to the bus stop.",
         alibi="At the west bus loop from 9:20 PM onward.",
         redHerringRecord="A hardware-store receipt shows Jamie bought a utility knife that afternoon.",
         redHerringExplanation="Jamie used it to cut tape and cardboard for event decorations.",
         alibiSupportTitle="Transit receipt",
         alibiSupportDescription="A transit-card tap places Jamie at the west bus loop at 9:24 PM.",
         contextTitleA="Volunteer roster",
         contextDescriptionA="Jamie is listed on the evening setup crew for the student-union event."),
    dict(id="morgan", displayName="Morgan Lee", campusRole="Student events assistant", tier="interviewee",
         bio="Events desk assistant with after-hours key privileges.",
         statement="I left the student union before 9:15 PM and went home.",
         alibi="At home from 9:10 PM onward.",
         redHerringRecord="An event sign-out sheet shows Morgan borrowed the storage-room key earlier that day.",
         redHerringExplanation="Morgan was inventorying decorations and returned the key before the theft window.",
         alibiSupportTitle="Residence hall door log",
         alibiSupportDescription="Morgan's hall exterior door registered an entry at 9:18 PM.",
         contextTitleA="Staff schedule",
         contextDescriptionA="Morgan's events shift ended at 9:00 PM according to the posted roster."),
    dict(id="avery", displayName="Avery Patel", campusRole="Campus facilities worker", tier="interviewee",
         bio="Night facilities tech covering linen and wing checks.",
         statement="I left the student union before 9:15 PM and continued my shift in the library wing.",
         alibi="Library wing maintenance corridor from 9:20 PM onward.",
         redHerringRecord="A work order shows Avery checked out a large laundry cart.",
         redHerringExplanation="Avery used it to move event linens to the laundry room.",
         alibiSupportTitle="Work radio check-in",
         alibiSupportDescription="Avery radioed a library-wing check-in at 9:28 PM.",
         contextTitleA="Facilities assignment board",
         contextDescriptionA="Avery was assigned to evening linen pickup in the student union before relocating to the library wing."),
    dict(id="riley", displayName="Riley Brooks", campusRole="Club treasurer", tier="interviewee",
         bio="Club treasurer finishing paperwork after the fair setup.",
         statement="I left the student union before 9:15 PM and went to a study group.",
         alibi="Science library study room B from 9:25 PM onward.",
         redHerringRecord="A club receipt shows Riley bought a roll of heavy-duty tape.",
         redHerringExplanation="Riley used it to repair a display board for the club fair.",
         alibiSupportTitle="Room reservation log",
         alibiSupportDescription="Study room B was reserved under Riley's name from 9:15 PM to 10:30 PM.",
         contextTitleA="Club chat notice",
         contextDescriptionA="Riley posted in the club chat that the treasurer report was finished before leaving the union."),
    dict(id="casey", displayName="Casey Nguyen", campusRole="Mascot squad member", tier="interviewee",
         bio="Mascot squad tech who handles spare shells.",
         statement="I left the student union before 9:15 PM and went to costume storage across campus.",
         alibi="Costume workshop annex from 9:20 PM onward.",
         redHerringRecord="A locker checkout shows Casey removed a spare mascot torso shell that afternoon.",
         redHerringExplanation="Casey was fitting a replacement shell for tomorrow's pep rally.",
         alibiSupportTitle="Annex badge swipe",
         alibiSupportDescription="Casey's badge opened the costume workshop annex at 9:22 PM.",
         contextTitleA="Squad practice note",
         contextDescriptionA="Casey was marked present at afternoon mascot drill and listed for evening shell maintenance."),
    dict(id="drew", displayName="Drew Okonkwo", campusRole="Campus security student aide", tier="interviewee",
         bio="Student aide on south-quad patrol with hallway key access.",
         statement="I left the student union before 9:15 PM and walked the south quad patrol loop.",
         alibi="South quad patrol from 9:15 PM onward.",
         redHerringRecord="A key cabinet log shows Drew signed out a master hallway key for the union.",
         redHerringExplanation="Drew was locking perimeter classrooms after the event and returned the key at 9:05 PM.",
         alibiSupportTitle="Patrol checkpoint ping",
         alibiSupportDescription="Drew's aide fob pinged the south quad checkpoint at 9:31 PM.",
         contextTitleA="Aide duty roster",
         contextDescriptionA="Drew was scheduled for the 8:00 PM to 11:00 PM student-aide patrol."),
    dict(id="quinn", displayName="Quinn Alvarez", campusRole="Student media photographer", tier="interviewee",
         bio="Campus photographer covering union events.",
         statement="I left the student union before 9:15 PM and uploaded photos from the media lab.",
         alibi="Media lab edit bay from 9:20 PM onward.",
         redHerringRecord="An equipment loan shows Quinn checked out a large rolling camera case.",
         redHerringExplanation="Quinn used the case to move lights and stands after covering the event.",
         alibiSupportTitle="Lab login record",
         alibiSupportDescription="Quinn logged into a media-lab workstation at 9:26 PM.",
         contextTitleA="Press pass list",
         contextDescriptionA="Quinn appears on the approved photographer list for the student-union event."),
    dict(id="sam", displayName="Sam Torvald", campusRole="Dining services closer", tier="interviewee",
         bio="Union cafe closer who seals inventory after events.",
         statement="I left the student union before 9:15 PM and finished closing the cafe downstairs.",
         alibi="Union cafe closing checklist from 9:15 PM onward.",
         redHerringRecord="A supply request shows Sam ordered industrial zip ties that morning.",
         redHerringExplanation="Sam used them to reseal bulk snack bags during inventory.",
         alibiSupportTitle="POS closeout stamp",
         alibiSupportDescription="The cafe register closeout was stamped complete under Sam's employee number at 9:33 PM.",
         contextTitleA="Closing checklist",
         contextDescriptionA="Sam is the named closer on the union cafe evening checklist."),
    # +4 denser interviewees
    dict(id="taylor", displayName="Taylor Kim", campusRole="RA on duty", tier="interviewee",
         bio="Residence hall RA covering late desk hours.",
         statement="I left the student union before 9:15 PM and went back to the north hall desk.",
         alibi="North hall RA desk from 9:20 PM onward.",
         redHerringRecord="A master-key checkout shows Taylor borrowed a wing key earlier.",
         redHerringExplanation="Taylor used it for a locked-out resident and returned it before 9:00 PM.",
         alibiSupportTitle="RA desk punch log",
         alibiSupportDescription="Taylor punched present at the north hall desk at 9:21 PM.",
         contextTitleA="Duty calendar",
         contextDescriptionA="Taylor is listed as the 8 PM–midnight RA on duty."),
    dict(id="jordan", displayName="Jordan Blake", campusRole="IT helpdesk student", tier="interviewee",
         bio="Helpdesk student who resets lab machines after events.",
         statement="I left the student union before 9:15 PM and opened a ticket shift in the IT closet.",
         alibi="IT closet B from 9:18 PM onward.",
         redHerringRecord="A badge trail shows Jordan near storage corridors that afternoon.",
         redHerringExplanation="Jordan was tracing a network drop for the AV team.",
         alibiSupportTitle="Ticket timestamp",
         alibiSupportDescription="Jordan opened ticket HD-4412 at 9:19 PM from closet B.",
         contextTitleA="Helpdesk roster",
         contextDescriptionA="Jordan is scheduled for the evening escalations queue."),
    dict(id="harper", displayName="Harper Singh", campusRole="Catering captain", tier="interviewee",
         bio="Student catering captain for union receptions.",
         statement="I left the student union before 9:15 PM and walked trays back to the loading dock.",
         alibi="Loading dock staging from 9:15 PM onward.",
         redHerringRecord="A van sign-out lists Harper with a cargo van that evening.",
         redHerringExplanation="Harper used the van to return chafing dishes to central kitchen.",
         alibiSupportTitle="Dock camera note",
         alibiSupportDescription="A dock attendant initialed Harper's tray return at 9:27 PM.",
         contextTitleA="Catering run sheet",
         contextDescriptionA="Harper is named captain on the union reception run sheet."),
    dict(id="Reese", displayName="Reese Delgado", campusRole="Campus bookstore closer", tier="interviewee",
         bio="Bookstore closer who cuts through the union after lockup.",
         statement="I left the student union before 9:15 PM after cutting through from the bookstore.",
         alibi="Bookstore register close from 9:10 PM onward.",
         redHerringRecord="A safe-log entry shows Reese opened the till room late.",
         redHerringExplanation="Reese was dropping the night deposit bag before walking home.",
         alibiSupportTitle="Register Z-tape",
         alibiSupportDescription="Bookstore Z-tape closes under Reese's ID at 9:12 PM.",
         contextTitleA="Closer schedule",
         contextDescriptionA="Reese is the assigned bookstore closer for the night."),
]

# Fix reese id lowercase
for a in AFFILIATES:
    if a["id"] == "Reese":
        a["id"] = "reese"

WITNESSES_BATCH1 = [
    ("noah", "Noah Chen", "Chemistry TA", "Grad TA who graded quizzes in the science atrium."),
    ("mia", "Mia Okada", "Library page", "Shelved reserves on the quiet floor until close."),
    ("lucas", "Lucas Grant", "Intramural ref", "Officiated a late pickup game in the rec annex."),
    ("zoe", "Zoe Martel", "Dance ensemble lead", "Ran a rehearsal in the studio above the union."),
    ("ethan", "Ethan Kowalski", "Bus driver (campus shuttle)", "Drove the west-loop shuttle through 10 PM."),
    ("ava", "Ava Rahman", "Career center aide", "Closed the resume lab and logged machines."),
    ("ben", "Ben Okoye", "HVAC vendor tech", "On-call vendor checking a union rooftop unit."),
    ("lily", "Lily Tran", "Orientation leader", "Collected leftover orientation swag from a closet."),
    ("caleb", "Caleb Ruiz", "Esports club president", "Streamed a practice match from the game lounge."),
    ("nina", "Nina Petrov", "Health center clerk", "Filed evening intake forms at student health."),
    ("owen", "Owen Farrell", "Parking enforcement", "Ticketed the loading zone behind the union."),
    ("sara", "Sara Ibrahim", "Language tutor", "Tutored Spanish in the learning commons."),
    ("kyle", "Kyle Pham", "AV rental desk", "Returned wireless mics after the event."),
    ("elena", "Elena Rossi", "Alumni office intern", "Packed name badges from a donor mixer."),
    ("marcus", "Marcus Hale", "Food truck vendor", "Parked a late taco truck on Alumni Drive."),
    ("priya", "Priya Desai", "Bio lab assistant", "Locked the teaching lab cold room."),
    ("dylan", "Dylan Morse", "Theatre carpenter", "Broke down a flat for tomorrow's tech."),
    ("chloe", "Chloe Park", "Admissions tour guide", "Finished a VIP family walk near the fountain."),
    ("ivan", "Ivan Sokolov", "Night custodian", "Mopped the union lobby after the crowd left."),
    ("jade", "Jade Winters", "Radio station DJ", "Hosted a late show from the basement booth."),
    ("parker", "Parker Ellis", "Mailroom clerk", "Sorted late package drops for resident halls."),
    ("sofia", "Sofia Mendes", "Swim team manager", "Logged pool equipment after practice."),
    ("ryan", "Ryan Paik", "Printing services", "Ran banner reprints for the pep rally."),
    ("amara", "Amara Diallo", "International student mentor", "Held drop-in hours in the global lounge."),
    ("theo", "Theo Lindqvist", "Bike shop mechanic", "Closed the campus bike co-op register."),
    ("grace", "Grace Adebayo", "Chaplain intern", "Locked the meditation room after a group."),
    ("felix", "Felix Ortega", "Vending contractor", "Restocked snack spirals on level two."),
    ("hana", "Hana Suzuki", "Museum gallery attendant", "Closed the small campus gallery."),
]

# Overnight expansion toward ~100 (batch 2+)
WITNESSES_BATCH2 = [
    ("amir", "Amir Hassan", "Mosque student chaplain aide", "Locked the prayer room after evening salah."),
    ("britt", "Britt Olsen", "Rowing team coxswain", "Logged shells after night practice on the creek."),
    ("cara", "Cara Whitfield", "Career fair vendor", "Packed booth crates in the union ballroom."),
    ("dev", "Dev Kapoor", "Data science club lead", "Closed a hack night in the innovation lab."),
    ("emme", "Emme Laurent", "French house RA", "Did quiet hours rounds on the language floor."),
    ("finn", "Finn Gallagher", "Climbing wall staff", "Reset routes in the rec climbing gym."),
    ("gina", "Gina Morales", "Financial aid clerk", "Batched late work-study timesheets."),
    ("hugo", "Hugo Berg", "Orchestra librarian", "Returned scores to the music building vault."),
    ("iris", "Iris Qu", "Robotics shop monitor", "Powered down the CNC after club hours."),
    ("jules", "Jules Abebe", "Track timer volunteer", "Packed photo-finish gear after practice."),
    ("kira", "Kira Novak", "Counseling peer listener", "Finished a wellness shift in the quiet suite."),
    ("leo", "Leo Santos", "Bike share rebalancer", "Moved share bikes from the union rack."),
    ("mona", "Mona Al-Farsi", "Arabic club secretary", "Posted flyers then left via the south doors."),
    ("nate", "Nate Feldman", "Hillel student cook", "Closed the kitchen after a late dinner."),
    ("ola", "Ola Nowak", "Polish club baker", "Delivered pastries then cleaned the annex oven."),
    ("pam", "Pam Ortiz", "Copy center supervisor", "Ran course-pack reprints until 9:40 PM."),
    ("quin", "Quin Sato", "Anime club projectionist", "Rewound a screening in lecture hall C."),
    ("rita", "Rita Gomes", "Portuguese tutor", "Held office hours in the language commons."),
    ("sid", "Sid Banerjee", "Quant finance society", "Locked the trading lab simulators."),
    ("tess", "Tess McLeod", "Bagpipe band manager", "Stored drums after a quad rehearsal."),
    ("uma", "Uma Reddy", "Med school scribe intern", "Filed notes from a late skills lab."),
    ("vic", "Vic Romano", "Pizza delivery contractor", "Dropped late orders at north towers."),
    ("wes", "Wes Carter", "Fire watch student", "Walked a construction fire-watch loop."),
    ("xander", "Xander Price", "Improv troupe booker", "Struck chairs after a basement show."),
    ("yara", "Yara Haddad", "Middle East studies aide", "Closed the map room in Baldy-style annex."),
    ("zeke", "Zeke Morrow", "Ice rink Zamboni trainee", "Helped resurface after club skate."),
    ("april", "April Yoon", "Floral lab assistant", "Watered greenhouse benches after class."),
    ("blake", "Blake Dunn", "Esports shoutcaster", "Ended a cast from the streaming closet."),
    ("cove", "Cove Jensen", "Sailing club rigger", "Dried sails in the boathouse loft."),
    ("dane", "Dane Okorie", "African student union", "Locked the cultural center lounge."),
    ("edie", "Edie Falk", "Printmaking monitor", "Washed screens in the art annex."),
    ("fran", "Fran Liu", "Statistics help room", "Closed tutoring after walk-in hours."),
    ("glen", "Glen Torres", "Moving crew temp", "Hauled chairs from the ballroom strike."),
    ("hope", "Hope Adler", "Title IX peer educator", "Finished a workshop cleanup in the suite."),
    ("izzy", "Izzy Kahn", "Debate team researcher", "Printed briefs in the late lab."),
    ("joss", "Joss Meier", "German house cook", "Closed the community kitchen."),
    ("kent", "Kent Abrams", "Alumni phonathon", "Ended call shifts in the phone bank."),
    ("lana", "Lana Kuznetsova", "Ballet pianist", "Packed sheet music after rehearsal."),
    ("mack", "Mack Holloway", "Grounds irrigation tech", "Checked sprinkler zones by the fountain."),
    ("nell", "Nell Borges", "Latin dance instructor", "Locked the studio after salsa night."),
    ("otis", "Otis Greene", "Jazz combo roadie", "Loaded amps into a van on the dock."),
    ("pearl", "Pearl Vo", "Optometry clinic aide", "Closed the evening screening room."),
    ("quill", "Quill Asher", "Poetry slam host", "Struck the mic in the cafe stage."),
    ("rome", "Rome Castillo", "Soccer kit manager", "Returned pinnies to the field house."),
    ("sage", "Sage Whitaker", "Herbal club gardener", "Locked the community plot shed."),
    ("tova", "Tova Stein", "Hillel night desk", "Signed out guests at the center lobby."),
    ("uri", "Uri Mandel", "Physics demo prep", "Secured the lecture demo cart."),
    ("vera", "Vera Costa", "Portuguese cafe volunteer", "Closed the language coffee hour."),
    ("wade", "Wade Simmons", "ROTC supply cadet", "Inventoried gear in the cage."),
    ("xiomara", "Xiomara Paz", "Salsa DJ", "Packed decks after the cultural night."),
    ("yetu", "Yetu Mensah", "African dance lead", "Locked the rehearsal mirrors room."),
    ("zara", "Zara Bloom", "Beekeeping club", "Checked hives behind the ag annex."),
    ("anson", "Anson Craig", "Pipe organ scholar", "Secured the chapel loft after practice."),
    ("briar", "Briar Holt", "Costume stitcher", "Finished hems in the drama loft."),
    ("cedric", "Cedric Ong", "Singapore students assoc", "Closed a study circle in the lounge."),
    ("daria", "Daria Volkov", "Russian choir alto", "Returned folders to the music office."),
    ("ellis", "Ellis Prado", "Skate park monitor", "Locked the outdoor pad lights."),
    ("flora", "Flora Beck", "Community garden lead", "Logged tool returns at the shed."),
    ("gareth", "Gareth Quinn", "Fencing armorer", "Oiled foils after club practice."),
    ("heidi", "Heidi Moss", "Veterinary clinic aide", "Closed kennel rounds for the night."),
]


def witness_hooks(aid: str, name: str, role: str, bio: str):
    first = name.split()[0]
    return [
        dict(suffix="bio", title=f"Campus directory — {name}",
             description=f"{name}, {role}. {bio}"),
        dict(suffix="sighting", title=f"Witness note — {first}",
             description=f"A statement places {name} near campus activity around the theft window, but not inside event storage."),
        dict(suffix="badge", title=f"Access adjacent — {first}",
             description=f"Peripheral badge or schedule noise for {name} ({role}); useful color, not a storage open."),
    ]


def interviewee_to_affiliate(raw: dict) -> dict:
    out = dict(raw)
    out["recordHooks"] = [
        dict(suffix="directory", title=f"Campus directory — {raw['displayName']}",
             description=raw["bio"]),
    ]
    return out


def append_witness(people: list, aid: str, name: str, role: str, bio: str) -> None:
    people.append(dict(
        id=aid,
        displayName=name,
        campusRole=role,
        tier="witness",
        bio=bio,
        statement=f"I wasn't part of the storage crew. I'm {name}, {role}.",
        alibi="Elsewhere on campus during the theft window.",
        redHerringRecord="",
        redHerringExplanation="",
        alibiSupportTitle="Peripheral schedule",
        alibiSupportDescription=f"{name}'s ordinary duties keep them away from event storage.",
        contextTitleA="Directory listing",
        contextDescriptionA=bio,
        recordHooks=witness_hooks(aid, name, role, bio),
    ))


def build_pool(limit: int | None = 40):
    people = [interviewee_to_affiliate(a) for a in AFFILIATES]
    for aid, name, role, bio in WITNESSES_BATCH1:
        append_witness(people, aid, name, role, bio)
    for aid, name, role, bio in WITNESSES_BATCH2:
        append_witness(people, aid, name, role, bio)
    # de-dupe by id, keep first
    seen = set()
    deduped = []
    for p in people:
        if p["id"] in seen:
            continue
        seen.add(p["id"])
        deduped.append(p)
    people = deduped
    if limit is not None:
        people = people[:limit]
    assert_unique_surnames(people)
    return people


def surname_of(display_name: str) -> str:
    parts = str(display_name or "").strip().split()
    if not parts:
        return ""
    # Treat multi-part surnames lightly (Al-Farsi, etc.) as the last token for uniqueness.
    return parts[-1].lower()


def assert_unique_surnames(people: list) -> None:
    """Fail generation if last names cluster (Magdy: no Nguyen spam)."""
    by_surname: dict[str, list[str]] = {}
    for p in people:
        sur = surname_of(p.get("displayName", ""))
        if not sur:
            continue
        by_surname.setdefault(sur, []).append(p["id"])
    clashes = {s: ids for s, ids in by_surname.items() if len(ids) > 1}
    if clashes:
        detail = ", ".join(f"{s}→{ids}" for s, ids in sorted(clashes.items()))
        raise SystemExit(f"Duplicate surnames in affiliate pool: {detail}")


def build_witness_dialogue(people: list) -> list:
    entries = []
    for p in people:
        if p["tier"] != "witness":
            continue
        sid = p["id"]
        name = p["displayName"].split()[0]
        role = p["campusRole"]
        lines = [
            ("small_talk", "calm", 0, 40, f"I'm {name}. {role}. I only saw the hallway crowd."),
            ("small_talk", "calm", 0, 100, f"Not my interview — I'm just in the directory as {name}."),
            ("alibi", "calm", 0, 50, f"I was on my {role} duties. Storage wasn't my stop."),
            ("evidence", "nervous", 0, 70, f"If my name's on a sheet, it's because I'm a {role}, not a thief."),
            ("generic", "calm", 0, 100, f"{name} here. Ask the people who had the key."),
            ("accusation", "defensive", 0, 80, f"Don't drag a {role} into a mascot theft."),
        ]
        for i, (intent, emotion, lo, hi, line) in enumerate(lines, 1):
            entries.append(dict(
                id=f"{sid}_{intent}_{i}",
                suspectId=sid,
                intent=intent,
                keywords="name|who|where|evidence|record|witness|directory|saw|mascot",
                minStress=lo,
                maxStress=hi,
                emotion=emotion,
                slip=False,
                line=line,
                requiresEvidence=False,
                requiresAggressive=False,
                culpritOnly=False,
                innocentOnly=False,
            ))
    # Shared witness flavor for any unmatched peripheral
    for i, line in enumerate([
        "I only know what the hallway looked like.",
        "Check the directory note — I'm peripheral.",
        "I didn't open storage. Ask the interviewees.",
    ], 1):
        entries.append(dict(
            id=f"witness_common_{i}",
            suspectId="*",
            intent="small_talk",
            keywords="witness|directory|saw|hallway",
            minStress=0,
            maxStress=100,
            emotion="calm",
            slip=False,
            line=line,
            requiresEvidence=False,
            requiresAggressive=False,
            culpritOnly=False,
            innocentOnly=False,
        ))
    return entries


def write_json(path: Path, obj) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(obj, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def ensure_meta(path: Path, guid: str) -> None:
    meta = path.with_suffix(path.suffix + ".meta")
    if meta.exists():
        return
    meta.write_text(
        "fileFormatVersion: 2\n"
        f"guid: {guid}\n"
        "TextScriptImporter:\n"
        "  externalObjects: {}\n"
        "  userData: \n"
        "  assetBundleName: \n"
        "  assetBundleVariant: \n",
        encoding="utf-8",
    )


def main(batch: int = 100) -> None:
    people = build_pool(limit=batch)
    write_json(CAMPUS / "affiliates.json", {
        "schemaVersion": 1,
        "batch": 2 if len(people) > 40 else 1,
        "targetPoolSize": 100,
        "count": len(people),
        "affiliates": people,
    })
    ensure_meta(CAMPUS / "affiliates.json", "caf10001a0014b02a003d004e005f006")

    witness_entries = build_witness_dialogue(people)
    write_json(DIALOGUE / "witnesses.json", {"entries": witness_entries})
    ensure_meta(DIALOGUE / "witnesses.json", "d1a10002b0034c05d006e007f008a009")

    # Dense dialogue for new interviewees via existing gen_dialogue_bank patterns — emit light banks here
    new_interviewees = [p for p in people if p["tier"] == "interviewee" and p["id"] not in
                        {"jamie", "morgan", "avery", "riley", "casey", "drew", "quinn", "sam"}]
    for p in new_interviewees:
        sid = p["id"]
        name = p["displayName"].split()[0]
        alibi = p["alibi"]
        entries = []
        for stress, emo, lo, hi, lines in [
            ("calm", "calm", 0, 29, [
                f"I was at {alibi}",
                f"I'm {name}. Check the desk log.",
                "Ask something that isn't already on my sheet.",
            ]),
            ("nervous", "nervous", 30, 59, [
                f"I keep saying {alibi}",
                "Why am I in the hard chair for this?",
                "My timeline hasn't moved.",
            ]),
            ("cracking", "scared", 60, 84, [
                "Stop cornering me — I wasn't in storage.",
                f"The {alibi} is all I have.",
                "You're treating schedule noise like guilt.",
            ]),
            ("broken", "broken", 85, 100, [
                "I can't invent a better story.",
                "Stamp whatever. I didn't take it.",
                "...",
            ]),
        ]:
            for i, line in enumerate(lines, 1):
                entries.append(dict(
                    id=f"{sid}_mix_{stress}_{i}",
                    suspectId=sid,
                    intent="alibi" if i == 1 else ("pressure" if i == 2 else "generic"),
                    keywords="where|alibi|were you|enough|name|who",
                    minStress=lo,
                    maxStress=hi,
                    emotion=emo,
                    slip=False,
                    line=line,
                    requiresEvidence=False,
                    requiresAggressive=False,
                    culpritOnly=False,
                    innocentOnly=False,
                ))
        for intent, line in [
            ("hold", f"HOLD? I was on duty as {p['campusRole']}."),
            ("release", "Release. Put the energy on storage access."),
            ("accusation", "I was not in that window."),
            ("evidence", "That paper fits my job, not a theft."),
            ("slip", "I... went back by storage. Just for a second."),
        ]:
            entries.append(dict(
                id=f"{sid}_{intent}_1",
                suspectId=sid,
                intent=intent,
                keywords="hold|release|stole|evidence|admit|storage",
                minStress=0 if intent != "slip" else 60,
                maxStress=100,
                emotion="scared" if intent == "slip" else "defensive",
                slip=intent == "slip",
                line=line,
                requiresEvidence=intent == "evidence",
                requiresAggressive=False,
                culpritOnly=intent == "slip",
                innocentOnly=False,
            ))
        write_json(DIALOGUE / f"{sid}.json", {"entries": entries})
        ensure_meta(DIALOGUE / f"{sid}.json", f"e{sid[:7].ljust(7,'0')}f00140015a016b017c018"[:32].ljust(32, "0"))

    # Update bank_index
    index_path = DIALOGUE / "bank_index.json"
    banks = ["common", "jamie", "morgan", "avery", "riley", "casey", "drew", "quinn", "sam",
             "taylor", "jordan", "harper", "reese", "witnesses"]
    # only include banks that exist
    banks = [b for b in banks if (DIALOGUE / f"{b}.json").exists()]
    write_json(index_path, {"banks": banks})

    interviewees = sum(1 for p in people if p["tier"] == "interviewee")
    witnesses = sum(1 for p in people if p["tier"] == "witness")
    print(json.dumps({
        "affiliates": len(people),
        "interviewees": interviewees,
        "witnesses": witnesses,
        "witness_lines": len(witness_entries),
        "new_interviewee_banks": [p["id"] for p in new_interviewees],
        "banks": banks,
    }, indent=2))


if __name__ == "__main__":
    import sys
    n = 100
    if len(sys.argv) > 1:
        n = int(sys.argv[1])
    main(n)
