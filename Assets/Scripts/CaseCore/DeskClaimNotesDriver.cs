using System;
using System.Collections.Generic;
using CaseClosed.CaseCore.Dialogue;
using CaseClosed.CaseCore.Gemini;
using CaseClosed.UI;
using CaseClosed.Voice;
using UnityEngine;

namespace CaseClosed.CaseCore
{
    /// <summary>
    /// Writes authored claims on <see cref="DeskClaimNotepad"/> after the interviewee's opening
    /// statement, then checks or crosses a line when one of their records is collected.
    /// Finds the notepad at runtime. Does not edit the scene, decide a stamp, or change dialogue.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeskClaimNotesDriver : MonoBehaviour
    {
        private CaseShiftController shiftController;
        private DeskClaimNotepad notepad;
        private ClaimNoteCase activeNotes;
        private string notesSuspectId = string.Empty;
        private string activeDecoyKey = string.Empty;
        private bool subscribed;
        private bool speechSubscribed;
        private bool warnedMissingNotepad;
        private string warnedMissingCase = string.Empty;
        private readonly List<ClaimNoteRecord> seenRecords = new List<ClaimNoteRecord>(8);

        // Same wait the clipboard uses before it clears a subtitle whose voice never starts.
        private const float SubtitleVoiceStartFallbackSeconds = 12f;
        private const string SubtitleObjectName = "SuspectSubtitle";

        private string[] pendingClaimIds = Array.Empty<string>();
        private string[] pendingClaimTexts = Array.Empty<string>();
        private bool awaitingOpening;
        private string awaitingSuspectId = string.Empty;
        private bool openingSpeechStarted;
        private bool openingSubtitleSeen;
        private float openingSentUnscaledTime;
        private GameObject openingSubtitle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!Application.isPlaying) return;
            if (FindAnyObjectByType<DeskClaimNotesDriver>() != null) return;

            CaseShiftController shift = FindAnyObjectByType<CaseShiftController>();
            if (shift == null) return;
            shift.gameObject.AddComponent<DeskClaimNotesDriver>();
        }

        private void Awake()
        {
            if (shiftController == null)
                shiftController = GetComponent<CaseShiftController>();
        }

        private void OnEnable()
        {
            if (shiftController == null)
                shiftController = GetComponent<CaseShiftController>();
            if (shiftController == null)
                shiftController = FindAnyObjectByType<CaseShiftController>();
            Subscribe();
            SubscribeSpeech();
        }

        private void Start()
        {
            if (shiftController == null) return;
            HandlePhaseChanged(shiftController.Phase);
        }

        private void OnDisable()
        {
            Unsubscribe();
            UnsubscribeSpeech();
        }

        private void Update()
        {
            if (!awaitingOpening || openingSpeechStarted)
                return;

            if (openingSubtitle == null)
                openingSubtitle = GameObject.Find(SubtitleObjectName);
            if (openingSubtitle != null && openingSubtitle.activeInHierarchy)
                openingSubtitleSeen = true;

            bool subtitleClosed = openingSubtitleSeen &&
                                  (openingSubtitle == null || !openingSubtitle.activeInHierarchy);
            bool noPlaybackOrSubtitle = !openingSubtitleSeen &&
                                        Time.unscaledTime - openingSentUnscaledTime >= SubtitleVoiceStartFallbackSeconds;
            if (subtitleClosed || noPlaybackOrSubtitle)
                FinishOpeningStatement();
        }

        private void Subscribe()
        {
            if (subscribed || shiftController == null) return;
            shiftController.PhaseChanged += HandlePhaseChanged;
            shiftController.EvidenceCollected += HandleEvidenceCollected;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed || shiftController == null) return;
            shiftController.PhaseChanged -= HandlePhaseChanged;
            shiftController.EvidenceCollected -= HandleEvidenceCollected;
            subscribed = false;
        }

        private void SubscribeSpeech()
        {
            if (speechSubscribed) return;
            SuspectDialogueEvents.SpeechStarted += HandleSpeechStarted;
            SuspectDialogueEvents.SpeechEnded += HandleSpeechEnded;
            speechSubscribed = true;
        }

        private void UnsubscribeSpeech()
        {
            if (!speechSubscribed) return;
            SuspectDialogueEvents.SpeechStarted -= HandleSpeechStarted;
            SuspectDialogueEvents.SpeechEnded -= HandleSpeechEnded;
            speechSubscribed = false;
        }

        /// <summary>
        /// Interview start reads <see cref="SuspectRecord.Statement"/> and speaks it.
        /// Claims stay off the notepad until that playback ends, or until the subtitle closes
        /// when no audio starts. An empty statement still writes the claims immediately.
        /// </summary>
        private void HandlePhaseChanged(CaseShiftPhase phase)
        {
            if (phase == CaseShiftPhase.Investigating)
                BeginInterviewNotes();
            else
                HideNotes();
        }

        private void HandleSpeechStarted(string suspectId)
        {
            if (!awaitingOpening || !IsAwaitingSuspect(suspectId))
                return;
            openingSpeechStarted = true;
        }

        private void HandleSpeechEnded(string suspectId)
        {
            if (!awaitingOpening || !openingSpeechStarted || !IsAwaitingSuspect(suspectId))
                return;
            FinishOpeningStatement();
        }

        private void HandleEvidenceCollected(EvidenceRecord record)
        {
            if (record == null || string.IsNullOrEmpty(notesSuspectId) || shiftController == null)
                return;

            SuspectRecord suspect = shiftController.CurrentSuspect;
            if (suspect == null || !string.Equals(suspect.Id, notesSuspectId, StringComparison.Ordinal))
                return;

            string ownerId = shiftController.FindEvidenceOwnerId(record.Id);
            if (!string.Equals(ownerId, suspect.Id, StringComparison.Ordinal))
                return;

            ApplyRecordMarks(suspect);
        }

        private void BeginInterviewNotes()
        {
            if (shiftController == null || shiftController.ActiveSession == null) return;

            SuspectRecord suspect = shiftController.CurrentSuspect;
            if (suspect == null) return;

            if (!string.IsNullOrEmpty(notesSuspectId) &&
                string.Equals(notesSuspectId, suspect.Id, StringComparison.Ordinal))
            {
                ApplyRecordMarks(suspect);
                return;
            }

            if (awaitingOpening && string.Equals(awaitingSuspectId, suspect.Id, StringComparison.Ordinal))
                return;

            CaseFile caseFile = shiftController.ActiveSession.Case;
            CaseTemplateCatalog.CaseTemplate template = caseFile == null
                ? null
                : CaseTemplateCatalog.FindByTitle(caseFile.Title);
            ClaimNoteCase notes = template == null ? null : ClaimNoteBook.Find(template.id);
            if (notes == null || notes.claims == null || notes.claims.Length == 0)
            {
                string title = caseFile != null ? caseFile.Title : string.Empty;
                if (!string.IsNullOrEmpty(title) && !string.Equals(warnedMissingCase, title, StringComparison.Ordinal))
                {
                    warnedMissingCase = title;
                    Debug.LogWarning("[DeskClaimNotes] No authored claims for \"" + title + "\".");
                }

                HideNotes();
                return;
            }

            int count = 0;
            for (int i = 0; i < notes.claims.Length; i++)
            {
                if (notes.claims[i] != null && !string.IsNullOrWhiteSpace(notes.claims[i].id))
                    count++;
            }

            pendingClaimIds = new string[count];
            pendingClaimTexts = new string[count];
            int written = 0;
            for (int i = 0; i < notes.claims.Length; i++)
            {
                ClaimNoteLine line = notes.claims[i];
                if (line == null || string.IsNullOrWhiteSpace(line.id)) continue;
                pendingClaimIds[written] = line.id.Trim();
                pendingClaimTexts[written] = line.text ?? string.Empty;
                written++;
            }

            activeNotes = notes;
            activeDecoyKey = PersonKey(template.decoySuspectId);

            if (string.IsNullOrWhiteSpace(suspect.Statement))
            {
                CancelOpeningWait();
                ShowPreparedClaims(suspect);
                return;
            }

            InterrogationDialogueService dialogue = FindDialogue();
            if (dialogue == null)
            {
                CancelOpeningWait();
                ShowPreparedClaims(suspect);
                return;
            }

            ArmOpeningWait(suspect);
            dialogue.SpeakOpeningStatement(suspect);
            NoteSubtitleIfAlreadyShowing();
        }

        private void ArmOpeningWait(SuspectRecord suspect)
        {
            awaitingOpening = true;
            awaitingSuspectId = suspect.Id;
            openingSpeechStarted = false;
            openingSubtitleSeen = false;
            openingSubtitle = null;
            openingSentUnscaledTime = Time.unscaledTime;
        }

        private void NoteSubtitleIfAlreadyShowing()
        {
            openingSubtitle = GameObject.Find(SubtitleObjectName);
            if (openingSubtitle != null && openingSubtitle.activeInHierarchy)
                openingSubtitleSeen = true;
        }

        private void FinishOpeningStatement()
        {
            if (!awaitingOpening || shiftController == null)
                return;
            if (shiftController.Phase != CaseShiftPhase.Investigating)
            {
                CancelOpeningWait();
                return;
            }

            SuspectRecord suspect = shiftController.CurrentSuspect;
            if (suspect == null || !string.Equals(suspect.Id, awaitingSuspectId, StringComparison.Ordinal))
            {
                CancelOpeningWait();
                return;
            }

            awaitingOpening = false;
            if (!ShowPreparedClaims(suspect))
                awaitingOpening = true;
        }

        private bool ShowPreparedClaims(SuspectRecord suspect)
        {
            DeskClaimNotepad pad = Notepad();
            if (pad == null || suspect == null)
                return false;

            pad.SetClaims(pendingClaimIds, pendingClaimTexts);
            notesSuspectId = suspect.Id;
            ApplyRecordMarks(suspect);
            return true;
        }

        private void CancelOpeningWait()
        {
            awaitingOpening = false;
            awaitingSuspectId = string.Empty;
            openingSpeechStarted = false;
            openingSubtitleSeen = false;
            openingSubtitle = null;
        }

        private bool IsAwaitingSuspect(string suspectId)
        {
            return !string.IsNullOrEmpty(awaitingSuspectId) &&
                   string.Equals(awaitingSuspectId, suspectId, StringComparison.Ordinal);
        }

        private InterrogationDialogueService FindDialogue()
        {
            InterrogationDialogueService dialogue = shiftController != null
                ? shiftController.GetComponent<InterrogationDialogueService>()
                : null;
            if (dialogue == null)
                dialogue = FindAnyObjectByType<InterrogationDialogueService>();
            return dialogue;
        }

        private void ApplyRecordMarks(SuspectRecord suspect)
        {
            DeskClaimNotepad pad = Notepad();
            if (pad == null || activeNotes == null || activeNotes.claims == null || suspect == null)
                return;

            seenRecords.Clear();
            IReadOnlyList<EvidenceRecord> collected = shiftController.GetCollectedEvidence();
            for (int i = 0; i < collected.Count; i++)
            {
                EvidenceRecord record = collected[i];
                if (record == null) continue;
                string ownerId = shiftController.FindEvidenceOwnerId(record.Id);
                if (!string.Equals(ownerId, suspect.Id, StringComparison.Ordinal)) continue;
                seenRecords.Add(new ClaimNoteRecord
                {
                    Suffix = ScenarioDialogueClient.EvidenceKeyFromId(record.Id),
                    Kind = record.Kind
                });
            }

            bool isDecoy = PersonKey(suspect.Id) == activeDecoyKey && activeDecoyKey.Length > 0;
            for (int i = 0; i < activeNotes.claims.Length; i++)
            {
                ClaimNoteLine line = activeNotes.claims[i];
                if (line == null || string.IsNullOrWhiteSpace(line.id)) continue;

                ClaimNoteMark mark = ClaimNoteBook.MarkFor(activeNotes, line.id.Trim(), isDecoy, seenRecords);
                if (mark == ClaimNoteMark.Matched)
                    pad.NoteMatched(line.id.Trim());
                else if (mark == ClaimNoteMark.Mismatch)
                    pad.NoteMismatch(line.id.Trim());
            }
        }

        private void HideNotes()
        {
            CancelOpeningWait();
            notesSuspectId = string.Empty;
            activeNotes = null;
            activeDecoyKey = string.Empty;
            pendingClaimIds = Array.Empty<string>();
            pendingClaimTexts = Array.Empty<string>();
            DeskClaimNotepad pad = notepad;
            if (pad == null)
                pad = FindAnyObjectByType<DeskClaimNotepad>(FindObjectsInactive.Include);
            if (pad != null)
                pad.Clear();
        }

        private DeskClaimNotepad Notepad()
        {
            if (notepad != null) return notepad;
            notepad = FindAnyObjectByType<DeskClaimNotepad>(FindObjectsInactive.Include);
            if (notepad == null && !warnedMissingNotepad)
            {
                warnedMissingNotepad = true;
                Debug.LogWarning("[DeskClaimNotes] DeskClaimNotepad is not in the loaded scene, so claim calls have nowhere to land.");
            }

            return notepad;
        }

        private static string PersonKey(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return string.Empty;
            string key = id.Trim().ToLowerInvariant();
            const string prefix = "suspect_";
            if (key.StartsWith(prefix, StringComparison.Ordinal))
                key = key.Substring(prefix.Length);
            return key;
        }
    }
}
