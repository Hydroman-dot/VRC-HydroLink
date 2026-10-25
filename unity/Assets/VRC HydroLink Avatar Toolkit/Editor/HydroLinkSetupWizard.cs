#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using nadena.dev.modular_avatar.core;

namespace HydroLink.AvatarToolkit.Editor
{
    // Editor-only authoring: the installed avatar contains only SDK and MA components.
    public sealed class HydroLinkSetupWizard : EditorWindow
    {
        public const string Folder = "Assets/VRC HydroLink Avatar Toolkit";
        public const string AddonName = "HydroLink Bridge Preview";
        const string Prefix = "HydroLink/";
        [SerializeField] GameObject avatar;
        [SerializeField] bool installDolce, installGemini, installNora, installGravity, installLushMini;
        [SerializeField] bool installMax2 = true, installRidge = true, installSolacePro = false;
        bool legacyExpanded;
        Vector2 scroll;
        string report = "Avatar auswaehlen und pruefen.";
        string lastFolder;
        static readonly string[] Names = { "Enabled", "Mode", "Strength", "Vibration", "Pump", "Rotation", "Clockwise", "Motion" };

        [Serializable] public sealed class SourceInfo
        {
            public string kind, path, displayName, explicitOscId, candidatePrefix;
            public string[] expectedSignals;
            public bool spsEnabled;
            public bool active, runtimeVerified;
            public string note;
        }
        [Serializable] public sealed class ParameterInfo
        {
            public string name, address, type;
            public float defaultValue;
            public bool localOnly = true, saved = false;
        }
        [Serializable] public sealed class DeviceProfile
        {
            public string id, label;
            public string[] requestedCapabilities;
            public string note;
        }
        [Serializable] public sealed class Contract
        {
            public int schemaVersion = 3;
            public string toolkitVersion = "0.4.0-preview";
            public string avatarName;
            public string defaultMode = "Together";
            public string status = "HydroLink 0.4 supports explicit capability selection, common routing and SPS Plug/Socket sources. Physical devices require verification.";
            public string discovery = "Resolve actual addresses and types from VRChat OSCQuery after build; source prefixes below are candidates, not verified routes.";
            public string togetherPolicy = "Send the shared normalized input only to explicitly selected devices and only to their advertised compatible capabilities. Never substitute Constrict for Rotate.";
            public string activationPolicy = "Disabled on startup, avatar change and reconnect. Explicit desktop output release is required. This Unity preview never starts hardware.";
            public bool existingMax2;
            public string[] legacyOutputs;
            public ParameterInfo[] parameters;
            public DeviceProfile[] profiles;
            public SourceInfo[] sockets;
            public SourceInfo[] plugs;
            public SourceInfo[] sources;
            public string sourcePolicy = "Both SPS Plug and SPS Socket are supported source kinds. Select sources explicitly after OSCQuery verification. Plug PenSelf/PenOthers and Socket NewRoot/NewTip are different measurements; never route them interchangeably or sum mirrored interactions.";
        }

        [MenuItem("Tools/HydroLink/Avatar Setup Wizard")]
        public static void Open()
        {
            var window = GetWindow<HydroLinkSetupWizard>("HydroLink Setup");
            window.minSize = new Vector2(540, 470);
            var selected = Selection.activeGameObject;
            var descriptor = selected ? selected.GetComponentInParent<VRCAvatarDescriptor>() : null;
            if (descriptor) { window.avatar = descriptor.gameObject; window.report = Inspect(window.avatar); }
            window.Show();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("VRC HydroLink Avatar Toolkit", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Unity-Einrichtung: Der Wizard speichert jeden Avatar getrennt. HydroLink waehlt beim Avatarwechsel automatisch die passende Einrichtung. VRChat-Avatar und Geraeteverbindung werden erst in der Desktop-App separat geprueft.", MessageType.Info);
            avatar = (GameObject)EditorGUILayout.ObjectField("Avatar", avatar, typeof(GameObject), true);
            installMax2 = EditorGUILayout.ToggleLeft("Max 2 - SPS / Manuell, Vibration, Pump und Stopp", installMax2);
            installRidge = EditorGUILayout.ToggleLeft("Ridge - SPS / Manuell, Vibration, Rotation, Richtung und Stopp", installRidge);
            installSolacePro = EditorGUILayout.ToggleLeft("Solace Pro - SPS / Manuell, Geschwindigkeit, Hubweg, Position, Fahrzeit und Stopp", installSolacePro);
            installDolce = EditorGUILayout.ToggleLeft("Dolce - Motor 1 / 2 - SPS / Manuell und Stopp", installDolce);
            installGemini = EditorGUILayout.ToggleLeft("Gemini - Motor 1 / 2 - SPS / Manuell und Stopp", installGemini);
            installNora = EditorGUILayout.ToggleLeft("Nora - Vibration / Rotation / Richtung - SPS / Manuell und Stopp", installNora);
            installGravity = EditorGUILayout.ToggleLeft("Gravity - Vibration / Hubgeschwindigkeit - SPS / Manuell und Stopp", installGravity);
            installLushMini = EditorGUILayout.ToggleLeft("Lush Mini - Vibration - SPS / Manuell und Stopp", installLushMini);
            using (new EditorGUI.DisabledScope(!avatar || EditorApplication.isCompiling || EditorApplication.isPlayingOrWillChangePlaymode))
            {
                if (GUILayout.Button("1. Avatar und SPS pruefen")) Run(() => report = Inspect(avatar));
                if (GUILayout.Button("2. Toy-Prefabs anlegen und automatisch uebergeben")) Run(() => { report = HydroLinkToyPrefabs.Install(avatar, installMax2, installRidge, installSolacePro, new[] { installDolce ? "Dolce" : null, installGemini ? "Gemini" : null, installNora ? "Nora" : null, installGravity ? "Gravity" : null, installLushMini ? "LushMini" : null }.Where(t => t != null).ToArray()); lastFolder = HydroLinkToyPrefabs.Folder; });
                if (GUILayout.Button("3. Toy-Prefabs pruefen")) Run(() => report = HydroLinkToyPrefabs.Validate(avatar));
                if (GUILayout.Button("Vorhandene Einrichtung erneut an HydroLink uebergeben")) Run(() => report = "Unity-Einrichtung: lokal fuer HydroLink gespeichert.\nVRChat-Avatar: wird in der Desktop-App geprueft.\nToy / Intiface: wird in der Desktop-App geprueft.\n\nDatei: " + HydroLinkToyPrefabs.PublishToHydroLink(avatar));
                legacyExpanded = EditorGUILayout.Foldout(legacyExpanded, "Erweitert: bisherige gemeinsame Menue-Einrichtung");
                if (legacyExpanded)
                {
                    if (GUILayout.Button("Gemeinsames altes Menue einrichten")) Run(() => { lastFolder = Install(avatar); report = Validate(avatar); });
                    if (GUILayout.Button("Altes Plug-/Socket-Inventar aktualisieren")) Run(() => { RefreshInventory(avatar); report = Inspect(avatar); });
                }
            }
            EditorGUILayout.LabelField("Erneutes Anlegen ergaenzt nur fehlende Prefabs. Abgewaehlte Toys werden nicht entfernt.", EditorStyles.wordWrappedLabel);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            EditorGUILayout.TextArea(report, GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            if (!string.IsNullOrEmpty(lastFolder) && GUILayout.Button("Erzeugte Dateien anzeigen"))
                EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(lastFolder));
        }
        void Run(Action action)
        {
            try { action(); } catch (Exception ex) { report = ex.Message; Debug.LogException(ex); }
        }
        static VRCAvatarDescriptor Descriptor(GameObject root)
        {
            if (!root || EditorUtility.IsPersistent(root) || !root.scene.IsValid())
                throw new InvalidOperationException("Bitte einen Avatar aus einer geladenen Szene auswaehlen.");
            var d = root.GetComponent<VRCAvatarDescriptor>();
            if (!d) throw new InvalidOperationException("Bitte das Avatar-Hauptobjekt mit VRCAvatarDescriptor auswaehlen.");
            if (root.GetComponentsInChildren<VRCAvatarDescriptor>(true).Length != 1)
                throw new InvalidOperationException("Verschachtelte Avatar-Deskriptoren: zuerst den Zielavatar eindeutig waehlen.");
            return d;
        }
        public static SourceInfo[] Scan(GameObject root)
        {
            Descriptor(root);
            var sources = new List<SourceInfo>();
            foreach (var c in root.GetComponentsInChildren<Component>(true))
            {
                if (!c) continue;
                var type = c.GetType().FullName;
                var plug = type == "VF.Component.VRCFuryHapticPlug";
                if (!plug && type != "VF.Component.VRCFuryHapticSocket") continue;
                var so = new SerializedObject(c);
                var name = so.FindProperty("name")?.stringValue ?? "";
                var id = plug ? name : so.FindProperty("oscId")?.stringValue ?? "";
                var candidate = string.IsNullOrWhiteSpace(id) ? name : id;
                sources.Add(new SourceInfo {
                    kind = plug ? "Plug" : "Socket",
                    path = AnimationUtility.CalculateTransformPath(c.transform, root.transform),
                    displayName = string.IsNullOrWhiteSpace(name) ? c.gameObject.name : name,
                    explicitOscId = id, active = c.gameObject.activeInHierarchy,
                    spsEnabled = !plug || (so.FindProperty("enableSps")?.boolValue ?? false),
                    candidatePrefix = string.IsNullOrWhiteSpace(candidate) ? "" : "/avatar/parameters/OGB/" + (plug ? "Pen/" : "Orf/") + candidate.Replace('/', '_') + "/",
                    expectedSignals = plug
                        ? new[] { "TouchSelf", "TouchOthers", "TouchSelfClose", "TouchOthersClose", "PenSelf", "PenOthers", "FrotOthers", "FrotOthersClose" }
                        : new[] { "PenSelfNewRoot", "PenOthersNewRoot", "PenSelfNewTip", "PenOthersNewTip" },
                    runtimeVerified = false,
                    note = string.IsNullOrWhiteSpace(candidate)
                        ? "Automatic ID: VRCFury may derive it from a renderer. Resolve after build; do not guess from the object name."
                        : "Read-only candidate. VRCFury and build callbacks may rename IDs; optional touch signals depend on socket configuration."
                });
            }
            foreach (var group in sources.Where(x => x.candidatePrefix.Length > 0).GroupBy(x => x.candidatePrefix).Where(g => g.Count() > 1))
                foreach (var source in group) { source.candidatePrefix = ""; source.note = "Ambiguous duplicate ID: resolve with OSCQuery after build."; }
            return sources.ToArray();
        }

        public static string RefreshInventory(GameObject root)
        {
            UpgradeExisting(root);
            Validate(root);
            var installer = root.transform.Find(AddonName).GetComponent<ModularAvatarMenuInstaller>();
            var directory = Path.GetDirectoryName(AssetDatabase.GetAssetPath(installer.menuToAppend)).Replace('\\', '/');
            var path = directory + "/HydroLink.contract.json";
            if (!directory.StartsWith(Folder + "/Generated/", StringComparison.Ordinal) || !File.Exists(path))
                throw new InvalidOperationException("Kein eigener HydroLink-Profilvertrag gefunden.");
            var contract = JsonUtility.FromJson<Contract>(File.ReadAllText(path));
            if (contract == null || contract.schemaVersion > 3)
                throw new InvalidOperationException("Unbekannte Vertragsversion; keine Aenderung.");
            contract.schemaVersion = 3;
            contract.toolkitVersion = "0.4.0-preview";
            contract.status = new Contract().status;
            contract.parameters = Parameters();
            contract.discovery = new Contract().discovery;
            contract.sourcePolicy = new Contract().sourcePolicy;
            contract.sources = Scan(root);
            contract.sockets = contract.sources.Where(x => x.kind == "Socket").ToArray();
            contract.plugs = contract.sources.Where(x => x.kind == "Plug").ToArray();
            File.WriteAllText(path, JsonUtility.ToJson(contract, true), new UTF8Encoding(false));
            File.WriteAllText(directory + "/Inspection.txt", Inspect(root), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(path);
            AssetDatabase.ImportAsset(directory + "/Inspection.txt");
            return path;
        }
        static HashSet<string> ExistingParameters(GameObject root)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            var d = Descriptor(root);
            if (d.expressionParameters)
                foreach (var p in d.expressionParameters.parameters ?? Array.Empty<VRCExpressionParameters.Parameter>())
                    if (!string.IsNullOrEmpty(p.name)) result.Add(p.name);
            foreach (var p in root.GetComponentsInChildren<ModularAvatarParameters>(true).SelectMany(c => c.parameters))
            {
                if (!string.IsNullOrEmpty(p.nameOrPrefix)) result.Add(p.nameOrPrefix);
                if (!string.IsNullOrEmpty(p.remapTo)) result.Add(p.remapTo);
            }
            var controllers = new List<RuntimeAnimatorController>();
            controllers.AddRange(d.baseAnimationLayers.Select(l => l.animatorController));
            controllers.AddRange(d.specialAnimationLayers.Select(l => l.animatorController));
            controllers.AddRange(root.GetComponentsInChildren<ModularAvatarMergeAnimator>(true).Select(c => c.animator));
            foreach (var controller in controllers.OfType<AnimatorController>())
                foreach (var p in controller.parameters) result.Add(p.name);
            return result;
        }
        public static string Inspect(GameObject root)
        {
            var d = Descriptor(root);
            var names = ExistingParameters(root);
            var sockets = Scan(root);
            var sb = new StringBuilder();
            sb.AppendLine("Avatar: " + root.name);
            sb.AppendLine("Unity: " + Application.unityVersion);
            sb.AppendLine("SPS-Plugs: " + sockets.Count(x => x.kind == "Plug"));
            sb.AppendLine("SPS-Sockets: " + sockets.Count(x => x.kind == "Socket"));
            sb.AppendLine("Max2-Ausgaenge: " + (names.Contains("MaxVibrate") && names.Contains("MaxPump") ? "vorhanden; bleiben unveraendert" : "nicht vollstaendig vorhanden"));
            sb.AppendLine("Bestehendes Hauptmenue: " + AssetDatabase.GetAssetPath(d.expressionsMenu));
            sb.AppendLine("Neue Parameter: lokal, feste OSC-Namen, keine zusaetzlichen Netzwerkbits.");
            sb.AppendLine("Profil-Modus: 1 = Max 2, 2 = Ridge, 3 = Gemeinsam (Standard).");
            sb.AppendLine("Plug-/Socket-Praefixe sind Kandidaten. Verifikation erfolgt erst im gebauten Avatar per OSCQuery.");
            foreach (var s in sockets) sb.AppendLine("- [" + s.kind + "] " + s.path + (s.active ? "" : " [inaktiv]") + "\n  " + (s.candidatePrefix.Length > 0 ? s.candidatePrefix : "ID erst nach Build eindeutig"));
            return sb.ToString();
        }
        static ParameterInfo[] Parameters()
        {
            return Names.Select(n => new ParameterInfo {
                name = Prefix + n, address = "/avatar/parameters/" + Prefix + n,
                type = n == "Enabled" || n == "Clockwise" ? "Bool" : n == "Mode" ? "Int" : "Float",
                defaultValue = n == "Mode" ? 3 : n == "Clockwise" ? 1 : 0
            }).ToArray();
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1));
        }
        public static string Install(GameObject root)
        {
            Descriptor(root);
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Setup nur im ruhenden Edit-Modus ausfuehren.");
            var existing = root.transform.Find(AddonName);
            if (existing) { UpgradeExisting(root); Validate(root); RefreshInventory(root); return AssetDatabase.GetAssetPath(existing.GetComponent<ModularAvatarMenuInstaller>().menuToAppend).Replace("/Menu.asset", ""); }
            var names = ExistingParameters(root);
            var collisions = Parameters().Where(p => names.Contains(p.name)).Select(p => p.name).ToArray();
            if (collisions.Length > 0) throw new InvalidOperationException("Keine Aenderung: vorhandene HydroLink-Parameter: " + string.Join(", ", collisions));
            // Prefix remaps on the avatar could rename public OSC addresses.
            foreach (var p in root.GetComponents<ModularAvatarParameters>().SelectMany(c => c.parameters))
                if (p.isPrefix && Parameters().Any(q => q.name.StartsWith(p.nameOrPrefix ?? "", StringComparison.Ordinal)))
                    throw new InvalidOperationException("Ein uebergeordneter MA-Praefix kann HydroLink-Namen veraendern; bitte zuerst pruefen.");
            EnsureFolder(Folder + "/Generated");
            var output = AssetDatabase.GenerateUniqueAssetPath(Folder + "/Generated/Setup");
            EnsureFolder(output);
            GameObject addon = null;
            var undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("HydroLink Menue-Vorschau hinzufuegen");
            try
            {
                var controls = Menu(output + "/Controls.asset", "HydroLink Vorschau");
                controls.controls.Add(Toggle("HydroLink Freigabe", "Enabled"));
                controls.controls.Add(Radial("Gemeinsame Staerke", "Strength"));
                controls.controls.Add(Radial("Vibration", "Vibration"));
                controls.controls.Add(Radial("Pump", "Pump"));
                controls.controls.Add(Radial("Rotation", "Rotation"));
                controls.controls.Add(Radial("Bewegung", "Motion"));
                controls.controls.Add(Toggle("Rotation im Uhrzeigersinn", "Clockwise"));
                var modes = Menu(output + "/Modes.asset", "Geraeteprofile");
                modes.controls.Add(Toggle("Max 2", "Mode", 1));
                modes.controls.Add(Toggle("Ridge", "Mode", 2));
                modes.controls.Add(Toggle("Gemeinsam", "Mode", 3));
                controls.controls.Add(Submenu("Profil: Gemeinsam / einzeln", modes));
                var menu = Menu(output + "/Menu.asset", "HydroLink Installation");
                menu.controls.Add(Submenu("HydroLink", controls));
                var controller = AnimatorController.CreateAnimatorControllerAtPath(output + "/HydroLink.controller");
                foreach (var p in Parameters())
                    controller.AddParameter(new AnimatorControllerParameter {
                        name = p.name, type = (AnimatorControllerParameterType)Enum.Parse(typeof(AnimatorControllerParameterType), p.type),
                        defaultFloat = p.defaultValue, defaultInt = (int)p.defaultValue, defaultBool = p.defaultValue > 0
                    });
                var idle = controller.layers[0].stateMachine.AddState("Menu parameters only - no hardware output");
                idle.writeDefaultValues = false;
                controller.layers[0].stateMachine.defaultState = idle;
                addon = new GameObject(AddonName);
                Undo.RegisterCreatedObjectUndo(addon, "HydroLink");
                Undo.SetTransformParent(addon.transform, root.transform, "HydroLink");
                addon.transform.localPosition = Vector3.zero;
                addon.transform.localRotation = Quaternion.identity;
                addon.transform.localScale = Vector3.one;
                var parameters = Undo.AddComponent<ModularAvatarParameters>(addon);
                parameters.parameters = Parameters().Select(p => new ParameterConfig {
                    nameOrPrefix = p.name, syncType = (ParameterSyncType)Enum.Parse(typeof(ParameterSyncType), p.type),
                    localOnly = true, saved = false, internalParameter = false, defaultValue = p.defaultValue,
                    hasExplicitDefaultValue = true
                }).ToList();
                Undo.AddComponent<ModularAvatarMenuInstaller>(addon).menuToAppend = menu;
                var merge = Undo.AddComponent<ModularAvatarMergeAnimator>(addon);
                merge.animator = controller;
                merge.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
                merge.mergeAnimatorMode = MergeAnimatorMode.Append;
                merge.pathMode = MergeAnimatorPathMode.Absolute;
                merge.matchAvatarWriteDefaults = false;
                var contract = new Contract {
                    avatarName = root.name, existingMax2 = names.Contains("MaxVibrate") && names.Contains("MaxPump"),
                    legacyOutputs = names.Where(n => n == "MaxVibrate" || n == "MaxPump").OrderBy(n => n).Select(n => "/avatar/parameters/" + n).ToArray(),
                    parameters = Parameters(), sources = Scan(root),
                    sockets = Scan(root).Where(x => x.kind == "Socket").ToArray(),
                    plugs = Scan(root).Where(x => x.kind == "Plug").ToArray(),
                    profiles = new[] {
                        new DeviceProfile { id = "max2", label = "Max 2", requestedCapabilities = new[] { "Vibrate", "Constrict" }, note = "Resolve feature indices from Intiface; keep existing legacy outputs untouched." },
                        new DeviceProfile { id = "ridge", label = "Ridge", requestedCapabilities = new[] { "Vibrate", "Rotate" }, note = "Requested profile only. Rotation and direction must be advertised and verified by the connected device/backend; otherwise unavailable." }
                    }
                };
                File.WriteAllText(output + "/HydroLink.contract.json", JsonUtility.ToJson(contract, true), new UTF8Encoding(false));
                File.WriteAllText(output + "/Inspection.txt", Inspect(root), new UTF8Encoding(false));
                foreach (var obj in new UnityEngine.Object[] { controls, modes, menu, controller, parameters, merge }) EditorUtility.SetDirty(obj);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(output + "/HydroLink.contract.json");
                AssetDatabase.ImportAsset(output + "/Inspection.txt");
                PrefabUtility.SaveAsPrefabAsset(addon, output + "/HydroLink Bridge Preview.prefab");
                Validate(root);
                EditorSceneManager.MarkSceneDirty(root.scene);
                Selection.activeGameObject = addon;
                Undo.CollapseUndoOperations(undoGroup);
                return output;
            }
            catch
            {
                if (addon) UnityEngine.Object.DestroyImmediate(addon);
                // Only the freshly allocated output folder is rolled back.
                AssetDatabase.DeleteAsset(output);
                throw;
            }
        }
        static VRCExpressionsMenu Menu(string path, string name)
        {
            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.name = name; menu.controls = new List<VRCExpressionsMenu.Control>();
            AssetDatabase.CreateAsset(menu, path);
            return menu;
        }
        static VRCExpressionsMenu.Control Toggle(string label, string parameter, float value = 1)
            => new VRCExpressionsMenu.Control { name = label, type = VRCExpressionsMenu.Control.ControlType.Toggle,
                parameter = new VRCExpressionsMenu.Control.Parameter { name = Prefix + parameter }, value = value };
        static VRCExpressionsMenu.Control Radial(string label, string parameter)
            => new VRCExpressionsMenu.Control { name = label, type = VRCExpressionsMenu.Control.ControlType.RadialPuppet,
                subParameters = new[] { new VRCExpressionsMenu.Control.Parameter { name = Prefix + parameter } } };
        static VRCExpressionsMenu.Control Submenu(string label, VRCExpressionsMenu menu)
            => new VRCExpressionsMenu.Control { name = label, type = VRCExpressionsMenu.Control.ControlType.SubMenu, subMenu = menu };

        // Upgrade only the toolkit's own generated objects and assets.
        public static void UpgradeExisting(GameObject root)
        {
            Descriptor(root);
            var addon = root.transform.Find(AddonName);
            if (!addon) throw new InvalidOperationException("Keine HydroLink-Installation gefunden.");
            var ma = addon.GetComponent<ModularAvatarParameters>();
            var merge = addon.GetComponent<ModularAvatarMergeAnimator>();
            var installer = addon.GetComponent<ModularAvatarMenuInstaller>();
            var ac = merge ? merge.animator as AnimatorController : null;
            if (!ma || !ac || !installer || !installer.menuToAppend)
                throw new InvalidOperationException("Unvollstaendige Installation.");
            var menuPath = AssetDatabase.GetAssetPath(installer.menuToAppend);
            if (!menuPath.StartsWith(Folder + "/Generated/", StringComparison.Ordinal) ||
                !AssetDatabase.GetAssetPath(ac).StartsWith(Folder + "/Generated/", StringComparison.Ordinal))
                throw new InvalidOperationException("Fremde Assets werden nicht aktualisiert.");
            if (ma.parameters.Any(p => !Names.Contains(p.nameOrPrefix.Replace(Prefix, ""))))
                throw new InvalidOperationException("Benutzerdefinierte Parameter: automatische Aktualisierung abgebrochen.");
            var controls = installer.menuToAppend.controls.Single().subMenu;
            if (!controls || !AssetDatabase.GetAssetPath(controls).StartsWith(Folder + "/Generated/", StringComparison.Ordinal))
                throw new InvalidOperationException("Unerwartetes Steuermenue.");
            if (!ma.parameters.Any(p => p.nameOrPrefix == Prefix + "Motion"))
            {
                if (ma.parameters.Count != 7 || ac.parameters.Any(p => p.name == Prefix + "Motion") || controls.controls.Count != 7)
                    throw new InvalidOperationException("Unerwarteter Upgrade-Ausgangszustand.");
                Undo.RecordObject(ma, "HydroLink Bewegungsregler");
                Undo.RecordObject(ac, "HydroLink Bewegungsregler");
                Undo.RecordObject(controls, "HydroLink Bewegungsregler");
                ma.parameters.Add(new ParameterConfig { nameOrPrefix = Prefix + "Motion", syncType = ParameterSyncType.Float,
                    localOnly = true, saved = false, internalParameter = false, hasExplicitDefaultValue = true, defaultValue = 0 });
                ac.AddParameter(Prefix + "Motion", AnimatorControllerParameterType.Float);
                controls.controls.Insert(5, Radial("Bewegung", "Motion"));
                EditorUtility.SetDirty(ma); EditorUtility.SetDirty(ac); EditorUtility.SetDirty(controls);
                PrefabUtility.RecordPrefabInstancePropertyModifications(ma);
                EditorSceneManager.MarkSceneDirty(root.scene);
            }
            installer.menuToAppend.controls[0].name = "HydroLink";
            controls.controls[0].name = "HydroLink Freigabe";
            EditorUtility.SetDirty(installer.menuToAppend); EditorUtility.SetDirty(controls);
            AssetDatabase.SaveAssets();
            PrefabUtility.SaveAsPrefabAsset(addon.gameObject, menuPath.Replace("Menu.asset", "HydroLink Bridge Preview.prefab"));
        }

        public static string Validate(GameObject root)
        {
            Descriptor(root);
            var addon = root.transform.Find(AddonName);
            if (!addon) throw new InvalidOperationException("Noch keine HydroLink-Vorschau an diesem Avatar.");
            var ma = addon.GetComponent<ModularAvatarParameters>();
            var merge = addon.GetComponent<ModularAvatarMergeAnimator>();
            var installer = addon.GetComponent<ModularAvatarMenuInstaller>();
            if (!ma || !merge || !installer || !installer.menuToAppend) throw new InvalidOperationException("Unvollstaendige HydroLink-Komponenten.");
            if (ma.parameters.Count != Names.Length || ma.parameters.Select(p => p.nameOrPrefix).Distinct().Count() != Names.Length)
                throw new InvalidOperationException("Parameteranzahl oder Eindeutigkeit stimmt nicht.");
            foreach (var expected in Parameters())
            {
                var p = ma.parameters.Single(x => x.nameOrPrefix == expected.name);
                if (!p.localOnly || p.internalParameter || p.saved || p.isPrefix || !string.IsNullOrEmpty(p.remapTo)
                    || !p.hasExplicitDefaultValue || p.defaultValue != expected.defaultValue || p.syncType.ToString() != expected.type)
                    throw new InvalidOperationException("Unerwartete Parameterkonfiguration: " + expected.name);
            }
            if (merge.mergeAnimatorMode != MergeAnimatorMode.Append || merge.layerType != VRCAvatarDescriptor.AnimLayerType.FX)
                throw new InvalidOperationException("Controller muss FX ergaenzen.");
            var controller = merge.animator as AnimatorController;
            if (!controller || controller.parameters.Length != Names.Length || controller.animationClips.Length != 0)
                throw new InvalidOperationException("Unerwarteter Controller.");
            var pending = new Queue<VRCExpressionsMenu>();
            var seen = new HashSet<VRCExpressionsMenu>();
            pending.Enqueue(installer.menuToAppend);
            while (pending.Count > 0)
            {
                var menu = pending.Dequeue();
                if (!seen.Add(menu)) continue;
                if (menu.controls.Count > 8) throw new InvalidOperationException("Menue hat mehr als acht Eintraege.");
                foreach (var c in menu.controls)
                {
                    if (c.subMenu) pending.Enqueue(c.subMenu);
                    var refs = (c.subParameters ?? Array.Empty<VRCExpressionsMenu.Control.Parameter>()).Select(x => x.name).ToList();
                    if (c.parameter != null && !string.IsNullOrEmpty(c.parameter.name)) refs.Add(c.parameter.name);
                    if (refs.Any(n => !ma.parameters.Any(p => p.nameOrPrefix == n)))
                        throw new InvalidOperationException("Menueparameter fehlt.");
                }
            }
            return "PASS: 8 feste lokale Parameter, Freigabe aus, Modus Gemeinsam, 3 Menues innerhalb des Limits, FX nur ergaenzt.\nDesktop 0.4 angebunden; physische Geraete und OSCQuery im laufenden VRChat separat pruefen.";
        }
    }
}
#endif
