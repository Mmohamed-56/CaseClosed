using System;
using CaseClosed.CaseCore.Gemini;

namespace CaseClosed.CaseCore.Dialogue
{
    /// <summary>Question intents used for keyword/tag matching in the offline bank.</summary>
    public static class ScenarioIntents
    {
        public const string Alibi = "alibi";
        public const string Evidence = "evidence";
        public const string Pressure = "pressure";
        public const string SmallTalk = "small_talk";
        public const string Accusation = "accusation";
        public const string Hold = "hold";
        public const string Release = "release";
        public const string Shutdown = "shutdown";
        public const string Slip = "slip";
        public const string Generic = "generic";
    }

    /// <summary>One authored reply line loaded from Resources JSON.</summary>
    [Serializable]
    public sealed class ScenarioLineEntry
    {
        public string id;
        /// <summary>Pool id ("drew"), full id ("suspect_drew"), or "*" for any.</summary>
        public string suspectId = "*";
        public string intent = ScenarioIntents.Generic;
        /// <summary>Pipe-separated keywords. Empty = intent-only / fallback match.</summary>
        public string keywords = string.Empty;
        public int minStress;
        public int maxStress = 100;
        public string emotion = "calm";
        public bool slip;
        public string line = "...";
        public bool requiresEvidence;
        public bool requiresAggressive;
        public bool culpritOnly;
        public bool innocentOnly;
    }

    /// <summary>JsonUtility root wrapper for a bank file under Resources/Dialogue.</summary>
    [Serializable]
    public sealed class ScenarioBankFile
    {
        public ScenarioLineEntry[] entries = Array.Empty<ScenarioLineEntry>();
    }

    /// <summary>Runtime ask context for evidence-driven choice trees.</summary>
    public sealed class ScenarioAskContext
    {
        public string SuspectId;
        public string SuspectName;
        public string Question;
        /// <summary>Tree question id from <see cref="DialogueQuestionChoice.Id"/>.</summary>
        public string QuestionId;
        public string EvidenceId;
        public bool HasEvidence;
        public bool Aggressive;
        public bool IsCulprit;
        public bool IsShutdown;
        /// <summary>True when this suspect already spoke a slip/confession line this interview.</summary>
        public bool HasSlipped;
        public InterrogationResult StressResult;
        public string AftermathDecision;
        /// <summary>This case's act. A verb phrase after "I ". Replaces {act} when the line is spoken.</summary>
        public string SpokenAct;
    }

    /// <summary>Index listing bank TextAsset names under Resources/Dialogue (no extension).</summary>
    [Serializable]
    public sealed class ScenarioBankIndex
    {
        public string[] banks = Array.Empty<string>();
    }
}
