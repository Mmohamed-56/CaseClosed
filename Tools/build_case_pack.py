#!/usr/bin/env python3
"""Write Resources/Cases/case_templates.json — 14 distinct UB cases.

Evidence ids stay on CaseGenerator suffixes: roster, alibi_support, sign_in, access, red_herring.
Briefings are four short facts, then the objects. No solve sheet.
"""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets" / "Resources" / "Cases" / "case_templates.json"

J = ["jamie", "morgan", "avery", "riley", "casey", "drew", "quinn", "sam", "taylor", "jordan", "harper", "reese"]

BANNED = (
    "difficulty:",
    "motive on the table",
    "the trap",
    "the page that",
    "the page",
    "checklist",
    "incident window",
    "becomes proof",
    "only if",
    "look worse",
    "not a costume",
    "not a storage",
    "not a confession",
    "is the point",
    "have to match",
    "have to be the same",
    "do not agree",
    "does not agree",
)


def C(**kwargs):
    prefs = kwargs.pop("prefer")
    missing = [p for p in prefs if p not in J]
    if missing:
        raise SystemExit(f"bad suspect ids {missing}")
    if len(prefs) < 8:
        raise SystemExit("need 8 prefer ids")
    kwargs["preferSuspectIds"] = prefs
    for key in (
        "accessDescription",
        "signInDescription",
        "rosterDescription",
        "alibiDescription",
        "redHerringDescription",
        "redHerringExplanation",
        "contradictionExplanation",
        "statementTemplate",
    ):
        if "{name}" not in kwargs[key] and key != "statementTemplate":
            raise SystemExit(f"{kwargs['id']} missing {{name}} in {key}")
    if kwargs["difficulty"] not in ("easy", "medium", "hard"):
        raise SystemExit("bad difficulty")
    briefing = kwargs["briefing"]
    for header in ("WHERE: ", "WHEN: ", "WHAT: ", "MOTIVE: "):
        if header not in briefing:
            raise SystemExit(f"{kwargs['id']} briefing missing {header.strip()}")
    if "\nWHERE:" in briefing or briefing.strip().startswith("DIFFICULTY"):
        raise SystemExit(f"{kwargs['id']} briefing has a bad header")
    return kwargs


def brief(where, when, what, motive, objects):
    lines = [
        f"WHERE: {where}",
        f"WHEN: {when}",
        f"WHAT: {what}",
        f"MOTIVE: {motive}",
        "",
    ]
    lines.extend(objects)
    return "\n".join(lines)


CASES = [
    C(
        id="victor_mural",
        title="Capen Victor Mural Paint-Out",
        difficulty="easy",
        motive="Rival week. They wanted Victor embarrassed before the game.",
        solvePath="Somebody painted red over the Victor mural on the Capen south wall between 4:40 and 5:10 PM.",
        briefing=brief(
            "Capen Hall, south brick wall.",
            "4:40 PM to 5:10 PM.",
            "Somebody's painted red over the Victor the Bull mural. The horns are the only original paint left.",
            "Rival week. They wanted Victor embarrassed before the game.",
            [
                "South-wall camera still. A directory jacket, a lanyard, hands on a roller, face turned away. Horns still blue.",
                "Afternoon tabling sheet from inside Capen. A signature at 3:10 PM and no time out.",
                "Founders tabling crew list.",
                "Lobby camera still at the coffee cart, 4:20 PM.",
                "Campus-store receipt. One tube of cadmium-red acrylic, 3:40 PM.",
            ],
        ),
        treeHook="victor_mural",
        scene="Capen Hall, south brick wall",
        windowStart="4:40 PM",
        windowEnd="5:10 PM",
        accessTime="4:52 PM",
        accessTitle="South-wall camera still",
        accessDescription=(
            "South-wall camera CP-S2, 4:52 PM. {name}'s directory jacket, lanyard readable, both hands on a roller over Victor. "
            "Face turned away. Horns still blue."
        ),
        contradictionExplanation="{name} painted red over the Victor mural on the Capen south wall.",
        signInTitle="Capen tabling sheet",
        signInDescription="{name} signed the afternoon tabling sheet inside Capen at 3:10 PM. No time out on the line.",
        rosterTitle="Founders tabling crew",
        rosterDescription="{name} is on the Founders tabling crew list.",
        alibiTitle="Lobby coffee still",
        alibiDescription="Lobby camera CP-L1, 4:20 PM. {name} at the coffee cart.",
        redHerringTitle="Campus-store receipt",
        redHerringDescription="Campus-store receipt on {name}'s card. One tube of cadmium-red acrylic, 3:40 PM.",
        redHerringExplanation="{name} bought the acrylic for a club table. The tube's still sealed in the poster bin.",
        statementTemplate="I wasn't on the south wall. I was inside Capen.",
        prefer=["casey", "jamie", "quinn", "reese", "morgan", "sam", "drew", "riley"],
    ),
    C(
        id="norton_clone",
        title="Norton Hall Cloned Night Badge",
        difficulty="hard",
        motive="A recommendation letter locked in a faculty office.",
        solvePath="Somebody opened a Norton faculty office at 9:22 PM on chip serial SE-B.",
        briefing=brief(
            "Norton Hall, faculty hallway.",
            "9:05 PM to 9:40 PM.",
            "Somebody opened a locked faculty office after the hallway had gone quiet.",
            "A recommendation letter locked in that office. They wanted a look at it.",
            [
                "Door reader log, faculty hallway, 9:22 PM. A chip serial, SE-B.",
                "Encode bench 2 sign-in sheet, 4:00 PM to 4:30 PM.",
                "Encode ticket EJ-77. Chip serial SE-B written at 4:16 PM. Operator line blank.",
                "Alumni gym photo, 9:20 PM.",
                "Hallway master-key checkout. Out at 8:40 PM, back at 9:50 PM.",
            ],
        ),
        treeHook="norton_clone",
        scene="Norton Hall, faculty hallway",
        windowStart="9:05 PM",
        windowEnd="9:40 PM",
        accessTime="9:22 PM",
        accessTitle="Norton door reader log",
        accessDescription=(
            "Norton door reader NRT-2, 9:22 PM. The door opened for a chip carrying {name}'s printed number. "
            "The chip serial on the read is SE-B."
        ),
        contradictionExplanation="{name} wrote chip SE-B on encode bench 2 and used it on the Norton faculty door at 9:22 PM.",
        signInTitle="Encode bench 2 sheet",
        signInDescription="{name} is on the encode bench 2 sheet from 4:00 PM to 4:30 PM.",
        rosterTitle="Encode ticket EJ-77",
        rosterDescription=(
            "Ticket EJ-77. One blank written to chip serial SE-B at 4:16 PM. The operator line is blank. "
            "{name} is on bench 2's sheet for that half hour."
        ),
        alibiTitle="Alumni gym photo",
        alibiDescription=(
            "Alumni gym photo, 9:20 PM. A hoodie like {name}'s. The scan on the photo is guest pass G-19, "
            "issued at 9:18 PM under a different name."
        ),
        redHerringTitle="Hallway master-key checkout",
        redHerringDescription="{name} signed a hallway master key out at 8:40 PM and back in at 9:50 PM.",
        redHerringExplanation="{name} used that key to lock Capen classrooms after an event. It came back to the cabinet.",
        statementTemplate="My card was in my pocket the whole time. I was at the gym.",
        prefer=["jordan", "drew", "morgan", "avery", "jamie", "taylor", "quinn", "sam"],
    ),
    C(
        id="hadley_party",
        title="Hadley Village Unit 4B Noise Sheet",
        difficulty="medium",
        motive="A recruit visit turned into a party. The host needed the write-up on somebody else.",
        solvePath="Somebody was in the Hadley 4B doorway at 11:36 PM with a case of cans.",
        briefing=brief(
            "Hadley Village, unit 4B.",
            "10:40 PM to 12:10 AM.",
            "Residents called in a party. Music, cans in the hall, and a recruit who wasn't supposed to stay the night.",
            "A recruit visit turned into a party. The host needed the write-up on somebody else.",
            [
                "Bodycam still in the 4B doorway, 11:36 PM. A person, a case of cans, music behind them.",
                "Laundry clipboard. A signature at 11:20 PM.",
                "Unit 4B guest line.",
                "Laundry machine log.",
                "Tops receipt. Two sleeves of red cups, 6:10 PM.",
            ],
        ),
        treeHook="hadley_party",
        scene="Hadley Village, unit 4B",
        windowStart="10:40 PM",
        windowEnd="12:10 AM",
        accessTime="11:36 PM",
        accessTitle="Hadley doorway still",
        accessDescription=(
            "Hadley bodycam HV-4, 11:36 PM. {name} in the unit 4B doorway, one hand on a case of cans. Music's going behind them."
        ),
        contradictionExplanation="{name} was in the Hadley 4B doorway at 11:36 PM with the cans.",
        signInTitle="Hadley laundry clipboard",
        signInDescription="{name} signed the Hadley laundry clipboard at 11:20 PM.",
        rosterTitle="Unit 4B guest line",
        rosterDescription="{name} is on the unit 4B guest line, written as someone who might stop by.",
        alibiTitle="Laundry machine log",
        alibiDescription="Hadley laundry machine log. No cycle under {name} between 11:00 PM and midnight.",
        redHerringTitle="Tops cup receipt",
        redHerringDescription="Tops receipt, 6:10 PM. {name} bought two sleeves of red cups.",
        redHerringExplanation="{name} bought the cups for a floor program. The program sheet accounts for both sleeves.",
        statementTemplate="I was in the laundry room. I didn't host anything in 4B.",
        prefer=["taylor", "reese", "morgan", "jamie", "casey", "sam", "harper", "riley"],
    ),
    C(
        id="furnas_reprint",
        title="Furnas Chem 201 Night Reprint",
        difficulty="hard",
        motive="The Chem 201 midterm is at 8 AM. Fail it and you fail the course.",
        solvePath="Somebody printed {name}-review.pdf in Furnas 214 at 1:17 AM.",
        briefing=brief(
            "Furnas Hall, room 214.",
            "1:04 AM to 1:28 AM.",
            "Somebody reprinted the Chem 201 answer key off a lab computer. The login on the job is the shared night account.",
            "The midterm's at 8 AM. Fail it and you fail the course.",
            [
                "Print job, 1:17 AM. A review file on the shared login chem-night.",
                "Room 214 tablet. Signed out at 1:06 AM.",
                "chem-night clearance list.",
                "Door badge. Exit at 1:22 AM.",
                "USB from the lobby lost-and-found. Sticker reads 201-KEY.",
            ],
        ),
        treeHook="furnas_reprint",
        scene="Furnas Hall, room 214",
        windowStart="1:04 AM",
        windowEnd="1:28 AM",
        accessTime="1:17 AM",
        accessTitle="LC-2 print job",
        accessDescription=(
            "Furnas terminal LC-2, 1:17 AM. It sent {name}-review.pdf to the room 214 printer. The login on the job is chem-night."
        ),
        contradictionExplanation="{name} printed the Chem 201 key in Furnas 214 at 1:17 AM.",
        signInTitle="Furnas 214 tablet",
        signInDescription="{name} signed the room 214 tablet out at 1:06 AM.",
        rosterTitle="chem-night clearance",
        rosterDescription="{name} is cleared on the shared login chem-night, with the rest of the night section.",
        alibiTitle="Furnas door badge",
        alibiDescription="Furnas door badge for {name}. Exit at 1:22 AM. The read before that is an entry at 12:40 AM.",
        redHerringTitle="USB labeled 201-KEY",
        redHerringDescription=(
            "Lost-and-found USB on {name}'s student number. The sticker says 201-KEY. The checkout line says returned at 6 PM."
        ),
        redHerringExplanation="{name}'s USB is a practice set from office hours, turned in at 6 PM.",
        statementTemplate="I signed out of Furnas 214 at 1:06 AM. I was gone before the printer ran.",
        prefer=["harper", "jordan", "riley", "avery", "quinn", "morgan", "jamie", "drew"],
    ),
    C(
        id="ellicott_shove",
        title="Ellicott Tunnel Shoulder Check",
        difficulty="medium",
        motive="A project slide one partner deleted.",
        solvePath="Somebody with tunnel grit on their shoes texted from the Ellicott tunnel at 11:18 PM.",
        briefing=brief(
            "Ellicott tunnel, by the bend.",
            "11:12 PM to 11:28 PM.",
            "A student got shouldered into the wall and threatened over a deleted project slide. The cameras catch shoes and coats.",
            "That deleted slide.",
            [
                "Shoe grit from the tunnel, and a text at 11:18 PM.",
                "Project review sign-out, 11:05 PM.",
                "Project thread. The slide comes out at 10:40 PM.",
                "Phone on a dorm-named Wi-Fi network, 11:20 PM.",
                "A bruise photo, posted at 11:40 PM. Caption reads tunnel night.",
            ],
        ),
        treeHook="ellicott_shove",
        scene="Ellicott tunnel, by the bend",
        windowStart="11:12 PM",
        windowEnd="11:28 PM",
        accessTime="11:18 PM",
        accessTitle="Tunnel grit and text",
        accessDescription=(
            "Lab note on the shoes {name} wore tonight: grit from the Ellicott tunnel. "
            "Their phone sent \"I'm in the tunnel, do not follow me\" at 11:18 PM."
        ),
        contradictionExplanation="{name} was in the Ellicott tunnel at 11:18 PM and started the shove.",
        signInTitle="Project review sign-out",
        signInDescription="{name} signed out of the Ellicott project review at 11:05 PM.",
        rosterTitle="Slide-deletion thread",
        rosterDescription="{name} is in the project thread. The slide was deleted at 10:40 PM.",
        alibiTitle="Dorm Wi-Fi log",
        alibiDescription=(
            "Phone log, 11:20 PM. {name}'s phone is on a network named for the dorm. "
            "The IT map for that access point includes the tunnel mouth."
        ),
        redHerringTitle="Bruise photo",
        redHerringDescription=(
            "Photo {name} posted at 11:40 PM. A fresh bruise. The caption says tunnel night. The file's own clock says 5:12 PM."
        ),
        redHerringExplanation="The bruise photo on {name}'s phone was taken at rugby practice, 5:12 PM.",
        statementTemplate="I left the project review and went back to the dorm. I never put a hand on anyone in the tunnel.",
        prefer=["reese", "drew", "jamie", "taylor", "morgan", "casey", "sam", "avery"],
    ),
    C(
        id="goodyear_bike",
        title="Goodyear Bike Cage Lock Cut",
        difficulty="easy",
        motive="Sell the bike the same night. They needed the cash.",
        solvePath="Somebody cut the lock off a blue Trek in the Goodyear cage at 7:22 PM.",
        briefing=brief(
            "Goodyear Hall bike cage.",
            "7:10 PM to 7:40 PM.",
            "Somebody cut the lock off a blue Trek and walked it out the south gate.",
            "Sell it tonight. They needed the cash.",
            [
                "Bike-cage camera still, 7:22 PM. A lanyard, a bolt cutter, the blue Trek.",
                "Intramural check-in sheet, 6:05 PM.",
                "Cage key list.",
                "South-gate counter. A bike leaves at 7:23 PM.",
                "Theater scene-shop checkout. A chain tool, 4:50 PM.",
            ],
        ),
        treeHook="goodyear_bike",
        scene="Goodyear Hall bike cage",
        windowStart="7:10 PM",
        windowEnd="7:40 PM",
        accessTime="7:22 PM",
        accessTitle="Bike-cage camera still",
        accessDescription=(
            "Bike-cage camera GC-1, 7:22 PM. {name}'s lanyard, both hands on a bolt cutter at the blue Trek's lock. "
            "The south gate opens in the next frame."
        ),
        contradictionExplanation="{name} cut the lock and took the blue Trek from the Goodyear cage at 7:22 PM.",
        signInTitle="Intramural check-in",
        signInDescription="{name} signed the Goodyear intramural sheet at 6:05 PM.",
        rosterTitle="Cage key list",
        rosterDescription="The facilities key list for the cage doesn't have {name} on it.",
        alibiTitle="South-gate counter",
        alibiDescription="South-gate counter, 7:23 PM. A bike left. {name} isn't printed on the counter slip.",
        redHerringTitle="Scene-shop chain tool",
        redHerringDescription=(
            "Scene-shop checkout. {name} took a chain tool at 4:50 PM. The return line is signed before 6 PM."
        ),
        redHerringExplanation="{name} used the chain tool on a set piece and brought it back before 6 PM.",
        statementTemplate="I didn't go to the bike cage tonight. My bike isn't the blue Trek.",
        prefer=["reese", "jamie", "sam", "casey", "drew", "quinn", "jordan", "morgan"],
    ),
    C(
        id="lockwood_notes",
        title="Lockwood Carrel Threat Notes",
        difficulty="hard",
        motive="A lab partner just got sole credit. They wanted that partner scared.",
        solvePath="note-final.pdf printed at Lockwood at 7:14 PM from inside the building.",
        briefing=brief(
            "Lockwood Library, carrels 3B and 3C.",
            "6:50 PM to 8:20 PM.",
            "Two printed threats were left for a lab partner who just got sole credit on the paper.",
            "The credit. They wanted that partner scared.",
            [
                "Printer job note-final.pdf, 7:14 PM. Shared account lib-night.",
                "Exit kiosk. OUT at 6:40 PM.",
                "lib-night roster.",
                "Exit turnstile log, and a print-desk unlock at 7:11 PM.",
                "Campus email, 7:20 PM. Sent from the dorm.",
            ],
        ),
        treeHook="lockwood_notes",
        scene="Lockwood Library, carrels 3B and 3C",
        windowStart="6:50 PM",
        windowEnd="8:20 PM",
        accessTime="7:14 PM",
        accessTitle="Printer job note-final.pdf",
        accessDescription=(
            "Lockwood printer P-3, 7:14 PM. note-final.pdf, from the shared account lib-night. "
            "A clerk penciled {name} in the owner blank."
        ),
        contradictionExplanation="{name} printed the Lockwood threats at 7:14 PM while their badge was still inside.",
        signInTitle="Lockwood exit kiosk",
        signInDescription="{name} pressed the Lockwood exit kiosk OUT at 6:40 PM.",
        rosterTitle="lib-night roster",
        rosterDescription=(
            "{name} is on the lib-night roster with three other students. Those three badged into Capen at 7:02 PM."
        ),
        alibiTitle="Turnstile and print desk",
        alibiDescription=(
            "Lockwood exit turnstile. No read for {name} between 6:40 PM and 8:05 PM. "
            "At 7:11 PM the print desk unlocked on that same badge."
        ),
        redHerringTitle="Dorm email",
        redHerringDescription=(
            "Campus mail from {name}, 7:20 PM. It calls the lab partner a thief and says they'll answer for the credit. "
            "The send address is the dorm."
        ),
        redHerringExplanation="{name} sent that email from their room. Their badge isn't the 7:11 PM print-desk unlock.",
        statementTemplate="I badged out of Lockwood at 6:40 PM. I didn't print anything after that.",
        prefer=["riley", "quinn", "jordan", "harper", "taylor", "jamie", "avery", "morgan"],
    ),
    C(
        id="abbott_eyepiece",
        title="Abbott Observatory Eyepiece",
        difficulty="medium",
        motive="Somebody needed the glass for a night shoot.",
        solvePath="The missing eyepiece's serial is scratched into a camera case returned at 9:40 PM.",
        briefing=brief(
            "Abbott Hall, observatory deck.",
            "8:15 PM to 9:05 PM.",
            "The main eyepiece left its case. The loan clipboard still says it's on the mount.",
            "Somebody needed the glass for a night shoot.",
            [
                "Camera case returned to the media lab, 9:40 PM. Grease in the foam, a serial scratched in.",
                "Observatory visitor sheet. In at 7:30 PM, out at 8:05 PM.",
                "Night-shoot request for the quad.",
                "Deck door log.",
                "Afternoon work order to clean a teaching lens.",
            ],
        ),
        treeHook="abbott_eyepiece",
        scene="Abbott Hall, observatory deck",
        windowStart="8:15 PM",
        windowEnd="9:05 PM",
        accessTime="8:44 PM",
        accessTitle="Camera-case return",
        accessDescription=(
            "Media lab return, 9:40 PM. {name}'s camera case. Abbott focus grease on the foam, "
            "and the missing eyepiece's serial scratched into it."
        ),
        contradictionExplanation="{name} took the Abbott eyepiece and brought it back in their camera case.",
        signInTitle="Observatory visitor sheet",
        signInDescription="{name} signed the Abbott visitor sheet in at 7:30 PM and out at 8:05 PM.",
        rosterTitle="Night-shoot request",
        rosterDescription="{name} filed a night-shoot request for the quad.",
        alibiTitle="Deck door log",
        alibiDescription="Abbott deck door. No read for {name} after 8:05 PM.",
        redHerringTitle="Lens-cleaning work order",
        redHerringDescription=(
            "Work order, 3 PM. {name} logged a tagged teaching lens out and back in. The tag number is on the form."
        ),
        redHerringExplanation="{name} moved a tagged teaching lens at 3 PM and logged it back. The missing piece is the untagged eyepiece.",
        statementTemplate="I left Abbott before 8:15. I shot the quad. I didn't take the glass off the mount.",
        prefer=["quinn", "avery", "jamie", "riley", "drew", "harper", "casey", "sam"],
    ),
    C(
        id="hochstetter_incubator",
        title="Hochstetter Incubator Left Open",
        difficulty="medium",
        motive="Judging's tomorrow. They wanted the other group's samples dead before that.",
        solvePath="Incubator B's glove log at 2:22 PM is one student's glove size, and the alarm was acknowledged there.",
        briefing=brief(
            "Hochstetter Hall, incubator B.",
            "2:10 PM to 2:40 PM.",
            "Incubator B was left open, the alarm was shut off at the box, and a symposium group's cultures died.",
            "Judging's tomorrow. They wanted the other group's samples dead before that.",
            [
                "Glove log H-B, 2:22 PM. A glove size, a skipped sanitizer beep, an alarm acknowledge.",
                "Cleanup sign-out sheet, 2:05 PM.",
                "Cleanup rotation list.",
                "Prep-kitchen motion sensor, 2:51 PM.",
                "Gel-pack slip, 9 AM.",
            ],
        ),
        treeHook="hochstetter_incubator",
        scene="Hochstetter Hall, incubator B",
        windowStart="2:10 PM",
        windowEnd="2:40 PM",
        accessTime="2:22 PM",
        accessTitle="Incubator glove log",
        accessDescription=(
            "Incubator glove log H-B, 2:22 PM. {name}'s glove size. The sanitizer beep was skipped. "
            "The alarm was acknowledged on the incubator panel."
        ),
        contradictionExplanation="{name} opened incubator B at 2:22 PM and shut the alarm off.",
        signInTitle="Lab cleanup sign-out",
        signInDescription="{name} signed the Hochstetter cleanup sheet out at 2:05 PM.",
        rosterTitle="Cleanup rotation",
        rosterDescription="{name} is on the Hochstetter cleanup rotation.",
        alibiTitle="Prep-kitchen motion",
        alibiDescription="Prep-kitchen motion sensor. {name}'s badge shows up at 2:51 PM.",
        redHerringTitle="Morning gel-pack slip",
        redHerringDescription="Gel-pack slip. {name} signed packs out at 9 AM.",
        redHerringExplanation="{name} restocked teaching-lab cold packs at 9 AM. Those packs stayed in the teaching lab.",
        statementTemplate="I finished cleanup and I was in the prep kitchen. I didn't touch that alarm.",
        prefer=["harper", "avery", "jordan", "riley", "morgan", "quinn", "taylor", "jamie"],
    ),
    C(
        id="silverman_reserve",
        title="Silverman Reserve Textbook Lift",
        difficulty="easy",
        motive="The 5 PM midterm uses problems that are only in that edition.",
        solvePath="The reserve-desk camera shows the green Organic II spine leaving at 3:28 PM.",
        briefing=brief(
            "Silverman Library, reserve desk.",
            "3:15 PM to 3:40 PM.",
            "The two-hour reserve copy of Organic II left while the clerk was in the stacks. Nobody scanned it out.",
            "The 5 PM midterm uses problems that are only in that edition.",
            [
                "Reserve-desk camera, 3:28 PM. Somebody behind the counter, then the green spine under a notebook.",
                "Organic II waitlist, 2:50 PM.",
                "5 PM midterm roster.",
                "Carrel lamp log. The lamp goes off at 3:10 PM.",
                "Bookstore receipt. A copy of Organic II, 9:15 AM.",
            ],
        ),
        treeHook="silverman_reserve",
        scene="Silverman Library, reserve desk",
        windowStart="3:15 PM",
        windowEnd="3:40 PM",
        accessTime="3:28 PM",
        accessTitle="Reserve-desk camera",
        accessDescription=(
            "Reserve-desk camera SR-D, 3:28 PM. {name} steps behind the counter while the clerk's gone, "
            "then leaves with the green Organic II spine under a notebook."
        ),
        contradictionExplanation="{name} took the Silverman reserve copy of Organic II at 3:28 PM.",
        signInTitle="Reserve waitlist",
        signInDescription="{name} signed the Organic II waitlist at 2:50 PM.",
        rosterTitle="5 PM midterm roster",
        rosterDescription="{name} is on the 5 PM Organic II roster.",
        alibiTitle="Carrel lamp log",
        alibiDescription="A Silverman carrel lamp under {name} turns off at 3:10 PM.",
        redHerringTitle="Bookstore receipt",
        redHerringDescription="Campus bookstore receipt, 9:15 AM. {name} bought a copy of Organic II.",
        redHerringExplanation="{name}'s bought copy is still in their locker with the receipt tucked in it.",
        statementTemplate="I waited for Organic II and then I left. I didn't go behind the reserve counter.",
        prefer=["riley", "jamie", "jordan", "harper", "quinn", "taylor", "morgan", "sam"],
    ),
    C(
        id="crosby_pane",
        title="Crosby Greenhouse Pane Break",
        difficulty="medium",
        motive="The gardener wouldn't give out cuttings.",
        solvePath="Jog camera at 6:12 AM shows shoes that match the soil under the broken cutting-bench pane.",
        briefing=brief(
            "Crosby greenhouse, cutting bench.",
            "5:55 AM to 6:25 AM.",
            "One pane over the cutting bench was smashed inward. Soil from the bed is on the brick outside.",
            "The gardener wouldn't give out cuttings.",
            [
                "Sunrise jog camera, 6:12 AM. Shoes with greenhouse soil.",
                "Dawn facilities sign-in, 5:40 AM. Exterior routes.",
                "Gardener's log. A request for cuttings, refused.",
                "Service-road radio check, 6:05 AM.",
                "Work order for a Crosby pane in bay 2.",
            ],
        ),
        treeHook="crosby_pane",
        scene="Crosby greenhouse, cutting bench",
        windowStart="5:55 AM",
        windowEnd="6:25 AM",
        accessTime="6:12 AM",
        accessTitle="Sunrise jog still",
        accessDescription=(
            "Jog camera CJ-1, 6:12 AM. {name} leaving Crosby. "
            "The shoes in the frame match the soil sample from under the broken cutting-bench pane."
        ),
        contradictionExplanation="{name} broke the Crosby cutting-bench pane and left at 6:12 AM with that soil on their shoes.",
        signInTitle="Dawn facilities sign-in",
        signInDescription="{name} signed the dawn facilities sheet at 5:40 AM for exterior routes.",
        rosterTitle="Gardener's log",
        rosterDescription="The gardener's log says {name} asked for cuttings yesterday and was told no.",
        alibiTitle="Service-road radio",
        alibiDescription="Service-road radio, 6:05 AM. {name} called in a check from the service road.",
        redHerringTitle="Bay 2 pane order",
        redHerringDescription=(
            "Work order with {name}'s name on it. Bay 2, a cracked pane logged last week, scheduled for tomorrow."
        ),
        redHerringExplanation="The work order with {name} on it is bay 2. The broken glass this morning is the cutting bench, bay 5.",
        statementTemplate="I was on the service road at dawn. I didn't go into the greenhouse.",
        prefer=["avery", "harper", "reese", "casey", "drew", "jamie", "sam", "morgan"],
    ),
    C(
        id="slee_amp",
        title="Slee Practice-Room Amp",
        difficulty="medium",
        motive="A show tonight, and their own amp died.",
        solvePath="A 10:05 PM photo shows the Slee house-amp serial while room 3 is still signed out.",
        briefing=brief(
            "Slee Hall, practice room 3.",
            "9:30 PM to 10:15 PM.",
            "The house amp left practice room 3. The key never hit the return box.",
            "A show tonight, and their own amp died.",
            [
                "Photo at 10:05 PM. A hallway outlet, and the house-amp serial sticker in the crop.",
                "Recital-crew sheet. Crew ended at 9:00 PM.",
                "Room 3 reservation card. Return line blank.",
                "Key-return box. No drop tonight.",
                "Reservation note for 8 AM. The note says borrowing the loud one.",
            ],
        ),
        treeHook="slee_amp",
        scene="Slee Hall, practice room 3",
        windowStart="9:30 PM",
        windowEnd="10:15 PM",
        accessTime="10:05 PM",
        accessTitle="Amp photo",
        accessDescription=(
            "Photo from {name}'s account, 10:05 PM. A hallway outlet. The Slee house-amp serial sticker is readable in the crop. "
            "Room 3 was still signed out to {name}."
        ),
        contradictionExplanation="{name} took the Slee room 3 amp and photographed the serial at 10:05 PM.",
        signInTitle="Recital-crew sheet",
        signInDescription="{name} signed the Slee recital-crew sheet. Crew ended at 9:00 PM.",
        rosterTitle="Room 3 reservation card",
        rosterDescription="{name} reserved room 3 until 9:20 PM. The return line is blank.",
        alibiTitle="Key-return box",
        alibiDescription="Slee key-return box. No drop under {name} tonight.",
        redHerringTitle="8 AM reservation note",
        redHerringDescription="{name} reserved room 3 for 8 AM and wrote \"borrowing the loud one\" on the note.",
        redHerringExplanation="The note on {name}'s card is for a reservation that starts at 8 AM.",
        statementTemplate="I returned the practice-room key before 9:30. I didn't take the amp.",
        prefer=["casey", "quinn", "reese", "sam", "jamie", "morgan", "riley", "drew"],
    ),
    C(
        id="baldy_alarm",
        title="Baldy Hall False Alarm Pull",
        difficulty="easy",
        motive="They weren't ready to sit that quiz.",
        solvePath="The pull-station camera at 8:11 PM shows a directory bracelet and a quiz lanyard on the lever.",
        briefing=brief(
            "Baldy Hall, third floor, by the pull station.",
            "8:05 PM to 8:20 PM.",
            "Somebody pulled the fire alarm during a quiz. No smoke. The building emptied.",
            "They weren't ready to sit that quiz.",
            [
                "Pull-station camera, 8:11 PM. A bracelet and a quiz lanyard on the lever.",
                "Quiz scratch sheet, 8:00 PM.",
                "Third-floor section list.",
                "Stairwell camera, 8:13 PM. Somebody heading down.",
                "Health-desk photo of a burn dressing, 11 AM.",
            ],
        ),
        treeHook="baldy_alarm",
        scene="Baldy Hall, third floor, by the pull station",
        windowStart="8:05 PM",
        windowEnd="8:20 PM",
        accessTime="8:11 PM",
        accessTitle="Pull-station camera still",
        accessDescription=(
            "Pull-station camera BA-3, 8:11 PM. {name}'s directory bracelet and quiz lanyard on the lever as it's pulled."
        ),
        contradictionExplanation="{name} pulled the Baldy third-floor alarm at 8:11 PM.",
        signInTitle="Quiz scratch sheet",
        signInDescription="Quiz scratch sheet. {name} was marked present at 8:00 PM and absent after the pull.",
        rosterTitle="Third-floor section list",
        rosterDescription="{name} is on the third-floor section list.",
        alibiTitle="Stairwell camera",
        alibiDescription="Stairwell camera BA-S, 8:13 PM. {name} heading down.",
        redHerringTitle="Health-desk burn photo",
        redHerringDescription="Health-desk photo, 11 AM. {name} has a fresh burn dressing on one hand.",
        redHerringExplanation="The burn on {name}'s hand was dressed at the health desk at 11 AM, after a morning lab.",
        statementTemplate="I was in the quiz room when the alarm went. I didn't touch the pull station.",
        prefer=["taylor", "jordan", "avery", "drew", "jamie", "harper", "morgan", "riley"],
    ),
    C(
        id="obrian_grade",
        title="O'Brian Registrar Grade Flip",
        difficulty="hard",
        motive="One course grade, changed overnight.",
        solvePath="A grade-change form saved at 12:41 AM on a registrar session that never switched users.",
        briefing=brief(
            "O'Brian Hall, registrar suite.",
            "12:20 AM to 12:55 AM.",
            "Somebody saved a grade change on a registrar computer. The author line on the form is blank.",
            "One course grade, changed overnight.",
            [
                "Grade-form save, 12:41 AM. Author line blank.",
                "Student-worker close sheet, 11:50 PM, and a building-clear line at 12:10 AM.",
                "Session on the registrar computer. Opened at 11:40 PM.",
                "Suite badge read, 12:33 AM.",
                "Night-class roster. Let out at 12:30 AM.",
            ],
        ),
        treeHook="obrian_grade",
        scene="O'Brian Hall, registrar suite",
        windowStart="12:20 AM",
        windowEnd="12:55 AM",
        accessTime="12:41 AM",
        accessTitle="Grade-form save",
        accessDescription=(
            "Registrar terminal REG-4, 12:41 AM. A grade-change form, author line blank. "
            "The session is the one opened at 11:40 PM. No user switch. {name} is the closer signed on that computer."
        ),
        contradictionExplanation="{name} came back into the registrar suite and saved the grade change at 12:41 AM.",
        signInTitle="Registrar clear line",
        signInDescription="{name} signed the student-worker close sheet at 11:50 PM and the building-clear line at 12:10 AM.",
        rosterTitle="REG-4 session",
        rosterDescription=(
            "REG-4 shows no user switch after the session opened at 11:40 PM. It was still open at 12:41 AM. "
            "{name} is the closer on the sign-on sheet."
        ),
        alibiTitle="Suite badge read",
        alibiDescription="Registrar suite reader, 12:33 AM. {name}'s badge.",
        redHerringTitle="Night-class roster",
        redHerringDescription=(
            "{name} is on the O'Brian night-class roster that let out at 12:30 AM. The changed grade is in a course they're failing."
        ),
        redHerringExplanation="{name} was in the lecture hall for that night class. Their badge doesn't read the registrar suite.",
        statementTemplate="I signed the clear line at 12:10 AM. I didn't go back into the suite.",
        prefer=["jordan", "riley", "morgan", "taylor", "avery", "jamie", "harper", "sam"],
    ),
]


def main() -> None:
    ids = [c["id"] for c in CASES]
    if len(ids) != len(set(ids)):
        raise SystemExit("duplicate ids")
    if not (12 <= len(CASES) <= 16):
        raise SystemExit(f"count {len(CASES)}")
    titles = [c["title"] for c in CASES]
    if len(titles) != len(set(titles)):
        raise SystemExit("duplicate titles")
    blob = json.dumps(CASES).lower()
    for phrase in ("9:37", "storage room", "storage-room", "mascot head", "nordy"):
        if phrase in blob:
            raise SystemExit(f"banned phrase {phrase}")
    for phrase in BANNED:
        if phrase in blob:
            raise SystemExit(f"player-voice phrase {phrase!r} still in the pack")
    if "victor the bull" not in blob and "victor" not in blob:
        raise SystemExit("Victor missing")
    file = {"schemaVersion": 1, "count": len(CASES), "templates": CASES}
    OUT.write_text(json.dumps(file, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"wrote {len(CASES)} templates to {OUT}")


if __name__ == "__main__":
    main()
