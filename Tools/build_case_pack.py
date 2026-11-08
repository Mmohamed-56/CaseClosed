#!/usr/bin/env python3
"""Write Resources/Cases/case_templates.json — 14 distinct UB cases.

Evidence ids stay on CaseGenerator suffixes: roster, alibi_support, sign_in, access, red_herring.
A briefing is four lines: WHAT, WHERE, WHEN, MOTIVE. Records hold the objects.
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
    "shoe grit",
    "dorm-named",
    "alarm acknowledge",
    "in the crop",
    "tabling",
    "south-wall",
    "south brick",
    "horns still",
    "camera still",
    "cadmium",
    "carrel",
    "acknowledge",
    "horns",
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
    lines = briefing.split("\n")
    prefixes = ("WHAT: ", "WHERE: ", "WHEN: ", "MOTIVE: ")
    if len(lines) != 4 or any(not lines[i].startswith(prefixes[i]) or len(lines[i]) <= len(prefixes[i]) for i in range(4)):
        raise SystemExit(f"{kwargs['id']} briefing must be WHAT, WHERE, WHEN, MOTIVE and nothing else")
    act = (kwargs.get("act") or "").strip()
    if not act or act.lower().startswith("i "):
        raise SystemExit(f"{kwargs['id']} act must be a verb phrase after 'I '")
    kwargs["act"] = act
    return kwargs


def brief(what, where, when, motive):
    return "\n".join([
        f"WHAT: {what}",
        f"WHERE: {where}",
        f"WHEN: {when}",
        f"MOTIVE: {motive}",
    ])


CASES = [
    C(
        id="victor_mural",
        title="Victor mural at Capen",
        difficulty="easy",
        motive="Rival week. They wanted Victor embarrassed before the game.",
        act="went back and painted over Victor",
        solvePath="Somebody painted over the Victor mural on the outside wall of Capen Hall between 4:40 and 5:10 PM.",
        briefing=brief(
            "Somebody painted over the Victor the Bull mural.",
            "On the outside wall of Capen Hall.",
            "4:40 PM to 5:10 PM.",
            "Rival week. They wanted Victor embarrassed before the game.",
        ),
        treeHook="victor_mural",
        scene="On the outside wall of Capen Hall",
        windowStart="4:40 PM",
        windowEnd="5:10 PM",
        accessTime="4:52 PM",
        accessTitle="Photo by the wall",
        accessDescription=(
            "A photo taken by the wall at 4:52 PM. A jacket and a lanyard, face turned away, a paint roller in the shot. The jacket looks like {name}'s."
        ),
        contradictionExplanation="{name} painted over the Victor mural on the outside wall of Capen Hall.",
        signInTitle="Club table sign-in sheet",
        signInDescription="{name} signed the club table sheet inside Capen at 3:10 PM. No time out on the line.",
        rosterTitle="Club table list",
        rosterDescription="{name} is on the Founders club table list.",
        alibiTitle="Photo at the coffee cart",
        alibiDescription="A photo at the coffee cart, 4:20 PM. {name} is in it.",
        redHerringTitle="Campus store receipt",
        redHerringDescription="A campus store receipt on {name}'s card. One tube of red paint, 3:40 PM.",
        redHerringExplanation="{name} bought that paint for a club table. The tube is still sealed. The wall was painted with outdoor paint.",
        statementTemplate="I wasn't at the wall. I was inside Capen.",
        prefer=["casey", "jamie", "quinn", "reese", "morgan", "sam", "drew", "riley"],
    ),
    C(
        id="norton_clone",
        title="Copied badge at Norton",
        difficulty="hard",
        motive="A recommendation letter locked in a faculty office.",
        act="copied the card and I opened that office",
        solvePath="Somebody opened a Norton faculty office at 9:22 PM with card number SE-B.",
        briefing=brief(
            "Somebody opened a locked faculty office after the hallway went quiet.",
            "The faculty hallway in Norton Hall.",
            "9:05 PM to 9:40 PM.",
            "A recommendation letter was locked in that office. They wanted to read it.",
        ),
        treeHook="norton_clone",
        scene="The faculty hallway in Norton Hall",
        windowStart="9:05 PM",
        windowEnd="9:40 PM",
        accessTime="9:22 PM",
        accessTitle="Door log",
        accessDescription=(
            "Norton door log, 9:22 PM. Card SE-B opened the faculty office. No name on that line. {name}'s own card number is not SE-B."
        ),
        contradictionExplanation="{name} wrote card SE-B at the ID desk and used it on the Norton faculty door at 9:22 PM.",
        signInTitle="ID desk sign-in sheet",
        signInDescription="{name} is on the ID desk sign-in sheet from 4:00 PM to 4:30 PM.",
        rosterTitle="Card list",
        rosterDescription=(
            "Ticket EJ-77. A blank card was written as number SE-B at 4:16 PM. The name line is blank. "
            "{name} is on the ID desk sheet for that half hour."
        ),
        alibiTitle="Gym photo",
        alibiDescription=(
            "A photo at Alumni gym, 9:20 PM. A hoodie like {name}'s. The pass in the photo is guest pass G-19, "
            "handed out at 9:18 PM under a different name."
        ),
        redHerringTitle="Hallway key receipt",
        redHerringDescription="{name} signed a hallway key out at 8:40 PM and back in at 9:50 PM.",
        redHerringExplanation="{name} used that key to lock Capen classrooms after an event. It came back.",
        statementTemplate="My card was in my pocket the whole time. I was at the gym.",
        prefer=["jordan", "drew", "morgan", "avery", "jamie", "taylor", "quinn", "sam"],
    ),
    C(
        id="hadley_party",
        title="Party at Hadley 4B",
        difficulty="medium",
        motive="A recruit visit turned into a party. The host needed the blame on somebody else.",
        act="was the one in 4B with the cans",
        solvePath="Somebody was in the Hadley 4B doorway at 11:36 PM with a case of cans.",
        briefing=brief(
            "Somebody threw a party, and a recruit stayed the night.",
            "Unit 4B in Hadley Village.",
            "10:40 PM to 12:10 AM.",
            "A recruit visit turned into a party, and the host needed the blame on somebody else.",
        ),
        treeHook="hadley_party",
        scene="Unit 4B in Hadley Village",
        windowStart="10:40 PM",
        windowEnd="12:10 AM",
        accessTime="11:36 PM",
        accessTitle="Photo in the doorway",
        accessDescription=(
            "A photo in the unit 4B doorway, 11:36 PM. Someone with a case of cans, face in shadow. The shirt looks like {name}'s."
        ),
        contradictionExplanation="{name} was in the Hadley 4B doorway at 11:36 PM with the cans.",
        signInTitle="Laundry sign-in sheet",
        signInDescription="{name} signed the laundry sheet at 11:20 PM.",
        rosterTitle="Guest list",
        rosterDescription="{name} is on the unit 4B guest list.",
        alibiTitle="Laundry machine log",
        alibiDescription="Hadley laundry machine log. No cycle under {name} between 11:00 PM and midnight.",
        redHerringTitle="Store receipt",
        redHerringDescription="A store receipt, 6:10 PM. {name} bought two packs of red cups.",
        redHerringExplanation="{name} bought the cups for a floor event. The event sheet accounts for both packs.",
        statementTemplate="I was in the laundry room. I didn't host anything in 4B.",
        prefer=["taylor", "reese", "morgan", "jamie", "casey", "sam", "harper", "riley"],
    ),
    C(
        id="furnas_reprint",
        title="Chem 201 answer key",
        difficulty="hard",
        motive="The Chem 201 midterm is at 8 AM. Fail it and you fail the course.",
        act="printed the Chem 201 answer key",
        solvePath="Somebody printed {name}-review.pdf in Furnas 214 at 1:17 AM.",
        briefing=brief(
            "Somebody printed the Chem 201 answer key.",
            "Room 214 in Furnas Hall.",
            "1:04 AM to 1:28 AM.",
            "The midterm is at 8 AM. Fail it and you fail the course.",
        ),
        treeHook="furnas_reprint",
        scene="Room 214 in Furnas Hall",
        windowStart="1:04 AM",
        windowEnd="1:28 AM",
        accessTime="1:17 AM",
        accessTitle="Print job",
        accessDescription=(
            "The room 214 printer, 1:17 AM. A review file went out on the shared night login. No personal name on the job. {name} is one of the people on that login."
        ),
        contradictionExplanation="{name} printed the Chem 201 key in Furnas 214 at 1:17 AM.",
        signInTitle="Room sign-out sheet",
        signInDescription="{name} signed the room 214 sheet. The time-out line is blank.",
        rosterTitle="Night login list",
        rosterDescription="{name} is on the list for the shared night login, with the rest of the night class.",
        alibiTitle="Furnas door badge",
        alibiDescription="Furnas door log. {name} has a badge read before 1:00 AM. The log doesn't say who was at the printer.",
        redHerringTitle="USB receipt",
        redHerringDescription=(
            "A lost-and-found USB on {name}'s student number. The sticker says 201-KEY. Signed out earlier, return line says 6 PM."
        ),
        redHerringExplanation="{name}'s USB is a practice set from office hours, turned in at 6 PM.",
        statementTemplate="I signed out of Furnas 214 at 1:06 AM. I was gone before the printer ran.",
        prefer=["harper", "jordan", "riley", "avery", "quinn", "morgan", "jamie", "drew"],
    ),
    C(
        id="ellicott_shove",
        title="Shove in the Ellicott tunnel",
        difficulty="medium",
        motive="A project slide one partner deleted.",
        act="shoved them in the tunnel",
        solvePath="Somebody with tunnel dirt on their shoes texted from the Ellicott tunnel at 11:18 PM.",
        briefing=brief(
            "Somebody shoved a student into the wall over a deleted project slide.",
            "In the Ellicott tunnel.",
            "11:12 PM to 11:28 PM.",
            "A partner deleted their project slide.",
        ),
        treeHook="ellicott_shove",
        scene="In the Ellicott tunnel",
        windowStart="11:12 PM",
        windowEnd="11:28 PM",
        accessTime="11:18 PM",
        accessTitle="Tunnel dirt and text",
        accessDescription=(
            "Shoes logged to {name} have dirt from the tunnel floor on them. The note has no time on it."
        ),
        contradictionExplanation="{name} was in the Ellicott tunnel at 11:18 PM and started the shove.",
        signInTitle="Project review sign-out",
        signInDescription="{name} signed out of the Ellicott project review at 11:05 PM.",
        rosterTitle="Slide-deletion thread",
        rosterDescription="{name} is in the project thread. The slide was deleted at 10:40 PM.",
        alibiTitle="Dorm Wi-Fi log",
        alibiDescription=(
            "Phone log, 11:20 PM. {name}'s phone was on the dorm Wi-Fi. "
            "That Wi-Fi reaches the tunnel entrance."
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
        title="Bike taken from Goodyear",
        difficulty="easy",
        motive="Sell the bike the same night. They needed the cash.",
        act="cut the lock and I walked the bike out",
        solvePath="Somebody cut the lock off a blue Trek in the Goodyear cage at 7:22 PM.",
        briefing=brief(
            "Somebody cut the lock off a blue Trek and walked it out of the cage.",
            "The bike cage at Goodyear Hall.",
            "7:10 PM to 7:40 PM.",
            "Sell it tonight. They needed the cash.",
        ),
        treeHook="goodyear_bike",
        scene="The bike cage at Goodyear Hall",
        windowStart="7:10 PM",
        windowEnd="7:40 PM",
        accessTime="7:22 PM",
        accessTitle="Photo in the bike cage",
        accessDescription=(
            "A photo in the bike cage at 7:22 PM. A lanyard and a bolt cutter next to the blue Trek. The name on the lanyard is turned away. The clip matches {name}'s."
        ),
        contradictionExplanation="{name} cut the lock and took the blue Trek from the Goodyear cage at 7:22 PM.",
        signInTitle="Sign-in sheet",
        signInDescription="{name} signed the Goodyear intramural sheet at 6:05 PM.",
        rosterTitle="Cage key list",
        rosterDescription="The key list for the cage doesn't have {name} on it.",
        alibiTitle="Gate list",
        alibiDescription="The gate list, 7:23 PM. A bike left. {name} isn't on it.",
        redHerringTitle="Shop receipt",
        redHerringDescription=(
            "Theater shop checkout. {name} took a chain tool at 4:50 PM. The return line is signed at 5:40 PM."
        ),
        redHerringExplanation="{name} used the chain tool on a set piece and brought it back before 6 PM.",
        statementTemplate="I didn't go to the bike cage tonight. My bike isn't the blue Trek.",
        prefer=["reese", "jamie", "sam", "casey", "drew", "quinn", "jordan", "morgan"],
    ),
    C(
        id="lockwood_notes",
        title="Lockwood Library Threat Notes",
        difficulty="hard",
        motive="A lab partner just got sole credit. They wanted that partner scared.",
        act="printed those threats",
        solvePath="note-final.pdf printed at Lockwood at 7:14 PM from inside the building.",
        briefing=brief(
            "Somebody left two printed threats for a lab partner.",
            "A study room in Lockwood Library.",
            "6:50 PM to 8:20 PM.",
            "That partner just got sole credit. They wanted them scared.",
        ),
        treeHook="lockwood_notes",
        scene="A study room in Lockwood Library",
        windowStart="6:50 PM",
        windowEnd="8:20 PM",
        accessTime="7:14 PM",
        accessTitle="Print job",
        accessDescription=(
            "Lockwood printer, 7:14 PM. The threat file, from the shared night account. The owner line is blank. {name} is on that account with three other students."
        ),
        contradictionExplanation="{name} printed the Lockwood threats at 7:14 PM while their badge was still inside.",
        signInTitle="Sign-out sheet",
        signInDescription="{name} signed out of Lockwood at 6:40 PM.",
        rosterTitle="Night desk list",
        rosterDescription=(
            "{name} is on the night desk list with three other students. Those three went into Capen at 7:02 PM."
        ),
        alibiTitle="Door log",
        alibiDescription=(
            "Lockwood door log. {name} signed out at 6:40 PM. The log doesn't name who sent the 7:14 PM print."
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
        title="Telescope lens at Abbott",
        difficulty="medium",
        motive="Somebody needed the glass for a night shoot.",
        act="took the lens off the telescope",
        solvePath="The missing lens number is scratched into a camera case returned at 9:40 PM.",
        briefing=brief(
            "Somebody took the main lens off the telescope.",
            "The telescope deck on Abbott Hall.",
            "8:15 PM to 9:05 PM.",
            "They needed it for a night photo shoot.",
        ),
        treeHook="abbott_eyepiece",
        scene="The telescope deck on Abbott Hall",
        windowStart="8:15 PM",
        windowEnd="9:05 PM",
        accessTime="8:44 PM",
        accessTitle="Camera case",
        accessDescription=(
            "A camera case turned in at 9:40 PM under {name}. Telescope grease on the foam. A number scratched in the foam reads L-88."
        ),
        contradictionExplanation="{name} took the Abbott lens and brought it back in their camera case.",
        signInTitle="Visitor sign-in sheet",
        signInDescription="{name} signed the Abbott visitor sheet in at 7:30 PM and out at 8:05 PM.",
        rosterTitle="Night shoot list",
        rosterDescription="The telescope loan sheet still lists lens L-88 on the mount. {name} asked to take photos on the quad that night.",
        alibiTitle="Deck door log",
        alibiDescription="Abbott deck door. {name} has a read at 8:05 PM. Nothing after that on this line.",
        redHerringTitle="Lens-cleaning work order",
        redHerringDescription=(
            "Work order, 3 PM. {name} signed a teaching lens out and back in. The tag on the form is T-14."
        ),
        redHerringExplanation="{name} moved a tagged teaching lens at 3 PM and logged it back. The missing piece is the other lens.",
        statementTemplate="I left Abbott before 8:15. I was shooting the quad. I didn't take the lens.",
        prefer=["quinn", "avery", "jamie", "riley", "drew", "harper", "casey", "sam"],
    ),
    C(
        id="hochstetter_incubator",
        title="Incubator left open",
        difficulty="medium",
        motive="Judging's tomorrow. They wanted the other group's samples dead before that.",
        act="opened the incubator and I left those samples to die",
        solvePath="Incubator B's glove log at 2:22 PM is one student's glove size, and they shut the alarm off at the box.",
        briefing=brief(
            "Somebody left an incubator open and a group's samples died.",
            "The incubator room in Hochstetter Hall.",
            "2:10 PM to 2:40 PM.",
            "Judging is tomorrow. They wanted the other group's samples dead before that.",
        ),
        treeHook="hochstetter_incubator",
        scene="The incubator room in Hochstetter Hall",
        windowStart="2:10 PM",
        windowEnd="2:40 PM",
        accessTime="2:22 PM",
        accessTitle="Incubator glove log",
        accessDescription=(
            "Incubator glove log, 2:22 PM. A glove in {name}'s size. The alarm on the box was turned off at 2:23 PM. The log prints a size, not a name."
        ),
        contradictionExplanation="{name} opened incubator B at 2:22 PM and shut the alarm off.",
        signInTitle="Lab cleanup sign-out",
        signInDescription="{name} signed the Hochstetter cleanup sheet out at 2:05 PM.",
        rosterTitle="Cleanup rotation",
        rosterDescription="{name} is on the Hochstetter cleanup rotation.",
        alibiTitle="Kitchen log",
        alibiDescription="Prep kitchen log. {name}'s badge shows up at 2:51 PM.",
        redHerringTitle="Morning receipt",
        redHerringDescription="A cold-pack slip. {name} signed packs out at 9 AM.",
        redHerringExplanation="{name} restocked cold packs at 9 AM. Those packs stayed in the teaching lab.",
        statementTemplate="I finished cleanup and I was in the prep kitchen. I didn't touch that alarm.",
        prefer=["harper", "avery", "jordan", "riley", "morgan", "quinn", "taylor", "jamie"],
    ),
    C(
        id="silverman_reserve",
        title="Reserve book taken",
        difficulty="easy",
        motive="The 5 PM midterm uses problems that are only in that edition.",
        act="took the reserve book",
        solvePath="A photo at the reserve desk shows the green Organic II book leaving at 3:28 PM.",
        briefing=brief(
            "Somebody took the reserve copy of Organic II.",
            "The reserve desk in Silverman Library.",
            "3:15 PM to 3:40 PM.",
            "The 5 PM midterm uses problems that are only in that edition.",
        ),
        treeHook="silverman_reserve",
        scene="The reserve desk in Silverman Library",
        windowStart="3:15 PM",
        windowEnd="3:40 PM",
        accessTime="3:28 PM",
        accessTitle="Photo at the reserve desk",
        accessDescription=(
            "A photo at the reserve desk, 3:28 PM. Someone behind the counter, the green Organic II book going under a notebook. The face is hidden. Initials on the notebook look like {name}'s."
        ),
        contradictionExplanation="{name} took the Silverman reserve copy of Organic II at 3:28 PM.",
        signInTitle="Wait list",
        signInDescription="{name} signed the Organic II wait list at 2:50 PM.",
        rosterTitle="Midterm list",
        rosterDescription="{name} is on the 5 PM Organic II list.",
        alibiTitle="Study room lamp log",
        alibiDescription="A study room lamp under {name} turns off at 3:10 PM.",
        redHerringTitle="Bookstore receipt",
        redHerringDescription="A bookstore receipt, 9:15 AM. {name} bought a copy of Organic II.",
        redHerringExplanation="{name}'s bought copy is still in their locker with the receipt tucked in it.",
        statementTemplate="I waited for Organic II and then I left. I didn't go behind the reserve counter.",
        prefer=["riley", "jamie", "jordan", "harper", "quinn", "taylor", "morgan", "sam"],
    ),
    C(
        id="crosby_pane",
        title="Broken window at Crosby",
        difficulty="medium",
        motive="The gardener wouldn't give out cuttings.",
        act="smashed that greenhouse window",
        solvePath="A photo at 6:12 AM shows shoes that match the dirt under the broken greenhouse window.",
        briefing=brief(
            "Somebody smashed a window in the greenhouse.",
            "The greenhouse at Crosby.",
            "5:55 AM to 6:25 AM.",
            "The gardener wouldn't let them take plants.",
        ),
        treeHook="crosby_pane",
        scene="The greenhouse at Crosby",
        windowStart="5:55 AM",
        windowEnd="6:25 AM",
        accessTime="6:12 AM",
        accessTitle="Photo outside the greenhouse",
        accessDescription=(
            "A photo taken outside Crosby at 6:12 AM. {name} is on the path, walking away. Dirt on the shoes. The broken window is not in the photo."
        ),
        contradictionExplanation="{name} broke the Crosby greenhouse window and left at 6:12 AM with that dirt on their shoes.",
        signInTitle="Sign-in sheet",
        signInDescription="{name} signed the early morning sheet at 5:40 AM for outside routes.",
        rosterTitle="Gardener's log",
        rosterDescription="The gardener's log says {name} asked for plants yesterday and was told no.",
        alibiTitle="Service-road radio",
        alibiDescription="Service-road radio, 6:05 AM. {name} called in a check from the service road.",
        redHerringTitle="Old window order",
        redHerringDescription=(
            "A work order with {name}'s name on it for a cracked window. The order is dated last week. The job is set for tomorrow."
        ),
        redHerringExplanation="The work order with {name} on it is a different room. The broken glass this morning is the plant bench.",
        statementTemplate="I was on the service road at dawn. I didn't go into the greenhouse.",
        prefer=["avery", "harper", "reese", "casey", "drew", "jamie", "sam", "morgan"],
    ),
    C(
        id="slee_amp",
        title="House amp taken from Slee",
        difficulty="medium",
        motive="A show tonight, and their own amp died.",
        act="took the house amp out of the practice room",
        solvePath="A 10:05 PM photo shows the Slee house amp's sticker while room 3 is still signed out.",
        briefing=brief(
            "Somebody took the house amp out of a practice room.",
            "Practice room 3 in Slee Hall.",
            "9:30 PM to 10:15 PM.",
            "A show tonight, and their own amp died.",
        ),
        treeHook="slee_amp",
        scene="Practice room 3 in Slee Hall",
        windowStart="9:30 PM",
        windowEnd="10:15 PM",
        accessTime="10:05 PM",
        accessTitle="Photo of the amp",
        accessDescription=(
            "A photo from {name}'s account, 10:05 PM. A hallway outlet, and part of an amp sticker in the corner. No face in the photo."
        ),
        contradictionExplanation="{name} took the Slee room 3 amp and photographed the sticker at 10:05 PM.",
        signInTitle="Crew list",
        signInDescription="{name} signed the Slee crew list. Crew ended at 9:00 PM.",
        rosterTitle="Room reservation card",
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
        title="Fire alarm at Baldy",
        difficulty="easy",
        motive="They weren't ready to sit that quiz.",
        act="pulled the fire alarm during the quiz",
        solvePath="A photo at 8:11 PM shows a bracelet and a quiz lanyard on the alarm handle.",
        briefing=brief(
            "Somebody pulled the fire alarm during a quiz. There was no fire.",
            "The third floor of Baldy Hall.",
            "8:05 PM to 8:20 PM.",
            "They weren't ready to take that quiz.",
        ),
        treeHook="baldy_alarm",
        scene="The third floor of Baldy Hall",
        windowStart="8:05 PM",
        windowEnd="8:20 PM",
        accessTime="8:11 PM",
        accessTitle="Photo at the alarm",
        accessDescription=(
            "A photo at the third-floor alarm, 8:11 PM. A bracelet and a quiz lanyard on the handle. No face in the photo. The lanyard color matches {name}'s."
        ),
        contradictionExplanation="{name} pulled the Baldy third-floor alarm at 8:11 PM.",
        signInTitle="Quiz sheet",
        signInDescription="Quiz sheet. {name} was marked present at 8:00 PM and gone after the alarm.",
        rosterTitle="Class list",
        rosterDescription="{name} is on the third-floor class list.",
        alibiTitle="Photo in the stairs",
        alibiDescription="A photo in the stairs, 8:13 PM. {name} is heading down.",
        redHerringTitle="Health desk photo",
        redHerringDescription="A health desk photo, 11 AM. {name} has a fresh burn wrap on one hand.",
        redHerringExplanation="The burn on {name}'s hand was dressed at the health desk at 11 AM, after a morning lab.",
        statementTemplate="I was in the quiz room when the alarm went. I didn't touch it.",
        prefer=["taylor", "jordan", "avery", "drew", "jamie", "harper", "morgan", "riley"],
    ),
    C(
        id="obrian_grade",
        title="Grade change at the registrar",
        difficulty="hard",
        motive="One course grade, changed overnight.",
        act="changed that grade on the registrar computer",
        solvePath="A grade-change form saved at 12:41 AM on a registrar session that never switched users.",
        briefing=brief(
            "Somebody changed a grade on a registrar computer.",
            "The registrar's office in O'Brian Hall.",
            "12:20 AM to 12:55 AM.",
            "They wanted a failing grade fixed before morning.",
        ),
        treeHook="obrian_grade",
        scene="The registrar's office in O'Brian Hall",
        windowStart="12:20 AM",
        windowEnd="12:55 AM",
        accessTime="12:41 AM",
        accessTitle="Saved grade form",
        accessDescription=(
            "The registrar computer, 12:41 AM. A grade change form. The name line is blank. The computer was already signed in. {name} is on the evening desk sheet."
        ),
        contradictionExplanation="{name} came back into the registrar's office and saved the grade change at 12:41 AM.",
        signInTitle="Close-out sheet",
        signInDescription="{name} signed the student worker close sheet at 11:50 PM and signed out of the building at 12:10 AM.",
        rosterTitle="Computer login",
        rosterDescription=(
            "The registrar computer was left signed in at 11:40 PM. {name} is one of the names on the evening desk sheet."
        ),
        alibiTitle="Office door log",
        alibiDescription="Registrar office door. {name} has a badge read during the evening shift. The 12:41 AM save has no name on it.",
        redHerringTitle="Night class list",
        redHerringDescription=(
            "{name} is on the O'Brian night class list. Marked present in the lecture hall at 12:30 AM. The changed grade is in a course they're failing."
        ),
        redHerringExplanation="{name} was in the lecture hall for that night class. Their badge doesn't open the registrar's office.",
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
