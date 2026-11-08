# Campus affiliate pool (~100 target)

`Resources/Campus/affiliates.json` feeds records + dialogue. **ScenarioDialogueClient and interviewee clue ids stay stable.**

## Tiers

| Tier | Count (batch 1) | Role |
| --- | --- | --- |
| `interviewee` | 12 | Dense interrogation + classic clue suffixes |
| `witness` | 28 | Bios / record hooks / light witness lines |

Overnight batches grow witnesses toward **~100** total affiliates.

## Clue id contract (do not break)

Interviewee evidence (CaseGenerator):

- `{caseId}_{id}_roster`
- `{caseId}_{id}_alibi_support`
- `{caseId}_{id}_sign_in`
- `{caseId}_{id}_access` (culprit only)
- `{caseId}_{id}_red_herring` (innocent only)
- Suspect ids: `suspect_{id}`

Directory / witness hooks:

- Person id: `affiliate_{id}`
- Record id: `{caseId}_aff_{id}_{suffix}` (`bio`, `sighting`, `badge`, `directory`, …)

## Regenerate

```bash
python Tools/gen_campus_affiliates.py          # batch 40
# later: edit AFFILIATES / WITNESSES lists → re-run toward 100
python Tools/gen_dialogue_bank.py              # dense banks for core 8
```

Website payload optional field: `affiliates[]` (schema still v1; older clients ignore it).
