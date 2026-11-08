using System;
using System.Collections.Generic;

namespace CaseClosed.CaseCore.Dialogue
{
    /// <summary>One stress-gated spoken reply under a question choice.</summary>
    [Serializable]
    public sealed class DialogueTreeReply
    {
        public int minStress;
        public int maxStress = 100;
        public string emotion = "calm";
        public bool slip;
        /// <summary>Spoken only when this interviewee is the culprit tonight.</summary>
        public bool onlyIfCulprit;
        /// <summary>Spoken only when this interviewee is innocent tonight.</summary>
        public bool onlyIfInnocent;
        public string line = "...";
    }

    /// <summary>Player-facing question choice tailored to presented evidence.</summary>
    [Serializable]
    public sealed class DialogueTreeQuestion
    {
        public string id;
        public string prompt;
        public bool aggressive;
        public DialogueTreeReply[] replies = Array.Empty<DialogueTreeReply>();
    }

    /// <summary>Evidence key → question list. Keys: access, red_herring, alibi_support, roster, sign_in, hold, release.</summary>
    [Serializable]
    public sealed class DialogueTreeBranch
    {
        public string evidenceKey;
        public DialogueTreeQuestion[] questions = Array.Empty<DialogueTreeQuestion>();
    }

    /// <summary>JsonUtility root for Resources/Dialogue/Trees/{id}.json.</summary>
    [Serializable]
    public sealed class DialogueTreeFile
    {
        public string suspectId;
        public string displayName;
        public DialogueTreeBranch[] branches = Array.Empty<DialogueTreeBranch>();
    }

    /// <summary>Index of tree stems under Resources/Dialogue/Trees.</summary>
    [Serializable]
    public sealed class DialogueTreeIndex
    {
        public string[] trees = Array.Empty<string>();
    }

    /// <summary>HUD-facing choice row (prompt + flags).</summary>
    public sealed class DialogueQuestionChoice
    {
        public string Id;
        public string Prompt;
        public bool Aggressive;
        public string EvidenceKey;
    }
}
