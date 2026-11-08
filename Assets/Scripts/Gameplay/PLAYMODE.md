# Magdy — Play Mode steps (dialogue-trees)

Branch: `dialogue-trees` (base: `origin/play-mode`).
Evidence-driven choice trees. No Gemini. No free-text Ask.

## Pull

```sh
cd "C:\Users\mags5\My project"
git fetch origin dialogue-trees
git checkout dialogue-trees
# or worktree: C:\Users\mags5\My project-worktrees\dialogue-trees
```

Open `Assets/Scenes/SampleScene.unity` → **Play**.

## Loop (one full shift)

1. **Start** — Play auto-starts seed `42` / 2 suspects via `CaseGameplayPrototype`.
2. **Briefing** — Top tags: **ACTIONS** | **CASE FILE** | **DECISION**. Open **CASE FILE**.
3. **Open records** — Press **Open public records** (in-game zoom to desk monitor / CEF — **no OS browser tab**) *or* click the monitor / press **F**, *or* stay in CASE FILE.
4. **Collect ≥2 evidence** — Press **Collect · …** on at least two case records (real `CaseGenerator` titles, not placeholders).
5. **Call suspect in** — Open **ACTIONS** → **Call in — Name** (enter/sit hooks fire when Bezi anchors exist; otherwise interview opens immediately).
6. **Sit / talk** — When seated:
   1. **Present ·** a collected record
   2. Pick an **Ask ·** / **Press ·** question choice (3–4 tailored to that evidence)
   3. Suspect replies from `Resources/Dialogue/Trees/` → same `SuspectLineReady` → ElevenLabs Speak path
7. **Stamp** — **CASE FILE** footer → **HOLD** or **RELEASE**. Next suspect briefing, or charge when roster done.
8. **Outcome** — **DECISION** → charge a held suspect or **Charge nobody** → morning headline.
9. **Restart** — **Restart case** on the headline panel.

## Expect

- No type-in InputField on ACTIONS — choices only.
- Header on ACTIONS shows the seated **DisplayName** (matches transcript log).
- Replies from `Assets/Resources/Dialogue/Trees/{suspect}.json` (no API key for dialogue).
- Voice: `SuspectLineReady` still fires; if silent, that's ElevenLabs/Kanna — not the tree path.
- Talk anim triggers only when ElevenLabs actually plays (sit-talk bridge); without a key, barks still fire.

## Stuck?

- Call-in locked → file ≥2 records in CASE FILE.
- No questions → present a record first (Present · …).
- No enter motion → door/chair anchors not assigned; interview still proceeds.
