#!/usr/bin/env python3
"""Write Resources/Cases/case_templates.json — 14 distinct UB cases.

Evidence ids stay on CaseGenerator suffixes: roster, alibi_support, sign_in, access, red_herring.
Hard cases keep the access card incomplete until sign-in and alibi/roster are read with it.
"""
from __future__ import annotations

import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "Assets" / "Resources" / "Cases" / "case_templates.json"

J = ["jamie", "morgan", "avery", "riley", "casey", "drew", "quinn", "sam", "taylor", "jordan", "harper", "reese"]


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
    if kwargs["difficulty"] == "hard":
        blob = kwargs["accessDescription"] + kwargs["signInDescription"] + kwargs["alibiDescription"]
        if "only if" not in blob.lower() and "becomes proof" not in blob.lower() and "does not" not in blob.lower():
            raise SystemExit(f"hard case {kwargs['id']} does not split the proof")
    return kwargs


CASES = [
    C(
        id="victor_mural",
        title="Capen Victor Mural Paint-Out",
        difficulty="easy",
        motive="A rival-week prank, aimed at Victor the Bull's mural before the evening game. Not a stolen costume.",
        solvePath="Capen south camera CP-S2 at 4:52 PM shows the culprit's directory jacket and lanyard rolling red paint across Victor. The tabling sheet only proves they were in the building earlier. The acrylic receipt is a sealed poster tube.",
        briefing=(
            "DIFFICULTY: EASY\n\n"
            "Capen Hall, south brick. Between 4:40 PM and 5:10 PM somebody painted out the Victor the Bull mural "
            "with exterior red. Victor's horns are the only original paint left. This is the wall, not a costume and not a storage cage.\n\n"
            "MOTIVE ON THE TABLE: rival-week embarrassment before the game. The paint is exterior red, not the poster acrylic sold in the campus store.\n\n"
            "The south-wall still is the page that names a jacket and a lanyard. The tabling sheet and the coffee camera are earlier. A red-acrylic receipt is the trap."
        ),
        treeHook="victor_mural",
        scene="Capen Hall south brick wall",
        windowStart="4:40 PM",
        windowEnd="5:10 PM",
        accessTime="4:52 PM",
        accessTitle="Capen south-wall still CP-S2",
        accessDescription=(
            "Camera CP-S2 at 4:52 PM shows forearms in {name}'s directory jacket pressing a roller into Victor the Bull's outline. "
            "The lanyard name is readable. The face is turned away. Victor's horns are still the original blue."
        ),
        contradictionExplanation="{name} painted out the Victor the Bull mural on the Capen south wall during the 4:40–5:10 PM window.",
        signInTitle="Capen tabling sheet",
        signInDescription="{name} signed the Capen afternoon tabling sheet at 3:10 PM. The sheet has no out-time and does not show the south wall.",
        rosterTitle="Founders tabling crew",
        rosterDescription="{name} is on the Founders tabling crew. That crew had poster paint inside Capen, not a roller for exterior brick.",
        alibiTitle="Capen lobby coffee still",
        alibiDescription="Lobby camera CP-L1 shows {name} at the coffee cart at 4:20 PM, before the paint window. The lobby camera then loses them.",
        redHerringTitle="Cadmium-red acrylic receipt",
        redHerringDescription="A campus-store debit under {name} bought a tube of cadmium-red acrylic at 3:40 PM, the same afternoon Victor was painted out.",
        redHerringExplanation="{name} bought poster acrylic for a club table. The tube is still sealed in the poster bin. The mural paint is a cheaper exterior red.",
        statementTemplate="I was not on the Capen south wall when Victor's mural was painted out. I was inside the building.",
        prefer=["casey", "jamie", "quinn", "reese", "morgan", "sam", "drew", "riley"],
    ),
    C(
        id="norton_clone",
        title="Norton Hall Cloned Night Badge",
        difficulty="hard",
        motive="A sealed recommendation letter in a Norton faculty office. The reader saw a copied chip, not the plastic in anyone's wallet.",
        solvePath="NRT-2 at 9:22 PM reads chip SE-B wearing the culprit's printed number. Encode ticket EJ-77 writes SE-B at 4:16 PM. The bench sheet is the only operator list, and only the person who also carries that printed number is closed. The gym still is guest pass G-19 for everyone who claims it. A master-key sign-out does not open Norton offices.",
        briefing=(
            "DIFFICULTY: HARD\n\n"
            "Norton Hall faculty corridor. Between 9:05 PM and 9:40 PM reader NRT-2 opened on a chip whose printed number belongs to a student, "
            "but the chip serial is not the plastic that student still carries.\n\n"
            "MOTIVE ON THE TABLE: a recommendation letter locked in a faculty office. Cloning a badge is how you photograph it without signing the door as yourself.\n\n"
            "The door read, the encode ticket, and the bench sign-in only close together. A gym photo and a master-key checkout both look worse than they are."
        ),
        treeHook="norton_clone",
        scene="Norton Hall faculty corridor",
        windowStart="9:05 PM",
        windowEnd="9:40 PM",
        accessTime="9:22 PM",
        accessTitle="Norton reader NRT-2",
        accessDescription=(
            "NRT-2 granted a chip at 9:22 PM. The printed number on that chip matches {name}. "
            "The chip serial on the read is SE-B. This page does not say who encoded SE-B, and it does not show a face."
        ),
        contradictionExplanation=(
            "{name} encoded chip SE-B on bench 2 at 4:16 PM and used that clone, wearing their own printed number, on Norton reader NRT-2 at 9:22 PM."
        ),
        signInTitle="Encode bench 2 sheet",
        signInDescription=(
            "{name} is signed onto helpdesk encode bench 2 from 4:00 PM to 4:30 PM. "
            "The sheet is a name list. It does not name a chip serial."
        ),
        rosterTitle="Encode ticket EJ-77",
        rosterDescription=(
            "Ticket EJ-77 records one blank written to serial SE-B at 4:16 PM. The operator line is blank. "
            "Bench 2's sign-in sheet is the only name list for that half hour. The ticket becomes proof only if the door chip is SE-B and the printed number is {name}."
        ),
        alibiTitle="Alumni gym still, 9:20 PM",
        alibiDescription=(
            "A 9:20 PM gym still shows a hoodie like {name}'s. The scan under it is guest pass G-19, issued at 9:18 PM to a different name. "
            "{name}'s own plastic has no gym read. The same failed alibi shape appears for more than one person tonight."
        ),
        redHerringTitle="Hallway master-key checkout",
        redHerringDescription=(
            "{name} signed a hallway master key out at 8:40 PM and returned it at 9:50 PM, covering the Norton window."
        ),
        redHerringExplanation=(
            "{name} used that key to lock Capen classrooms after an event. The cabinet camera shows the return. That key does not open Norton faculty offices."
        ),
        statementTemplate="My card was on me the whole time Norton was opened. I was at the gym.",
        prefer=["jordan", "drew", "morgan", "avery", "jamie", "taylor", "quinn", "sam"],
    ),
    C(
        id="hadley_party",
        title="Hadley Village Unit 4B Noise Sheet",
        difficulty="medium",
        motive="A recruit visit that turned into an unregistered party. The host needed the noise sheet to name somebody else.",
        solvePath="Bodycam HV-4 at 11:36 PM shows the culprit in the 4B doorway with the case of cans. The laundry-room sign-in is a clipboard with no machine cycle under their name. The RA cup receipt is a floor program, not the party.",
        briefing=(
            "DIFFICULTY: MEDIUM\n\n"
            "Hadley Village, unit 4B. From 10:40 PM to 12:10 AM residents reported a party: music, a hallway of cans, and a recruit who should not have been overnight.\n\n"
            "MOTIVE ON THE TABLE: keep a visiting recruit out of the conduct file by letting the noise sheet land on the RA who wrote it.\n\n"
            "The doorway still and the laundry sign-in disagree. A cup receipt with an RA's name on it is the page that feels like a confession and is not."
        ),
        treeHook="hadley_party",
        scene="Hadley Village unit 4B",
        windowStart="10:40 PM",
        windowEnd="12:10 AM",
        accessTime="11:36 PM",
        accessTitle="Hadley bodycam still HV-4",
        accessDescription=(
            "Bodycam HV-4 at 11:36 PM shows {name} in the unit 4B doorway, one hand on a case of cans, music behind them. "
            "The still is the doorway, not the laundry room they named."
        ),
        contradictionExplanation="{name} was hosting the Hadley 4B party at 11:36 PM after claiming to be in the laundry room.",
        signInTitle="Hadley laundry clipboard",
        signInDescription="{name} signed the Hadley laundry clipboard at 11:20 PM. The sheet does not say a machine actually ran.",
        rosterTitle="Unit 4B guest line",
        rosterDescription="{name} is written on the 4B guest line earlier in the week as someone who 'might stop by.' A guest line is not the 11:36 PM doorway.",
        alibiTitle="Laundry cycle log",
        alibiDescription="Hadley's machine log has no cycle under {name} between 11:00 PM and midnight. The clipboard signature is the only laundry trace.",
        redHerringTitle="Tops cup receipt",
        redHerringDescription="{name} bought two sleeves of red cups at Tops at 6:10 PM, the same night as the Hadley noise complaint.",
        redHerringExplanation="{name} bought the cups for a scheduled floor program. The receipt is hours before 4B, and the program sign-in sheet accounts for both sleeves.",
        statementTemplate="I was in the Hadley laundry room during the noise. I did not host anything in 4B.",
        prefer=["taylor", "reese", "morgan", "jamie", "casey", "sam", "harper", "riley"],
    ),
    C(
        id="furnas_reprint",
        title="Furnas Chem 201 Night Reprint",
        difficulty="hard",
        motive="The Chem 201 key, reprinted before an 8 AM midterm that fails the course.",
        solvePath="LC-2 prints {name}-review.pdf at 1:17 AM on shared login chem-night. Tablet OUT is 1:06 AM. Door exit is 1:22 AM with no re-entry. Several students share that clipboard-versus-door gap. It closes only for the name on the 1:17 AM file. The lobby USB is their own practice set from 6 PM.",
        briefing=(
            "DIFFICULTY: HARD\n\n"
            "Furnas Hall room 214. Between 1:04 AM and 1:28 AM the Chem 201 answer key was reprinted from a lab terminal on a shared night login.\n\n"
            "MOTIVE ON THE TABLE: the 8 AM midterm. The filename can be typed by anyone who knows the login.\n\n"
            "Read the print job, the tablet sign-out, and the door exit as three pages. A clipboard-versus-door gap shows up on more than one student. A USB labeled 201-KEY is the trap."
        ),
        treeHook="furnas_reprint",
        scene="Furnas Hall 214",
        windowStart="1:04 AM",
        windowEnd="1:28 AM",
        accessTime="1:17 AM",
        accessTitle="LC-2 print job",
        accessDescription=(
            "Furnas terminal LC-2 sent {name}-review.pdf to the 214 printer at 1:17 AM. The login is the shared account chem-night. "
            "This page is the filename and the clock. It does not show who was sitting there."
        ),
        contradictionExplanation=(
            "{name} was still inside Furnas 214 at 1:17 AM, despite a 1:06 AM tablet sign-out, and printed the Chem 201 key under their filename."
        ),
        signInTitle="Furnas 214 tablet",
        signInDescription="{name} signed the Furnas 214 tablet OUT at 1:06 AM. The tablet is a clipboard. It does not open the door.",
        rosterTitle="chem-night clearance",
        rosterDescription="{name} is cleared on the shared login chem-night, with the rest of the night section. Clearance is not a print job.",
        alibiTitle="Furnas door exit",
        alibiDescription=(
            "{name}'s Furnas door exit is 1:22 AM. The previous read is entry at 12:40 AM, with no entry between the tablet OUT and that exit. "
            "Several night-section students show this same gap. It becomes proof only if the 1:17 AM print job names {name}."
        ),
        redHerringTitle="USB labeled 201-KEY",
        redHerringDescription="A USB checked out on {name}'s student number was in the Furnas lobby lost-and-found. The sticker reads 201-KEY.",
        redHerringExplanation="{name}'s USB is their own practice set from office hours, turned in at 6 PM, hours before the reprint.",
        statementTemplate="I signed out of Furnas 214 at 1:06 AM. I was gone before any printer ran.",
        prefer=["harper", "jordan", "riley", "avery", "quinn", "morgan", "jamie", "drew"],
    ),
    C(
        id="ellicott_shove",
        title="Ellicott Tunnel Shoulder Check",
        difficulty="medium",
        motive="A group-project slide one partner deleted. The shove happened where the tunnel cameras lose faces and keep shoes.",
        solvePath="Tunnel grit on the culprit's shoes matches node ET-2, and the 11:18 PM text from their phone says they are in the tunnel now. The SOS beacon was pressed by the reporting student, not by the suspect list. A rugby bruise photo is from practice at 5 PM.",
        briefing=(
            "DIFFICULTY: MEDIUM\n\n"
            "Ellicott tunnel, node ET-2. Between 11:12 PM and 11:28 PM a student was shouldered into the wall and threatened over a deleted project slide. "
            "The tunnel cameras catch shoes and coats, not faces.\n\n"
            "MOTIVE ON THE TABLE: the slide. The reporting student hit an SOS beacon. That beacon is not a suspect's confession.\n\n"
            "Shoe grit and a text timestamp have to be the same person. A bruise photo from earlier in the day is the trap."
        ),
        treeHook="ellicott_shove",
        scene="Ellicott tunnel node ET-2",
        windowStart="11:12 PM",
        windowEnd="11:28 PM",
        accessTime="11:18 PM",
        accessTitle="Tunnel grit and text pair",
        accessDescription=(
            "Lab notes on shoes {name} wore tonight match Ellicott tunnel grit from node ET-2. "
            "Their phone sent 'I'm in the tunnel, do not follow me' at 11:18 PM. Either page alone is a walk through a public hall. Together they are the shove window."
        ),
        contradictionExplanation="{name} was at Ellicott node ET-2 at 11:18 PM and started the shove.",
        signInTitle="Project review sign-out",
        signInDescription="{name} signed out of the Ellicott project review at 11:05 PM. The review room is one turn from the tunnel.",
        rosterTitle="Slide-deletion thread",
        rosterDescription="{name} is in the project thread where the slide was deleted at 10:40 PM. Being in the thread is not being in the tunnel.",
        alibiTitle="Dorm Wi-Fi roam",
        alibiDescription=(
            "{name}'s phone shows a dorm-named Wi-Fi at 11:20 PM. That access point also covers the tunnel mouth. The name of the network is not a room."
        ),
        redHerringTitle="Rugby bruise photo",
        redHerringDescription="{name} posted a photo of a fresh bruise at 11:40 PM and captioned it 'tunnel night.'",
        redHerringExplanation="The photo's file time is 5:12 PM from rugby practice. The caption is a joke posted after the rumor spread. It does not place {name} at ET-2.",
        statementTemplate="I left the project review and went back to the dorm. I never put a hand on anyone in the tunnel.",
        prefer=["reese", "drew", "jamie", "taylor", "morgan", "casey", "sam", "avery"],
    ),
    C(
        id="goodyear_bike",
        title="Goodyear Bike Cage Lock Cut",
        difficulty="easy",
        motive="A blue Trek sold before a same-night debt. The cage camera caught a lanyard, not a silhouette.",
        solvePath="GC-1 at 7:22 PM shows the culprit's lanyard and a bolt cutter on the Trek. The intramural sheet is earlier. A theater chain-tool checkout was returned to the scene shop with the seal intact.",
        briefing=(
            "DIFFICULTY: EASY\n\n"
            "Goodyear Hall bike cage. Between 7:10 PM and 7:40 PM the lock was cut off a blue Trek and the bike was walked out the cage's south gate.\n\n"
            "MOTIVE ON THE TABLE: a same-night resale. The cage is outdoors and camera GC-1 faces the locks.\n\n"
            "The still names a lanyard. A tool checkout from the theater shop is the trap."
        ),
        treeHook="goodyear_bike",
        scene="Goodyear Hall bike cage",
        windowStart="7:10 PM",
        windowEnd="7:40 PM",
        accessTime="7:22 PM",
        accessTitle="Bike-cage still GC-1",
        accessDescription=(
            "Camera GC-1 at 7:22 PM shows {name}'s lanyard and both hands on a bolt cutter at the blue Trek's lock. "
            "The south gate opens in the next frame."
        ),
        contradictionExplanation="{name} cut the Goodyear bike-cage lock and took the Trek at 7:22 PM.",
        signInTitle="Intramural check-in",
        signInDescription="{name} signed the Goodyear intramural sheet at 6:05 PM, before the cage window.",
        rosterTitle="Cage key list",
        rosterDescription="{name} is not on the facilities key list for the cage. The lock was cut, not opened with a key.",
        alibiTitle="South-gate pedestrian count",
        alibiDescription="The south-gate counter logged a bike leaving at 7:23 PM. It does not name {name}. The still does.",
        redHerringTitle="Scene-shop chain tool",
        redHerringDescription="{name} checked a chain tool out of the theater scene shop at 4:50 PM, the day the Trek lock was cut.",
        redHerringExplanation="{name} used the tool on a set piece and returned it sealed before 6 PM. The cage cutter is a different tool, still missing.",
        statementTemplate="I did not go to the Goodyear bike cage tonight. My bike is not the blue Trek.",
        prefer=["reese", "jamie", "sam", "casey", "drew", "quinn", "jordan", "morgan"],
    ),
    C(
        id="lockwood_notes",
        title="Lockwood Carrel Threat Notes",
        difficulty="hard",
        motive="Notes left for a lab partner who took sole credit. The printer account is shared. The exit kiosk can be pressed without leaving.",
        solvePath="Printer P-3 runs note-final.pdf at 7:14 PM on lib-night. The kiosk OUT at 6:40 PM never hits the turnstile. Anti-passback keeps the badge inside, and the 7:11 PM unlock at the print desk is that same badge. Other lib-night names are on Capen's reader. A nasty email from dorm wifi is real and is not the printer.",
        briefing=(
            "DIFFICULTY: HARD\n\n"
            "Lockwood Library carrels 3B and 3C. Between 6:50 PM and 8:20 PM two printed threats were left for a lab partner who had just been given sole credit.\n\n"
            "MOTIVE ON THE TABLE: the credit. The printer login is shared, and an exit kiosk can be tapped from inside.\n\n"
            "The print job, the kiosk sign-out, and the turnstile do not agree. An angry email is the trap."
        ),
        treeHook="lockwood_notes",
        scene="Lockwood Library carrels 3B and 3C",
        windowStart="6:50 PM",
        windowEnd="8:20 PM",
        accessTime="7:14 PM",
        accessTitle="Printer job note-final.pdf",
        accessDescription=(
            "Lockwood printer P-3 produced note-final.pdf at 7:14 PM from the shared account lib-night. "
            "A clerk penciled {name} in the blank owner field after hearing about a print-desk badge. The pencil is not the printer's log. This page does not prove the badge was still inside."
        ),
        contradictionExplanation=(
            "{name} unlocked lib-night from inside Lockwood at 7:11 PM and printed the threat, after a kiosk sign-out that never reached the turnstile."
        ),
        signInTitle="Lockwood exit kiosk",
        signInDescription="{name} pressed the Lockwood exit kiosk OUT at 6:40 PM. The kiosk is a button. It is not the turnstile.",
        rosterTitle="lib-night roster",
        rosterDescription=(
            "{name} is on the lib-night roster with three other students. "
            "The other three badged into Capen at 7:02 PM. Roster presence at Lockwood becomes proof only alongside the print-desk unlock."
        ),
        alibiTitle="Turnstile anti-passback",
        alibiDescription=(
            "Lockwood's exit turnstile has no read for {name} between 6:40 PM and 8:05 PM. "
            "Anti-passback still shows the badge inside at 7:11 PM, when the print desk was unlocked by that badge. "
            "A kiosk OUT without a turnstile read is not an exit. It becomes the print only if note-final.pdf is theirs to explain."
        ),
        redHerringTitle="Dorm-wifi email",
        redHerringDescription=(
            "At 7:20 PM {name}'s campus mail called the same lab partner a thief and said they would 'answer for the credit.' "
            "The send IP is the dorm, not Lockwood."
        ),
        redHerringExplanation="{name} sent a cruel email from their room. It is real, and it is not the printer. Their badge is not the 7:11 PM print-desk unlock.",
        statementTemplate="I badged out of Lockwood at 6:40 PM. I did not print anything after that.",
        prefer=["riley", "quinn", "jordan", "harper", "taylor", "jamie", "avery", "morgan"],
    ),
    C(
        id="abbott_eyepiece",
        title="Abbott Observatory Eyepiece",
        difficulty="medium",
        motive="A night-shoot that needed the observatory glass. The loan clipboard was signed. The grease on the camera case was not on the loan list.",
        solvePath="Media-lab return of the culprit's camera case has Abbott focus-grease and the missing eyepiece's serial scratched into the foam. Their observatory sign-in ended at 8:05 PM, before the loss window. A facilities lens-cleaning order that afternoon moved a different, tagged lens.",
        briefing=(
            "DIFFICULTY: MEDIUM\n\n"
            "Abbott Hall observatory deck. Between 8:15 PM and 9:05 PM the main eyepiece left its case. The loan clipboard still says it is on the mount.\n\n"
            "MOTIVE ON THE TABLE: a night photograph. The glass is worth more as a lens than as a souvenir.\n\n"
            "Grease on a returned camera case has to match the serial scratch. A facilities cleaning order is the trap."
        ),
        treeHook="abbott_eyepiece",
        scene="Abbott Hall observatory deck",
        windowStart="8:15 PM",
        windowEnd="9:05 PM",
        accessTime="8:44 PM",
        accessTitle="Camera-case return, media lab",
        accessDescription=(
            "At 9:40 PM {name} returned a camera case to the media lab. The foam has Abbott focus-grease and a scratch of the missing eyepiece's serial. "
            "The loan clipboard never lists that serial leaving the mount."
        ),
        contradictionExplanation="{name} took the Abbott eyepiece during the loss window and brought it back inside their camera case.",
        signInTitle="Observatory visitor sheet",
        signInDescription="{name} signed the Abbott visitor sheet in at 7:30 PM and out at 8:05 PM, before the loss window.",
        rosterTitle="Night-shoot request",
        rosterDescription="{name} filed a night-shoot request for the quad, not for Abbott. The request does not authorize removing glass.",
        alibiTitle="Deck door after 8:05 PM",
        alibiDescription="Abbott's deck door shows no read for {name} after the 8:05 PM sign-out. The case grease is the return, not the door.",
        redHerringTitle="Lens-cleaning work order",
        redHerringDescription="{name} is named on an afternoon work order to clean Abbott lenses, the same day the eyepiece went missing.",
        redHerringExplanation="{name} moved a tagged teaching lens at 3 PM and logged it back. It is not the untagged eyepiece missing after 8:15 PM.",
        statementTemplate="I left Abbott before 8:15 PM. I photographed the quad, and I did not take glass off the mount.",
        prefer=["quinn", "avery", "jamie", "riley", "drew", "harper", "casey", "sam"],
    ),
    C(
        id="hochstetter_incubator",
        title="Hochstetter Incubator Left Open",
        difficulty="medium",
        motive="A rival group's cultures, ruined before symposium judging. The incubator alarm was silenced from inside the lab, not by a power cut.",
        solvePath="Glove log H-B at 2:22 PM is the culprit's size and skips the sanitizer beep. Their cleanup sign-in says they had already left for the prep kitchen. A cold-pack slip from the morning is a restock, not the incubator.",
        briefing=(
            "DIFFICULTY: MEDIUM\n\n"
            "Hochstetter incubator B. Between 2:10 PM and 2:40 PM the door was left ajar, the alarm was silenced, and a symposium group's cultures died.\n\n"
            "MOTIVE ON THE TABLE: judging is tomorrow. A power cut would have alarmed the whole floor. This alarm was acknowledged at the incubator.\n\n"
            "The glove log and the cleanup sign-out have to be the same pair of hands. A morning supply slip is the trap."
        ),
        treeHook="hochstetter_incubator",
        scene="Hochstetter incubator B",
        windowStart="2:10 PM",
        windowEnd="2:40 PM",
        accessTime="2:22 PM",
        accessTitle="Incubator glove log H-B",
        accessDescription=(
            "Glove log H-B at 2:22 PM records {name}'s glove size, a skipped sanitizer beep, and an alarm acknowledge at the incubator panel. "
            "The log is the hands, not a hallway badge."
        ),
        contradictionExplanation="{name} opened incubator B at 2:22 PM and silenced the alarm after claiming to have left for the prep kitchen.",
        signInTitle="Lab cleanup sign-out",
        signInDescription="{name} signed the Hochstetter cleanup sheet OUT at 2:05 PM, before the incubator window.",
        rosterTitle="Symposium bench list",
        rosterDescription="{name} is not on the symposium bench that lost the cultures. They are on the cleanup rotation.",
        alibiTitle="Prep-kitchen motion",
        alibiDescription="The prep-kitchen motion sensor does not show {name}'s badge until 2:51 PM. The 2:05 PM sign-out did not put them in the kitchen.",
        redHerringTitle="Morning cold-pack slip",
        redHerringDescription="{name} signed out gel packs the morning of the incubator failure.",
        redHerringExplanation="{name} restocked teaching-lab cold packs at 9 AM. The packs never entered incubator B.",
        statementTemplate="I finished cleanup and I was in the prep kitchen when the incubator failed. I did not silence any alarm.",
        prefer=["harper", "avery", "jordan", "riley", "morgan", "quinn", "taylor", "jamie"],
    ),
    C(
        id="silverman_reserve",
        title="Silverman Reserve Textbook Lift",
        difficulty="easy",
        motive="Organic II, a two-hour reserve, taken ninety minutes before the midterm that assigns from it.",
        solvePath="Reserve-desk camera SR-D at 3:28 PM shows the culprit duck behind the counter while the clerk is in the stacks, then leave with the green reserve spine. A morning purchase of the same title is a paid copy, still in their locker.",
        briefing=(
            "DIFFICULTY: EASY\n\n"
            "Silverman Library reserve desk. Between 3:15 PM and 3:40 PM the two-hour reserve copy of Organic II left while the clerk was in the stacks. "
            "The checkout scanner never rang.\n\n"
            "MOTIVE ON THE TABLE: the midterm at 5 PM uses the problems printed only in that edition.\n\n"
            "The desk camera is the page. A receipt for a bought copy is the trap."
        ),
        treeHook="silverman_reserve",
        scene="Silverman Library reserve desk",
        windowStart="3:15 PM",
        windowEnd="3:40 PM",
        accessTime="3:28 PM",
        accessTitle="Reserve-desk camera SR-D",
        accessDescription=(
            "Camera SR-D at 3:28 PM shows {name} step behind the reserve counter while the clerk is gone, then leave with the green Organic II spine under a notebook. "
            "The scanner has no checkout."
        ),
        contradictionExplanation="{name} took the Silverman reserve copy of Organic II at 3:28 PM without checking it out.",
        signInTitle="Reserve waitlist",
        signInDescription="{name} signed the Organic II waitlist at 2:50 PM. The list is a request. It is not a checkout.",
        rosterTitle="5 PM midterm roster",
        rosterDescription="{name} is on the 5 PM Organic II roster, with half the section. Sitting the exam is not taking the reserve copy.",
        alibiTitle="Carrel lamp log",
        alibiDescription="A Silverman carrel lamp under {name} turns off at 3:10 PM. They were in a carrel before the desk camera, not during it.",
        redHerringTitle="Morning bookstore receipt",
        redHerringDescription="{name} bought Organic II at the campus bookstore at 9:15 AM, the day the reserve copy disappeared.",
        redHerringExplanation="{name}'s purchased copy is still in their locker with the receipt tucked in it. The missing book is the reserve spine, stamped SIL-RES.",
        statementTemplate="I waited for Organic II and then I left. I did not go behind the reserve counter.",
        prefer=["riley", "jamie", "jordan", "harper", "quinn", "taylor", "morgan", "sam"],
    ),
    C(
        id="crosby_pane",
        title="Crosby Greenhouse Pane Break",
        difficulty="medium",
        motive="A student gardener refused cuttings. The broken pane is the one above the cutting bench, not the pane already on a work order.",
        solvePath="Sunrise jog camera CJ-1 at 6:12 AM shows the culprit leaving Crosby with greenhouse soil on the shoes that match the bed under the broken pane. Their facilities radio check-in at 6:05 AM is from the service road, which does not see the cutting bench. Tomorrow's pane-replacement order is a different bay.",
        briefing=(
            "DIFFICULTY: MEDIUM\n\n"
            "Crosby greenhouse, cutting bench. Between 5:55 AM and 6:25 AM one pane was broken inward. Soil from the bed is on the brick outside.\n\n"
            "MOTIVE ON THE TABLE: cuttings the gardener would not give out. A work order already schedules a different pane for tomorrow, and that order is the trap.\n\n"
            "Shoe soil and the jog-camera exit have to match. A radio check-in on the service road does not see the bench."
        ),
        treeHook="crosby_pane",
        scene="Crosby greenhouse cutting bench",
        windowStart="5:55 AM",
        windowEnd="6:25 AM",
        accessTime="6:12 AM",
        accessTitle="Sunrise jog still CJ-1",
        accessDescription=(
            "Jog camera CJ-1 at 6:12 AM shows {name} leaving Crosby. The shoes in that frame match the soil under the broken cutting-bench pane, "
            "sampled an hour later. The service road is a different camera."
        ),
        contradictionExplanation="{name} broke the Crosby cutting-bench pane and left at 6:12 AM with that soil on their shoes.",
        signInTitle="Dawn facilities sign-in",
        signInDescription="{name} signed the dawn facilities sheet at 5:40 AM for exterior routes. The sheet does not assign the greenhouse.",
        rosterTitle="Cutting-bench denial",
        rosterDescription="The gardener's log says {name} asked for cuttings yesterday and was refused. A refusal is motive-shaped. It is not the pane.",
        alibiTitle="Service-road radio",
        alibiDescription="{name} radioed a service-road check at 6:05 AM. That post cannot see the cutting bench. The jog camera is the exit that can.",
        redHerringTitle="Tomorrow's pane order",
        redHerringDescription="{name} is the name on a work order to replace a Crosby pane tomorrow.",
        redHerringExplanation="The work order with {name} on it covers bay 2, a cracked pane logged last week. The broken glass this morning is the cutting bench, bay 5, which was intact at lockup.",
        statementTemplate="I was on the service-road route at dawn. I did not go into the greenhouse.",
        prefer=["avery", "harper", "reese", "casey", "drew", "jamie", "sam", "morgan"],
    ),
    C(
        id="slee_amp",
        title="Slee Practice-Room Amp",
        difficulty="medium",
        motive="A same-night show after the player's own amp died. Room 3's key never hit the return box.",
        solvePath="Room 3 stayed signed out to the culprit past the window, and a 10:05 PM photo they posted crops the amp's serial sticker. The recital-crew sheet ended at 9:00 PM. Another musician's joke about borrowing gear is a next-morning reservation.",
        briefing=(
            "DIFFICULTY: MEDIUM\n\n"
            "Slee Hall practice room 3. Between 9:30 PM and 10:15 PM the house amp left the room. The key is still out.\n\n"
            "MOTIVE ON THE TABLE: a show tonight and a dead personal amp. The serial sticker is small and people photograph it by accident.\n\n"
            "The open sign-out and the photo have to be the same amp. A reservation for tomorrow morning is the trap."
        ),
        treeHook="slee_amp",
        scene="Slee Hall practice room 3",
        windowStart="9:30 PM",
        windowEnd="10:15 PM",
        accessTime="10:05 PM",
        accessTitle="Cropped amp photo",
        accessDescription=(
            "A photo posted from {name}'s account at 10:05 PM shows a hallway outlet. In the crop, the Slee house-amp serial sticker is legible. "
            "Room 3's sign-out was still open under {name} at that minute."
        ),
        contradictionExplanation="{name} took the Slee room 3 amp while the key was still signed out to them and photographed the serial at 10:05 PM.",
        signInTitle="Recital-crew sheet",
        signInDescription="{name} signed the Slee recital-crew sheet, which ended at 9:00 PM. Crew duty does not include a key past 9:30 PM.",
        rosterTitle="Room 3 reservation card",
        rosterDescription="{name} reserved room 3 until 9:20 PM. The card's return line is blank.",
        alibiTitle="Key-return box",
        alibiDescription="The Slee key-return box has no drop for {name} tonight. An open reservation is not a returned key.",
        redHerringTitle="Next-morning reservation joke",
        redHerringDescription="{name} reserved room 3 for 8 AM and wrote 'borrowing the loud one' on the note line.",
        redHerringExplanation="The note is a joke on a reservation that starts after the amp was already gone. {name} has no 10:05 PM photo and no open key.",
        statementTemplate="I returned the practice-room key before 9:30 PM. I did not take the amp.",
        prefer=["casey", "quinn", "reese", "sam", "jamie", "morgan", "riley", "drew"],
    ),
    C(
        id="baldy_alarm",
        title="Baldy Hall False Alarm Pull",
        difficulty="easy",
        motive="Empty a third-floor quiz the puller was not ready to sit. The station camera faces the hand, and the hand is wearing a directory bracelet.",
        solvePath="Pull-station camera BA-3 at 8:11 PM shows the culprit's bracelet and their quiz lanyard on the lever. A burn photo from a morning lab is a different hand, hours earlier.",
        briefing=(
            "DIFFICULTY: EASY\n\n"
            "Baldy Hall, third floor. Between 8:05 PM and 8:20 PM the fire pull was thrown during a quiz. There was no smoke. The building emptied.\n\n"
            "MOTIVE ON THE TABLE: the quiz. The pull station has its own camera, aimed at the lever.\n\n"
            "The still is the page. A burn on someone's hand from a morning lab is the trap."
        ),
        treeHook="baldy_alarm",
        scene="Baldy Hall third-floor pull station",
        windowStart="8:05 PM",
        windowEnd="8:20 PM",
        accessTime="8:11 PM",
        accessTitle="Pull-station still BA-3",
        accessDescription=(
            "Camera BA-3 at 8:11 PM shows {name}'s directory bracelet and quiz lanyard on the Baldy third-floor lever as it is pulled. "
            "The quiz roster marks them absent two minutes later."
        ),
        contradictionExplanation="{name} pulled the Baldy third-floor alarm at 8:11 PM during the quiz.",
        signInTitle="Quiz attendance scratch",
        signInDescription="{name} was on the quiz scratch sheet at 8:00 PM and marked absent after the pull. The scratch is a headcount, not the station.",
        rosterTitle="Third-floor section list",
        rosterDescription="{name} is on the third-floor section list, with the rest of the quiz. The list does not pull an alarm.",
        alibiTitle="Stairwell count",
        alibiDescription="Stairwell camera BA-S shows {name} going down at 8:13 PM, after the pull. It does not show the lever.",
        redHerringTitle="Morning lab burn",
        redHerringDescription="{name} has a fresh burn dressing on their hand, photographed at the health desk the day of the pull.",
        redHerringExplanation="The burn is from a morning lab, dressed at 11 AM. The pull-station hand wears {name}'s bracelet and is not the dressed hand.",
        statementTemplate="I was in the quiz room when the alarm went. I did not touch the pull station.",
        prefer=["taylor", "jordan", "avery", "drew", "jamie", "harper", "morgan", "riley"],
    ),
    C(
        id="obrian_grade",
        title="O'Brian Registrar Grade Flip",
        difficulty="hard",
        motive="One grade, changed overnight on a terminal that was never locked. The form's author line is blank on purpose.",
        solvePath="REG-4 saves a grade change at 12:41 AM with a blank author. The clear-line clipboard says the culprit left at 12:10 AM. The suite reader shows their badge back in at 12:33 AM, and the session never switched users. A night-class roster and a failing grade put a different person in the lecture hall, not the suite.",
        briefing=(
            "DIFFICULTY: HARD\n\n"
            "O'Brian Hall registrar suite. Between 12:20 AM and 12:55 AM a grade-change form was saved on terminal REG-4. The author line is blank.\n\n"
            "MOTIVE ON THE TABLE: one course grade. The terminal was left unlocked by the closer. A blank author line is the point.\n\n"
            "The save, the clear-line clipboard, and the suite badge do not agree. Someone failing the class and sitting a night lecture is the trap."
        ),
        treeHook="obrian_grade",
        scene="O'Brian Hall registrar suite",
        windowStart="12:20 AM",
        windowEnd="12:55 AM",
        accessTime="12:41 AM",
        accessTitle="REG-4 grade-form save",
        accessDescription=(
            "Terminal REG-4 saved a grade-change form at 12:41 AM. The author field is blank. "
            "The session id is the closer's session. This page does not type {name}. It only says the session that saved the form was never switched."
        ),
        contradictionExplanation=(
            "{name} signed the registrar clear line at 12:10 AM, badged back into the suite at 12:33 AM, and saved the grade change on their still-open REG-4 session at 12:41 AM."
        ),
        signInTitle="Registrar clear line",
        signInDescription="{name} signed the student-worker close sheet at 11:50 PM and the building-clear line at 12:10 AM. The clear line is a clipboard.",
        rosterTitle="REG-4 session lock",
        rosterDescription=(
            "REG-4 shows no user switch after the closer session began at 11:40 PM. The same session was alive at 12:41 AM. "
            "The session becomes {name}'s only if the suite badge that re-entered is {name}'s."
        ),
        alibiTitle="Suite re-entry badge",
        alibiDescription=(
            "The registrar suite reader shows {name} at 12:33 AM, after the 12:10 AM clear line. "
            "A clipboard clear is not a door. This badge becomes the grade save only together with the unswitched REG-4 session."
        ),
        redHerringTitle="Night-class hallway pass",
        redHerringDescription=(
            "{name} is on the O'Brian night-class roster that let out at 12:30 AM, and the changed grade is in a course they are failing."
        ),
        redHerringExplanation="{name} was in the lecture hall. Their badge never reads the registrar suite. Failing the course is motive-shaped and in the wrong room.",
        statementTemplate="I signed the registrar clear line at 12:10 AM. I did not go back into the suite.",
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
    banned = ("9:37", "storage room", "storage-room", "mascot head", "nordy")
    blob = json.dumps(CASES).lower()
    for phrase in banned:
        if phrase in blob:
            raise SystemExit(f"banned phrase {phrase}")
    if "victor the bull" not in blob and "victor" not in blob:
        raise SystemExit("Victor missing")
    file = {"schemaVersion": 1, "count": len(CASES), "templates": CASES}
    OUT.write_text(json.dumps(file, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"wrote {len(CASES)} templates to {OUT}")


if __name__ == "__main__":
    main()
