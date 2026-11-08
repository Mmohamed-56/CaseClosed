# Magdy — Play Mode steps (proto-loop)

Branch: `proto-loop` (base: `origin/dialogue-bank` @ Chizuru ScenarioDialogueClient + bank).
No Gemini ask path required.

## Pull

```sh
cd "C:\Users\mags5\My project"
git fetch origin proto-loop
git checkout proto-loop
# or: git worktree add "...\My project-worktrees\proto-loop" proto-loop
```

Open `Assets/Scenes/SampleScene.unity` → **Play**.

## Loop (one full shift)

1. **Start** — Play auto-starts seed `42` / 2 suspects via `CaseGameplayPrototype`.
2. **Briefing** — Top tags: **ACTIONS** | **CASE FILE** | **DECISION**. Open **CASE FILE**.
3. **Open records** — Press **Open public records** (browser → https://ubcaseclosed.netlify.app/ with case hash) *or* stay in CASE FILE.
4. **Collect ≥2 evidence** — Press **Collect · …** on at least two case records (real `CaseGenerator` titles, not placeholders).
5. **Call suspect in** — Open **ACTIONS** → **Call in — Name** (enter/sit hooks fire when Bezi anchors exist; otherwise interview opens immediately).
6. **Sit / talk** — When seated, ask row unlocks. Type a question → **ASK** (offline `ScenarioDialogueClient` bank). Optional: **Present ·** a collected record first.
7. **Stamp** — **CASE FILE** footer → **HOLD** or **RELEASE**. Next suspect briefing, or charge when roster done.
8. **Outcome** — **DECISION** → charge a held suspect or **Charge nobody** → morning headline.
9. **Restart** — **Restart case** on the headline panel.

## Expect

- Ask replies come from `Assets/Resources/Dialogue/*.json` (no API key).
- Door / stamp / UI click / ambience via Emilia `Resources/SFX` when `SfxPlayer` auto-boots.
- Talk anim triggers only when ElevenLabs actually plays (sit-talk bridge); without a key, barks still fire.

## Stuck?

- Call-in locked → file ≥2 records in CASE FILE.
- ASK grey → still in Briefing or waiting on seat walk.
- No enter motion → door/chair anchors not assigned; interview still proceeds.
