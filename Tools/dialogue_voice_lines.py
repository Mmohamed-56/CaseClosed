# -*- coding: utf-8 -*-
"""Spoken interview lines for the 12 playable interviewees.

One tree per person, shared across cases. Lines stay in that person's job and in the chair.
Guilt and innocence are separate on access, HOLD, and RELEASE.
"""
from __future__ import annotations


def _r(lo, hi, emotion, line, slip=False, culprit=False, innocent=False):
    row = {
        "minStress": lo,
        "maxStress": hi,
        "emotion": emotion,
        "slip": bool(slip),
        "line": line,
    }
    if culprit:
        row["onlyIfCulprit"] = True
    if innocent:
        row["onlyIfInnocent"] = True
    return row


def _bands(lines, innocent=False, culprit=False, confess_last=False):
    spans = ((0, 29, "calm"), (30, 59, "nervous"), (60, 84, "scared"), (85, 100, "broken"))
    if len(lines) != 4:
        raise SystemExit(f"need 4 lines, got {len(lines)}")
    out = []
    for i, ((lo, hi, emotion), line) in enumerate(zip(spans, lines)):
        if innocent and i == 3:
            emotion = "angry"
        slip = bool(confess_last and culprit and i == 3)
        if slip:
            emotion = "broken"
        out.append(_r(lo, hi, emotion, line, slip=slip, culprit=culprit, innocent=innocent))
    return out


def replies_for(person_id: str, branch: str, qindex: int):
    person = PEOPLE[person_id]
    if branch == "hold":
        h = person["hold"]
        return [
            _r(0, 100, "scared", h[0], innocent=True),
            _r(0, 100, "angry", h[1], innocent=True),
            _r(0, 84, "nervous", h[2], culprit=True),
            _r(85, 100, "broken", h[3], slip=True, culprit=True),
        ]
    if branch == "release":
        rel = person["release"]
        return [
            _r(0, 100, "calm", rel[0], innocent=True),
            _r(0, 84, "nervous", rel[1], culprit=True),
            _r(85, 100, "broken", rel[2], slip=True, culprit=True),
        ]
    lines = person[branch][qindex]
    if branch == "access":
        return _bands(lines[:4], innocent=True) + _bands(lines[4:], culprit=True, confess_last=True)
    return _bands(lines)


PROMPTS = {
    "access": [
        ("access_q1", "You told me you were gone. This says you weren't. Talk.", True),
        ("access_q2", "You told me one thing. This says another. Which one's true?", True),
        ("access_q3", "Slow down. Where were you actually standing?", False),
    ],
    "red_herring": [
        ("herring_q1", "What's this for?", False),
        ("herring_q2", "You've got this from today. Why'd you have it?", True),
        ("herring_q3", "Does this have anything to do with what happened?", False),
    ],
    "alibi_support": [
        ("alibi_q1", "This is what you're leaning on. Were you really there?", False),
        ("alibi_q2", "Could you have gone back after this?", True),
        ("alibi_q3", "If this is off, who actually saw you?", False),
    ],
    "roster": [
        ("roster_q1", "You're on this list. When did you leave?", False),
        ("roster_q2", "Did being on this let you stay late?", True),
        ("roster_q3", "Your name's here. You said you were somewhere else. Explain that.", False),
    ],
    "sign_in": [
        ("sign_q1", "You signed this. When did you actually leave?", False),
        ("sign_q2", "This puts you in the building. How late were you there?", True),
        ("sign_q3", "Did signing this get you a key?", False),
    ],
}


# Each branch other than hold/release is three question-sets.
# access sets are 8 lines: 4 innocent, then 4 culprit (last is the admit).
# The other sets are 4 lines, low stress to high, nobody admits on those.
PEOPLE = {
    "jamie": {
        "hold": [
            "Please don't keep me. I finished volunteering and I went to the west bus loop. I didn't do this.",
            "No. I didn't do it. I was at the west bus loop, and I'm not gonna make something up because you're staring.",
            "You're keeping me? I went to the west bus loop after my shift. I didn't do this. Please.",
            "I'm sorry. I did it. I {act}. I wasn't at the bus loop.",
        ],
        "release": [
            "Thank you. I'm gonna go before I fall apart in this chair. I was at the bus loop.",
            "Okay. I'll go. I went to the west bus loop. I didn't do it.",
            "You're letting me leave after I said it? I did it. I {act}. I'm not taking that back.",
        ],
        "access": [
            [
                "I finished my volunteer shift and I walked to the west bus loop. I watched the buses. I didn't come back.",
                "I get shaky when something says otherwise. I still walked to the bus loop, and I stayed under the shelter.",
                "Please slow down. I was at the west bus loop. I would remember turning around. I didn't.",
                "I'm not inventing a trip just so this can end. I was at the bus loop. I didn't do it.",
                "I know how that looks. I talk too much. I went to the bus loop after my shift, and I'm staying with that.",
                "If I rush to fill the quiet, you'll write it down wrong. I was at the west bus loop.",
                "My hands won't stay still. I was at the bus loop. I didn't come back onto campus.",
                "Stop. I did it. I {act}. The bus loop is what I wanted you to picture.",
            ],
            [
                "The bus loop is the true one. I finished volunteering and I left. I didn't drift back.",
                "I know a clean answer sounds practiced. I've said this since I sat down. I was at the bus loop.",
                "My hands are shaking and I hate it. I was at the west bus loop. I didn't do it.",
                "You can ask it again. I was already at the bus loop. I didn't do this.",
                "I want the bus loop to be the one you keep. I went there after volunteering. That's what I'm giving you.",
                "I can hear myself getting smaller. I was at the west bus loop. That's the answer.",
                "I'm staying with the bus loop. I finished the shift and I left. That's what happened.",
                "The bus loop was wrong. I did it. I {act}. Write that down, not the buses.",
            ],
            [
                "At the west bus loop, under the shelter, watching the lights. That's where I was standing.",
                "I was under the shelter. I can tell you which bus I let go. I wasn't back in a building.",
                "Please. The bus shelter. I was standing there. I didn't turn around.",
                "The shelter. I'm done describing it. I was there, and I didn't do this.",
                "West bus loop. The shelter. That's the spot I'm giving you, and I'm not dressing it up.",
                "I keep saying the shelter because that's where I was standing. Don't push me off it.",
                "I was at the bus shelter. I'm tired and I'm not moving off that spot.",
                "I wasn't standing at that shelter at all. I did it. I {act}.",
            ],
        ],
        "red_herring": [
            [
                "That was for the volunteer table. I picked it up so the setup would be ready. That's what it's for.",
                "It's a supply from the volunteer run. I used it while we were setting tables.",
                "I can see why it looks planned. It was planned for a club table. I didn't use it for this.",
                "It's table stuff. I'm done apologizing for a supply I actually needed for setup.",
            ],
            [
                "Because the table ran short. I grabbed it on the way into my shift. That's the whole reason.",
                "Same day doesn't make it part of this. I needed it so the volunteer table would work.",
                "I had it because setup doesn't happen on its own. I got it for the table.",
                "I had it for the table. You can hate the timestamp. It's still a volunteer supply.",
            ],
            [
                "No. It was for the table I was setting up. It doesn't belong in what happened.",
                "It stayed with the volunteer bins. I'm telling you it isn't part of this.",
                "You're trying to glue it onto the night. It's a table supply. Leave it there.",
                "No. Volunteer table. Not this. I'm done going over that one.",
            ],
        ],
        "alibi_support": [
            [
                "Yes. I was at the west bus loop. I got there after my shift and I stayed under the shelter.",
                "Once I reached the shelter I stayed. I didn't walk back onto campus.",
                "People waiting at the stop saw me. I wasn't hiding. I was waiting on a bus.",
                "Shift, then the bus loop. That's the night. I didn't build a second trip.",
            ],
            [
                "No. After I got to the shelter I stayed until a bus I knew. I didn't double back.",
                "Go back? I was counting buses. I would've missed mine if I'd left the shelter.",
                "I couldn't have. I was standing there. I can tell you which route I skipped.",
                "No. I didn't go back. The shelter was where my night stopped.",
            ],
            [
                "Whoever was waiting on the west loop saw me. I was under the shelter the whole time.",
                "A couple of people at the stop. I don't have their names. They saw me standing there.",
                "The driver on the bus I didn't take looked right at me. I was at that shelter.",
                "I'm not gonna invent a roommate. The people at the shelter saw me. That's who.",
            ],
        ],
        "roster": [
            [
                "I left when the volunteer block ended. My name on there is the shift, and then I walked.",
                "When the setup was done. I don't hang around after the tables come down.",
                "End of the volunteer block. I went to the bus. Being listed doesn't keep me after that.",
                "I left when setup ended. The list is who helped. I helped, and then I left.",
            ],
            [
                "No. Being on the volunteer list let me set a table. It didn't let me stay out after.",
                "It let me help with setup. That's what I signed up for. Then I was done.",
                "No. The list is who showed up to help. I showed up, I finished, I walked to the bus.",
                "No. A crew list isn't a key. I set a table and I left.",
            ],
            [
                "Because I volunteered. I was at the bus loop after the shift, like I said.",
                "My name's there because I helped set up. Then I left for the bus. Those fit.",
                "It's the volunteer block. I can be on it and still have been at the shelter after.",
                "The list is the setup. I did the setup and then I left. That's one night.",
            ],
        ],
        "sign_in": [
            [
                "I signed in for the volunteer block and I left when it ended. Then I was at the bus.",
                "Start of setup, then the work, then the walk to the bus. I didn't hang around after.",
                "I left when the tables were done. The signature is just the start of that block.",
                "When setup ended. I'm not gonna keep redrawing the walk from the table to the bus.",
            ],
            [
                "Until the setup ended. After that I was at the bus loop, not roaming the building.",
                "Long enough to finish one table. Then I left. I wasn't your problem after that.",
                "Through the volunteer block. That's how late. Then the shelter.",
                "As late as setup needed me. Then I left for the bus. Don't stretch it.",
            ],
            [
                "No. Signing in got me a spot at a folding table. It didn't hand me a key.",
                "A sign-in puts me on the volunteer block. It doesn't put me in a locked room.",
                "No key. I signed a volunteer sheet. That's my name on a clipboard, nothing else.",
                "No. I got a table to set. I didn't get a key. We can leave that there.",
            ],
        ],
    },
    "morgan": {
        "hold": [
            "I clocked out of the events desk and I walked back to housing. Check that before you keep me. I didn't do this.",
            "Wrong person. I ended the desk and I left for housing. I didn't do it.",
            "The shift ended. I went back to housing. I didn't do this.",
            "Fine. I did it. I {act}. I wasn't back in housing.",
        ],
        "release": [
            "Good. I've still got a desk to shut down properly. I ended the shift and I went to housing.",
            "I'm going. The shift ended and I went to housing. I didn't do this.",
            "Let me go if you want. I did it. I {act}. I'm not unsaying it in the hall.",
        ],
        "access": [
            [
                "I closed the events desk and I went straight to housing. I don't come back after I clock out.",
                "A record that fights a clock-out annoys me more than it scares me. I was in housing.",
                "I'm not filling the quiet with a better version. I ended the desk. I left.",
                "Write the clock-out. I was gone. I'm not rewriting a shift I actually finished.",
                "I know a desk worker still in the building looks bad. I clocked out. I'm standing on that.",
                "Don't rush me into a cleaner lie. The shift ended. I went to housing.",
                "I ended the desk. I'm tired of the clock, and I'm still saying I left.",
                "The clock-out was wrong. I did it. I {act}. I came back after the desk.",
            ],
            [
                "Events work is clocks. Mine says I left. This doesn't match the night I had, so this is wrong.",
                "I can be short without having done it. I ended the desk. I went to housing. I didn't come back.",
                "You're asking me to doubt a clock-out I watched myself sign. I won't. I was out.",
                "Final version. I was gone before any of this. Write that and stop circling.",
                "A badge with my name on it isn't a mood. I hear you. The shift still ended.",
                "I keep wanting to nudge the time by a minute. A minute would be a lie. I left when I said.",
                "I ended it. I'm not gonna pretty the clock-out up. I left.",
                "I did it. I {act}. I came back after the desk. The clock-out was for you.",
            ],
            [
                "At the events desk until clock-out, then the walk to housing. That's where I was.",
                "Housing. My room. I can tell you which stair I took. I wasn't still at the desk.",
                "Don't loom. I was in housing. I ended the shift and I left the building.",
                "Housing. I'm done pointing at it. I didn't do this.",
                "Events desk, then housing. That's the path I'm giving you.",
                "I keep saying housing because that's where I went. Don't add a stop I didn't make.",
                "I was in my room. I'm not moving that. The desk was already closed.",
                "I wasn't in housing. I did it. I {act}. I came back after the desk.",
            ],
        ],
        "red_herring": [
            [
                "That's a desk supply from the events shift. I signed for it so the table would have what it needed.",
                "Desk staff sign for keys and cups all week. This one was for the shift I actually worked.",
                "It looks like prep because my job is prep. The time on it lines up with the shift, not with this.",
                "If you sit me here over a supply line, you'll be wrong in ink. It was for the desk.",
            ],
            [
                "Because the events desk was short. I picked it up before the shift. That's why it's today.",
                "Same-day is how a desk runs. I had it for the job, not for whatever you're building.",
                "I had it because the closer on days left us thin. I replaced it. Ordinary.",
                "I had it for the desk. Be annoyed about the date if you want. It's still work.",
            ],
            [
                "No. It's an events-desk supply. It doesn't touch what you're asking me about.",
                "It went back to the desk cabinet when the shift ended. It isn't part of this.",
                "Don't staple it to the night. I used it for the job and I was done.",
                "No. Desk supply. I'm done explaining a form I sign twice a week.",
            ],
        ],
        "alibi_support": [
            [
                "Yes. I was in housing. The clock-out matches the walk. I stayed in.",
                "Once I got to my room I stayed. Going back out would be a second clock, and there isn't one.",
                "My roommate had the light on. I was there. I don't collect a crowd to prove a shift ended.",
                "Throw that out and you still have the clock-out. I'm not inventing a third place.",
            ],
            [
                "No. I don't double back after housing. It makes two logs and I would have to explain both.",
                "After I badged into the hall I stayed in. I didn't turn around.",
                "I couldn't. I was already in the room, shoes off. That's not a person who doubled back.",
                "No. I didn't go back out. Housing was the end of the night.",
            ],
            [
                "My roommate. They were up. They saw me come in and I didn't leave again.",
                "The person on the hall desk saw me badge through. Then I was in my room for the night.",
                "Ask housing, not me, if you want a second pair of eyes. I was in the room.",
                "Roommate. Hall desk. That's two people. I'm not adding a fictional third.",
            ],
        ],
        "roster": [
            [
                "The list is the events assignment. The assignment ended. I left for housing.",
                "When the posted shift ended. Everyone on that list had a task. Mine finished.",
                "End of the desk block. Being scheduled isn't the same as staying through this.",
                "I didn't stay past the shift. Staying would be overtime on that list. It isn't.",
            ],
            [
                "No. The list let me run the events desk. It didn't let me linger after clock-out.",
                "It gave me a desk and a stop time. I hit the stop time. That's the whole privilege.",
                "No. Scheduling me isn't a key to the rest of the night. I worked the block and I left.",
                "No. I'm not gonna treat a shift list like permission. I clocked out.",
            ],
            [
                "Because I work the events desk. The list is the shift. After it, I was in housing.",
                "My name's there because I was scheduled. I worked it and I left. That matches what I said.",
                "It's a desk schedule. I can be on it and still have been in housing when this happened.",
                "The list is the shift I finished. I said I finished it. Same fact.",
            ],
        ],
        "sign_in": [
            [
                "I signed the events desk in, I worked the block, and I signed it closed. Then housing.",
                "The signature is the start of the desk. The clock-out is the end. I left after that.",
                "I left at clock-out. Signing in didn't keep me in the building.",
                "Clock-out. I'm not decorating it. Signed in, worked, left.",
            ],
            [
                "Until clock-out. After that I was in housing, not in the building I work.",
                "Late enough to close the desk. Not late enough to still be there. I walked.",
                "Through the posted block. That's how late. Then I was gone.",
                "As late as the desk required. Then I left. Don't drag it past clock-out.",
            ],
            [
                "No. Signing the desk sheet got me the events terminal. It didn't get me a building key.",
                "A sign-in is how the desk knows I showed up. It isn't a key ring.",
                "No key from a signature. Anything I touch for events goes back in the drawer.",
                "No. I got a login for the desk. I didn't get a key. Leave it.",
            ],
        ],
    },
    "avery": {
        "hold": [
            "Look, I was on the linen route. I'm tired, and I know which building I was in. I didn't do this.",
            "Come on. I didn't do it. I've been on my feet since five and I was on the route.",
            "I was halfway through the linen route. You're sitting me down and the radios are still going. I didn't do this.",
            "Alright. I did it. I {act}. I stepped off the linen route to do it.",
        ],
        "release": [
            "Appreciate it. I'm going back to the route before the next building thinks I vanished.",
            "Fine by me. I was on the route, and I'm gonna go finish it.",
            "Letting me walk doesn't undo it. I did it. I {act}. I left the route.",
        ],
        "access": [
            [
                "I left that building and I stayed on the rest of the linen route. I radioed where I was.",
                "I'm too tired to invent a second visit. I was on the loop with the radio. I didn't turn around.",
                "You can raise your voice. I was on the route. I didn't peel off for a personal stop.",
                "I was on the route. Done. I'm not building a crime so your photo feels right.",
                "A facilities badge in a hallway looks like work. I was supposed to be on the next building. I was.",
                "Don't confuse a tired pause with anything else. I said I'd moved on. I had.",
                "I was on the route. I'm sweaty from the cart and I still didn't go back.",
                "I left the route and I did it. I {act}. Don't trust the radio call.",
            ],
            [
                "Facilities nights are hallways and radios. Mine's written down. I kept walking the route.",
                "A wrong badge read happens. A wrong night from me doesn't. I was on the loop.",
                "You can keep asking. I was on the route. I didn't go back off it.",
                "Last time. Route, radio, no return. Believe the radio if you don't believe my face.",
                "The log puts my badge where the route says I wasn't. I hear it. I had already moved on.",
                "I almost said I only checked a door. I didn't. I stayed on the route.",
                "I kept walking. The route log is what I want you to trust, because it's what I did.",
                "It was me. I did it. I {act}. The route I gave you wasn't the night.",
            ],
            [
                "On the linen route, in the hallway I radioed from. That's where I was standing.",
                "Wherever the radio check says. I was in that hall with a cart. I wasn't somewhere else.",
                "Look at the radio, not my boots. I was on the assigned hall.",
                "The hall on the route. I'm done. I didn't do this.",
                "The next building on the linen route. That's the spot.",
                "I keep naming the route because I was on it. Don't invent a room for me.",
                "I was in the hall with the cart. I'm not shifting that to make you comfortable.",
                "I wasn't on that hall with the cart. I did it. I {act}.",
            ],
        ],
        "red_herring": [
            [
                "That's linen, or a cart, or a tool for the route. It did the job it was signed out for.",
                "I sign for equipment every shift. This one went back, or it did the round it was for.",
                "It looks like prep because the job is prep. It's a route supply.",
                "I'm done walking you through a work slip. It was for the linen round.",
            ],
            [
                "Because the cart was short a stack. I signed it out at the start of the route. Ordinary.",
                "Same day is every day for facilities. I had it so the round wouldn't stall.",
                "I had it because nights run out of clean stock. I replaced it and I moved on.",
                "I had it for the route. You can squint at the time. It's still linen.",
            ],
            [
                "No. It's a route supply. It doesn't have anything to do with what you're on about.",
                "It stayed on the cart. I'm telling you it isn't part of this.",
                "Don't promote a linen slip into the night. I used it and I kept walking.",
                "No. It was gear for the route. I'm done with that slip.",
            ],
        ],
        "alibi_support": [
            [
                "Yes. The radio check is where I was. I called it from the loop and I stayed on the loop.",
                "I don't double back unless dispatch sends me, and dispatch didn't. One route.",
                "Whoever was in that hall heard the radio or they didn't. The log did. I was there.",
                "If that check-in is wrong, the whole night's radios are wrong. They aren't. I was there.",
            ],
            [
                "No. Dispatch didn't send me back. I kept the order of the buildings.",
                "After that radio check I went to the next hall. I didn't spin around.",
                "I couldn't have. The next building was already expecting the cart.",
                "No. I didn't go back. The route only runs one direction when we're behind.",
            ],
            [
                "Dispatch. They logged the check. That's who had me, even if they didn't see my face.",
                "The resident who held the door on that hall. They saw the cart. They saw me.",
                "Ask the night lead. They were on the radio with me. I wasn't quiet about where I was.",
                "Night lead, and the radio. I'm not gonna cast a crowd I didn't have.",
            ],
        ],
        "roster": [
            [
                "The board gave me a loop, not a spare hour. I worked the loop and I left it when it ended.",
                "When the route sheet ended. I had doors to check. I checked them and I moved.",
                "End of the loop. Being assigned isn't the same as hanging around after.",
                "I didn't linger. Lingering is how you miss the next building, and I don't miss buildings.",
            ],
            [
                "No. The route list let me into the halls on the sheet. It didn't let me freelance after.",
                "It gave me a cart and a sequence. I followed the sequence. Then I was off.",
                "No. Assignment isn't a reason to still be out. I finished the names on the sheet.",
                "No. I'm not gonna read a route board like a blank check. I did the loop.",
            ],
            [
                "Because I'm on nights. The list is the loop. I was on that loop, which is what I said.",
                "My name's there because they scheduled the linen round. I did the round.",
                "It's a route assignment. I can be on it and still have been in the hall I radioed from.",
                "The list is the work I did. I said I did the work. That's not a second night.",
            ],
        ],
        "sign_in": [
            [
                "I signed the shift start like everyone on nights. Then I walked the route. Then I was done.",
                "The sign-in is the start of the loop. It isn't a promise to be in some other room later.",
                "I left the first building and I kept going. The signature doesn't pin me after that.",
                "Start of shift, then the route. I'm not gonna narrate it prettier than a loop.",
            ],
            [
                "Until the route moved me on. I was in the building on the sheet, then I wasn't.",
                "Long enough to do that hall. Then the radio says the next one. I went.",
                "Through the hall I was assigned. That's how late. Then I was elsewhere on the loop.",
                "As late as that stop on the route. Then I left it. Don't park me there.",
            ],
            [
                "No. Signing the night sheet doesn't cut me a private key. Keys go back on the board.",
                "A sign-in starts the shift. The keys I touch are logged, and I put them back.",
                "No spare key from a signature. If I had one, it'd be on the board.",
                "No. I got a route. I didn't get a key of my own. That's the job.",
            ],
        ],
    },
    "riley": {
        "hold": [
            "I finished the club paperwork and I went to study room B. I can go through the times again. I didn't do this.",
            "This is a mistake. I didn't do it, and I'm not signing anything that says I did.",
            "I took the paperwork to study room B and I stayed in that room. Holding me here is a mistake.",
            "Okay. I did it. I {act}. I wasn't in the study room.",
        ],
        "release": [
            "Thank you. I'll be in the office if you need the receipts again. I was in study room B.",
            "I'll go. Please write down what I actually said. I was in the study room.",
            "I already told you. I did it. I {act}. Don't lose that just because you're letting me leave.",
        ],
        "access": [
            [
                "My night is paperwork, then study room B. I was in that room. I didn't come back out.",
                "I don't spend time I can't explain. I can explain the study room. I can't explain a return I didn't make.",
                "A bad entry unsettles me. I don't fix it by agreeing with it. I was in study room B.",
                "The study room stands. I was in there. I'm not forging a return so your record balances.",
                "I know a treasurer still on campus looks convenient. I was in study room B. I'm keeping that.",
                "Don't wait me into a neater sentence. I went to the study room and I stayed.",
                "I was in study room B. I'm not gonna round the night so it fits your sheet.",
                "I did it. I {act}. I wasn't in the study room. I said I was because it was quiet.",
            ],
            [
                "If I'd gone back, the night would have a second line, and I'd have to account for it. It doesn't.",
                "A mismatched record is an error until you prove it against the study-room block. You haven't.",
                "I dislike being pushed. I dislike lying more. I was in study room B.",
                "Final entry. I was in the study room. Post that. Don't ask me to forge the other way.",
                "I want the study room to win because it's what I did. I'm not amending it for you.",
                "I keep almost adding a stop. There wasn't one. Study room B.",
                "I'm staying with the study room. The first version is the one that adds up.",
                "The study room wasn't where I was. I did it. I {act}. I wasn't in there.",
            ],
            [
                "Study room B, science library, in the chair by the outlet. That's where I was standing, then sitting.",
                "In that room. I can tell you which problem set was open. I wasn't out in a hallway.",
                "Please look at the room booking. I was in study room B. I didn't step out.",
                "Study room B. I'm done itemizing the furniture. I was there. I didn't do this.",
                "Study room B. That's the coordinate I'm giving you.",
                "I keep naming the room because I was in it. Don't relocate me.",
                "I was in the chair in study room B. I'm not shifting buildings for you.",
                "I wasn't in that study-room chair. I did it. I {act}.",
            ],
        ],
        "red_herring": [
            [
                "That's a club purchase. I paid it out of the account I treasurer. It was for the club, full stop.",
                "Different column from whatever this is. I can show you the budget line it sits on.",
                "The figures are dull because they're true. It was a club expense. I filed it.",
                "I'm not rounding a receipt into the night. It was the club.",
            ],
            [
                "Because the club needed it today. I buy what the minutes say to buy. That's the reason.",
                "Same-day is just when the vendor was open. The club line explains it.",
                "I had it because the treasurer job is to have it. I didn't pocket it for this.",
                "I had it for the club books. Be suspicious of the date. The line item is still the club.",
            ],
            [
                "No. It's a club expense. It doesn't touch what happened.",
                "It reconciles to the club, not to this. I'm telling you the columns don't merge.",
                "Don't add it into the night. It's a receipt I already filed under the club.",
                "No. Club books. I'm done defending a purchase that has a minute attached.",
            ],
        ],
        "alibi_support": [
            [
                "Yes. I was in study room B. The booking and the night I described are the same block.",
                "Once I sat down I stayed through the block. Leaving would've been a second line.",
                "The person in the next carrel heard me on a call about dues. I was in there.",
                "If you toss the booking, the dues call still happened in that room. I was there.",
            ],
            [
                "No. I had the room until the block ended. I didn't pack up and double back.",
                "After I badged into the library I went to B and I stayed. I didn't tour.",
                "I couldn't have. I was in the middle of a reconciliation. I don't pause those.",
                "No. I didn't go back out. The room was the rest of the night.",
            ],
            [
                "The student in the next carrel. They knocked once about the outlet. They saw me.",
                "Library staff who confirmed the booking. They saw the room occupied. That was me.",
                "Ask the carrel next door before you ask me to invent a witness. They were there.",
                "Next carrel. That's a real person. I'm not adding a friend I didn't have.",
            ],
        ],
        "roster": [
            [
                "The list is the club meeting. The meeting ended. I took the paperwork to study room B.",
                "When we adjourned. Lots of names were on it. Mine had a task, and the task finished.",
                "End of the meeting. A treasurer on a roster isn't still in the room an hour later.",
                "I didn't stay through whatever this is. Staying would've been in the minutes. It isn't.",
            ],
            [
                "No. The club list let me hold the cash box during the meeting. Then I locked it up.",
                "It gave me the books for that hour. It didn't give me a reason to still be out.",
                "No. Being treasurer isn't a key to the rest of campus. I closed the box and I left.",
                "No. I'm not gonna treat a club roster like access. I did the meeting and I went to B.",
            ],
            [
                "Because I'm the treasurer. The list is the meeting. After it, I was in study room B.",
                "My name's there because I have to be. I did the meeting, then the study room. That matches.",
                "It's a club roster. I can be on it and still have been in B when you're asking about.",
                "The list is the meeting I finished. I said I finished it. Same entry.",
            ],
        ],
        "sign_in": [
            [
                "I signed the meeting sheet, I did the books, and I left for study room B when we adjourned.",
                "The signature is the start of the meeting. It isn't the rest of the night.",
                "I left at adjournment. Signing in didn't keep me at the club table.",
                "Adjournment. Then study room B. I'm not gonna add a column that isn't there.",
            ],
            [
                "Until adjournment. After that I was in the library, in B, not wherever this happened.",
                "Long enough to close the cash box. Then I left the meeting room.",
                "Through the meeting. That's how late. Then I was in the study room.",
                "As late as the agenda. Then I left. Don't extend the meeting past the minutes.",
            ],
            [
                "No. Signing the club sheet got me the cash box for the hour. I locked it after.",
                "A sign-in is attendance. It isn't a key to anything past the meeting room.",
                "No key. I signed a club attendance line. That's a name, then the box goes away.",
                "No. I got the books for an hour. I didn't get a key. The box has its own lock.",
            ],
        ],
    },
    "casey": {
        "hold": [
            "No. I didn't do this. I left practice and I went to the costume annex. I'm freaking out.",
            "Are you serious? I didn't do it. I was in the annex and I've got a game to get to.",
            "Come on. I left practice and I was in the costume annex. I didn't do it.",
            "God. Okay. I did it. I {act}. I wasn't in the costume annex.",
        ],
        "release": [
            "Thank you, seriously. I'm out of here. I was in the annex, and I've still got a call time.",
            "Cool. I'm gone. I was in the costume annex. Don't call me back for the same thing.",
            "You're letting me leave? I did it. I {act}. I'm not gonna pretend I didn't.",
        ],
        "access": [
            [
                "I left practice early and I went to the costume annex. I was in there with the gear. I didn't bounce back out.",
                "Yeah I'm jumpy. I was still in the annex. I didn't sneak back to wherever that says.",
                "Slow down. Annex. I was sewing a tear. I would've noticed if I'd left.",
                "You can ask it louder. I was in the costume annex. I didn't do this.",
                "I know spirit kids get blamed first. I was in the annex after practice. I'm keeping that.",
                "Don't stare me into a different building. I was with the costumes.",
                "I was in the annex. I'm done being cute about it. I didn't leave.",
                "I did it. I {act}. I wasn't in the costume annex like I said.",
            ],
            [
                "The annex is the true one. Practice ended, I went there, I stayed. I didn't drift.",
                "A fast denial isn't a script. It's fast because I've said it since the door. Annex.",
                "My knee's bouncing and I hate that you can see it. I was in the annex. I didn't do it.",
                "Fourth time, same answer. Costume annex. I was already there.",
                "I want you to keep the annex. I went there after practice. That's the night I'm selling, and it's the one I had.",
                "I can hear myself getting louder. Louder isn't a new place. I was in the annex.",
                "I'm staying with the annex. Practice, then costumes. That's the order.",
                "The annex wasn't the night. I did it. I {act}. Don't write the costumes down.",
            ],
            [
                "In the costume annex, at the back table, with a needle. That's where I was standing.",
                "Back table. I can tell you which seam I was on. I wasn't out on a sidewalk.",
                "The annex. Don't crowd the table. I was there the whole time.",
                "Back table in the annex. I'm done pointing. I didn't do this.",
                "Costume annex, back table. That's the spot. Take it or don't.",
                "I keep saying the back table because that's where the work was. I was doing the work.",
                "I was at that table. I'm not relocating for a photo.",
                "I wasn't at the back table in the annex. I did it. I {act}.",
            ],
        ],
        "red_herring": [
            [
                "That's spirit-squad gear. I checked it out so practice wouldn't look like a mess. That's the use.",
                "We burn through tape and paint every week. This was for the squad, not for tonight's problem.",
                "It looks like I was planning something. I was planning a sideline. Different plan.",
                "Squad supply. I'm done being sorry that spirit kids buy loud stuff.",
            ],
            [
                "Because practice ate the last of it. I replaced it the same day. That's the whole scandal.",
                "Same day is how a game week works. I had it for the squad.",
                "I had it because call time doesn't wait on a supply closet. I grabbed it.",
                "I had it for the game. Glare at the receipt. It's still squad gear.",
            ],
            [
                "No. It's for the sideline. It doesn't have anything to do with this.",
                "It went back in the squad bin. I'm telling you it stayed with us.",
                "Don't pin the night on a spirit receipt. I used it for the show.",
                "No. Squad. Not this. I've got a call time and I'm done with that scrap.",
            ],
        ],
        "alibi_support": [
            [
                "Yes. I was in the costume annex. After practice I went in and I stayed with the repairs.",
                "Once I sat at the back table I stayed. Walking out would've shown on the squad chat. It doesn't.",
                "Two other squad kids were in there painting. They saw me. I wasn't alone inventing a room.",
                "Practice, then the annex. That's the night. I'm not building a second one.",
            ],
            [
                "No. I had a seam to finish. I didn't pack the needle up and double back.",
                "After I got to the annex I was stuck on a tear. I didn't leave it half-done.",
                "I couldn't have. Call time was already tight. I was sewing.",
                "No. I didn't go back out. The annex was the rest of it until I had to leave for the game.",
            ],
            [
                "The two squad kids painting letters. They were three feet away. They saw me sewing.",
                "Our captain stuck their head in about call time. They saw me at the table.",
                "Ask the squad before you ask me to name a stranger. They were in the room.",
                "Captain, and the painters. Real people. I'm not adding a fan I didn't have.",
            ],
        ],
        "roster": [
            [
                "The list is spirit practice. Practice ended. I went to the annex.",
                "When practice released us. A lot of names are on that list. Mine left for costumes.",
                "End of practice. Being on the spirit list isn't a curfew extension.",
                "I didn't stay on the field. Staying would've been extra reps. I didn't take them.",
            ],
            [
                "No. The spirit list let me on the practice field. It didn't let me roam after.",
                "It gave me a call time and a costume. I did practice. Then the annex.",
                "No. Squad roster isn't a key. I practiced and I went to sew.",
                "No. I'm not gonna treat a spirit list like an all-access pass. I left practice.",
            ],
            [
                "Because I'm on the squad. The list is practice. After it, I was in the annex, like I said.",
                "My name's there because I have to make practice. I made it, then I sewed.",
                "It's a practice roster. I can be on it and still have been in the annex later.",
                "The list is practice. I said I went to practice. Then the annex. One night.",
            ],
        ],
        "sign_in": [
            [
                "I signed the practice sheet, I did the reps, and I left for the annex when they released us.",
                "The signature is the start of practice. It isn't the whole night.",
                "I left when practice ended. Signing in didn't chain me to the field.",
                "End of practice. Then the annex. That's the order I remember.",
            ],
            [
                "Until they blew the end of practice. After that I was in the annex, not on the field.",
                "Long enough to get yelled at and released. Then I sewed.",
                "Through practice. That's how late on the field. Then costumes.",
                "As late as practice ran. Then I left. Don't keep me on the turf.",
            ],
            [
                "No. Signing practice got me onto the field. It didn't hand me a building key.",
                "A sign-in is how the captain counts heads. It isn't a key.",
                "No key. I signed a practice sheet. My name, then I was a body on the field.",
                "No. I got counted. I didn't get a key. We've covered it.",
            ],
        ],
    },
    "drew": {
        "hold": [
            "I didn't do this. I was on the south quad loop. I walked the whole thing.",
            "Wrong person. I walked the south quad. I didn't do it, and I'm not gonna help you force it.",
            "I was walking the south quad. You're keeping me and I was on that loop. I didn't do this.",
            "It was me. I did it. I {act}. I left the south quad loop.",
        ],
        "release": [
            "Thanks. I'll badge out and get back on the loop if they'll still have me. I was on the south quad.",
            "I'll head out. I was on the south quad loop. I didn't leave it.",
            "Letting me go doesn't change it. I did it. I {act}. I left the patrol.",
        ],
        "access": [
            [
                "I left the aide desk and I walked the south quad loop. I hit the checkpoints in order. I didn't peel off.",
                "A bad checkpoint makes me careful. I was on the quad. I have the order of the lamps.",
                "Don't raise it. I was on the south quad. I would have logged a stop if I'd made one.",
                "South quad. Checkpoints. I didn't do this. Write the loop.",
                "I know an aide badge looks like access. I was on the assigned loop. I'm not expanding it.",
                "I was on the south quad. I'm not adding a stop I didn't walk.",
                "I walked the loop. I'm flat about it because it was flat. I didn't leave it.",
                "I did it. I {act}. I left the south quad. The loop I reported wasn't the night.",
            ],
            [
                "The loop is the true report. Desk, then south quad, in order. I didn't add a stop.",
                "A clean report sounds drilled because we drill them. This one happens to be what I walked.",
                "I'm not gonna invent a stop to match you. I was on the quad.",
                "Same report. South quad. I didn't do this. Write the loop the way I walked it.",
                "I want the loop on the report because I walked it. I'm not amending a true round.",
                "I keep almost adding 'except.' There isn't an except. South quad.",
                "I'm staying with the loop. Desk, then the quad. That's the patrol.",
                "The loop was a false report. I did it. I {act}. I left the quad.",
            ],
            [
                "South quad, between the two lamps by the library path. That's where the loop had me.",
                "On the path. I can give you the checkpoint order. I wasn't inside a building.",
                "The quad. Don't stand over me. I was on the path I was assigned.",
                "South quad path. I'm done pointing at it. I didn't do this.",
                "South quad, library path. That's the coordinate I'm giving you.",
                "I keep saying the path because the path is what I walked. I didn't branch.",
                "I was on that path. I'm not moving the pin for you.",
                "I wasn't on the library path at all. I did it. I {act}.",
            ],
        ],
        "red_herring": [
            [
                "That's aide equipment. I signed it for the patrol and I turned it back in at the desk.",
                "We check gear out every shift. This was for the loop, and it came back.",
                "It looks official because the job is official. It was patrol gear.",
                "I'm done reading a gear line out loud. It was for the south quad round.",
            ],
            [
                "Because the shift starts with a checkout. I had it so the loop was legal. That's why today.",
                "Same day is the shift. I didn't carry it off for something else.",
                "I had it because an aide without the gear doesn't walk. I walked with it, then I returned it.",
                "I had it for the patrol. The date is the shift date. That's not clever.",
            ],
            [
                "No. It's patrol gear. It doesn't touch what you're asking about.",
                "It was back on the aide hook. I'm telling you it made the loop and came home.",
                "Don't attach it to the night. I used it for checkpoints and I signed it in.",
                "No. Aide gear. I've returned it. We're good on that line.",
            ],
        ],
        "alibi_support": [
            [
                "Yes. I was on the south quad. The checkpoint order matches the walk I reported.",
                "Once I started the loop I finished it in order. I don't skip and I don't spin back.",
                "The desk logged the checks as I called them. I was on the path.",
                "If you toss the checks, the order I remember is the same order. I was out there.",
            ],
            [
                "No. A loop doesn't run backward unless the desk retasks you. They didn't.",
                "After the library-path check I went to the next lamp. I didn't return to a building.",
                "I couldn't have. The next checkpoint had a time, and I made it.",
                "No. I didn't go back. The loop ended at the aide desk, and that was me done.",
            ],
            [
                "The aide desk. They took the radio checks. They didn't see my face. They heard the loop.",
                "A student cutting across the quad nodded at the radio. They saw me on the path.",
                "Ask the desk sergeant before you ask me for a crowd. The checks are the witness.",
                "Desk, and one student on the path. I'm not padding the report.",
            ],
        ],
        "roster": [
            [
                "The list is the aide shift. The shift had a loop. I walked it and I came back to the desk.",
                "When the loop closed. Other aides are on that list. Mine was the south quad.",
                "End of the patrol block. A name on the aide roster isn't a reason I stayed out.",
                "I didn't stay past the loop. Extra time would be on the desk sheet. It isn't there.",
            ],
            [
                "No. The aide list let me walk the assigned loop. It didn't let me freelance a building.",
                "It gave me a radio and a path. I used both and I brought the radio back.",
                "No. Being scheduled isn't permission to still be out. I closed the loop.",
                "No. I'm not reading the roster like it covers the whole campus. I had one loop.",
            ],
            [
                "Because I'm an aide tonight. The list is the shift. I was on the south quad, which I reported.",
                "My name's there because they staffed the quad. I staffed the quad.",
                "It's an aide roster. I can be on it and still have been on the path, which is what I said.",
                "The list is the shift. I said I worked the shift. Same report.",
            ],
        ],
        "sign_in": [
            [
                "I signed the aide sheet, I took the radio, and I left on the loop. I signed back when the loop closed.",
                "The signature is the start of the aide shift. The loop is the work. Then I was back.",
                "I left the desk on time for the path. Signing in didn't keep me at the desk.",
                "Start of shift, loop, back. I'm not gonna narrate a straight walk like it's complicated.",
            ],
            [
                "Until the loop sent me out. After the sign-in I was on the quad, not in the office.",
                "Long enough to grab the radio. Then I was outside. That's the point of the sign-in.",
                "Through the handover. That's how late at the desk. Then the path.",
                "As late as the handover. Then I was on the quad. Don't leave me in the office.",
            ],
            [
                "No. Signing the aide sheet got me a radio for the loop. The radio isn't a building key.",
                "A sign-in is accountability. Keys, if any, are a separate hook, and I didn't take one.",
                "No building key. I signed for a radio. I brought the radio back.",
                "No. I got the radio. I didn't get a key. Log it that way.",
            ],
        ],
    },
    "quinn": {
        "hold": [
            "That's not me. I didn't do this. I was in the edit bay dumping cards.",
            "I didn't do it. I was in the edit bay. That's the whole answer.",
            "I've got files uploading. I was in the edit bay. I didn't go stand in a doorway.",
            "Yeah. I did it. I {act}. I wasn't in the edit bay.",
        ],
        "release": [
            "Thanks. I've got a card to dump before the editor kills me. I was in the bay.",
            "Okay. I'll go. I was in the edit bay. I said what I said.",
            "I already admitted it. I did it. I {act}. Letting me out doesn't take that back.",
        ],
        "access": [
            [
                "I finished the shoot and I went to the edit bay. I was at the desk with the cards. I didn't wander out.",
                "A frame with my jacket in it makes my stomach drop. I was still in the bay. I didn't leave the chair.",
                "Slow down and look at the timestamps on the uploads. I was in the edit bay.",
                "Edit bay. Cards. I didn't do this. You can scroll the uploads if you don't believe my mouth.",
                "I know a photographer near a lens looks convenient. I was in the bay uploading. I'm keeping that.",
                "Don't treat a quiet as a new location. I was at the edit desk.",
                "I was in the bay. I'm sick of the frame you're holding. I didn't leave.",
                "I did it. I {act}. I wasn't in the edit bay like I told you.",
            ],
            [
                "The bay is the true one. Shoot, then the desk, then uploads. I didn't add a stop.",
                "A clean timeline sounds edited because I edit for a living. This one is just what I did.",
                "My hands are on the chair arms and I hate it. I was in the edit bay. I didn't do it.",
                "Ask it in a different crop. Edit bay. I was already there.",
                "I want the bay on your notes because I was in the chair. I'm not recutting the night.",
                "I keep almost saying I stepped into the hall. I didn't. Edit bay.",
                "I'm staying with the bay. The shoot ended and I uploaded. That's the cut.",
                "The bay wasn't the night. I did it. I {act}. Don't caption it as an upload.",
            ],
            [
                "In the edit bay, at the desk under the lamp, cards on the left. That's where I was.",
                "At that desk. I can tell you which clip I was marking. I wasn't in a doorway.",
                "The bay. Don't block the monitor in your head. I was in the chair.",
                "Edit desk. I'm done framing it. I was there. I didn't do this.",
                "Edit bay, under the lamp. That's the spot I'm giving you.",
                "I keep saying the desk because the work was on the desk. I was doing the work.",
                "I was in that chair. I'm not walking it down a hall for you.",
                "I wasn't in the edit-bay chair at all. I did it. I {act}.",
            ],
        ],
        "red_herring": [
            [
                "That's a media-lab checkout. I took it for the shoot I was assigned. It came back with the cards.",
                "Photographers check gear out all week. This was for the assignment, and the assignment had a call sheet.",
                "It looks like I was staging something. I was staging a photo. That's the job.",
                "Gear for a shoot. I'm done explaining a checkout I do four times a week.",
            ],
            [
                "Because the shoot was today. I checked it out before call. That's why the date matches.",
                "Same day is the assignment. I didn't carry it off for a second job.",
                "I had it because you can't shoot a night event on a phone and call it the paper. I had the camera.",
                "I had it for the shoot. The date is call time. Not a scheme.",
            ],
            [
                "No. It's shoot gear. It doesn't have anything to do with what happened after.",
                "It was back in the cage with the cards. I'm telling you the checkout closed.",
                "Don't crop it into the night. I used it for the assignment.",
                "No. Camera gear. The cage has it. I'm done with that form.",
            ],
        ],
        "alibi_support": [
            [
                "Yes. I was in the edit bay. The upload times match the chair I said I was in.",
                "Once the first card started copying I stayed. I don't leave a copy halfway.",
                "The editor on the late desk saw the transfers pop up. I was in the chair.",
                "If you ignore the uploads, the editor still saw me. I was in the bay.",
            ],
            [
                "No. A card copy takes forever. I didn't pause it and double back across campus.",
                "After I sat down I was stuck on selects. I didn't go for a walk.",
                "I couldn't have. The editor would've noticed the chair empty. They didn't.",
                "No. I didn't go back out. The bay was the rest of the night until the copies finished.",
            ],
            [
                "The late editor. They were on the other machine. They saw me come in with the cards.",
                "A writer waiting on two frames knocked. They saw me at the desk.",
                "Ask the editor before you ask me to produce a stranger. They were right there.",
                "Late editor. That's a person with a name on the board. I'm not casting extras.",
            ],
        ],
        "roster": [
            [
                "The list is the photo assignment. The shoot ended. I went to the bay.",
                "When the event let out. Other shooters are on that list. I covered my slice and I left.",
                "End of the shoot. A name on the photo list isn't a reason I stayed at the venue.",
                "I didn't stay on site. Staying would've been more frames. I had the frames. I left.",
            ],
            [
                "No. The assignment list let me into the event with a camera. It didn't let me linger after.",
                "It gave me a credential for the shoot. I shot it. Then the bay.",
                "No. Press list isn't a key to the rest of the building. I did the job and I uploaded.",
                "No. I'm not treating a photo list like a hall pass. I shot and I left.",
            ],
            [
                "Because I was assigned. The list is the shoot. After it, I was in the edit bay.",
                "My name's there because the editor put me on the event. I shot the event.",
                "It's an assignment list. I can be on it and still have been in the bay, which is what I said.",
                "The list is the shoot. I said I shot it and then I edited. One night.",
            ],
        ],
        "sign_in": [
            [
                "I signed the media sheet, I shot the event, and I left for the bay when the event ended.",
                "The signature is call time. It isn't the chair I sat in after.",
                "I left the venue when the event broke. Signing in didn't chain me to the door.",
                "Call time, shoot, bay. I'm not gonna add a frame that isn't in the roll.",
            ],
            [
                "Until the event ended. After the sign-in I was shooting, then I was in the bay.",
                "Long enough to get the credential checked. Then I was working, not loitering.",
                "Through the event. That's how late on site. Then the edit bay.",
                "As late as the event. Then I left for the bay. Don't keep me at the door.",
            ],
            [
                "No. Signing the media sheet got me a press credential for that event. Not a building key.",
                "A sign-in is how the editor knows I showed. It isn't a key to a closet.",
                "No key. I signed a call sheet. My name, a camera, a time.",
                "No. I got credentialed. I didn't get a key. The camera cage is a different form.",
            ],
        ],
    },
    "sam": {
        "hold": [
            "Please. I didn't do this. I was closing the cafe.",
            "This is ridiculous. I closed the cafe. I didn't do it.",
            "You're holding me. Fine. I closed the cafe. I was on the register until we locked the door.",
            "Yeah. I did it. I {act}. I wasn't just closing the cafe.",
        ],
        "release": [
            "Good. I've got a cooler to check before I can actually leave. I closed the cafe.",
            "Fine. I'm out. I closed the cafe. I didn't do it.",
            "You can let me walk. I did it. I {act}. I'm not taking it back.",
        ],
        "access": [
            [
                "I closed the cafe. I locked the door and I went home. I wasn't out on the floor after that.",
                "You're making me jumpy with that. I was in the cafe, on the register. I didn't wander off.",
                "I didn't do it. I was closing. Ask the people who wanted the last sandwich.",
                "I'm done. I closed the cafe. I wasn't out doing this.",
                "I closed the cafe. That's what I've got. You can set that in front of me all night.",
                "I don't like how that looks. I was still closing. I'm not changing it.",
                "I closed. I'm beat, and I'm sticking with the cafe.",
                "Yeah. I did it. I {act}. I wasn't only closing the cafe like I said.",
            ],
            [
                "The cafe close is the true one. I was on the register. I didn't leave it to do this.",
                "Both can't be right, so keep mine. I closed the cafe and I stayed there.",
                "I hate this part. The true one is the cafe. I didn't do it.",
                "Last time. I closed. That's true. I didn't do the other thing.",
                "I'm telling you the cafe. That's the one I want written down.",
                "If I start hedging I'm gonna mess it up. I closed the cafe.",
                "The cafe is the one I'm keeping. I closed. I was on the register.",
                "The cafe close was wrong. I did it. I {act}. I wasn't on the register.",
            ],
            [
                "In the cafe, at the register. That's where I was standing.",
                "I was at the register. I can still smell the sanitizer. I wasn't wherever that puts me.",
                "Don't crowd me. I was in the cafe. I can point at the register from here.",
                "The cafe. The register. I didn't do this. I'm done pointing.",
                "I was in the cafe. Register. That's the spot I'm giving you.",
                "Cafe. I keep saying cafe because that's where I was. Don't rush me.",
                "I was closing. Standing at the register. I'm not moving off that.",
                "I wasn't at the register the whole time. I did it. I {act}.",
            ],
        ],
        "red_herring": [
            [
                "It's a cafe order. I needed it for close. That's what it's for.",
                "It's a cafe thing from the shift. I used it on the counter.",
                "You're looking at a supply I bought for work. I didn't pocket it for this.",
                "It's for the cafe. I'm not dressing it up. It was work.",
            ],
            [
                "Because the cafe ran out. I grabbed it on the way in. That's the whole reason.",
                "Same-day doesn't mean I used it for this. The cafe needed it.",
                "I had it because close doesn't run on wishes. I bought it for the counter.",
                "I had it for work. Be mad about the date. It's still a cafe purchase.",
            ],
            [
                "No. It's cafe stock. It doesn't touch what you're asking me about.",
                "It sits in the cafe. It doesn't have anything to do with this.",
                "You're trying to glue it on. It's a work buy. Leave it with the cafe.",
                "No. It was the cafe. Not this. I'm done with that receipt.",
            ],
        ],
        "alibi_support": [
            [
                "Yes. I was in the cafe. The close is real. I stayed through it.",
                "I was there. Register, then the cooler, then the lock. I didn't peel off.",
                "Don't shake it at me. I was in the cafe and I stayed in the cafe.",
                "Yeah, I was there. I actually did that close. Believe the lock if you want.",
            ],
            [
                "No. Once I started close I stayed until the door locked. I didn't double back.",
                "Go back where? I was stuck on the register. I didn't leave the cafe.",
                "I couldn't have. Close runs long. I was still in there.",
                "No. I didn't leave and come back. I locked up and that was the end of me.",
            ],
            [
                "Whoever was waiting on a sandwich saw me. I was at the register.",
                "The other person on close saw me counting the drawer. They were right there.",
                "Ask the people who got the last food. They saw me. I was at the counter.",
                "The other closer. They saw me. I'm not inventing a dining hall full of fans.",
            ],
        ],
        "roster": [
            [
                "I left when the cafe locked. My name on a crew list is the shift, and the shift ended.",
                "When close finished. I don't hang around after the lights go down.",
                "End of close. I did the work and I went. Being listed doesn't keep me after the lock.",
                "I left at lockup. The list is just who closed. I closed, and then I left.",
            ],
            [
                "No. Being on the cafe list let me run the register. It didn't let me stay out after.",
                "It let me close. That's the job. It didn't give me a reason to be somewhere else.",
                "No. The list is who closes. I closed. I didn't get a pass to wander.",
                "No. A crew list isn't a key to the rest of campus. I ran the register.",
            ],
            [
                "Because I work the cafe. The list is my shift. I was in the cafe, like I said.",
                "My name's there because I close. I was closing. Those aren't two different nights.",
                "It's my job. I can be on the list and still have been at the register.",
                "The list is the cafe. I said I was at the cafe. That's the same fact.",
            ],
        ],
        "sign_in": [
            [
                "I signed in for the cafe shift and I left when we locked. That's the time.",
                "Start of close, then I stayed, then I left at the lock. I didn't hang after.",
                "I left when the door locked. The signature is the start. The lock is the end.",
                "When the door locked. I'm not gonna keep restating the cafe for you.",
            ],
            [
                "Until lockup. I was in the cafe the whole close. I wasn't roaming.",
                "Late enough to finish the drawer. Then I locked it and I left. I was in the cafe.",
                "Through close. That's how late. I was on the register.",
                "As late as the cafe needed. Then I left. Don't stretch it past the lock.",
            ],
            [
                "No. Signing in got me a register login. It didn't hand me a key to anywhere else.",
                "A sign-in gets me into my shift. It doesn't get me into somebody else's room.",
                "No key. I signed the cafe sheet. That's a name and a drawer, not a key ring.",
                "No. I got the drawer. I didn't get a key. We're done with that.",
            ],
        ],
    },
    "taylor": {
        "hold": [
            "I was on the north desk the whole block. Please don't keep me. I didn't do this.",
            "You can't pin this on the RA who stayed at the desk. I didn't do it.",
            "Wait, you're actually keeping me? I was on the north desk. I didn't get up. Please, I didn't do this.",
            "I did it. I left the north desk. I {act}.",
        ],
        "release": [
            "Thank you. I'll go back to the desk before the hall thinks I abandoned it. I was there.",
            "Okay. I'll go. I was on the north desk. Please write that, not a guess.",
            "I already said it. I did it. I {act}. I'm not gonna clean it up on the way out.",
        ],
        "access": [
            [
                "I was on the north hall desk for my whole block. I didn't get up except to hand out a package.",
                "My voice shakes on this and I need you to hear the words anyway. I was at the desk.",
                "Please. North desk. I signed people in. I didn't leave the chair for this.",
                "I was at the north desk. I'm not gonna invent a walk so you'll let me go. I didn't do it.",
                "I know an RA near a write-up looks like the write-up. I was on the north desk. I'm staying there.",
                "Don't make me fill the quiet. I was at the desk. I didn't get up.",
                "I keep thinking about the conduct file, and I still didn't leave the desk.",
                "I did it. I {act}. I left the north desk. I wasn't sitting there.",
            ],
            [
                "The desk is the true one. I had duty. I stayed. I didn't slip out.",
                "I know it sounds like the speech we give residents. It's also just where I was. The north desk.",
                "My hands are under my legs so you won't see them. I was at the desk. I didn't do this.",
                "You can ask the duty group chat. North desk. I didn't do it.",
                "I need the desk to be what you write, because it's what I did. I didn't leave.",
                "I almost said I walked a floor. I walked one package to a door and I came straight back.",
                "I'm staying with the desk. Duty started, duty ran, I was in the chair.",
                "The desk wasn't the night. I did it. I {act}. I left the north hall stool.",
            ],
            [
                "At the north hall desk, on the stool, facing the door. That's where I was.",
                "On the stool. I can tell you who I signed in. I wasn't down a side hall.",
                "The desk. Please don't stand so close. I was on the stool the whole block.",
                "North desk, the stool. I'm done. I didn't do this.",
                "North hall desk. The stool. That's the spot I'm giving you.",
                "I keep saying the stool because I was on it. I didn't tour the building.",
                "I was at that desk. I'm not relocating myself to make the file easier.",
                "I wasn't on that north-desk stool. I did it. I {act}.",
            ],
        ],
        "red_herring": [
            [
                "That's a hall purchase. I bought it for a floor program I already had on the calendar.",
                "RAs buy cups and snacks for programs. This was the program. I have the signup.",
                "It looks like I was setting something up. I was. A floor program. Not this.",
                "Program supplies. I'm not gonna apologize for doing the RA job in the afternoon.",
            ],
            [
                "Because the program was tonight, earlier than my desk block. I bought it so residents would show.",
                "Same day is how programming works. I had it for the floor, not for this.",
                "I had it because a program without supplies is me standing in an empty lounge. I bought the supplies.",
                "I had it for the residents. The date is the program date. That's all.",
            ],
            [
                "No. It's for the floor program. It doesn't have anything to do with this.",
                "The signup sheet ate the supplies. I'm telling you they were for that, and they're gone that way.",
                "Don't put it on what happened. I used it for a program I advertised.",
                "No. It was a floor program. I'm done defending snacks.",
            ],
        ],
        "alibi_support": [
            [
                "Yes. I was on the north desk. The duty log and the night I described are the same chair.",
                "Once I sat for duty I stayed. Getting up for a package is not leaving the hall.",
                "Residents came for keys. They saw me. I was on the stool.",
                "If you ignore the duty log, the residents still saw me. I was there.",
            ],
            [
                "No. Duty doesn't let me vanish. I didn't leave the desk and double back from somewhere.",
                "After the package I was back on the stool in a minute. I didn't go on a trip.",
                "I couldn't have. The hall phone rings on that desk. I answered it.",
                "No. I didn't go out. The north desk was the whole block.",
            ],
            [
                "Residents who picked up keys. A few of them. They saw me on the stool.",
                "The RA who relieved me. They saw me still there at the end of the block.",
                "Ask the person who took the next shift. They didn't find an empty desk.",
                "The relief RA. That's a named person. I'm not inventing a friend.",
            ],
        ],
        "roster": [
            [
                "The list is the duty roster. My block ended when the next RA sat down. I was on the desk until then.",
                "When relief showed up. Other RAs are on that list. Mine was the north desk block.",
                "End of my duty block. A name on the RA list isn't me loose in the building after.",
                "I didn't stay past relief. The next name on the roster took the stool.",
            ],
            [
                "No. The duty list put me on the north desk. It didn't send me anywhere else.",
                "It gave me a stool and a phone. I used both. Then relief came.",
                "No. Being the RA on duty isn't a pass to leave the desk. I stayed.",
                "No. I'm not gonna read a duty roster like permission. I had one desk.",
            ],
            [
                "Because I'm the RA on duty. The list is the block. I was on the north desk, which is what I said.",
                "My name's there because housing scheduled me. I did the block.",
                "It's a duty roster. I can be on it and still have been on the stool. That's the point of the list.",
                "The list is the desk I worked. I said I worked the desk. Same thing.",
            ],
        ],
        "sign_in": [
            [
                "I signed the duty sheet when I sat down, and I signed the handoff when relief got there.",
                "The signature is the start of the desk block. The handoff is the end. I was there between them.",
                "I stayed until relief. Signing in didn't send me on a round.",
                "Handoff. That's when I left the stool. I'm not gonna stretch the block.",
            ],
            [
                "The whole block, until relief. I was at the north desk. I wasn't out in the hall doing this.",
                "Late enough that residents were still coming for keys. I was on the stool for them.",
                "Through my duty block. That's how late. Then the next RA had it.",
                "As late as the roster says my block runs. Then I handed it off. Don't add time.",
            ],
            [
                "No. Signing the duty sheet got me the desk phone and the key box for packages. I didn't take a master.",
                "A sign-in is how housing knows the desk is covered. It isn't a personal key.",
                "No key for me. The box opens for packages and it stays at the desk.",
                "No. I got the desk. I didn't get a key to carry around. The box doesn't leave.",
            ],
        ],
    },
    "jordan": {
        "hold": [
            "I was in my room on the ticket queue. I didn't do this, and I'm not thrilled you pulled me out of a shift.",
            "Wrong room. I was on the ticket queue. I didn't do it.",
            "Stamp it. I was in my room on the ticket queue. You don't get another login out of me.",
            "Alright. I did it. I {act}. I wasn't on the ticket queue. Happy?",
        ],
        "release": [
            "Cool. I'll go close the rest of the tickets. I was in my room.",
            "I'll take the door. I was on the queue. I didn't do it.",
            "Letting me leave is your call. I did it. I {act}. That's still true when I walk out.",
        ],
        "access": [
            [
                "I left the helpdesk counter and I opened the queue from my room. I was on the laptop. I didn't come back.",
                "A log with my account makes me sarcastic. I was in my room. The tickets will show the typing.",
                "Slow down. Room. Queue. I can tell you the ticket numbers. I wasn't at a door.",
                "I was in my room. I'm not inventing a commute to make this shorter. I didn't do it.",
                "I know a helpdesk login looks like I can be anywhere. I was in my room. The queue was remote on purpose.",
                "Don't wait. There isn't a second version. I was on the tickets.",
                "I was in the room. You can be sure and still be wrong. I didn't leave the laptop.",
                "I did it. I {act}. The remote queue was me buying time. I wasn't in that room.",
            ],
            [
                "The room is the true one. Counter, then housing, then tickets. I didn't add a stop.",
                "A short answer isn't a script. It's short because the tickets are the whole night. I was home.",
                "I'm gripping the chair and I resent it. I was in my room. I didn't do this.",
                "Ask the queue if you don't believe me. My room. I didn't do it.",
                "I want the room in your notes because the tickets came from there. I'm not moving them.",
                "I keep almost saying I walked back to the lab. I didn't. Room.",
                "I'm staying with the room. The counter closed and I went home to the queue.",
                "The remote queue was me stalling. I did it. I {act}. I wasn't in my room.",
            ],
            [
                "In my room, at the desk, laptop on the queue. That's where I was sitting.",
                "At that desk. I can tell you which ticket I was rude on. I wasn't in a hallway.",
                "My room. Don't picture me at a door. I was on the laptop.",
                "The desk in my room. I'm done describing it. I didn't do this.",
                "My room, at the desk, queue up. That's the spot I was in.",
                "I keep saying the laptop because the work was on the laptop. I was doing the work.",
                "I was in that chair in my room. I'm not commuting for you.",
                "I wasn't in the chair in my room. I did it. I {act}.",
            ],
        ],
        "red_herring": [
            [
                "That's a helpdesk checkout. I took it for a ticket and I closed the ticket. The gear went back.",
                "We check adapters out all day. This one has a ticket number. Read the number.",
                "It looks like access because the job is access. It was a ticket. It closed.",
                "Gear for a ticket. I'm done performing surprise over a form I fill out hourly.",
            ],
            [
                "Because the ticket came in today. I checked the gear out, I fixed it, I brought it back.",
                "Same day is the queue. I didn't keep it for a side project.",
                "I had it because the user couldn't connect. I had it for them. Then I didn't.",
                "I had it for a ticket. The date is the ticket date. Not a plot.",
            ],
            [
                "No. It's a closed ticket. It doesn't have anything to do with this.",
                "The gear is back on the hook. I'm telling you the ticket's shut.",
                "Don't bolt it onto the night. I used it for the user and I returned it.",
                "No. It was ticket gear, and it's closed. I'm done with that row.",
            ],
        ],
        "alibi_support": [
            [
                "Yes. I was in my room. The ticket timestamps match a person sitting still, which was me.",
                "Once I opened the queue I stayed on it. Walking away dumps the session. It didn't dump.",
                "My roommate heard me swearing at a printer ticket. I was in the room.",
                "If you ignore the queue, the roommate still heard me. I was there.",
            ],
            [
                "No. I had a stack. I didn't pause a remote session and sprint across campus.",
                "After I sat down I was in tickets until the queue thinned. I didn't tour.",
                "I couldn't have. The session would've dropped. It didn't drop.",
                "No. I didn't go back out. My room was the rest of the shift.",
            ],
            [
                "My roommate. They were on the other bed with headphones half off. They heard me.",
                "The user on the printer ticket. They were on the phone with me. I was clearly not at their door.",
                "Ask the roommate. Don't ask me to produce a crowd. One person was in the room.",
                "Roommate. That's enough. I'm not adding the whole floor.",
            ],
        ],
        "roster": [
            [
                "The list is the helpdesk shift. The counter part ended. I went to my room for the remote half.",
                "When the counter block ended. Other techs are on that list. Mine went remote.",
                "End of the counter block. A name on the helpdesk list isn't me still at the counter.",
                "I didn't stay on the counter. The remote half is in my room. That's on the schedule if you read it.",
            ],
            [
                "No. The helpdesk list let me on the counter, then on the queue. It didn't let me into a building.",
                "It gave me a login and a block. I did the block from my room after the counter.",
                "No. Being scheduled isn't a pass into every room. I had tickets. I did tickets.",
                "No. I'm not gonna read a staffing list like a master credential. I had a queue.",
            ],
            [
                "Because I work the desk. The list is the shift. After the counter, I was in my room, like I said.",
                "My name's there because they staffed me. I worked it. Counter, then room.",
                "It's a shift list. I can be on it and still have been in my room for the remote half.",
                "The list is the shift I worked. I said I worked it. Same shift.",
            ],
        ],
        "sign_in": [
            [
                "I signed the helpdesk sheet, I did the counter, and I signed the remote handoff when I left for my room.",
                "The signature is the start of the counter. The remote note is me leaving it. Then I was home.",
                "I left the counter on time. Signing in didn't keep me at the bench.",
                "Remote handoff. That's when I left the building. I'm not gonna drag it.",
            ],
            [
                "Until the counter block ended. After that I was in my room, not at the bench.",
                "Long enough to burn through the walk-ups. Then I went home and kept the queue.",
                "Through the counter hours. That's how late in the lab. Then my room.",
                "As late as the counter schedule. Then I left. Don't leave me on the bench.",
            ],
            [
                "No. Signing the helpdesk sheet got me the counter login. It didn't get me a building key.",
                "A sign-in is how the lead knows the bench is covered. It isn't a key.",
                "No key. I signed a shift sheet. My name, a login, a queue.",
                "No. I got the queue. I didn't get a key. The key cabinet is a different tech.",
            ],
        ],
    },
    "harper": {
        "hold": [
            "I took the trays to the prep kitchen after cleanup. Please don't keep me for that. I didn't do this.",
            "This isn't fair. I was in the prep kitchen. I didn't do it, and I'm not gonna guess who did.",
            "I need you to hear me. I took the trays to the prep kitchen and I stayed there. You're holding the wrong pair of hands.",
            "I did it. I {act}. I didn't stay in the kitchen.",
        ],
        "release": [
            "Thanks. I'm going to wash my hands and go home. I was in the prep kitchen.",
            "Okay. I'll go. I took the trays to the kitchen. Don't twist that.",
            "I already told you. I did it. I {act}. I didn't stay in the kitchen.",
        ],
        "access": [
            [
                "I finished cleanup and I walked the trays to the prep kitchen. I stayed there washing up. I didn't come back.",
                "A log with my glove size makes my stomach hurt. I was in the kitchen. I didn't go back to the bench.",
                "Please hear the order. Cleanup, trays, kitchen. I was at the sink.",
                "I was in the prep kitchen. I'm not gonna make up a return trip. I didn't do this.",
                "I know a lab aide near a mess looks like the mess. I took the trays to the kitchen. I stayed.",
                "Don't rush the quiet. I was at the sink. I didn't go back.",
                "I was in the kitchen. My hands were in the water. I didn't leave them to do this.",
                "I did it. I {act}. I wasn't at the sink like I wanted you to think.",
            ],
            [
                "The kitchen is the true one. I cleaned, I carried trays, I washed. I didn't double back.",
                "A plain order sounds rehearsed because lab work is an order. I followed it. Kitchen.",
                "I'm holding my own wrists so I'll stay still. I was in the prep kitchen. I didn't do it.",
                "Ask the person on dishes. Prep kitchen. I didn't do this.",
                "I need you to keep the kitchen, because that's where the trays went, and I went with them.",
                "I almost said I checked a fridge. I checked the one in the kitchen. I didn't go back out.",
                "I'm staying with the kitchen. Cleanup ended at the sink.",
                "The prep kitchen wasn't where I stayed. I did it. I {act}.",
            ],
            [
                "In the prep kitchen, at the big sink, trays to my left. That's where I was standing.",
                "At the sink. I can tell you which rack I loaded. I wasn't back at the bench.",
                "The kitchen. Please don't talk over me. I was at the sink.",
                "Prep kitchen, the sink. I'm done. I didn't do this.",
                "Prep kitchen sink. That's the spot. The trays were there with me.",
                "I keep saying the sink because my hands were in it. I didn't wander.",
                "I was at that sink. I'm not moving the trays back out for you.",
                "I wasn't at the prep-kitchen sink. I did it. I {act}.",
            ],
        ],
        "red_herring": [
            [
                "That's a restock for the teaching lab. I signed it in the morning and I put it where it belongs.",
                "Lab aides pull supplies every shift. This one was a restock. It went to the teaching fridge.",
                "It looks like I was setting a bench. I was restocking. That's the aide job.",
                "A supply slip. I'm not gonna treat a morning restock like it followed me all day.",
            ],
            [
                "Because the teaching fridge was low at the start of the day. I filled it. That's why it's today.",
                "Same day is how a lab runs out. I had it for the teaching section.",
                "I had it because students burn through packs before noon. I replaced them.",
                "I had it for the teaching lab. The time is morning. It stayed there.",
            ],
            [
                "No. It's a teaching-lab restock. It doesn't have anything to do with this.",
                "Those packs are still in the teaching fridge. I'm telling you they never left that room.",
                "Don't carry it into the afternoon. I stocked it and I moved on to cleanup.",
                "No. It was a morning restock. I'm done with that slip.",
            ],
        ],
        "alibi_support": [
            [
                "Yes. I was in the prep kitchen. The trays and the sink match what I told you.",
                "Once I started the dishes I stayed until the rack was done. I don't leave a half-washed tray.",
                "The person on the other sink saw me. I was there the whole wash.",
                "If you ignore the kitchen log, the other sink still saw me. I was washing.",
            ],
            [
                "No. A full rack takes time. I didn't drop it and double back to the bench.",
                "After I set the trays down I was in the water. I didn't dry off and leave.",
                "I couldn't have. The other person would've had to finish my rack. They didn't.",
                "No. I didn't go back. The kitchen was the end of cleanup.",
            ],
            [
                "The aide on the other sink. They handed me a rack. They saw me stay.",
                "The grad student who stuck their head in for a beaker. They saw me at the water.",
                "Ask the other sink before you ask me for a hallway full of people. One person saw me.",
                "Other sink. That's a real coworker. I'm not adding the whole lab.",
            ],
        ],
        "roster": [
            [
                "The list is the cleanup rotation. My name came up, I cleaned, I took the trays to the kitchen.",
                "When the rotation released me. Other aides are on it. I finished my benches.",
                "End of my cleanup block. Being on the rotation isn't me still at the incubator after.",
                "I didn't stay at the bench. The trays leaving is me leaving. That's the rotation.",
            ],
            [
                "No. The cleanup list put me on benches and then the kitchen. It didn't put me back.",
                "It gave me a pair of gloves and a sink at the end. I did both.",
                "No. Rotation isn't permission to hang around the equipment. I washed up and I was done.",
                "No. I'm not gonna read a cleanup list like a reason to stay. I finished.",
            ],
            [
                "Because I'm on the aide rotation. The list is cleanup. After it, I was in the kitchen.",
                "My name's there because they scheduled dishes and benches. I did them.",
                "It's a rotation. I can be on it and still have been at the sink, which is what I said.",
                "The list is the cleanup I finished. I said I finished it. Same work.",
            ],
        ],
        "sign_in": [
            [
                "I signed cleanup when I started, and I signed out when the trays hit the kitchen.",
                "The signature is the start of the benches. The out-time is the kitchen. I was done then.",
                "I left the benches for the sink. Signing in didn't keep me at the equipment.",
                "Out at the kitchen. That's the time. I'm not gonna add a lap.",
            ],
            [
                "Until the trays were ready. After that I was in the kitchen, not at the bench.",
                "Long enough to wipe the benches I was assigned. Then the sink.",
                "Through the benches on my card. That's how late there. Then the prep kitchen.",
                "As late as those benches took. Then I left them. Don't put me back.",
            ],
            [
                "No. Signing the cleanup sheet got me gloves from the box. It didn't get me a lab key of my own.",
                "A sign-in is how the lead knows the rotation showed. It isn't a key.",
                "No personal key. I signed a cleanup card. Gloves, benches, sink.",
                "No. I got gloves. I didn't get a key. The key stays with the lead.",
            ],
        ],
    },
    "reese": {
        "hold": [
            "I didn't do this. I'm jumpy because you pulled me off the court. I was at rec.",
            "Come on. I didn't do it. I was on the court at rec.",
            "This is a bad call. I was on the court at rec. Hold me all night. I didn't leave.",
            "Yeah, it was me. I {act}. I wasn't on the court.",
        ],
        "release": [
            "Appreciate it. I'm gonna go. I was on the court, and I've still got legs for it.",
            "Bet. I'm out. I was at rec. I said I didn't do it.",
            "You heard me. I did it. I {act}. Walking out doesn't change that.",
        ],
        "access": [
            [
                "I cut through campus after practice and I went straight to the rec courts. I was in a run. I didn't peel off.",
                "A camera that says otherwise makes me laugh, and then it doesn't. I was on the court.",
                "Slow down. Rec. I was playing. I would've been sweaty in a different building and I wasn't.",
                "I was on the court. I'm not inventing a side trip. I didn't do this.",
                "I know athletes get the first look. I was at rec after practice. I'm keeping the court.",
                "Don't stare me into a shortcut. I went to rec. I played.",
                "I was on the court. You can be loud. I didn't leave the run.",
                "I did it. I {act}. I wasn't at rec like I wanted you to think.",
            ],
            [
                "The court is the true one. Practice, then rec, then a run that went long. I didn't add a stop.",
                "A quick no is just how I talk. It's also where I was. The court at rec.",
                "My heel's tapping and you can hear it. I was at rec. I didn't do it.",
                "Run it back if you want. Rec court. I didn't do this.",
                "I want the court in the report because I was on it. I'm not drawing a different path.",
                "I almost said I grabbed water and left. I grabbed water and I checked back in. I stayed.",
                "I'm staying with rec. Practice ended and I played. That's the night.",
                "The rec run wasn't the night. I did it. I {act}. I wasn't on that court.",
            ],
            [
                "On the rec court, near the far basket, in a run. That's where I was standing between points.",
                "Far basket. I can tell you the score we were dumb about. I wasn't outside.",
                "The court. Don't box me into a corner. I was in the run.",
                "Far court at rec. I'm done describing the run. I didn't do this.",
                "Rec, far basket. That's the spot. We were playing.",
                "I keep saying the basket because that's where the ball was. I was with the ball.",
                "I was on that court. I'm not jogging you a different map.",
                "I wasn't on that rec court at all. I did it. I {act}.",
            ],
        ],
        "red_herring": [
            [
                "That's rec gear. I borrowed it for the run and I put it back on the cage when we stopped.",
                "I borrow stuff for team nights all the time. This was the run. It went back.",
                "It looks like I was gearing up for something else. I was gearing up to play. That's it.",
                "Rec gear. I'm done acting shocked that an athlete borrowed a ball.",
            ],
            [
                "Because we had a run tonight. I checked it out so we'd have a ball that wasn't dead. That's why.",
                "Same night is the run. I didn't carry it off campus for a scheme.",
                "I had it because the good ball was signed out to us. I played with it.",
                "I had it for rec. The time is the run. Not a plan.",
            ],
            [
                "No. It's a ball and a pinny. It doesn't have anything to do with this.",
                "It's back on the rec cage. I'm telling you the run used it and the run ended.",
                "Don't fold that gear into the night. I played with it and I brought it back.",
                "No. Rec gear. Returned. I'm done with that checkout.",
            ],
        ],
        "alibi_support": [
            [
                "Yes. I was on the rec court. The run matches what I told you, including the score we argued about.",
                "Once the run started I stayed in it. I don't sub out and wander.",
                "Four other people were on the court. They saw me. I wasn't a ghost in the corner.",
                "If you toss the court sheet, the people in the run still saw me. I was playing.",
            ],
            [
                "No. A run doesn't pause so I can jog across campus and jog back sweaty in a new way.",
                "After I checked in I was in the game. I didn't take a mysterious water break off site.",
                "I couldn't have. They would've played four-on-four and yelled. They didn't yell.",
                "No. I didn't leave the court. The run was the night.",
            ],
            [
                "The people in the run. They'll remember the argument about the score. I was in it.",
                "The rec attendant who handed me the ball. They saw me come in and they didn't see me leave early.",
                "Ask the run. Don't ask me to invent a ref. We didn't have one. We had each other.",
                "The run. Real names if you pull the court. I'm not adding a coach.",
            ],
        ],
        "roster": [
            [
                "The list is practice. Practice ended. I went to rec.",
                "When the coach released us. Half the team is on that list. I went to play more, because I'm like that.",
                "End of practice. A name on the practice list isn't me still in the practice gym.",
                "I didn't stay at practice. Extra time was at rec, by choice, on a different court.",
            ],
            [
                "No. The practice list let me into the team gym. It didn't let me into the rest of campus after.",
                "It gave me a jersey for the session. I did the session. Then rec, on my own.",
                "No. Team roster isn't a key. I practiced and I left for a run.",
                "No. I'm not gonna treat a practice sheet like an all-campus pass. I went to rec.",
            ],
            [
                "Because I play. The list is practice. After it, I was at rec, which is what I said.",
                "My name's there because I don't skip. I made practice, then I played more.",
                "It's a practice roster. I can be on it and still have been on the rec court after.",
                "The list is practice. I said I went to practice and then rec. One night, two gyms.",
            ],
        ],
        "sign_in": [
            [
                "I signed the practice sheet, I did the workout, and I left for rec when the coach blew it dead.",
                "The signature is the start of practice. Rec is where I went after. Different building.",
                "I left when practice ended. Signing in didn't chain me to that gym.",
                "End of practice. Then rec. I'm not gonna add a lap I didn't run.",
            ],
            [
                "Until the coach released us. After that I was at rec, not in the practice building.",
                "Long enough to get smoked and released. Then I went to play for fun, which is rec.",
                "Through practice. That's how late in that gym. Then the rec court.",
                "As late as practice. Then I left. Don't keep me in the team gym.",
            ],
            [
                "No. Signing practice got me into the team session. It didn't hand me a key.",
                "A sign-in is how the coach counts who showed. It isn't a key.",
                "No key. I signed a practice sheet. My name, then I sweated, then I left.",
                "No. I got counted at practice. I didn't get a key. Rec has its own desk.",
            ],
        ],
    },
}
