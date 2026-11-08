using UnityEngine;
using UnityEngine.Events;

namespace CaseClosed.Interaction
{
    /// <summary>Provides a prompt and UnityEvent callbacks for objects targeted by a look interaction controller.</summary>
    [DisallowMultipleComponent]
    public sealed class LookInteractable : MonoBehaviour
    {
        [Header("Interaction")]
        [SerializeField, TextArea(1, 2)] private string promptText = "Interact";
        [SerializeField] private bool isAvailable = true;
        [SerializeField] private UnityEvent onInteract = new UnityEvent();

        [Header("Hover")]
        [SerializeField] private UnityEvent onHoverEnter = new UnityEvent();
        [SerializeField] private UnityEvent onHoverExit = new UnityEvent();

        /// <summary>The contextual prompt shown while this object is targeted.</summary>
        public string PromptText { get { return promptText; } }

        /// <summary>Whether this enabled component can currently be targeted and interacted with.</summary>
        public bool IsAvailable { get { return isAvailable && isActiveAndEnabled; } }

        /// <summary>UnityEvent invoked when the player interacts with this object.</summary>
        public UnityEvent OnInteract { get { return onInteract; } }

        /// <summary>UnityEvent invoked when the player's look target enters this object.</summary>
        public UnityEvent OnHoverEnter { get { return onHoverEnter; } }

        /// <summary>UnityEvent invoked when the player's look target leaves this object.</summary>
        public UnityEvent OnHoverExit { get { return onHoverExit; } }

        /// <summary>Changes whether this object is available to look interaction.</summary>
        public void SetAvailable(bool available)
        {
            isAvailable = available;
        }

        /// <summary>Invokes the configured interaction callback.</summary>
        public void InvokeInteraction()
        {
            if (IsAvailable)
                onInteract.Invoke();
        }

        internal void InvokeHoverEnter()
        {
            if (IsAvailable)
                onHoverEnter.Invoke();
        }

        internal void InvokeHoverExit()
        {
            onHoverExit.Invoke();
        }
    }
}
