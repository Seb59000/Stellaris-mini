using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace StellarisMini.EditorTools
{
    /// Configure automatiquement le projet à l'ouverture dans Unity :
    ///  - crée la scène principale et l'ajoute aux scènes du build,
    ///  - crée le matériau additif utilisé pour les lueurs (inclus ainsi dans les builds).
    /// Le jeu lui-même se construit entièrement par code au lancement (GameManager).
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        const string Root = "Assets/StellarisMini";
        const string ScenePath = Root + "/Scenes/Main.unity";
        const string ResourcesDir = Root + "/Resources/StellarisMini";

        static ProjectSetup()
        {
            EditorApplication.delayCall += Run;
        }

        [MenuItem("Stellaris Mini/Configurer le projet")]
        public static void RunFromMenu()
        {
            Run();
            Debug.Log("Stellaris Mini : projet configuré. Ouvrez la scène Main et appuyez sur Play.");
        }

        static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            EnsureMaterials();
            EnsureScene();
#if !ENABLE_INPUT_SYSTEM
            Debug.LogWarning("Stellaris Mini : le nouveau système d'entrées n'est pas actif, la manette ne fonctionnera pas. " +
                             "Activez-le dans Edit > Project Settings > Player > Active Input Handling (\"Input System Package\" ou \"Both\").");
#endif
        }

        static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder(parent + "/" + name)) AssetDatabase.CreateFolder(parent, name);
        }

        static void EnsureMaterials()
        {
            EnsureFolder(Root, "Resources");
            EnsureFolder(Root + "/Resources", "StellarisMini");
            CreateMaterial(ResourcesDir + "/Additive.mat", "Legacy Shaders/Particles/Additive");
            CreateMaterial(ResourcesDir + "/Lit.mat", "Standard");   // vaisseaux et planètes des combats 3D
        }

        static void CreateMaterial(string path, string shaderName)
        {
            if (AssetDatabase.LoadAssetAtPath<Material>(path) != null) return;
            var shader = Shader.Find(shaderName);
            if (shader == null) return;
            AssetDatabase.CreateAsset(new Material(shader), path);
            AssetDatabase.SaveAssets();
        }

        static void EnsureScene()
        {
            if (!File.Exists(ScenePath))
            {
                var active = EditorSceneManager.GetActiveScene();
                if (active.isDirty) return; // ne jamais écraser un travail non sauvegardé
                EnsureFolder(Root, "Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                PlayerSettings.productName = "Stellaris Mini";
            }

            bool listed = false;
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path == ScenePath) listed = true;
            if (!listed)
            {
                var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = list.ToArray();
            }

            var current = EditorSceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(current.path) && !current.isDirty) EditorSceneManager.OpenScene(ScenePath);
        }
    }
}
