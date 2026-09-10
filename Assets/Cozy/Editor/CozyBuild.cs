using System;
using System.IO;
using Cozy;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class CozyAssetImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Cozy/Resources/Cozy/"))return;
        var t=(TextureImporter)assetImporter;t.textureType=TextureImporterType.Default;t.mipmapEnabled=false;
        t.alphaIsTransparency=true;t.wrapMode=TextureWrapMode.Clamp;
        t.filterMode=assetPath.EndsWith("Aurora.png")?FilterMode.Bilinear:FilterMode.Point;
        t.textureCompression=TextureImporterCompression.Uncompressed;t.maxTextureSize=2048;
        if(assetPath.Contains("/Capybara/") || assetPath.Contains("/Cat/")) t.npotScale=TextureImporterNPOTScale.None;
    }
}

[InitializeOnLoad]
public static class CozyBuild
{
    public const string Version="0.3.4";
    public const int AndroidVersionCode=7;
    public const string ApkPath="Builds/Android/CozySurvivors-v"+Version+".apk";
    const string AndroidPackageName="com.secondwindgames.cozysurvivors";
    const string ScenePath="Assets/Cozy/Scenes/AuroraSnowfield.unity";
    static bool busy;
    static double nextPoll;
    static CozyBuild(){EditorApplication.update+=Poll;}
    // Local development commands allow the open editor to build without a second Unity instance.
    static void Poll()
    {
        if(busy || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup<nextPoll)return;
        nextPoll=EditorApplication.timeSinceStartup+1;
        const string path="Temp/CozyCommand.txt";
        if(!File.Exists(path))return;
        string command=File.ReadAllText(path).Trim();File.Delete(path);
        busy=true;
        try
        {
            if(command=="setup")Setup();
            else if(command=="build")BuildAndroid();
            else if(command=="play")EditorApplication.isPlaying=true;
            else if(command=="stop")EditorApplication.isPlaying=false;
            else if(command=="test")
            {
                var game=UnityEngine.Object.FindFirstObjectByType<CozyGame>();
                if(!EditorApplication.isPlaying || !game)throw new Exception("Start Cozy scene in Play mode before test.");
                File.WriteAllText("Temp/CozyTests.txt",game.RunSmokeChecks());
            }
            else if(command=="start")UnityEngine.Object.FindFirstObjectByType<CozyGame>().StartRun();
            else if(command=="touchtest")UnityEngine.Object.FindFirstObjectByType<CozyGame>().CheckTouchInput();
            else if(command=="storeprepare")CozyStoreCapture.Prepare();
            else if(command=="storecapture")UnityEngine.Object.FindFirstObjectByType<CozyGame>().CaptureStoreScreens();
            else if(command=="featureprepare")CozyStoreCapture.SetSize(1024,500);
            else if(command=="featurecapture")UnityEngine.Object.FindFirstObjectByType<CozyGame>().CaptureStoreFeature();
            else if(command=="capture")ScreenCapture.CaptureScreenshot("Temp/CozyGameplay.png");
            else if(command=="refresh")AssetDatabase.Refresh();
            else if(command=="package")ApplyAndroidPackageName();
            else if(command=="review")
            {
                var type=typeof(Editor).Assembly.GetType("UnityEditor.GameView");
                var view=EditorWindow.GetWindow(type);view.Show();view.Focus();
                type.GetProperty("selectedSizeIndex",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic)?.SetValue(view,0);
                view.maximized=true;
            }
            else if(command=="inspect")
            {
                var output=new System.Text.StringBuilder();
                output.AppendLine($"Screen {Screen.width}x{Screen.height}; safe {Screen.safeArea}; playing {EditorApplication.isPlaying}");
                foreach(var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))output.AppendLine($"Canvas {c.name} enabled {c.enabled} rect {((RectTransform)c.transform).rect}");
                foreach(var r in UnityEngine.Object.FindObjectsByType<UnityEngine.UI.RawImage>(FindObjectsSortMode.None))if(r.name=="Aurora snowfield" || r.name=="Penguin")output.AppendLine($"Image {r.name} active {r.gameObject.activeInHierarchy} pos {r.transform.position} scale {r.transform.lossyScale} texture {r.texture}");
                File.WriteAllText("Temp/CozyInspect.txt",output.ToString());
            }
            File.WriteAllText("Temp/CozyCommandResult.txt","OK "+command+" "+DateTime.Now);
        }
        catch(Exception e){Debug.LogException(e);File.WriteAllText("Temp/CozyCommandResult.txt","FAILED "+command+"\n"+e);}
        finally{busy=false;}
    }

    [MenuItem("Cozy/1. Create or open penguin scene")]
    public static void Setup()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play mode first.");
        if(SceneManager.GetActiveScene().isDirty)throw new Exception("Current scene has unsaved changes. Save it before opening the Cozy scene.");
        Directory.CreateDirectory("Assets/Cozy/Scenes");
        if(File.Exists(ScenePath)){EditorSceneManager.OpenScene(ScenePath);Configure();return;}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var camera=new GameObject("Camera",typeof(Camera),typeof(AudioListener)).GetComponent<Camera>();
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.19f,.31f);camera.orthographic=true;camera.transform.position=new Vector3(0,0,-10);
        var game=new GameObject("Cozy Penguin Prototype").AddComponent<CozyGame>();
        string settingsPath="Assets/Cozy/CozyFeel.asset";
        var settings=AssetDatabase.LoadAssetAtPath<CozyTuning>(settingsPath);
        if(!settings){settings=ScriptableObject.CreateInstance<CozyTuning>();AssetDatabase.CreateAsset(settings,settingsPath);}
        game.tuning=settings;game.referenceShader=Shader.Find("Cozy/ReferenceSprite");
        EditorSceneManager.SaveScene(scene,ScenePath);Configure();AssetDatabase.SaveAssets();
    }

    static void Configure()
    {
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        PlayerSettings.companyName="Cozy Studio";PlayerSettings.productName="CozySurvivors";
        CozyBranding.Apply();
        ApplyAndroidPackageName();
        PlayerSettings.bundleVersion=Version;PlayerSettings.Android.bundleVersionCode=AndroidVersionCode;
        PlayerSettings.defaultInterfaceOrientation=UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait=false;PlayerSettings.allowedAutorotateToPortraitUpsideDown=false;
        PlayerSettings.allowedAutorotateToLandscapeLeft=true;PlayerSettings.allowedAutorotateToLandscapeRight=true;
        PlayerSettings.defaultInterfaceOrientation=UIOrientation.AutoRotation;
        PlayerSettings.Android.minSdkVersion=AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion=AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android,ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures=AndroidArchitecture.ARM64;
        PlayerSettings.Android.useCustomKeystore=false;
        PlayerSettings.runInBackground=false;
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
        QualitySettings.vSyncCount=0;
        EditorUserBuildSettings.buildAppBundle=false;
    }

    static void ApplyAndroidPackageName()
    {
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android,AndroidPackageName);
        AssetDatabase.SaveAssets();
        if(PlayerSettings.GetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android)!=AndroidPackageName)
            throw new Exception("Android package name was not applied.");
    }

    [MenuItem("Cozy/2. Build Android APK")]
    public static void BuildAndroid()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play mode before building.");
        Setup();CozyCommercialBuild.Validate();CozyCommercialBuild.ValidateCharacterAssets();Directory.CreateDirectory("Builds/Android");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
            scenes=new[]{ScenePath},locationPathName=ApkPath,
            target=BuildTarget.Android,options=BuildOptions.Development
        });
        File.WriteAllText("Builds/Android/build-report.txt",$"Result: {report.summary.result}\nSize: {report.summary.totalSize}\nDuration: {report.summary.totalTime}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nUnity: {Application.unityVersion}\nARM64 IL2CPP / Android 8.0+ / Development APK");
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Android build failed: "+report.summary.result);
        Debug.Log("COZY_BUILD_SUCCESS: "+ApkPath);
    }
}
