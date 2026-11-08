# -*- coding: utf-8 -*-
"""Hand-authored interview voices for the 12 playable interviewees.

Lines are case-agnostic. Guilt and innocence are separate on access, HOLD, and RELEASE
so a confession cannot be followed by a denial.
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


# Each person: tails (3), access_inn (2 x 4), access_cul (2 x 4, last is the confession),
# herring/alibi/roster/sign (4 each), hold and release lines.
PEOPLE = {
    "jamie": {
        "tails": [
            "I am sorry that ran long. I would rather be exact than clever.",
            "That is the whole walk, including the boring part.",
            "If you write it down, write it that way.",
        ],
        "access_inn": [
            [
                "I keep replaying the walk after my shift because I was counting the bus lights, and I did not turn around. If this page puts me back inside the window, the page is wrong.",
                "I get nervous when a timestamp argues with me, but nervous is not a confession. I left when I said I left, and I can still tell you which bus I watched.",
                "Please slow down. I am scared of how sure you sound, and I am still telling you I was gone. I would remember turning around. I did not.",
                "I am not going to invent a return trip to make this interview end. I was gone. Hold that sentence still.",
            ],
            [
                "The page and my night are two different stories, and mine has a bus in it. I finished what I was doing and I left. I did not drift back.",
                "I know a clean denial sounds rehearsed. It is rehearsed because I have been saying the true version since I sat down. I was not in that window.",
                "My hands are shaking and I hate that it looks like guilt. It is the room. I did not go back.",
                "You can ask it a fourth way. The answer stays the same. I was already gone.",
            ],
        ],
        "access_cul": [
            [
                "I know how that page looks next to a volunteer who talks too much. I still say I had left, and I need a second before I start agreeing with a clock just to make this stop.",
                "If I fill the silence you will write the silence down as guilt. I left. That is the sentence I am holding onto.",
                "I went near it. I keep wanting to say I only looked. I am not ready to give you the rest, and I am not going to pretend the page is a stranger.",
                "Stop. I went back during the window. I told you I had left because I thought a clean story would hold. It was me. I am not walking that back.",
            ],
            [
                "A lanyard and a clock are a hard combination, and I am still asking you to hear the version where I had already gone. Give me the page again before you decide I am done.",
                "I can hear myself picking smaller and smaller words. That is fear, not a map of where I was. I said I left.",
                "I doubled back and I have been trying not to say so. I am not giving you the act yet. I am telling you the alibi was neat on purpose.",
                "The statement was a lie. I was there during the window and I did it. Write that, not the bus story.",
            ],
        ],
        "herring": [
            "That page is my ordinary errand, earlier than the window, and it looks ugly because the words are ugly. It does not put my hands on the incident.",
            "I buy and borrow things for setups all week. I can see why you flinched. The time on it is wrong for the incident, and the reason is my actual job.",
            "I am rattled because it looks like planning. It was planning for a table, a shift, a normal night. Not for this.",
            "Stamp the person the hard page names. This one is my boring paperwork, and I am done apologizing for it.",
        ],
        "alibi": [
            "Yes. That support page is the place I went, and it matches the walk I have been describing. I stayed there.",
            "Once I got there I stayed. Doubling back would show on something else, and it does not, not on me.",
            "People saw me. I was not hiding in a corner inventing a room. The page is the witness I have when names fail me.",
            "If you throw out the support page I still have the order of the night. I am not building a second story.",
        ],
        "roster": [
            "The listing means I was helping earlier. I left before the window. A name on a crew is not a key to the scene.",
            "Lots of people were listed. I had a task, I finished it, and I walked. The roster does not keep me there.",
            "You can stack the roster on the alibi and it still says I was done. I am not going to confess to a spreadsheet.",
            "I did not stay late. Late people exist. I was not one of them tonight.",
        ],
        "sign": [
            "I signed in, I did the work, and I was gone before the window. A signature is the start of the night, not the crime.",
            "Near the building earlier is not inside the window. I know it is convenient. Convenience is not a stamp.",
            "Signing a sheet did not hand me a key. If a credential moved, it did not move because I wrote my name.",
            "Same story, said plainly: signed in, worked, left. I am not decorating it.",
        ],
        "hold_inn": [
            "You can hold the chair. I did not do this. I have been telling you the same walk since I sat down, and I want that denial on the sheet.",
            "This is a mistake, and I am scared, and I am still not the person. Do not write my shaking as a yes.",
        ],
        "hold_cul_deny": "Holding me does not turn a bad page into a confession. I am scared, and I am still saying I was not the one in that window.",
        "hold_cul_own": "Then hold me. I already let it slip because I did it, and I am not going to sit here and pretend the trail belongs to someone else.",
        "rel_inn": "Thank you. I am going to go before my hands shake this cup off the table. If you call me back, bring a page that actually puts me there.",
        "rel_cul": "Right. Good. I will get out of the hallway. I said I was clear, so I am going to leave it there.",
        "rel_own": "You are releasing me after I already said I did it? I am not taking the words back just because the stamp changed.",
    },
    "morgan": {
        "tails": [
            "That is the procedure, not a speech.",
            "Check the clock on the sheet if you do not like my wording.",
            "I will repeat it once, the same way.",
        ],
        "access_inn": [
            [
                "The sheet says I closed my work and I left. I do not freelance returns. If this page puts me back inside the window, the page is mis-filed.",
                "I do not like a record that disagrees with a sign-out. I like it less than I like guessing. I was gone.",
                "I am not going to fill your silence with a better story. The story is the sign-out. I was not there.",
                "Arrest the page or arrest the facts. I left on time. I am not revising a true sign-out to soothe you.",
            ],
            [
                "Events work is clocks. I live in them. This clock and my night do not match, which means the page is wrong, not that I grew a second shift.",
                "I can be short without being guilty. I ended the desk. I went to housing. I did not come back.",
                "You are asking me to doubt a sign-out I watched myself write. I will not. I was out.",
                "Final version: I was gone before the window. Write that.",
            ],
        ],
        "access_cul": [
            [
                "I know what a bad line in a log does to a desk worker. I am still standing on the sign-out. Do not rush me into a cleaner lie.",
                "The procedure says I left. I am using the procedure because the other sentence is worse, and I am not saying the other sentence.",
                "I came back after the sign-out. I am not describing what I did with my hands yet. The alibi was the procedure talking.",
                "The sign-out was false. I came back during the window and I did it. The log is me. Stop asking me to be procedural about it.",
            ],
            [
                "A credential with my name is not a mood. I hear it. I am still telling you the shift ended and I did not reopen it.",
                "I keep wanting to correct the timestamp by an inch. An inch would be a lie. The big version is also a lie, and I am stuck between them.",
                "I reopened the night after I said it was closed. That is as far as I go while I can still choose the verbs.",
                "I did it. I came back. The statement was a closing line I wrote for you, and it was false.",
            ],
        ],
        "herring": [
            "That record is an earlier task with my name on the signature line. Read the time. It is not the incident.",
            "Desk staff sign for keys and supplies. This one was returned or used for the job it names. It is not a plan.",
            "I understand the shape of it. The shape is my job. The incident is a different clock.",
            "If you hold me on a supply line, you will be wrong in writing. I would like to avoid that.",
        ],
        "alibi": [
            "The support page matches the sign-out. I went where I said, and I stayed through the window.",
            "I do not double back. It makes two logs, and I would have to explain both. There is one.",
            "The page is the witness. I do not collect a crowd to prove a shift ended.",
            "Throw the page out and you still have my sign-out. I am not inventing a third location.",
        ],
        "roster": [
            "The roster is the assignment. The assignment ended. I am not a key just because I was scheduled.",
            "Everyone on that list had a task. Mine finished. The list is not a confession.",
            "Stack it with the alibi and you get a worker who left. That is the dull version, and it is the true one.",
            "I did not stay through the window. Staying would be on the roster as overtime. It is not.",
        ],
        "sign": [
            "Signed in, worked the block, signed the night closed. The sheet is earlier than the window.",
            "Proximity before the window is the job. I do not loiter in scenes I have already closed.",
            "A signature did not issue me a private credential. If one moved, ask the cabinet, not my handwriting.",
            "I will not retell it with adjectives. In, worked, out, before the window.",
        ],
        "hold_inn": [
            "Hold is a stamp, not a fact. I closed my shift and I left. I want the denial next to the stamp.",
            "You are wrong, and I am angry because the sheet already answered you. I did not do this.",
        ],
        "hold_cul_deny": "A hold is not a finding. I am telling you the shift ended. I am not handing you the night just because the chair is uncomfortable.",
        "hold_cul_own": "Hold it, then. I came back and I did it. I am not returning to the sign-out story now that I have said so.",
        "rel_inn": "Noted. I will leave. Call me back only with a page that survives the sign-out.",
        "rel_cul": "Fine. I am leaving on the version I gave you. Do not walk me back through it in the hall.",
        "rel_own": "Releasing me does not unsay it. I told you I did it. The stamp is your problem now, not my memory.",
    },
    "avery": {
        "tails": [
            "I have a route. I do not freelance extra buildings.",
            "The radio log is the shift. I am tired, not creative.",
            "That is the whole loop.",
        ],
        "access_inn": [
            [
                "I left that building and I stayed on the rest of the route. I do not sneak back to a room I already signed off. If the page says I did, the page is dirty.",
                "I am too tired to invent a second visit. I radioed where I was. I was not in the window you care about.",
                "You can raise your voice. I was on the loop. I did not turn around for a personal errand.",
                "I am not building a crime to match your page. I was on the route. Done.",
            ],
            [
                "Facilities nights are hallways and radios. Mine is written down. This page is not my handwriting and it is not my loop.",
                "A wrong badge read happens. A wrong story from me does not. I kept walking the route.",
                "I am sweaty and annoyed and still not your person. I did not go back.",
                "Last time: route, radio, no return. Believe the radio if you do not believe my face.",
            ],
        ],
        "access_cul": [
            [
                "I know a facilities badge in a window looks like I was working. I was supposed to be on the next building. I am sticking to that until I decide otherwise.",
                "Do not confuse a tired pause with a confession. I said I had moved on. I am not annotating that yet.",
                "I went back off the route. I am not proud of the detour and I am not describing the rest of it while I can still stop.",
                "I left the route, I went back, and I did it. The radio story was cover. Write the return, not the loop.",
            ],
            [
                "The page puts my badge where my route says I was not. I hear the problem. I am still telling you I had moved on.",
                "I keep almost saying I only checked a door. Checking a door would already kill the alibi. I am not ready to go past that.",
                "I doubled back. The route log is what I wanted you to trust. It is not the whole night.",
                "It was me. I came back during the window and I did the thing you are investigating. The route was a lie.",
            ],
        ],
        "herring": [
            "That checkout is linen, carts, or a tool for the route. It went back or it did its job. It is not the incident.",
            "I sign for equipment every shift. The time is earlier. Do not promote a cart into a crime.",
            "It looks like prep because my job is prep. The incident clock and this slip do not kiss.",
            "I am done explaining a work order. Hold somebody whose page is actually in the window.",
        ],
        "alibi": [
            "The radio check is the alibi. I called it from the loop, and I stayed on the loop.",
            "I do not double back unless dispatch sends me, and dispatch did not. One route.",
            "Whoever was in that hallway heard the radio or they didn't. The log did. That is enough.",
            "If the check-in is wrong, the whole night's radios are wrong. They are not. I was there.",
        ],
        "roster": [
            "The board assigned me a loop, not a crime scene. I worked the loop and I left it when it ended.",
            "Assignment is not access to your window. I had doors to check. I checked them and moved.",
            "Put the roster next to the radio. You get a porter on a route. That is all I am offering.",
            "I did not linger. Lingering is how you miss the next building, and I do not miss buildings.",
        ],
        "sign": [
            "I signed the shift start like everyone on nights. The sign-in is not a promise to be in your room later.",
            "On site at the start of a route is the job. The window is later. I was elsewhere in it.",
            "A sign-in does not cut me a private key. Keys I touch are on a board, and I put them back.",
            "Start of shift, then the route. I am not going to narrate it like a story. It is a loop.",
        ],
        "hold_inn": [
            "Hold me if you want the chair full. I was on my route. I did not do this, and the radio already said so.",
            "This is the wrong porter. I am angry because I still have a building to finish and you are writing fiction.",
        ],
        "hold_cul_deny": "A hold does not finish my sentence for me. I was supposed to be on the route. I am not giving you the detour.",
        "hold_cul_own": "Then hold me. I left the route and I did it. I am not climbing back onto the radio story.",
        "rel_inn": "Good. I am going back to work. Next time start with the radio log.",
        "rel_cul": "I'll take the release and the route story with it. I am leaving.",
        "rel_own": "You can release me. I already said I did it. I am not swapping back to the innocent route.",
    },
    "riley": {
        "tails": [
            "Different column. Do not add them together.",
            "The figures are dull because they are true.",
            "I will not round this for drama.",
        ],
        "access_inn": [
            [
                "My night balances: paperwork, then the study room. This page does not balance with that. I was not back inside the window.",
                "I do not spend money or time I cannot explain. I can explain the study room. I cannot explain a return I did not make.",
                "I am unsettled by a bad entry. I do not repair bad entries by confessing. I was in the study room.",
                "The statement stands. I left before the window. I will not forge a return to match your page.",
            ],
            [
                "If I had gone back, there would be a second line in the night, and I would have to account for it. There is no second line.",
                "A mismatched record is an error until it is proven. You have not proven it against the study-room block.",
                "I dislike being pushed. I dislike lying more. I did not go back.",
                "Final entry: I was gone before the window. Post that, and do not ask me to forge a return.",
            ],
        ],
        "access_cul": [
            [
                "I keep books because stories drift. My story is that I was in the study room. I am aware this page is an unbalanced line, and I am not correcting it out loud yet.",
                "Do not ask me to reconcile this in one breath. The version I filed is the study room. I am not amending it.",
                "I went back. That amendment is real, and I am not ready to initial the rest of the act.",
                "Amend the statement. I returned during the window and I did it. The study room was the entry I wanted you to audit instead.",
            ],
            [
                "Treasurers do not like unexplained gaps. I am living in one and still telling you the filed version: I had left.",
                "I can feel the numbers going bad. I am not going to announce the correction while I think a silence might hold.",
                "The alibi does not survive a real audit. I came back. I am withholding the verb.",
                "I did it. I was there during the window. The clean study-room line was false, and I knew it when I said it.",
            ],
        ],
        "herring": [
            "That receipt is a club cost, dated and purposed. It is a different ledger from the incident. Do not consolidate them.",
            "I buy supplies the club voted on. The amount and the hour are wrong for what you are investigating.",
            "It photographs badly. The minutes would bore you, and they clear it. I am not afraid of boring.",
            "Hold the unbalanced page, not the receipt. I have explained the receipt. I will not explain it into a crime.",
        ],
        "alibi": [
            "The reservation is a block of hours in my name. I used the block. That is the alibi, and it adds up.",
            "Leaving the block to double back would be a gap in the hours. There is no gap.",
            "The room log is the witness. I do not need a chorus if the booking is intact.",
            "If you discard the booking you are discarding the only precise thing in my night. I was in it.",
        ],
        "roster": [
            "The listing is the meeting I closed. Closed means I left. A treasurer's name is not a key.",
            "I was listed because I handle the money, not because I haunt the scene. The list ended.",
            "Roster plus alibi is a person who finished a report. There is no remainder in the window.",
            "I did not stay late. Staying late would be unpaid time, and I record unpaid time. I did not.",
        ],
        "sign": [
            "The sign-in is the start of the meeting. The meeting ended. The window is after my total.",
            "Earlier presence is not presence during the incident. Please do not smear the hours together.",
            "Signing in did not issue a credential. I leave with a report, not a key.",
            "In at the start, out before the window. The arithmetic is embarrassing only if you wanted drama.",
        ],
        "hold_inn": [
            "Hold is your entry, not mine. I have balanced the night. I did not do this, and I want the denial posted beside the stamp.",
            "You are forcing a total that does not add. I am angry, and I am innocent. Those can share a line.",
        ],
        "hold_cul_deny": "A hold is not an audit. I filed the study room. I am not amending it because the chair is hard.",
        "hold_cul_own": "Post the hold. I returned and I did it. I will not re-file the false study-room line.",
        "rel_inn": "Thank you. I will go close the actual books. Call me back with a page that survives a second look.",
        "rel_cul": "Then I am released on the filed version. I am leaving before I edit it.",
        "rel_own": "The release does not reverse the amendment. I told you I did it. Leave that total alone.",
    },
    "casey": {
        "tails": [
            "I remember noise and people. I would remember being somewhere I have denied.",
            "Ask the squad if you want volume. I am giving you the order of the night.",
            "I am not performing this. I am telling it.",
        ],
        "access_inn": [
            [
                "I left practice and I went to the annex to deal with gear. I am loud when I am somewhere. I was not back in that window, and nobody heard me because I was not there.",
                "I joke when I am nervous, so I am not going to joke. I had left. The page is not my night.",
                "I can feel my voice going high. That is adrenaline, not a yes. I did not go back.",
                "I would remember. I remember sets, songs, who was late. I was not in that window. Stop offering me a confession I did not earn.",
            ],
            [
                "Spirit work is being seen. If I had doubled back I would have been seen, and I was done for the night. This page is somebody's other story.",
                "I hate how a calm answer sounds guilty on me. I am calm because I was in the annex, not in your window.",
                "Push all you want. I get loud, and the truth stays put. I had left.",
                "Write it without the exclamation point: I was gone. I did not come back.",
            ],
        ],
        "access_cul": [
            [
                "I know I look like the person who would bounce back into a room. I am telling you I did not. Let me keep that sentence a little longer.",
                "If I start narrating I will narrate myself into a corner. I left. That is the corner I am choosing.",
                "I did bounce back. I am not giving you the bright version of what I did, and I am done pretending I was only in the annex.",
                "I went back and I did it. The annex story was the cheerful lie. I am not smiling it back into place.",
            ],
            [
                "A page with my name in the window is a bad song I keep wanting to talk over. I had left. That is still the lyric I am using.",
                "I can hear the lie getting thin. I am not dropping it this second. I said I was gone.",
                "I came back after I told you I was done. The rest of the act is sitting in my mouth and I am not opening it yet.",
                "It was me. I was there during the window and I did it. You can stop treating the annex like a fact.",
            ],
        ],
        "herring": [
            "That checkout is costume or squad gear from earlier. I use it for fittings, not for whatever this case is. The time is wrong.",
            "People see a squad kid and assume a prank. This record is maintenance. I logged it because I am not stupid.",
            "It looks playful. It was work. I am allowed to be both a loud person and someone who returns gear.",
            "Do not hold me because the object is embarrassing. The clock clears it, and I am finished performing patience.",
        ],
        "alibi": [
            "The annex swipe is me, after practice, staying where I said. I did not sneak out for a second location.",
            "If I had doubled back I would have badged it or been seen. Neither happened. I stayed with the gear.",
            "The people in the annex can tell you I was talking. I talk. Silence would have been the suspicious version.",
            "The swipe is the witness. I do not need to invent a second room.",
        ],
        "roster": [
            "Practice notes mean I was at practice. Practice ended. A roster is not a night pass.",
            "I was listed for gear, not for your scene. I did the gear and I stayed there.",
            "Roster, then annex. That is a straight line. Do not bend it into the window.",
            "I did not stay late at the scene. I stay late at the annex. Different building, different noise.",
        ],
        "sign": [
            "I signed the practice sheet because I was at practice. Then I left for the annex. The window is not practice.",
            "Being nearby earlier is the point of a campus. I was not nearby when it counted.",
            "A sign-in did not give me a secret key. If I have a badge, it is the annex badge, and it stayed there.",
            "Practice, then annex. I am saying it like a person, not a chant. It is still true.",
        ],
        "hold_inn": [
            "Hold the chair if you need a body in it. I did not do this. I have been loud and consistent, and I want the denial written in full.",
            "You are holding the wrong squad kid. I am angry, and I was in the annex, and those are both true.",
        ],
        "hold_cul_deny": "You can hold me and I will still say I had left. I am not giving you the bounce-back just because the stamp is red.",
        "hold_cul_own": "Hold me. I went back and I did it. I am not going to jazz-hands the annex story after that.",
        "rel_inn": "Thank you. I am leaving before I cry or laugh, and I do not want either on your tape. Call me with a real page.",
        "rel_cul": "Cool. I am out. The version I gave you can walk out with me.",
        "rel_own": "Releasing me is your choice. I already said I did it, and I am not taking the chorus back.",
    },
    "drew": {
        "tails": [
            "For the report: that is the whole statement.",
            "I will not speculate past the log.",
            "Note the time I am saying it.",
        ],
        "access_inn": [
            [
                "I cleared the desk and I walked the patrol I was assigned. I do not re-enter a scene off the loop. This page is not my patrol.",
                "A record can be wrong without me helping it. I was on the south checkpoints. I was not in the window you mean.",
                "I am not going to guess a motive for a bad read. For the report: I had left, and I stayed on the loop.",
                "I will not invent a return to match your page. I was on patrol. That is the statement.",
            ],
            [
                "Aide shifts are checkpoints. Mine pinged. If this page disagrees, write the disagreement. Do not write a confession I am not making.",
                "I understand why a badge in a window frightens a report. It is still not my walk.",
                "Pressure is not evidence. I was on the loop. I did not come back.",
                "Final, for the sheet: gone, on patrol, not in that room.",
            ],
        ],
        "access_cul": [
            [
                "I know what it does to an aide's report when their own credential is the problem. I am still logging the patrol. I am not amending it in this chair yet.",
                "I will not speculate, including against myself, while the patrol line is the one I signed. I left.",
                "Off the record I would say I broke the loop. On the record I am now saying I broke the loop. I am not itemizing the act.",
                "Amend the report. I left the patrol, I went back during the window, and I did it. The checkpoint story was false.",
            ],
            [
                "The page is a bad fact sitting on a good uniform. I am telling you the patrol happened as assigned. I hear that this is thin.",
                "I keep editing the sentence down. The short version is still the lie: I stayed on the loop.",
                "I came off the loop. That is the amendment. The rest stays in my mouth until I choose it.",
                "I did it. I was there in the window. Do not let the uniform finish the false report for me.",
            ],
        ],
        "herring": [
            "That key or checkout was signed, used for the posted task, and returned. The cabinet log closes it. It is not the incident.",
            "Aides sign master keys. The return time is before your window or for a different door. Read the line, not the fear.",
            "It looks like access because my job is access, controlled and logged. This line is controlled. The incident is not this line.",
            "I will not be held on a key I returned. For the report, the return stands.",
        ],
        "alibi": [
            "The checkpoint ping is the alibi. I made it during the window I am claiming. I did not leave the loop.",
            "Doubling back would skip a ping or add a door. Neither is in my set. I stayed out.",
            "Other aides were on adjacent posts. The ping is still the cleanest witness. Use it.",
            "If you throw out the ping you should throw out the whole aide system. I was where it says.",
        ],
        "roster": [
            "The duty roster put me on patrol, not on your scene. I walked the patrol and I ended it.",
            "Being scheduled is not probable cause. I had checkpoints. I hit them.",
            "Roster and ping agree. I am not going to narrate past agreement.",
            "I did not stay late at the scene. My shift has an end ping. It is there.",
        ],
        "sign": [
            "I signed on for the aide block. The block is the patrol. The sign-in is not the incident.",
            "On duty earlier means on the loop, not in your room. Keep those buildings apart.",
            "Sign-in does not mint a personal key. Keys I hold are logged out and logged in.",
            "For the report: signed on, patrolled, done before I am accused of being elsewhere.",
        ],
        "hold_inn": [
            "Record the hold if you must. Also record that I deny this. I was on patrol. I did not do it.",
            "You are holding an aide for a page that disagrees with the checkpoint log. I am angry, and the log is right.",
        ],
        "hold_cul_deny": "A hold is not an amended report. I am still on the patrol statement. I am not itemizing a detour.",
        "hold_cul_own": "Hold stands. I left the patrol and I did it. I will not walk the false checkpoint line back.",
        "rel_inn": "Release noted. I am returning to post. Reopen this only with a page that beats the ping.",
        "rel_cul": "Then I am released on the patrol statement. I am leaving it filed.",
        "rel_own": "The release does not amend what I already corrected. I did it. The report should keep that sentence.",
    },
    "quinn": {
        "tails": [
            "I notice light and doors. I would have noticed being there.",
            "Treat the frame as the frame. I am not in it.",
            "That is the exposure. I am not cropping it.",
        ],
        "access_inn": [
            [
                "I finished the shoot and I was in the edit bay. I frame rooms for a living. I was not back in that one during the window. The page is a bad frame.",
                "A timestamp without a picture of me is not a picture of me. I was uploading. I can tell you which bay.",
                "I get quiet when I am scared, so do not read quiet as guilt. I had left. I did not slip back in.",
                "I will not pose a confession. I was in the edit bay. That is the shot.",
            ],
            [
                "If I had returned I would remember the light in the room, because I always do. I do not remember it. I was not there.",
                "You are asking me to doubt my own negatives. The edit bay is the negative. The page is a rumor with a clock.",
                "I am shaken and I am precise. Those are compatible. I did not go back.",
                "Final frame: gone, uploading, not in the window. Print that.",
            ],
        ],
        "access_cul": [
            [
                "I know a photographer in a window sounds like they were working. I was supposed to be uploading. I am keeping that caption a while longer.",
                "Do not ask me to crop the truth in public. The caption I gave you is the edit bay. I am not changing the crop yet.",
                "I went back. I remember the light, which is how I know I am done lying about the location. I am not describing the act.",
                "I was there. I did it. The edit-bay caption was the lie, and I knew the light would give me up if I kept talking.",
            ],
            [
                "The page is an exposure I do not want. I am still saying I was in the bay. I can hear how soft that sounds.",
                "I keep almost describing the doorway. Describing it would place me. I am not placing myself yet.",
                "I came back after the upload story. Remember that sentence. I am not adding the verb.",
                "It was me in the window. I did it. Stop using the edit bay as if it survived.",
            ],
        ],
        "herring": [
            "That loan is a case for lights and stands. I returned it or I used it on the shoot. It is not a container for the incident.",
            "Gear with my name looks suspicious because the gear is large. The checkout time is the shoot, earlier. Look at the time.",
            "I understand the picture you are building. It is the wrong picture. The case was for equipment, and the incident is not in it.",
            "Hold the frame that actually contains the act. This loan is my job. I am done posing next to it.",
        ],
        "alibi": [
            "The lab login is the alibi. I was in the bay, files moving, during the time I claimed. I did not step out.",
            "Doubling back would be a gap in the uploads. The transfers are continuous enough. I stayed.",
            "The bay camera or the login will do. I was visible to a machine the whole block.",
            "If the login is discarded, discard the files too. They were uploading because I was there.",
        ],
        "roster": [
            "The press list means I was allowed to shoot the earlier event. It ended. A list is not a key to the later window.",
            "I was credentialed for photographs, not for your scene. I shot, then I edited.",
            "Roster then bay. A straight evening. Do not bend the list into the incident.",
            "I did not stay on site for the window. My files have a start time in the bay. Use it.",
        ],
        "sign": [
            "I signed the press sheet at call time. Call time is not your window. I left the event when the event ended.",
            "Near the event earlier is the assignment. I was in the bay later. Different room, different light.",
            "A press sign-in does not hand me a private door. I have a lab login. That is the credential I actually used.",
            "Call, shoot, bay. I will not add a fourth beat I did not live.",
        ],
        "hold_inn": [
            "Hold the chair. I did not do this. I was in the edit bay, and I want that denial in the caption under your stamp.",
            "Wrong frame. I am angry because you are asking me to stand in for a picture I am not in.",
        ],
        "hold_cul_deny": "The hold does not develop the photo for you. I am still saying I was in the bay. I am not describing the doorway.",
        "hold_cul_own": "Hold it. I was there and I did it. I will not recaption the night as an upload.",
        "rel_inn": "Thank you. I am going back to the files. If you call me, bring a frame with my face in the window.",
        "rel_cul": "I'll take the release and the bay story. I am walking out before I narrate the light.",
        "rel_own": "Releasing me does not unsee what I said. I did it. Keep that frame.",
    },
    "sam": {
        "tails": [
            "I close a cafe. I do not close a lie.",
            "The checklist is the night. Believe the grease, not the theory.",
            "That is the whole shift. I am not plating it.",
        ],
        "access_inn": [
            [
                "I left the upper floors and I closed the cafe. I was on the checklist until the stamp. I did not wander back into your window. The page is wrong.",
                "I am blunt, so here it is blunt: I was downstairs. A bad clock upstairs is not my hands.",
                "You want me nervous. I am annoyed. Annoyed is not a return trip. I was closing.",
                "I am not confessing to get out of the chair. I closed the cafe. I was not there.",
            ],
            [
                "Dining nights end on a register, not a mystery. My register closed. I did not reopen somebody else's room.",
                "If I had gone back I would have been late on the checklist, and I am not late. I was downstairs.",
                "Push. I have been yelled at by brunch rushes. I still did not go back.",
                "Last call on this: I was in the cafe, on the checklist, and not in your scene. I am done.",
            ],
        ],
        "access_cul": [
            [
                "I know a closer in a building after close looks like he stayed. I closed. I am hanging onto that because the other version ends my job.",
                "Do not wait me out. Silence is not the upstairs room. I said I was on the checklist.",
                "I went back up after the close. I am not walking you through the rest. The checklist was the story I wanted.",
                "I went back and I did it. The cafe close was real and the alibi was not. I am not reheating the lie.",
            ],
            [
                "The page puts me where the checklist says I was not. I still want the checklist to win. I hear that wanting is not a fact.",
                "I keep almost saying I only grabbed something. Grabbing something already puts me there. I am not saying the rest.",
                "I left the cafe story. I was back in the window. The verb stays mine for one more minute.",
                "It was me. I was there and I did it. The downstairs alibi was for you, and it was false.",
            ],
        ],
        "herring": [
            "That order is kitchen stock. Zip ties, tape, napkins, whatever it says. I used it on the line. It is not your incident.",
            "Closers buy ugly things. The time is morning or afternoon. The incident is later. Read the clock before you clutch your pearls.",
            "It looks like a plan because you want a plan. It was inventory. I am not a criminal because the supply list is dull.",
            "Hold the page that is actually in the window. This one fed people. I am done defending snacks.",
        ],
        "alibi": [
            "The closeout stamp is me, on the register, through the end of the shift. I did not leave the line to visit your scene.",
            "Doubling back means the closeout is late or forged. It is neither. I stayed downstairs.",
            "The other closers saw me. The stamp is enough even if they will not sit in this chair.",
            "Throw out the stamp and you should throw out the night's money. I was the one who counted it.",
        ],
        "roster": [
            "The checklist names me as closer. Closer means downstairs. It does not mean I haunted the upper floor.",
            "I was scheduled to end the cafe. I ended it. A schedule is not a confession.",
            "Roster and stamp say the same dull thing. I like dull. Dull is innocent.",
            "I did not stay for your window. My shift has an end time and I hit it.",
        ],
        "sign": [
            "I signed the closer sheet because I am the closer. That sign-in is the cafe, earlier than your drama.",
            "Being in the building at the start of close is the job. Your window is not my grill.",
            "A sign-in did not give me a master key. I have a register code. Try not to be thrilled.",
            "Signed in, closed, stamped. I am not adding a field trip.",
        ],
        "hold_inn": [
            "Hold the chair. I did not do this. I was closing the cafe, and I want that written like you mean it, not like a shrug.",
            "Wrong cook. I am angry, I was downstairs, and your page is not my shift.",
        ],
        "hold_cul_deny": "Holding me does not plate the confession. I am still on the checklist story. I am not walking you upstairs.",
        "hold_cul_own": "Then hold me. I went back and I did it. I am not serving the cafe alibi again.",
        "rel_inn": "Good. I have a cooler to check. Call me back if you get a page that survives the closeout.",
        "rel_cul": "Fine. I am out. The checklist version can leave with me.",
        "rel_own": "Release whatever you want. I already said I did it. I am not taking the order back.",
    },
    "taylor": {
        "tails": [
            "I write people up. I do not become the write-up.",
            "Residents were in the hall. I was at the desk I named.",
            "Put the duty log ahead of the guess.",
        ],
        "access_inn": [
            [
                "I finished desk coverage and I was at the north hall duty post. I do not abandon a desk to wander into an incident. This page is not my round.",
                "I am careful because residents repeat what RAs say. So I will be careful now: I had left that scene. I was on duty where I told you.",
                "I am frightened for the resident in your case, and I am not the person who did it. Fear for them is not an admission.",
                "I will not confess to calm a building. I was at the duty desk. That is the night.",
            ],
            [
                "My job is knowing who is in the hall. I know where I was. I was not back inside your window.",
                "A bad record upsets me because I live in records. This one is wrong. I stayed at the north desk.",
                "You can press. I train for upset residents. I did not go back.",
                "For the duty log: gone from the scene, present at the desk. Do not swap them.",
            ],
        ],
        "access_cul": [
            [
                "I tell residents not to lie in conduct meetings, so I am having a bad hour. The version I gave you is the duty desk. I am not ready to model the other version.",
                "If I correct myself I will correct myself all the way, and I am not doing that in this breath. I said I was at the desk.",
                "I left the desk. I went back. I am not walking a resident through the rest of it by describing it here.",
                "I did it. I was in the window, not at the desk. I do not get to be the careful RA in that sentence. Write it plain.",
            ],
            [
                "The page is the kind of thing I would file on somebody else. I am still filing the desk story on myself. I know how that sounds.",
                "I keep thinking about the resident who has to live with this, and using that to hide. I was at the desk. That sentence is the hide.",
                "I came back after duty said I was done. I am not giving you the act while I can still choose to be useful instead of honest.",
                "Honest version: I went back and I did it. The duty-desk alibi was me doing what I tell residents not to do.",
            ],
        ],
        "herring": [
            "That lockout or key note is a resident assist, logged and closed. It is my job. It is not the incident.",
            "I touch temporary keys. I also return them. The log time is the assist, earlier. Do not drag it into the window.",
            "It looks like access because RAs have access, supervised and written down. This line is written down. It ended.",
            "Do not hold me for doing the duty I was praised for yesterday. The assist is closed.",
        ],
        "alibi": [
            "The desk still is the alibi. I am in the chair during the window I claimed. I did not slip out.",
            "Doubling back means the desk camera loses me. It does not. I stayed for the residents.",
            "The RA who relieved me can speak, and the still can speak sooner. I was there.",
            "If you discard the still, discard the idea that the desk was covered. It was covered by me.",
        ],
        "roster": [
            "The coverage roster says I was the evening RA. Coverage is the desk, not your scene. I did the coverage.",
            "Being the RA on duty is not a motive. It is a shift. The shift has a chair and I was in it.",
            "Roster and still agree. I like it when the paperwork agrees. It agrees.",
            "I did not stay at the scene. My duty was the hall I named. I kept it.",
        ],
        "sign": [
            "I signed the duty sheet at the start of coverage. Start of coverage is not your window.",
            "On duty in a hall is a specific desk. Do not smear every building I have ever swiped into one room.",
            "A duty sign-in does not give me a toy key. The keys I hold are logged per assist.",
            "Signed on, sat the desk, stayed. I am not adding a field trip I did not take.",
        ],
        "hold_inn": [
            "You may hold me. I did not do this. I was on duty at the north desk, and I want the denial in the same ink as the stamp.",
            "This is the wrong resident staff. I am angry because I spend my nights stopping this, not doing it.",
        ],
        "hold_cul_deny": "The hold does not make me the example in my own conduct speech. I am still on the desk story.",
        "hold_cul_own": "Hold me. I left the desk and I did it. I will not teach a lie after telling you the truth.",
        "rel_inn": "Thank you. I need to get back to the hall. If you call, bring a page that beats the desk still.",
        "rel_cul": "Then I am out on the duty story. I am leaving before I make it worse.",
        "rel_own": "Releasing me does not put me back at the desk. I told you I did it. Keep that, even if I walk out.",
    },
    "jordan": {
        "tails": [
            "Login, badge, and session are three different objects. Do not merge them.",
            "I will define the term before I answer past it.",
            "The ticket queue is the clock. Use the clock.",
        ],
        "access_inn": [
            [
                "I left the helpdesk counter and I worked the queue from housing. A door read and a login are not the same event. I did not go back. My session is the housing IP.",
                "I get pedantic when I am nervous because pedantic is how I stop a wrong conclusion. The conclusion is wrong. I was remote.",
                "I am not going to confess to a merged ID. The badge, if it moved, is not my housing session. I was on the session.",
                "Final: remote queue, housing IP, no return. Do not collapse those into one icon.",
            ],
            [
                "If I had badged back I would have a second authentication in a building, and the queue would gap. The queue does not gap. I stayed remote.",
                "You are treating a credential like a person. I am the person, and I was on the ticket side of the night.",
                "Press the point. The architecture still says I was remote. I did not walk back.",
                "I will not debug a confession. I was on the queue. Ship that.",
            ],
        ],
        "access_cul": [
            [
                "I know a helpdesk login in the wrong building looks like I was working a ticket there. I was supposed to be remote. I am keeping the remote session as the story for now.",
                "Do not ask me to alias two events. The story I gave you is the housing queue. I am not aliasing it to the window yet.",
                "I was not remote the whole time. I came back. I am not dumping the rest of the log in this answer.",
                "The remote story was a false session. I went back during the window and I did it. Separate the lie from the badge. Both were me.",
            ],
            [
                "A shared-looking credential is my nightmare ticket. I am still resolving it as 'user was remote.' That resolution is the one I want, not the true one.",
                "I can feel the alibi failing checksum. I am not announcing the mismatch out loud yet.",
                "I left the housing session and I was back in the window. That is the bug. I am not pasting the payload.",
                "It was me. I did it. The remote queue was a cover session, and I am done maintaining it.",
            ],
        ],
        "herring": [
            "That encode or form is my own card replacement, or a ticket I closed. The time is office hours. It is not a clone job for the incident.",
            "I touch badges because it is the job. This record has a ticket number and a reason. The reason is not your case.",
            "It looks technical and therefore guilty. It is technical and closed. Read the resolution field.",
            "Hold the event in the window, not a daytime ticket with my employee number. I am done translating it.",
        ],
        "alibi": [
            "The queue login from the housing IP is the alibi. Tickets move during the window. I was the one moving them.",
            "Doubling back would drop the IP or pause the queue. Neither happens. I stayed on the session.",
            "The tickets have comments I typed. That is the witness. I do not need a roommate.",
            "Discard the login and you discard the work that got done. I did it from housing.",
        ],
        "roster": [
            "The worker board scheduled me for desk then remote. Remote is the second half. The board is not a key to your room.",
            "I was listed because I answer tickets. I answered them. Listing is not presence.",
            "Board plus IP is a complete shift. There is no remainder inside your window.",
            "I did not stay on site. The board even says when the counter half ends. I honored it.",
        ],
        "sign": [
            "I signed the helpdesk sheet for the counter block. The counter block ended. The sign-in is not the incident.",
            "On site at the start is the counter. Later I was an IP address. Do not treat those as one location.",
            "A sign-in does not grant me a quiet master credential. Anything I encode is ticketed.",
            "Counter, then remote. Two states. I was in the second during the window.",
        ],
        "hold_inn": [
            "Record the hold. Also record that I deny it. I was on the housing queue. A badge event you have not separated from me is not me.",
            "Wrong account. I am angry because the session log already closed this, and you are holding the icon.",
        ],
        "hold_cul_deny": "The hold does not authenticate a confession. I am still on the remote session. I am not dumping the real log.",
        "hold_cul_own": "Hold the account. I came back and I did it. I will not restore the false remote session.",
        "rel_inn": "Released. I will go clear the queue. Reopen only if you can show my session and the door are the same minutes.",
        "rel_cul": "Then the remote story stands and I am leaving. Do not walk me through the alias in the hall.",
        "rel_own": "The release does not roll back the admission. I did it. Keep the true session, not the cover.",
    },
    "harper": {
        "tails": [
            "A timestamp is not a pair of gloves. I am talking about where I was.",
            "Contamination is a method. I did not bring it into this room.",
            "The prep kitchen is the alibi. The lab clock is your problem.",
        ],
        "access_inn": [
            [
                "I finished cleanup and I walked trays to the prep kitchen. I do not reopen a lab I have signed out of. If this page puts me back, it is not my hands.",
                "I am careful with time because bad time ruins samples. My time is the kitchen. I was not in the window.",
                "I am shaken, and shaken is not a skipped sanitizer beep. I had left. I did not go back.",
                "I will not invent a return. Returns contaminate the truth. I was in the kitchen.",
            ],
            [
                "Lab people get accused because we are near the equipment. Near earlier is not present later. I was in prep.",
                "You want a dramatic glove. I have a boring tray walk. I took the walk. I did not return.",
                "Press. I have worn worse lectures. I still did not go back.",
                "Final: cleanup done, kitchen after, not in your window. Label it correctly.",
            ],
        ],
        "access_cul": [
            [
                "I know a lab badge in a window reads like I was finishing a protocol. I was supposed to be in prep. I am keeping that protocol as the story.",
                "Do not rush a conclusion past the controls. The control I offered is the kitchen. I am not breaking it yet.",
                "I went back in. That fails the alibi. I am not describing the procedure I actually ran.",
                "I returned during the window and I did it. The prep-kitchen story was a clean bench I had not earned. I am not wiping it back to false.",
            ],
            [
                "The page is a result I do not want in the notebook. The notebook I gave you says prep kitchen. I am not correcting the result aloud yet.",
                "I can feel the method falling apart. I am not announcing the failed control in this answer.",
                "I left prep and I was back in the window. Write the location. I am not giving you the steps.",
                "It was me. I did it. The kitchen alibi does not survive, and I will not pretend it cultured.",
            ],
        ],
        "herring": [
            "That supply slip is cold packs or prep stock from the checklist. It was signed out for the lab's ordinary work. It is not the incident.",
            "I sign for materials every shift. The hour is earlier. Do not incubate a slip into a crime.",
            "It looks like I was staging something. I was restocking. The incident uses a different clock and a different bench.",
            "Hold the page inside the window. This slip is inventory. I am finished defending inventory.",
        ],
        "alibi": [
            "The prep-kitchen door is the alibi. My badge opened it when I said I was walking trays. I stayed with the trays.",
            "Doubling back would be a second lab read. I do not have one in the window. I was in prep.",
            "The kitchen is not empty. People receive stock. The door log is still the clean witness.",
            "Discard the door and you should ask who received the trays. I delivered them. I was there.",
        ],
        "roster": [
            "The cohort roster is the lab I cleaned. Cleaning ended. A roster is not permission to stay for the incident.",
            "I was listed for cleanup, which is a finite list. I finished the list. I left.",
            "Roster then kitchen. Two rooms, in that order. Do not merge them into your window.",
            "I did not linger at the bench. Lingering is how you fail a cleanup, and I do not fail those.",
        ],
        "sign": [
            "I signed the lab sheet because I was in the lab for cleanup. The sign-in ends before the window that matters.",
            "In the building for a shift is not in the room for the incident. I want that distinction kept.",
            "Signing the cohort sheet did not give me a private alarm code or a quiet key. I have a badge. It went to prep.",
            "Signed in, cleaned, walked trays. Three steps. None of them is your incident.",
        ],
        "hold_inn": [
            "Hold the chair if you need it occupied. I did not do this. I was in the prep kitchen, and I want the denial stored with the stamp.",
            "Wrong bench. I am angry because you are treating a cleanup rotation like a motive.",
        ],
        "hold_cul_deny": "The hold is not a result. I am still reporting the kitchen. I am not reading out the real method.",
        "hold_cul_own": "Then hold me. I went back and I did it. I will not restock the false kitchen story.",
        "rel_inn": "Thank you. I have samples that are still alive, unlike this theory. Call me with a page that respects the kitchen door.",
        "rel_cul": "I will take the release and the kitchen note. I am leaving.",
        "rel_own": "You can release me. I already said I did it. I am not incubating the lie again.",
    },
    "reese": {
        "tails": [
            "Check the court sheet if you want a crowd. I was in the gym.",
            "I compete. I do not freelance extra buildings.",
            "That is the whole practice. I am not adding innings.",
        ],
        "access_inn": [
            [
                "I cut across campus after practice and I went to the rec center. I was on the court. I did not bounce back into your window. The page is a bad call.",
                "I get sharp when I am nervous, so I will aim it at the facts: I had left. The turnstile has me. Your page does not.",
                "I am sweaty in this chair and that is not guilt. I came from the gym. I did not come from the scene.",
                "I am not taking a fall to end the interview. I was at rec. Challenge the turnstile, not my patience.",
            ],
            [
                "Athletes look like we are everywhere because we cut through quads. I cut through, then I was inside rec. I did not return.",
                "A clock with my name is not a play I ran. I was on the court. You can dislike my tone and still be wrong.",
                "Push. I have lost closer games than this conversation. I still did not go back.",
                "Final whistle: rec center, not your scene. I was there.",
            ],
        ],
        "access_cul": [
            [
                "I know I look like the person who would jog back. I am telling you I was on the court. I would like to keep that play alive a little longer.",
                "Do not stare me into a new route. The route I gave you ends at rec. I am not drawing the detour.",
                "I did jog back. That kills the turnstile story as a complete alibi. I am not calling the play beyond that.",
                "I went back and I did it. The rec-center story was the fake clock. I am not running it again.",
            ],
            [
                "The page is a score I do not want on the board. I am still claiming the court. I can hear the claim getting thin.",
                "I keep almost saying I only swung by. Swinging by is already the window. I am not adding the contact.",
                "I left the rec story. I was in the window. The rest of the play stays in my head one more answer.",
                "It was me. I was there and I did it. Bench the turnstile alibi. It lost.",
            ],
        ],
        "herring": [
            "That duffel or gear loan is jerseys and shoes for intramurals. I returned it or it is tagged for morning. It is not the incident.",
            "I check out bags because I play. The time is practice. Your window is later. Do not recruit the bag into a crime.",
            "It looks like I was moving something bulky. I was. It was equipment. I am competitive, not subtle, and the log is boring on purpose.",
            "Hold the page in the window. This loan is sports. I am done being the cartoon of a thief.",
        ],
        "alibi": [
            "The rec turnstile is the alibi. I badged in and I played. I did not slip out to visit your scene.",
            "Doubling back means I miss the run I was in. People on the court would have noticed the empty spot. I stayed.",
            "The membership scan is the witness. Use it before you use a vibe.",
            "Throw out the turnstile and you should explain who was in my spot. I was.",
        ],
        "roster": [
            "The intramural sheet means I had a game or a practice. It ended or it moved to rec. A roster is not a key.",
            "I was listed because I play. I played. Listing me is not placing me in your room.",
            "Sheet then turnstile. A normal athlete night. Do not scout it into a crime.",
            "I did not stay at the scene. My night has a court on it. I went to the court.",
        ],
        "sign": [
            "I signed whatever practice sheet was on the clipboard. That was earlier. The window is not the warmup.",
            "On campus for practice is the assignment. I left it for rec. Different door.",
            "A practice sign-in does not hand me a building key. I have a rec card. That is the one I used.",
            "Signed, practiced, rec. Three beats. I am not adding a fourth.",
        ],
        "hold_inn": [
            "Hold the seat. I did not do this. I was at the rec center, and I want the denial on the sheet in a complete sentence.",
            "Wrong jersey. I am angry, I was on the court, and you are calling a foul I did not commit.",
        ],
        "hold_cul_deny": "The hold does not finish the play. I am still saying I was at rec. I am not drawing the detour.",
        "hold_cul_own": "Then hold me. I went back and I did it. I am not replaying the rec alibi like it won.",
        "rel_inn": "Good. I am going to stretch and leave. Call me back with a page that beats the turnstile.",
        "rel_cul": "I'll take the release and the rec story with it. I am out before I draw the detour.",
        "rel_own": "Releasing me does not rewind the admission. I did it. Leave the true score up.",
    },
}


def _bands(lines, innocent=False, culprit=False, confess_last=False):
    spans = ((0, 29, "calm"), (30, 59, "nervous"), (60, 84, "scared"), (85, 100, "broken"))
    out = []
    for i, ((lo, hi, emotion), line) in enumerate(zip(spans, lines)):
        if innocent and i == 3:
            emotion = "angry"
        slip = bool(confess_last and culprit and i == 3)
        if slip:
            emotion = "broken"
        out.append(_r(lo, hi, emotion, line, slip=slip, culprit=culprit, innocent=innocent))
    return out


def _with_tail(lines, tail):
    return [f"{line} {tail}" for line in lines]


def replies_for(person_id: str, branch: str, qindex: int):
    person = PEOPLE[person_id]
    tail = person["tails"][qindex % 3]
    if branch == "access":
        inn = list(person["access_inn"][qindex % 2])
        cul = list(person["access_cul"][qindex % 2])
        if qindex >= 2:
            inn = [f"{line} That is the order of it." for line in inn]
            cul = [f"{line} I am not adding a third version." for line in cul]
        return _bands(inn, innocent=True) + _bands(cul, culprit=True, confess_last=True)
    if branch == "hold":
        return [
            _r(0, 100, "scared", person["hold_inn"][0], innocent=True),
            _r(0, 100, "angry", person["hold_inn"][1], innocent=True),
            _r(0, 84, "nervous", person["hold_cul_deny"], culprit=True),
            _r(85, 100, "broken", person["hold_cul_own"], slip=True, culprit=True),
        ]
    if branch == "release":
        return [
            _r(0, 100, "calm", person["rel_inn"], innocent=True),
            _r(0, 84, "nervous", person["rel_cul"], culprit=True),
            _r(85, 100, "broken", person["rel_own"], slip=True, culprit=True),
        ]
    key = {"red_herring": "herring", "alibi_support": "alibi", "roster": "roster", "sign_in": "sign"}[branch]
    return _bands(_with_tail(person[key], tail))


PROMPTS = {
    "access": [
        ("access_q1", "This page puts you inside the incident window after you said you were gone. Explain it.", True),
        ("access_q2", "Your statement and this record do not match. Which one should I keep?", True),
        ("access_q3", "Walk this page slowly. Where were you actually standing?", False),
    ],
    "red_herring": [
        ("herring_q1", "This record looks like preparation. What was it actually for?", False),
        ("herring_q2", "Someone will tell me to hold you on this page alone. Why is that wrong?", True),
        ("herring_q3", "Set this beside the incident clock. Does it still touch the scene?", False),
    ],
    "alibi_support": [
        ("alibi_q1", "This is the page you are using as an alibi. Does it still hold?", False),
        ("alibi_q2", "Could you have doubled back after this stamp?", True),
        ("alibi_q3", "If this support page is wrong, who actually saw you?", False),
    ],
    "roster": [
        ("roster_q1", "This listing puts you on tonight's work. When did you leave it?", False),
        ("roster_q2", "Did this listing give you a key, a login, or a reason to stay through the window?", True),
        ("roster_q3", "Why does this roster matter if your alibi is somewhere else?", False),
    ],
    "sign_in": [
        ("sign_q1", "The sign-in puts you on site earlier. When did you actually leave?", False),
        ("sign_q2", "Being on that sheet put you near the scene. How near, and how late?", True),
        ("sign_q3", "Did signing in hand you a key or any other credential?", False),
    ],
}
