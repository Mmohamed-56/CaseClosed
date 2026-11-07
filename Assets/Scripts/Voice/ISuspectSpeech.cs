namespace CaseClosed.Voice
{
    /// <summary>
    /// Shared speech hook for Magdy / Bezi / Mikasa.
    /// Gemini raises <see cref="SuspectDialogueEvents.ReplyReady"/>; any <see cref="ISuspectSpeech"/>
    /// implementation (ElevenLabs) plays the line without blocking gameplay.
    /// </summary>
    public interface ISuspectSpeech
    {
        /// <summary>True when an API key is present and the client can attempt TTS.</summary>
        bool IsAvailable { get; }

        /// <summary>True while a request or playback is in progress.</summary>
        bool IsBusy { get; }

        /// <summary>Speak a prepared request. Safe to call from UI / Gemini callbacks; never blocks the main gameplay loop.</summary>
        void Speak(SuspectSpeechRequest request);

        /// <summary>Abort the active download and stop playback immediately.</summary>
        void CancelInFlight();
    }
}
