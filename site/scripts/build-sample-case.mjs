/**
 * Builds data/sample-case.json — archive case with POIs + campus directory (~100).
 * Keeps legacy collectable record ids stable for Unity CollectEvidence.
 */
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const outPath = path.join(__dirname, "..", "data", "sample-case.json");

const CASE_ID = "UB-0000000042";

/** Chizuru dialogue-bank pool (exact display names / roles where known). */
const BANK = {
  jamie: { name: "Jamie Rivera", role: "Student volunteer — Spirit Committee" },
  morgan: { name: "Morgan Lee", role: "Student events assistant" },
  avery: { name: "Avery Patel", role: "Campus facilities worker" },
  riley: { name: "Riley Brooks", role: "Club treasurer" },
  casey: { name: "Casey Nguyen", role: "Mascot squad member" },
  drew: { name: "Drew Okonkwo", role: "Campus security student aide" },
  quinn: { name: "Quinn Alvarez", role: "Student media photographer" },
  sam: { name: "Sam Torvald", role: "Dining services closer" }
};

const firstNames = [
  "Alex", "Blair", "Cameron", "Dana", "Eden", "Finley", "Gray", "Harper", "Indigo", "Jordan",
  "Kai", "Logan", "Marley", "Noel", "Oakley", "Parker", "Reese", "Sage", "Taylor", "Val",
  " Wren", "Yael", "Zion", "Adrian", "Blake", "Chris", "Devon", "Elliott", "Francis", "Gabe",
  "Hayden", "Ivan", "Jules", "Kelly", "Lee", "Micah", "Nina", "Omar", "Pat", "Robin",
  "Shawn", "Tess", "Uma", "Victor", "Whitney", "Xavier", "Yuri", "Zoe", "Amari", "Brooke",
  "Carmen", "Diego", "Elena", "Farah", "Gina", "Hector", "Iris", "Jose", "Kira", "Luis",
  "Maya", "Nadia", "Oscar", "Pilar", "Ravi", "Sofia", "Talia", "Ursula", "Vera", "Will",
  "Anita", "Ben", "Celia", "Dan", "Eva", "Felix", "Grace", "Hugo", "Ivy", "Jack",
  "Nora", "Owen", "Paul", "Quinn", "Ruth", "Sean", "Tina", "Ulysses", "Vince", "Wendy"
].map((s) => s.trim());

const lastNames = [
  "Nguyen", "Patel", "Garcia", "Kim", "Chen", "Williams", "Johnson", "Brown", "Davis", "Miller",
  "Wilson", "Moore", "Taylor", "Anderson", "Thomas", "Jackson", "White", "Harris", "Martin", "Thompson",
  "Martinez", "Robinson", "Clark", "Rodriguez", "Lewis", "Lee", "Walker", "Hall", "Allen", "Young",
  "Hernandez", "King", "Wright", "Lopez", "Hill", "Scott", "Green", "Adams", "Baker", "Nelson",
  "Carter", "Mitchell", "Perez", "Roberts", "Turner", "Phillips", "Campbell", "Parker", "Evans", "Edwards",
  "Collins", "Stewart", "Sanchez", "Morris", "Rogers", "Reed", "Cook", "Morgan", "Bell", "Murphy",
  "Bailey", "Rivera", "Cooper", "Richardson", "Cox", "Howard", "Ward", "Torres", "Peterson", "Gray",
  "Ramirez", "James", "Watson", "Brooks", "Kelly", "Sanders", "Price", "Bennett", "Wood", "Barnes",
  "Ross", "Henderson", "Coleman", "Jenkins", "Perry", "Powell", "Long", "Patterson", "Hughes", "Flores",
  "Singh", "Chopra", "Okafor", "Mensah", "Diallo", "Hassan", "Kowalski", "Novak", "Petrov", "Ivanov",
  "Silva", "Costa", "Moreau", "Dupont", "Berg", "Lindqvist", "Olsen", "Sato", "Tanaka", "Park",
  "Cho", "Yilmaz", "Demir", "Costa", "Almeida", "Fernandez", "Castillo", "Jimenez", "Vargas", "Ortiz",
  "Buckley", "Fitzgerald", "OBrien", "McKenzie", "Singhania", "Banerjee", "Ibrahim", "Ali", "Zhou", "Huang"
];

const roles = [
  "Undergraduate — undeclared",
  "Undergraduate — Biology",
  "Undergraduate — Computer Science",
  "Undergraduate — English",
  "Undergraduate — History",
  "Undergraduate — Psychology",
  "Graduate student — Education",
  "Graduate student — Engineering",
  "Library student aide",
  "Residence hall RA",
  "Dining services associate",
  "Facilities day porter",
  "Facilities night porter",
  "Athletics office clerk",
  "Box office volunteer",
  "Campus shuttle driver",
  "IT helpdesk student worker",
  "Lab technician (Biology)",
  "Admissions tour guide",
  "Bookstore cashier",
  "Parking services attendant",
  "Health center front desk",
  "Registrar clerk",
  "Mailroom clerk",
  "Marching band member",
  "Theatre tech crew",
  "Intramural referee",
  "Orientation leader",
  "Alumni relations intern",
  "Campus radio DJ"
];

const buildings = [
  "Mill Student Center", "Cedar Pavilion", "Hale Library", "Harper Hall", "West Residences",
  "North Quad", "Science Complex", "Union Cafe", "Admin Annex", "Field House"
];

function poiJamie() {
  return {
    id: "suspect_jamie",
    poi: true,
    name: BANK.jamie.name,
    role: BANK.jamie.role,
    statement:
      "I worked the Founders Weekend spirit table at the Mill Student Center until about 22:30, then walked home along North Quad. I did not enter Cedar Pavilion or the equipment cage at any time that night.",
    records: [
      {
        id: `${CASE_ID}_jamie_context`,
        title: "Event sign-in / sign-out — Mill atrium",
        description:
          "FORM EV-19 · Founders Weekend spirit table.\nSubject: RIVERA, Jamie · Student ID on file.\nCheck-in: 19:05 (Mill Student Center atrium).\nCheck-out: 22:28.\nSupervisor initial: K.P.\nRemarks: None."
      },
      {
        id: `${CASE_ID}_jamie_access`,
        title: "Keycard transaction — Cedar Pavilion east service",
        description:
          "ACCESS LOG · Reader CPD-E-SVC-01\nCard #: NS-3301 (issued: RIVERA, J.)\nTimestamp: 05 Nov 22:41:08\nResult: GRANTED\nDoor held open: 00:00:47\nAdjacent readers (cage interior): no transaction recorded."
      },
      {
        id: `${CASE_ID}_jamie_phone`,
        title: "SMS extract — outbound 22:44",
        description:
          'CARRIER MIRROR · Case assist request #CA-4421\nFrom: subject handset\nTo: contact labeled "Sam"\nSent: 05 Nov 22:44:19\nBody: got it, meet by the loading dock\nTower / sector at send: North Quad — Cedar Pavilion facing.'
      },
      {
        id: `${CASE_ID}_jamie_purchase`,
        title: "Campus card — Mill Convenience Terminal",
        description:
          "CARD LEDGER · Terminal MILL-CONV-02\n05 Nov 21:50:33 — Energy drink — $2.75\nBalance after: on file.\nNo other Mill purchases that evening."
      },
      {
        id: `${CASE_ID}_jamie_social`,
        title: "CampusConnect post — @jamie.r_spirit",
        description:
          "PUBLIC POST CAPTURE · 05 Nov 21:12\nAccount: @jamie.r_spirit\nText: Spirit table lives! Nordy would be proud (if he still had a head lmao)\nGeotag: Mill Student Center\nVisibility: campus network."
      }
    ]
  };
}

function poiSam() {
  return {
    id: "suspect_sam",
    poi: true,
    name: BANK.sam.name,
    role: "Athletics equipment intern / dining closer",
    statement:
      "I closed Cage E-12 at 21:00 per schedule, completed the inventory log, and studied at Hale Library until nearly midnight. The desk attendant can confirm I was there. I did not return to the Pavilion after close-out.",
    records: [
      {
        id: `${CASE_ID}_sam_context`,
        title: "Equipment cage close-out log — E-12",
        description:
          "FORM ATH-EQ-12 · Cedar Pavilion\nClose-out signed: TORVALD, S. at 21:03\nMascot head (Nordy primary): marked PRESENT\nLock serial noted on form.\nNext scheduled open: 06 Nov 07:30 athletics staff."
      },
      {
        id: `${CASE_ID}_sam_access`,
        title: "Turnstile — Hale Library",
        description:
          "ACCESS LOG · Hale main turnstile\nCard #: NS-5519 (issued: TORVALD, S.)\nEntry: 05 Nov 21:22:41\nExit: 05 Nov 23:58:07\nNo Cedar Pavilion badge events after 21:03 close-out on this card."
      },
      {
        id: `${CASE_ID}_sam_phone`,
        title: "Call detail — missed inbound from Rivera",
        description:
          "CDR EXTRACT\nInbound missed call from RIVERA, Jamie at 22:43:51\nDuration: 0 (no connect)\nVoicemail: none left\nSubject handset sector at ring: North Quad — Hale Library."
      },
      {
        id: `${CASE_ID}_sam_purchase`,
        title: "Off-campus debit — Harbor Hardware",
        description:
          "FRAUD-ALERT MIRROR · Merchant Harbor Hardware\n05 Nov — Debit ****2291\nItem: Mini bolt cutters — $18.99\nReceipt image retained in assist folder.\nNote: purchase predates loss window; relevance undetermined."
      },
      {
        id: `${CASE_ID}_sam_social`,
        title: "CampusConnect story — @sam.ok_eq",
        description:
          'STORY CAPTURE · 05 Nov 22:05 (24h)\nAccount: @sam.t_eq\nMedia: study carrel, Hale Library Level 2\nCaption: midterms vs Founders Weekend, midterms winning\nGeotag: Hale Library'
      }
    ]
  };
}

function poiPriya() {
  return {
    id: "suspect_priya",
    poi: true,
    name: "Priya Nair",
    role: "Marching band — brass section leader",
    statement:
      "Band rehearsal ran in Harper Hall until 22:00. After that I was with section members getting food at West Residences. I do not have equipment-cage keys and would not know the head storage location.",
    records: [
      {
        id: `${CASE_ID}_priya_context`,
        title: "Rehearsal attendance — Harper Hall",
        description:
          "FORM BAND-ATT · Harper Hall Rm 110\nNAIR, Priya — present 18:30–22:05\nDirector stamp on file.\nDismissed with section; no late return noted."
      },
      {
        id: `${CASE_ID}_priya_access`,
        title: "Keycard — West Residences lobby",
        description:
          "ACCESS LOG · WRES lobby reader\nCard #: NS-8842 (issued: NAIR, P.)\nEntry: 05 Nov 22:18:22\nCedar Pavilion readers: no transactions for this card on 05 Nov."
      },
      {
        id: `${CASE_ID}_priya_phone`,
        title: "Group message thread — Brass Chat",
        description:
          "DEVICE EXTRACT (consensual copy)\nThread: Brass Chat\nWindow: 22:20–23:10\nContent: coordinating late food run / West Residences\nTower sector during thread: West Residences complex."
      },
      {
        id: `${CASE_ID}_priya_social`,
        title: "CampusConnect post — @priya.brass",
        description:
          "PUBLIC POST CAPTURE · 05 Nov 22:31\nAccount: @priya.brass\nText: If anyone 'borrows' Nordy's head for a prank I will personally conduct your apology.\nGeotag: none\nVisibility: friends + campus."
      }
    ]
  };
}

function poiDevon() {
  return {
    id: "suspect_devon",
    poi: true,
    name: "Devon Blake",
    role: "Facilities — night porter",
    statement:
      "I walked Route A-North including the Cedar Pavilion sweep on my usual schedule. The cage hallway was quiet. Exterior cage door appeared locked when I passed around 22:20. I reported Pavilion clear to dispatch after the sweep.",
    records: [
      {
        id: `${CASE_ID}_devon_context`,
        title: "Night porter route sheet — A-North",
        description:
          "FORM FAC-RTE-A\nAssigned: BLAKE, D.\nCedar Pavilion interior sweep initialed 22:18–22:35\nCage hallway noted; no exception written on sheet."
      },
      {
        id: `${CASE_ID}_devon_access`,
        title: "Staff keycard — Pavilion service corridor",
        description:
          "ACCESS LOG · Reader CPD-SVC-COR\nStaff card #: STF-204 (issued: BLAKE, D.)\n05 Nov 22:19:04 — GRANTED (service corridor)\nCage interior reader CPD-E12-IN: no swipe."
      },
      {
        id: `${CASE_ID}_devon_phone`,
        title: "Radio / phone — Facilities Dispatch check-in",
        description:
          'DISPATCH LOG\nOutbound to Facilities Dispatch — 22:36\nDuration: 0m 48s\nOperator note: "Pavilion clear"\nCall origin sector: Cedar Pavilion.'
      },
      {
        id: `${CASE_ID}_devon_purchase`,
        title: "Vending transaction — Pavilion L1",
        description:
          "VEND LOG · Machine PV-L1-01\n05 Nov 22:40:12 — Hot beverage — $1.50 (cash)\nLocation: Cedar Pavilion Level 1 corridor."
      },
      {
        id: `${CASE_ID}_devon_social`,
        title: "CampusConnect check-in — @devon.nights",
        description:
          "PUBLIC POST CAPTURE · 05 Nov 23:05\nAccount: @devon.nights\nText: Floors done. Someone left glitter everywhere near the cage hall. Not it.\nGeotag: Cedar Pavilion"
      }
    ]
  };
}

function relatedBank(key, note) {
  const p = BANK[key];
  return {
    id: "suspect_" + key,
    poi: false,
    related: true,
    name: p.name,
    role: p.role,
    statement: note,
    records: [
      {
        id: `dir_${key}_roster`,
        title: "Campus directory — employment / enrollment",
        description: `DIRECTORY SNAPSHOT\nName: ${p.name}\nRole: ${p.role}\nStatus: Active · Northbridge State\nLinked to case ${CASE_ID} as related person (not designated POI on this archive file).`
      },
      {
        id: `dir_${key}_access_night`,
        title: "Badge summary — night of incident",
        description: `ACCESS SUMMARY · 05 Nov\nSubject card on file.\nNo Cedar Pavilion equipment-cage interior swipe attributed to this subject in the loss window.\nSee full badge export on request.`
      }
    ]
  };
}

function directoryPerson(index, first, last, role) {
  const n = String(index).padStart(4, "0");
  const id = "person_ns_" + n;
  const bldg = buildings[index % buildings.length];
  const hour = 8 + (index % 12);
  const min = (index * 7) % 60;
  const ts = `${String(hour).padStart(2, "0")}:${String(min).padStart(2, "0")}`;
  return {
    id,
    poi: false,
    name: `${first} ${last}`,
    role,
    statement: `Directory file only. No sworn statement taken for case ${CASE_ID}.`,
    records: [
      {
        id: `${id}_directory`,
        title: "Campus directory card",
        description: `UPSR DIRECTORY\nID: ${id}\nName: ${last}, ${first}\nRole: ${role}\nPrimary location: ${bldg}\nStatus: Active\nLast directory sync: 05 Nov 06:00`
      },
      {
        id: `${id}_card`,
        title: "Campus card — recent terminal",
        description: `CARD LEDGER (sample)\n05 Nov ${ts} — terminal near ${bldg}\nAmount/item: routine campus purchase or door tap on file.\nNot flagged for case assist.`
      }
    ]
  };
}

const suspects = [];
const usedNames = new Set();

function takeName(first, last) {
  const key = (first + "|" + last).toLowerCase();
  if (usedNames.has(key)) return false;
  usedNames.add(key);
  return true;
}

// Seed reserved names so directory filler won't collide
[
  "Jamie Rivera",
  "Sam Torvald",
  "Priya Nair",
  "Devon Blake",
  "Morgan Lee",
  "Avery Patel",
  "Riley Brooks",
  "Casey Nguyen",
  "Drew Okonkwo",
  "Quinn Alvarez"
].forEach((n) => usedNames.add(n.toLowerCase().replace(" ", "|")));

suspects.push(poiJamie(), poiSam(), poiPriya(), poiDevon());

suspects.push(
  relatedBank("morgan", "Related person — events staff. Interview not opened on this archive desk file."),
  relatedBank("avery", "Related person — facilities. Badge summary on file; not designated POI."),
  relatedBank("riley", "Related person — club treasurer. Roster cross-check only."),
  relatedBank("casey", "Related person — mascot squad. Costume annex activity outside loss window."),
  relatedBank("drew", "Related person — student security aide. Patrol loop south of Pavilion."),
  relatedBank("quinn", "Related person — student media. Press pass list for Founders Weekend.")
);

// Build unique first+last pairs with round-robin surnames (no single-name spam).
const pairs = [];
const surnameUse = Object.create(null);
let cursor = 0;
for (let n = 0; n < 400 && pairs.length < 200; n++) {
  const first = firstNames[n % firstNames.length];
  // Prefer least-used surnames; offset by n so pairs stay varied.
  let bestLast = null;
  let bestCount = Infinity;
  for (let k = 0; k < lastNames.length; k++) {
    const last = lastNames[(cursor + k) % lastNames.length];
    const key = (first + "|" + last).toLowerCase();
    if (usedNames.has(key)) continue;
    const c = surnameUse[last] || 0;
    if (c < bestCount) {
      bestCount = c;
      bestLast = last;
      if (c === 0) break;
    }
  }
  cursor = (cursor + 7) % lastNames.length;
  if (!bestLast) continue;
  if (!takeName(first, bestLast)) continue;
  surnameUse[bestLast] = (surnameUse[bestLast] || 0) + 1;
  pairs.push([first, bestLast]);
}

let i = 1;
for (const [first, last] of pairs) {
  if (suspects.length >= 100) break;
  suspects.push(directoryPerson(i, first, last, roles[(i * 3) % roles.length]));
  i++;
}

const payload = {
  schemaVersion: 1,
  caseId: CASE_ID,
  title: "Theft — Athletic Mascot Property (Cedar Pavilion)",
  briefing:
    "INCIDENT SUMMARY (open file)\n\nOn 06 Nov, approx. 07:40, Athletics Equipment reported the Nordy the Owl mascot head missing from Cedar Pavilion Equipment Cage E-12. Cage exterior lock found secured at morning inventory; interior count shows head absent and spare jersey displaced. Estimated loss window: 21:00–23:00 on 05 Nov (night before Founders Weekend).\n\nFour persons of interest are flagged on this file. Campus directory and related-person cards are available for cross-reference. Pull public records, compare against sworn statements, and attach relevant items to the working case file before referral.",
  suspects
};

fs.writeFileSync(outPath, JSON.stringify(payload, null, 2) + "\n");
const poi = suspects.filter((s) => s.poi).length;
const related = suspects.filter((s) => s.related).length;
console.log(
  `Wrote ${suspects.length} subjects (${poi} POI, ${related} related bank, ${suspects.length - poi - related} directory) → ${outPath}`
);
