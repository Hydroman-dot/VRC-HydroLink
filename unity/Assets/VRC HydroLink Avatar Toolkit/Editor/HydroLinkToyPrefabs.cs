#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;

namespace HydroLink.AvatarToolkit.Editor
{
    public static class HydroLinkToyPrefabs
    {
        public const string Folder = "Assets/VRC HydroLink Avatar Toolkit/Toys";
        public static string Label(string toy) { CheckToy(toy); return toy == "Max2" ? "Max 2" : "Ridge"; }
        public static string ObjectName(string toy) => "HydroLink " + Label(toy);
        public static string AssetPath(string toy) => Folder + "/" + toy + "/" + ObjectName(toy) + ".prefab";
        public static string Prefix(string toy) { CheckToy(toy); return "HydroLink/" + toy + "/"; }
        public static string[] Channels(string toy) => toy == "Max2" ? new[] { "Enabled", "Vibration", "Pump", "Manual", "ManualVibration", "ManualPump", "Stop" } : new[] { "Enabled", "Vibration", "Rotation", "Clockwise", "Manual", "ManualVibration", "ManualRotation", "ManualClockwise", "Stop" };
        static void CheckToy(string toy) { if (toy != "Max2" && toy != "Ridge") throw new ArgumentException("Unbekanntes Toy."); }
        static bool IsBool(string key) => key == "Enabled" || key == "Stop" || key.EndsWith("Clockwise") || key == "Manual";
        static float Default(string key) => key == "Enabled" || key == "Stop" || key == "Manual" ? 0 : key.EndsWith("Clockwise") ? 1 : .25f;
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var i = path.LastIndexOf('/'); EnsureFolder(path.Substring(0, i));
            AssetDatabase.CreateFolder(path.Substring(0, i), path.Substring(i + 1));
        }
        static void CheckRoot(GameObject root)
        {
            if (!root || EditorUtility.IsPersistent(root) || !root.scene.IsValid() ||
                !root.GetComponent<VRCAvatarDescriptor>() || root.GetComponentsInChildren<VRCAvatarDescriptor>(true).Length != 1)
                throw new InvalidOperationException("Avatar-Hauptobjekt aus der Szene auswaehlen.");
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Bitte den Edit-Modus und die Kompilierung abwarten.");
        }
        static GameObject Existing(GameObject root, string toy)
        {
            var prefix = Prefix(toy);
            var candidates = root.GetComponentsInChildren<ModularAvatarParameters>(true)
                .Where(m => m.parameters.Any(p => (p.nameOrPrefix ?? "").StartsWith(prefix, StringComparison.Ordinal)))
                .Select(m => m.gameObject).Distinct().ToArray();
            if (candidates.Length > 1) throw new InvalidOperationException("Doppelte " + Label(toy) + "-Parameter: zuerst Duplikat entfernen.");
            var named = root.transform.Find(ObjectName(toy));
            if (named && (candidates.Length == 0 || candidates[0] != named.gameObject))
                throw new InvalidOperationException("Gleichnamiges fremdes Objekt: " + ObjectName(toy));
            if (candidates.Length == 0) return null;
            var existing = candidates[0];
            if (PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(existing) != AssetPath(toy))
                throw new InvalidOperationException("Vorhandene " + Label(toy) + "-Parameter gehoeren nicht zum Toy-Prefab.");
            ValidatePrefab(existing, toy, true);
            return existing;
        }
        static void Preflight(GameObject root, string toy)
        {
            var existing = Existing(root, toy);
            var names = new HashSet<string>(Channels(toy).Select(k => Prefix(toy) + k));
            Func<Component, bool> outside = c => !existing || !c.transform.IsChildOf(existing.transform);
            foreach (var ma in root.GetComponentsInChildren<ModularAvatarParameters>(true).Where(c => outside(c)))
                foreach (var p in ma.parameters)
                    if (names.Contains(p.nameOrPrefix ?? "") || names.Contains(p.remapTo ?? ""))
                        throw new InvalidOperationException("Parameterkollision fuer " + Label(toy));
            foreach (var ma in root.GetComponentsInParent<ModularAvatarParameters>(true))
                if (ma.parameters.Any(p => p.isPrefix && names.Any(n => n.StartsWith(p.nameOrPrefix ?? "", StringComparison.Ordinal))))
                    throw new InvalidOperationException("Uebergeordnete MA-Praefix-Regel wuerde Toy-Parameter umbenennen.");
            var d = root.GetComponent<VRCAvatarDescriptor>();
            if (d.expressionParameters && (d.expressionParameters.parameters ?? Array.Empty<VRCExpressionParameters.Parameter>()).Any(p => names.Contains(p.name)))
                throw new InvalidOperationException("Toy-Parameter sind bereits im Avatar-Parameterasset vorhanden.");
            var controllers = (d.baseAnimationLayers ?? Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>()).Concat(d.specialAnimationLayers ?? Array.Empty<VRCAvatarDescriptor.CustomAnimLayer>()).Select(l => l.animatorController)
                .Concat(root.GetComponentsInChildren<ModularAvatarMergeAnimator>(true).Where(c => outside(c)).Select(m => m.animator));
            foreach (var ac in controllers.OfType<AnimatorController>())
                if (ac.parameters.Any(p => names.Contains(p.name))) throw new InvalidOperationException("Toy-Parameter sind bereits in einem anderen Controller vorhanden.");
        }
        static VRCExpressionsMenu Menu(string path)
        {
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.controls = new List<VRCExpressionsMenu.Control>();
            AssetDatabase.CreateAsset(menu, path); return menu;
        }
        public static GameObject EnsurePrefab(string toy)
        {
            CheckToy(toy);
            var path = AssetPath(toy);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset) { ValidatePrefab(asset, toy, true); UpgradeManual(toy); asset = AssetDatabase.LoadAssetAtPath<GameObject>(path); ValidatePrefab(asset, toy); return asset; }
            var directory = Folder + "/" + toy;
            if (AssetDatabase.IsValidFolder(directory)) throw new InvalidOperationException("Unvollstaendiger Toy-Ordner: " + directory + ". Keine Dateien ueberschrieben.");
            EnsureFolder(directory);
            GameObject go = null;
            try
            {
                var controls = Menu(directory + "/Controls.asset");
                BuildMenus(toy, controls);
                var menu = Menu(directory + "/Menu.asset");
                menu.controls.Add(new VRCExpressionsMenu.Control { name = ObjectName(toy), type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = controls });
                var controller = AnimatorController.CreateAnimatorControllerAtPath(directory + "/Toy.controller");
                foreach (var key in Channels(toy))
                    controller.AddParameter(new AnimatorControllerParameter { name = Prefix(toy) + key,
                        type = IsBool(key) ? AnimatorControllerParameterType.Bool : AnimatorControllerParameterType.Float,
                        defaultBool = Default(key) > 0, defaultFloat = Default(key) });
                var state = controller.layers[0].stateMachine.AddState("Toy menu parameters");
                state.writeDefaultValues = false; controller.layers[0].stateMachine.defaultState = state;
                go = new GameObject(ObjectName(toy));
                var parameters = go.AddComponent<ModularAvatarParameters>();
                parameters.parameters = Channels(toy).Select(key => new ParameterConfig {
                    nameOrPrefix = Prefix(toy) + key, syncType = IsBool(key) ? ParameterSyncType.Bool : ParameterSyncType.Float,
                    localOnly = true, saved = false, internalParameter = false, hasExplicitDefaultValue = true, defaultValue = Default(key)
                }).ToList();
                go.AddComponent<ModularAvatarMenuInstaller>().menuToAppend = menu;
                var merge = go.AddComponent<ModularAvatarMergeAnimator>();
                merge.animator = controller; merge.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
                merge.mergeAnimatorMode = MergeAnimatorMode.Append; merge.pathMode = MergeAnimatorPathMode.Absolute;
                merge.matchAvatarWriteDefaults = false;
                EditorUtility.SetDirty(menu); EditorUtility.SetDirty(controls); EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                asset = PrefabUtility.SaveAsPrefabAsset(go, path);
                ValidatePrefab(asset, toy);
                return asset;
            }
            catch { AssetDatabase.DeleteAsset(directory); throw; }
            finally { if (go) UnityEngine.Object.DestroyImmediate(go); }
        }
        public static string Install(GameObject root, bool max2, bool ridge)
        {
            CheckRoot(root);
            var selected = new[] { max2 ? "Max2" : null, ridge ? "Ridge" : null }.Where(s => s != null).ToArray();
            if (selected.Length == 0) throw new InvalidOperationException("Mindestens ein Toy auswaehlen.");
            foreach (var toy in selected) Preflight(root, toy);
            foreach (var toy in selected) EnsurePrefab(toy);
            var created = new List<GameObject>();
            Undo.IncrementCurrentGroup(); var group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("HydroLink Toy-Prefabs einrichten");
            try
            {
                foreach (var toy in selected)
                {
                    if (Existing(root, toy)) continue;
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath(toy)), root.transform);
                    created.Add(instance); Undo.RegisterCreatedObjectUndo(instance, "HydroLink " + toy);
                    instance.transform.localPosition = Vector3.zero; instance.transform.localRotation = Quaternion.identity; instance.transform.localScale = Vector3.one;
                }
                var report = Validate(root);
                if (created.Count > 0) EditorSceneManager.MarkSceneDirty(root.scene);
                var syncPath = PublishToHydroLink(root);
                Undo.CollapseUndoOperations(group);
                return report + "\nPrefab-Dateien: " + Folder + "\nUnity-Einrichtung lokal gespeichert. VRChat-Avatar und Toy-Verbindung bitte in HydroLink pruefen.\nDatei: " + syncPath;
            }
            catch { foreach (var go in created) if (go) Undo.DestroyObjectImmediate(go); throw; }
        }

        [Serializable] public sealed class SyncProfile
        {
            public int schemaVersion = 1;
            public string revision, avatarId, avatarName;
            public string[] toys;
            public int plugs, sockets;
        }
        public static string PublishToHydroLink(GameObject root)
        {
            Validate(root);
            var pipeline = root.GetComponents<Component>().FirstOrDefault(c => c && c.GetType().Name == "PipelineManager");
            var id = pipeline ? new SerializedObject(pipeline).FindProperty("blueprintId")?.stringValue ?? "" : "";
            var sources = HydroLinkSetupWizard.Scan(root);
            var profile = new SyncProfile {
                revision = Guid.NewGuid().ToString(), avatarId = id, avatarName = root.name,
                toys = new[] { "Max2", "Ridge" }.Where(t => { var go = Existing(root, t); return go && go.activeInHierarchy; }).ToArray(),
                plugs = sources.Count(s => s.kind == "Plug"), sockets = sources.Count(s => s.kind == "Socket")
            };
            var folder = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VRC HydroLink", "WizardSync");
            return SaveProfile(profile, folder);
        }
        static bool ValidId(string id) => !string.IsNullOrEmpty(id) && System.Text.RegularExpressions.Regex.IsMatch(id, @"\Aavtr_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\z");
        public static string SaveProfile(SyncProfile profile, string folder)
        {
            if (!string.IsNullOrEmpty(profile.avatarId) && !ValidId(profile.avatarId)) throw new InvalidOperationException("Ungueltige Avatar-ID.");
            System.IO.Directory.CreateDirectory(folder);
            var archive = System.IO.Path.Combine(folder, "Avatars");
            System.IO.Directory.CreateDirectory(archive);
            var latest = System.IO.Path.Combine(folder, "active.json");
            // Preserve the previous single-avatar handoff when upgrading.
            if (System.IO.File.Exists(latest) && new System.IO.FileInfo(latest).Length <= 65536)
            {
                try {
                    var previous = JsonUtility.FromJson<SyncProfile>(System.IO.File.ReadAllText(latest));
                    if (previous != null && previous.schemaVersion == 1 && ValidId(previous.avatarId)) {
                        var oldPath = System.IO.Path.Combine(archive, previous.avatarId + ".json");
                        if (!System.IO.File.Exists(oldPath)) WriteProfile(previous, oldPath);
                    }
                } catch (ArgumentException) { /* A malformed legacy handoff is not migrated. */ }
            }
            var target = ValidId(profile.avatarId) ? System.IO.Path.Combine(archive, profile.avatarId + ".json") : latest;
            WriteProfile(profile, target);
            if (target != latest) WriteProfile(profile, latest);
            return target;
        }
        static void WriteProfile(SyncProfile profile, string target)
        {
            var temp = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(target), "handoff-" + Guid.NewGuid().ToString("N") + ".tmp");
            try {
                System.IO.File.WriteAllText(temp, JsonUtility.ToJson(profile, true), new System.Text.UTF8Encoding(false));
                if (System.IO.File.Exists(target)) System.IO.File.Replace(temp, target, null);
                else System.IO.File.Move(temp, target);
            }
            finally { if (System.IO.File.Exists(temp)) System.IO.File.Delete(temp); }
        }

        public static string Validate(GameObject root)
        {
            CheckRoot(root);
            var lines = new List<string>();
            foreach (var toy in new[] { "Max2", "Ridge" })
            {
                var instance = Existing(root, toy);
                if (!instance) { lines.Add(Label(toy) + ": nicht installiert"); continue; }
                Preflight(root, toy);
                lines.Add("PASS " + Label(toy) + ": eigene Prefab-Instanz, " + Channels(toy).Length + " lokale Parameter, eigenes Menue und FX-Ergaenzung.");
            }
            return string.Join("\n", lines) + "\nDesktop 0.9 erforderlich fuer Toy-Menues. Alle SPS-Plugs und Sockets bleiben automatisch nutzbar.";
        }

        static VRCExpressionsMenu.Control Control(string toy, string key)
        {
            var label = key == "Enabled" ? "Haptik einschalten" : key == "Manual" ? "Manuell einschalten" : key == "Stop" ? "Toy stoppen" : key.EndsWith("Clockwise") ? "Im Uhrzeigersinn" : key.Replace("Manual", "");
            var c = new VRCExpressionsMenu.Control { name = label };
            if (IsBool(key)) { c.type = key == "Stop" ? VRCExpressionsMenu.Control.ControlType.Button : VRCExpressionsMenu.Control.ControlType.Toggle; c.parameter = new VRCExpressionsMenu.Control.Parameter { name = Prefix(toy) + key }; c.value = 1; }
            else { c.type = VRCExpressionsMenu.Control.ControlType.RadialPuppet; c.subParameters = new[] { new VRCExpressionsMenu.Control.Parameter { name = Prefix(toy) + key } }; }
            return c;
        }
        static void BuildMenus(string toy, VRCExpressionsMenu controls)
        {
            var directory = Folder + "/" + toy;
            var spsPath = directory + "/SpsStrengths.asset";
            var manualPath = directory + "/ManualControls.asset";
            if (AssetDatabase.LoadMainAssetAtPath(spsPath) || AssetDatabase.LoadMainAssetAtPath(manualPath))
                throw new InvalidOperationException("Untermenue-Assets existieren bereits; bitte Einrichtung pruefen.");
            var strengths = Menu(spsPath); var manual = Menu(manualPath);
            strengths.controls = Channels(toy).Where(k => k != "Enabled" && k != "Stop" && !k.StartsWith("Manual")).Select(k => Control(toy,k)).ToList();
            manual.controls = Channels(toy).Where(k => k.StartsWith("Manual")).Select(k => Control(toy,k)).ToList();
            controls.controls = new List<VRCExpressionsMenu.Control> { Control(toy,"Enabled"),
                new VRCExpressionsMenu.Control { name = "Haptik-Staerken", type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = strengths },
                new VRCExpressionsMenu.Control { name = "Manuell", type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = manual }, Control(toy,"Stop") };
            EditorUtility.SetDirty(strengths); EditorUtility.SetDirty(manual); EditorUtility.SetDirty(controls);
        }
        static void UpgradeManual(string toy)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetPath(toy));
            if (asset.GetComponent<ModularAvatarParameters>().parameters.Any(p => p.nameOrPrefix == Prefix(toy) + "Enabled")) return;
            var go = PrefabUtility.LoadPrefabContents(AssetPath(toy));
            try
            {
                var ma = go.GetComponent<ModularAvatarParameters>();
                var ac = go.GetComponent<ModularAvatarMergeAnimator>().animator as AnimatorController;
                var menu = go.GetComponent<ModularAvatarMenuInstaller>().menuToAppend.controls.Single().subMenu;
                var own = Folder + "/" + toy + "/";
                if (!AssetDatabase.GetAssetPath(ac).StartsWith(own, StringComparison.Ordinal) || !AssetDatabase.GetAssetPath(menu).StartsWith(own, StringComparison.Ordinal))
                    throw new InvalidOperationException("Fremde Toy-Assets werden nicht aktualisiert.");
                foreach (var key in Channels(toy).Where(k => !ma.parameters.Any(p => p.nameOrPrefix == Prefix(toy) + k)))
                {
                    ma.parameters.Add(new ParameterConfig { nameOrPrefix = Prefix(toy) + key, syncType = IsBool(key) ? ParameterSyncType.Bool : ParameterSyncType.Float,
                        localOnly = true, saved = false, internalParameter = false, hasExplicitDefaultValue = true, defaultValue = Default(key) });
                    ac.AddParameter(new AnimatorControllerParameter { name = Prefix(toy) + key, type = IsBool(key) ? AnimatorControllerParameterType.Bool : AnimatorControllerParameterType.Float, defaultBool = Default(key)>0, defaultFloat = Default(key) });
                }
                BuildMenus(toy, menu);
                EditorUtility.SetDirty(ac); EditorUtility.SetDirty(menu);
                ValidatePrefab(go, toy);
                PrefabUtility.SaveAsPrefabAsset(go, AssetPath(toy)); AssetDatabase.SaveAssets();
            }
            finally { PrefabUtility.UnloadPrefabContents(go); }
        }
        public static void ValidatePrefab(GameObject go, string toy, bool allowLegacy = false)
        {
            var component = go.GetComponent<ModularAvatarParameters>();
            var legacy = allowLegacy && component && !component.parameters.Any(p => p.nameOrPrefix == Prefix(toy) + "Enabled");
            var keys = Channels(toy).Where(k => !legacy || k != "Enabled" && (!k.StartsWith("Manual") || k == "Manual" && component.parameters.Any(p => p.nameOrPrefix == Prefix(toy) + "Manual"))).ToArray();
            var expected = keys.Select(k => Prefix(toy) + k).ToArray();
            var ma = go.GetComponent<ModularAvatarParameters>();
            var merge = go.GetComponent<ModularAvatarMergeAnimator>();
            var installer = go.GetComponent<ModularAvatarMenuInstaller>();
            if (!ma || !merge || !installer || !installer.menuToAppend || go.transform.childCount != 0 ||
                go.GetComponents<Component>().Any(c => !c || !(c is Transform || c is ModularAvatarParameters || c is ModularAvatarMergeAnimator || c is ModularAvatarMenuInstaller)))
                throw new InvalidOperationException("Unerwartete Toy-Prefab-Komponenten.");
            if (ma.parameters.Count != expected.Length || !ma.parameters.Select(p => p.nameOrPrefix).OrderBy(n => n).SequenceEqual(expected.OrderBy(n => n)))
                throw new InvalidOperationException("Toy-Parameter fehlen oder sind doppelt.");
            foreach (var key in keys)
            {
                var p = ma.parameters.Single(v => v.nameOrPrefix == Prefix(toy) + key);
                if (!p.localOnly || p.saved || p.internalParameter || p.isPrefix || !string.IsNullOrEmpty(p.remapTo) ||
                    !p.hasExplicitDefaultValue || p.defaultValue != Default(key) || p.syncType != (IsBool(key) ? ParameterSyncType.Bool : ParameterSyncType.Float))
                    throw new InvalidOperationException("Unerwartete Toy-Parameterkonfiguration.");
            }
            var ac = merge.animator as AnimatorController;
            if (!ac || ac.animationClips.Length != 0 || ac.parameters.Length != expected.Length ||
                !ac.parameters.Select(p => p.name).OrderBy(n => n).SequenceEqual(expected.OrderBy(n => n)) ||
                merge.layerType != VRCAvatarDescriptor.AnimLayerType.FX || merge.mergeAnimatorMode != MergeAnimatorMode.Append)
                throw new InvalidOperationException("Toy-FX muss ausschliesslich eigene Parameter ergaenzen.");
            var menu = installer.menuToAppend;
            if (menu.controls.Count != 1 || !menu.controls[0].subMenu) throw new InvalidOperationException("Toy-Menue unvollstaendig.");
            var controls = new List<VRCExpressionsMenu.Control>();
            var visited = new HashSet<VRCExpressionsMenu>();
            var queue = new Queue<VRCExpressionsMenu>(); queue.Enqueue(menu.controls[0].subMenu);
            while (queue.Count > 0) {
                var current = queue.Dequeue();
                if (!visited.Add(current) || current.controls.Count > 8) throw new InvalidOperationException("Toy-Menue doppelt oder zu gross.");
                foreach (var c in current.controls) {
                    if (c.type == VRCExpressionsMenu.Control.ControlType.SubMenu) { if (!c.subMenu) throw new InvalidOperationException("Untermenue fehlt."); queue.Enqueue(c.subMenu); }
                    else controls.Add(c);
                }
            }
            if ((!legacy && visited.Count != 3) || controls.Count != expected.Length) throw new InvalidOperationException("Toy-Menue hat falsche Anzahl Eintraege.");
            var references = controls.SelectMany(c => c.type == VRCExpressionsMenu.Control.ControlType.RadialPuppet
                ? (c.subParameters ?? Array.Empty<VRCExpressionsMenu.Control.Parameter>()).Select(p => p.name)
                : new[] { c.parameter == null ? "" : c.parameter.name }).ToArray();
            if (!references.OrderBy(n => n).SequenceEqual(expected.OrderBy(n => n))) throw new InvalidOperationException("Toy-Menue verweist auf fremde Parameter.");
            if (controls.Single(c => c.parameter != null && c.parameter.name == Prefix(toy) + "Stop").type != VRCExpressionsMenu.Control.ControlType.Button)
                throw new InvalidOperationException("Toy-Stopp muss ein Taster sein.");
        }
    }
}
#endif
