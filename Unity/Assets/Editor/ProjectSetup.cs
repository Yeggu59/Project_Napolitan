// 프로젝트 초기 설정 (11/1): URP 에셋 생성·지정 + 기본 씬. 여러 번 실행해도 안전함
// 메뉴: Napolitan > Setup > URP + 기본 씬
// 배치모드: Unity -batchmode -projectPath Unity -executeMethod ProjectSetup.Run -quit

using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class ProjectSetup
{
    const string SettingsDir = "Assets/Settings";
    const string PipelinePath = SettingsDir + "/URP.asset";
    const string RendererPath = SettingsDir + "/URP_Renderer.asset";
    const string ScenePath = "Assets/Scenes/SeorimB2.unity";

    [MenuItem("Napolitan/Setup/URP + 기본 씬")]
    public static void Run()
    {
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
        if (pipeline == null)
        {
            Directory.CreateDirectory(SettingsDir);
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssignDefaultPostProcessData(renderer);
            AssetDatabase.CreateAsset(renderer, RendererPath);
            pipeline = UniversalRenderPipelineAsset.Create(renderer);
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
        }

        GraphicsSettings.defaultRenderPipeline = pipeline;
        int current = QualitySettings.GetQualityLevel();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            QualitySettings.SetQualityLevel(i, false);
            QualitySettings.renderPipeline = pipeline;
        }
        QualitySettings.SetQualityLevel(current, false);

        if (!File.Exists(ScenePath))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        AssetDatabase.SaveAssets();
        Debug.Log("[ProjectSetup] URP 지정 + 기본 씬 완료");
    }

    // 메뉴로 만든 렌더러와 같게: 포스트프로세싱 기본 데이터 연결 (URP 내부 API라 리플렉션)
    static void AssignDefaultPostProcessData(UniversalRendererData renderer)
    {
        var getDefault = typeof(PostProcessData).GetMethod(
            "GetDefaultPostProcessData", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (getDefault == null)
        {
            Debug.LogWarning("[ProjectSetup] PostProcessData 기본값을 찾지 못함 — 렌더러 에셋에서 직접 지정 필요");
            return;
        }
        renderer.postProcessData = (PostProcessData)getDefault.Invoke(null, null);
    }
}
