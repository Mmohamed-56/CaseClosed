using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.Serialization;

namespace CaseClosed.Characters
{
    /// <summary>
    /// Optional CaseShift integration via reflection so this file compiles even when CaseCore
    /// scripts are not on the branch yet. When a <c>CaseShiftController</c> exists at runtime:
    /// Briefing → Investigating: enter current suspect; Investigating → Briefing decision: exit;
    /// Charging/Headline/Resolved → exit clear.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SuspectEnterController))]
    public sealed class SuspectEnterShiftBinder : MonoBehaviour
    {
        private const string ShiftControllerTypeName = "CaseClosed.CaseCore.CaseShiftController";

        [Header("Case Shift")]
        [Tooltip("CaseShiftController instance. Auto-found by type name when empty.")]
        [SerializeField] private MonoBehaviour shiftController;

        [Tooltip("Suspect enter controller on this object.")]
        [SerializeField] private SuspectEnterController enterController;

        [Tooltip("When enabled, entering Investigating plays the enter sequence for CurrentSuspect.")]
        [FormerlySerializedAs("autoPlayOnBriefing")]
        [SerializeField] private bool autoPlayOnInvestigating = true;

        [Tooltip("When enabled, leaving Investigating for Briefing or a terminal phase plays the exit sequence.")]
        [SerializeField] private bool autoExitOnDecision = true;

        private EventInfo phaseChangedEvent;
        private PropertyInfo phaseProperty;
        private PropertyInfo currentSuspectProperty;
        private PropertyInfo suspectIdProperty;
        private Delegate phaseHandler;
        private object previousPhaseBoxed;
        private bool bindingReady;

        private void Awake()
        {
            if (enterController == null) enterController = GetComponent<SuspectEnterController>();
            TryResolveShiftController();
            bindingReady = CacheShiftApi();
            if (!bindingReady)
                Debug.LogWarning(
                    "SuspectEnterShiftBinder: CaseShiftController not found yet. Pathing still works via SuspectEnterController.PlayEnter/PlayExit; binder activates once CaseCore is in the project.",
                    this);
        }

        private void OnEnable()
        {
            if (!bindingReady && !CacheShiftApi()) return;
            SubscribePhaseChanged();
            previousPhaseBoxed = ReadPhase();
        }

        private void OnDisable()
        {
            UnsubscribePhaseChanged();
        }

        /// <summary>Starts the enter sequence for the shift's current suspect.</summary>
        public void PlayEnterCurrentSuspect()
        {
            string suspectId = ReadCurrentSuspectId();
            if (string.IsNullOrEmpty(suspectId) || enterController == null)
            {
                Debug.LogWarning("SuspectEnterShiftBinder: no CurrentSuspect to enter.", this);
                return;
            }
            enterController.PlayEnter(suspectId);
        }

        private void TryResolveShiftController()
        {
            if (shiftController != null) return;

            Type shiftType = FindType(ShiftControllerTypeName);
            if (shiftType == null) return;

            shiftController = GetComponent(shiftType) as MonoBehaviour;
            if (shiftController == null)
            {
                UnityEngine.Object found = FindAnyObjectByType(shiftType);
                shiftController = found as MonoBehaviour;
            }
        }

        private bool CacheShiftApi()
        {
            TryResolveShiftController();
            if (shiftController == null) return false;

            Type shiftType = shiftController.GetType();
            phaseChangedEvent = shiftType.GetEvent("PhaseChanged", BindingFlags.Instance | BindingFlags.Public);
            phaseProperty = shiftType.GetProperty("Phase", BindingFlags.Instance | BindingFlags.Public);
            currentSuspectProperty = shiftType.GetProperty("CurrentSuspect", BindingFlags.Instance | BindingFlags.Public);
            if (phaseChangedEvent == null || phaseProperty == null || currentSuspectProperty == null)
                return false;

            Type suspectType = currentSuspectProperty.PropertyType;
            suspectIdProperty = suspectType.GetProperty("Id", BindingFlags.Instance | BindingFlags.Public);
            return suspectIdProperty != null;
        }

        private void SubscribePhaseChanged()
        {
            if (phaseChangedEvent == null || shiftController == null || phaseHandler != null) return;

            Type handlerType = phaseChangedEvent.EventHandlerType;
            Type[] generics = handlerType.IsGenericType ? handlerType.GetGenericArguments() : null;
            if (generics == null || generics.Length != 1)
            {
                Debug.LogWarning("SuspectEnterShiftBinder: PhaseChanged event signature is unexpected.", this);
                return;
            }

            MethodInfo open = GetType().GetMethod(
                nameof(HandlePhaseGeneric),
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo closed = open.MakeGenericMethod(generics[0]);
            phaseHandler = Delegate.CreateDelegate(handlerType, this, closed);
            phaseChangedEvent.AddEventHandler(shiftController, phaseHandler);
        }

        private void UnsubscribePhaseChanged()
        {
            if (phaseChangedEvent == null || shiftController == null || phaseHandler == null) return;
            phaseChangedEvent.RemoveEventHandler(shiftController, phaseHandler);
            phaseHandler = null;
        }

        // Closed over CaseShiftPhase (or whatever enum PhaseChanged uses) via MakeGenericMethod.
        private void HandlePhaseGeneric<T>(T phase)
        {
            HandlePhaseChanged(phase);
        }

        private void HandlePhaseChanged(object phase)
        {
            if (enterController == null || phase == null) return;

            string phaseName = phase.ToString();
            string previousName = previousPhaseBoxed != null ? previousPhaseBoxed.ToString() : "Inactive";

            if (autoPlayOnInvestigating && phaseName == "Investigating")
            {
                PlayEnterCurrentSuspect();
            }
            else if (autoExitOnDecision &&
                     ((previousName == "Investigating" && phaseName == "Briefing") ||
                      phaseName == "Charging" || phaseName == "Headline" || phaseName == "Resolved") &&
                     !string.IsNullOrEmpty(enterController.ActiveSuspectId))
            {
                enterController.PlayExit(null);
            }

            previousPhaseBoxed = phase;
        }

        private object ReadPhase()
        {
            return phaseProperty != null && shiftController != null
                ? phaseProperty.GetValue(shiftController, null)
                : null;
        }

        private string ReadCurrentSuspectId()
        {
            if (currentSuspectProperty == null || suspectIdProperty == null || shiftController == null)
                return null;

            object suspect = currentSuspectProperty.GetValue(shiftController, null);
            if (suspect == null) return null;
            object id = suspectIdProperty.GetValue(suspect, null);
            return id as string;
        }

        private static Type FindType(string fullName)
        {
            Type direct = Type.GetType(fullName);
            if (direct != null) return direct;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(fullName);
                if (type != null) return type;
            }
            return null;
        }
    }
}
