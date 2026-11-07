#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace CaseClosed.EditorTools
{
    /// <summary>
    /// Mixamo + Low Poly People (David Jalbert) import helpers for door→chair walk/sit.
    /// Trigger names match <c>CharacterAnimationDriver</c> / <c>SuspectEnterController</c>:
    /// idle, walk, sit, stand.
    /// </summary>
    public static class MixamoLowPolyPeopleSetup
    {
        private const string MenuRoot = "Case Closed/Characters/Mixamo/";
        private const string MixamoFolder = "Assets/Animations/Mixamo";
        private const string ReadmePath = MixamoFolder + "/README.md";
        private const string SuspectControllerPath = MixamoFolder + "/SuspectWalkSit.controller";

        private static readonly string[] PackageControllerPaths =
        {
            "Assets/DavidJalbert/LowPolyPeople/Prefabs/Animations/normal.controller",
            "Assets/DavidJalbert/LowPolyPeople/Prefabs/Animations/stout.controller",
            "Assets/DavidJalbert/LowPolyPeople/Prefabs/Animations/strong.controller"
        };

        private static readonly string[] PathingTriggers = { "idle", "walk", "sit", "stand" };

        [MenuItem(MenuRoot + "Create Drop Folder")]
        private static void CreateDropFolder()
        {
            EnsureMixamoFolder();
            EditorUtility.DisplayDialog(
                "Mixamo Drop Folder",
                "Drop Magdy's Mixamo FBX clips into:\n" + MixamoFolder + "\n\nSee README.md in that folder.",
                "OK");
            PingPath(MixamoFolder);
        }

        [MenuItem(MenuRoot + "Open Import README")]
        private static void OpenReadme()
        {
            EnsureMixamoFolder();
            Object readme = AssetDatabase.LoadAssetAtPath<Object>(ReadmePath);
            if (readme == null)
            {
                EditorUtility.DisplayDialog("Mixamo README", "Missing " + ReadmePath, "OK");
                return;
            }

            Selection.activeObject = readme;
            EditorGUIUtility.PingObject(readme);
        }

        [MenuItem(MenuRoot + "Configure Selected FBX As Generic")]
        private static void ConfigureSelectedAsGeneric()
        {
            ConfigureSelectedModels(ModelImporterAnimationType.Generic, null);
        }

        [MenuItem(MenuRoot + "Configure Selected FBX As Humanoid")]
        private static void ConfigureSelectedAsHumanoid()
        {
            Avatar sourceAvatar = PromptOptionalSourceAvatar();
            ConfigureSelectedModels(ModelImporterAnimationType.Human, sourceAvatar);
        }

        [MenuItem(MenuRoot + "Build SuspectWalkSit Controller")]
        private static void BuildSuspectWalkSitController()
        {
            EnsureMixamoFolder();

            Dictionary<string, AnimationClip> clips = FindClipsByRole(MixamoFolder);
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(SuspectControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(SuspectControllerPath);
            }

            EnsureTriggerParameters(controller, PathingTriggers);
            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            ClearGeneratedAnyStateGraph(machine);

            float y = 60f;
            AnimatorState defaultState = null;
            foreach (string trigger in PathingTriggers)
            {
                AnimatorState state = FindOrCreateState(machine, trigger, new Vector3(270f, y, 0f));
                y += 70f;

                AnimationClip clip;
                if (clips.TryGetValue(trigger, out clip) && clip != null)
                    state.motion = clip;

                AddAnyStateTriggerTransition(machine, state, trigger);

                if (trigger == "idle")
                    defaultState = state;
            }

            if (defaultState != null)
                machine.defaultState = defaultState;

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = controller;
            EditorGUIUtility.PingObject(controller);

            string missing = string.Join(", ", PathingTriggers.Where(t => !clips.ContainsKey(t) || clips[t] == null));
            EditorUtility.DisplayDialog(
                "SuspectWalkSit Controller",
                "Wrote " + SuspectControllerPath + "\n\nTriggers: idle, walk, sit, stand (Any State).\n" +
                (string.IsNullOrEmpty(missing)
                    ? "All roles found clips under Mixamo/."
                    : "Still missing clips for: " + missing + "\nDrop FBX named *Walk*, *Sit*/Sitting, *Stand*/StandingUp."),
                "OK");
        }

        [MenuItem(MenuRoot + "Add Sit/Stand To Package Controllers")]
        private static void AddSitStandToPackageControllers()
        {
            Dictionary<string, AnimationClip> clips = FindClipsByRole(MixamoFolder);
            AnimationClip sitClip;
            AnimationClip standClip;
            clips.TryGetValue("sit", out sitClip);
            clips.TryGetValue("stand", out standClip);

            if (sitClip == null && standClip == null)
            {
                EditorUtility.DisplayDialog(
                    "Mixamo Sit/Stand",
                    "No sit/stand clips found under " + MixamoFolder +
                    ".\nDrop Mixamo FBX (names containing Sit/Sitting and Stand/StandingUp), then run again.",
                    "OK");
                PingPath(MixamoFolder);
                return;
            }

            int patched = 0;
            foreach (string path in PackageControllerPaths)
            {
                AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
                if (controller == null)
                {
                    Debug.LogWarning("Mixamo setup: missing package controller " + path);
                    continue;
                }

                EnsureTriggerParameters(controller, new[] { "sit", "stand" });
                AnimatorStateMachine machine = controller.layers[0].stateMachine;

                if (sitClip != null)
                {
                    AnimatorState sitState = FindOrCreateState(machine, "sit", new Vector3(270f, 340f, 0f));
                    sitState.motion = sitClip;
                    EnsureAnyStateTriggerTransition(machine, sitState, "sit");
                }

                if (standClip != null)
                {
                    AnimatorState standState = FindOrCreateState(machine, "stand", new Vector3(270f, 410f, 0f));
                    standState.motion = standClip;
                    EnsureAnyStateTriggerTransition(machine, standState, "stand");
                }

                EditorUtility.SetDirty(controller);
                patched++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog(
                "Package Controllers Patched",
                "Updated " + patched + " Low Poly People controller(s) with sit/stand.\n" +
                "Pathing: SuspectEnterController already calls sit on chair arrival and stand on exit.\n" +
                "Sit clip: " + (sitClip != null ? sitClip.name : "(skipped)") + "\n" +
                "Stand clip: " + (standClip != null ? standClip.name : "(skipped)"),
                "OK");
        }

        private static void ConfigureSelectedModels(ModelImporterAnimationType animationType, Avatar sourceAvatar)
        {
            Object[] targets = Selection.objects;
            if (targets == null || targets.Length == 0)
            {
                EditorUtility.DisplayDialog(
                    "Mixamo Import",
                    "Select one or more Mixamo FBX assets under " + MixamoFolder + " first.",
                    "OK");
                return;
            }

            int configured = 0;
            foreach (Object obj in targets)
            {
                string path = AssetDatabase.GetAssetPath(obj);
                if (string.IsNullOrEmpty(path) || !path.EndsWith(".fbx", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                    continue;

                importer.animationType = animationType;
                importer.importAnimation = true;

                if (animationType == ModelImporterAnimationType.Human)
                {
                    if (sourceAvatar != null)
                    {
                        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                        importer.sourceAvatar = sourceAvatar;
                    }
                    else
                    {
                        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    }
                }
                else
                {
                    importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                }

                ApplyClipLoopHints(importer, path);
                importer.SaveAndReimport();
                configured++;
            }

            EditorUtility.DisplayDialog(
                "Mixamo Import",
                "Configured " + configured + " FBX as " + animationType +
                ".\nLoop: sit/idle/walk usually loop; stand-up does not.",
                "OK");
        }

        private static void ApplyClipLoopHints(ModelImporter importer, string assetPath)
        {
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            if (clips == null || clips.Length == 0)
                clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
                return;

            string role = GuessRoleFromName(Path.GetFileNameWithoutExtension(assetPath));
            bool loop = role == "sit" || role == "idle" || role == "walk" || role == "run";

            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].loopTime = loop;
                // Prefer in-place for chair snaps — pathing moves the root Transform itself.
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
            }

            importer.clipAnimations = clips;
        }

        private static Avatar PromptOptionalSourceAvatar()
        {
            string[] guids = AssetDatabase.FindAssets("t:Avatar", new[] { "Assets/DavidJalbert/LowPolyPeople" });
            if (guids == null || guids.Length == 0)
            {
                // Package ships Generic with empty Avatar — Humanoid Option B needs Magdy to
                // convert a body FBX to Humanoid first, then re-run with that avatar selected.
                EditorUtility.DisplayDialog(
                    "Humanoid Avatar",
                    "No Avatar found under Low Poly People yet (package is Generic by default).\n\n" +
                    "For Option B: set one body FBX (e.g. normal-man-a) to Humanoid → Create From This Model, " +
                    "apply, then select your Mixamo FBX and run this again after selecting that Avatar in the Project window.\n\n" +
                    "Continuing with Create From This Model on the selected Mixamo FBX.",
                    "OK");
                return null;
            }

            Avatar selected = Selection.activeObject as Avatar;
            if (selected != null)
                return selected;

            string firstPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<Avatar>(firstPath);
        }

        private static Dictionary<string, AnimationClip> FindClipsByRole(string folder)
        {
            var result = new Dictionary<string, AnimationClip>();
            if (!AssetDatabase.IsValidFolder(folder))
                return result;

            string[] guids = AssetDatabase.FindAssets("t:AnimationClip", new[] { folder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clip == null || clip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                    continue;

                string role = GuessRoleFromName(clip.name);
                if (role == null)
                    role = GuessRoleFromName(Path.GetFileNameWithoutExtension(path));
                if (role == null)
                    continue;

                // Prefer the first hit; later duplicates are ignored.
                if (!result.ContainsKey(role))
                    result[role] = clip;
            }

            return result;
        }

        private static string GuessRoleFromName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;

            string n = name.ToLowerInvariant().Replace(" ", "").Replace("_", "").Replace("-", "");

            if (n.Contains("standingup") || n.Contains("standup") || (n.Contains("stand") && !n.Contains("sitting")))
                return "stand";
            if (n.Contains("sitting") || n.Contains("sitidle") || n.Contains("sit"))
                return "sit";
            if (n.Contains("walk"))
                return "walk";
            if (n.Contains("idle"))
                return "idle";
            if (n.Contains("run"))
                return "run";
            if (n.Contains("wave"))
                return "wave";
            return null;
        }

        private static void EnsureTriggerParameters(AnimatorController controller, IEnumerable<string> names)
        {
            foreach (string name in names)
            {
                if (controller.parameters.Any(p => p.name == name))
                    continue;
                controller.AddParameter(name, AnimatorControllerParameterType.Trigger);
            }
        }

        private static AnimatorState FindOrCreateState(AnimatorStateMachine machine, string stateName, Vector3 position)
        {
            foreach (ChildAnimatorState child in machine.states)
            {
                if (child.state != null && child.state.name == stateName)
                    return child.state;
            }

            return machine.AddState(stateName, position);
        }

        private static void AddAnyStateTriggerTransition(AnimatorStateMachine machine, AnimatorState state, string trigger)
        {
            AnimatorStateTransition transition = machine.AddAnyStateTransition(state);
            transition.AddCondition(AnimatorConditionMode.If, 0, trigger);
            transition.hasExitTime = false;
            transition.duration = 0.15f;
            transition.canTransitionToSelf = false;
        }

        private static void EnsureAnyStateTriggerTransition(AnimatorStateMachine machine, AnimatorState state, string trigger)
        {
            foreach (AnimatorStateTransition transition in machine.anyStateTransitions)
            {
                if (transition == null || transition.destinationState != state)
                    continue;
                if (transition.conditions.Any(c => c.mode == AnimatorConditionMode.If && c.parameter == trigger))
                    return;
            }

            AddAnyStateTriggerTransition(machine, state, trigger);
        }

        private static void ClearGeneratedAnyStateGraph(AnimatorStateMachine machine)
        {
            // Rebuild a clean Any-State graph for the four pathing triggers only.
            AnimatorStateTransition[] existing = machine.anyStateTransitions;
            for (int i = existing.Length - 1; i >= 0; i--)
                machine.RemoveTransition(existing[i]);

            foreach (string trigger in PathingTriggers)
            {
                ChildAnimatorState[] states = machine.states;
                for (int i = states.Length - 1; i >= 0; i--)
                {
                    if (states[i].state != null && states[i].state.name == trigger)
                        machine.RemoveState(states[i].state);
                }
            }
        }

        private static void EnsureMixamoFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Animations"))
                AssetDatabase.CreateFolder("Assets", "Animations");
            if (!AssetDatabase.IsValidFolder(MixamoFolder))
                AssetDatabase.CreateFolder("Assets/Animations", "Mixamo");
        }

        private static void PingPath(string path)
        {
            Object obj = AssetDatabase.LoadAssetAtPath<Object>(path);
            if (obj != null)
            {
                Selection.activeObject = obj;
                EditorGUIUtility.PingObject(obj);
            }
        }
    }
}
#endif
