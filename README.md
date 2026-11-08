# Case Closed at UB

Case Closed at UB is a desk-duty detective game. A student tells you a story, you check it against a real campus records site, and you stamp HOLD or RELEASE.

## Features

- **Opening story:** The student sits and talks first. The notepad stays empty until they finish.
- **Claim notepad:** Three or four claims land on the desk. A matching record checks a line. A bad record marks it.
- **Nervous lies:** An innocent student slips once. A second record can clear them. The culprit has one lie that never clears.
- **Desk computer:** A real early-2000s records site opens on the in-game monitor. Search that student. A large campus directory is on the site.
- **Clipboard:** Pull clues off the site into the case file. Questions use the evidence you present. Answers are unique and voiced.
- **HOLD / RELEASE:** Hold them, or release them if you think they are innocent. Right guesses score. Wrong guesses cost you.
- **14 cases:** The first shift is the Victor the Bull mural.
- **UB office:** The room is hand-placed to look like a UB investigation office.
- **Voices, music, and SFX:** Spoken lines, music, and sound effects come from ElevenLabs. Keys stay on the server.

## Tech Stack

| Layer | Technology |
| --- | --- |
| Game | Unity + C# |
| Characters | Mixamo (sit, walk, stand) |
| Scene | Hand-placed models |
| Records site | Netlify |
| In-game browser | Unity Web Browser (CEF) |
| Voice, music, SFX | ElevenLabs through a proxy |
| Dialogue | Offline trees |

## How It Works

1. Start a shift. Read what happened, where, when, and why.
2. A student walks in, sits, and tells their story.
3. Their claims show up on the notepad.
4. Look that name up on the desk computer and pull clues onto the clipboard.
5. Ask from the questions that match the evidence you have.
6. Stamp HOLD or RELEASE.

## Getting Started

Play the Windows build. No Unity install.

https://github.com/Mmohamed-56/CaseClosed/releases

Download the zip, unzip it, and run the game.

To open the project:

git clone https://github.com/Mmohamed-56/CaseClosed.git

Open the folder in Unity 6 on the `main` branch. Press Play from the main menu. `SampleScene` is the desk.

## Web App Link

https://ubcaseclosed.netlify.app/

## Future Plans

- More handcrafted cases. The current ones get solved too fast.
- Better animations for nervousness, confidence, and other tells, so you can read a lie.
- A build on itch.io so people can play it and send feedback.
