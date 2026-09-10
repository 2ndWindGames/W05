using System;
using System.IO;
using Cozy;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class CozyCommercialBuild
{
    [MenuItem("Cozy/3. Validate commercial rules")]
    public static void Validate()
    {
        Directory.CreateDirectory("Documentation");
        try{string result=CozyChecks.Run();File.WriteAllText("Documentation/CommercialValidation.txt",result);Debug.Log(result);}
        catch(Exception e){File.WriteAllText("Documentation/CommercialValidation.txt",e.ToString());throw;}
    }
    [MenuItem("Cozy/4. Build commercial Windows demo")]
    public static void BuildWindows()
    {
        Directory.CreateDirectory("Builds/Windows");
        PlayerSettings.bundleVersion=CozyBuild.Version;
        PlayerSettings.defaultScreenWidth=1280;PlayerSettings.defaultScreenHeight=720;
        PlayerSettings.fullScreenMode=FullScreenMode.Windowed;
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Cozy/Scenes/AuroraSnowfield.unity"},locationPathName="Builds/Windows/CozySurvivors.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        File.WriteAllText("Builds/Windows/build-report.txt",$"Result: {report.summary.result}\nErrors: {report.summary.totalErrors}\nWarnings: {report.summary.totalWarnings}\nDuration: {report.summary.totalTime}\nRules: {CozyRules.Version}\nUnity: {Application.unityVersion}");
        if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Windows build failed.");
    }
    public static void ValidateCharacterAssets()
    {
        var output=new System.Text.StringBuilder();
        foreach(string animal in new[]{"Capybara","Cat"}) for(int frame=0;frame<8;frame++)
        {
            string path=$"Assets/Cozy/Resources/Cozy/{animal}/Idle{frame}.png";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!texture || !importer || texture.width!=384 || texture.height!=384 || texture.filterMode!=FilterMode.Point || importer.mipmapEnabled || importer.textureCompression!=TextureImporterCompression.Uncompressed)
                throw new Exception("Invalid animation frame/import settings: "+path);
            output.AppendLine("PASS "+path+" · 384x384, Point, uncompressed, no mipmaps");
        }
        float[] sampleTimes={.3f,.7f,.95f,1.10f,1.24f,1.36f,1.5f,1.84f};
        for(int i=0;i<8;i++) if(CozyGame.CompanionIdleFrame(sampleTimes[i])!=i || CozyGame.CompanionIdleFrame(sampleTimes[i]+2)!=i)
            throw new Exception("Idle animation timing/loop mismatch at frame "+i);
        output.AppendLine("PASS all eight timed poses and 2-second repeat");
        Directory.CreateDirectory("Documentation");File.WriteAllText("Documentation/CharacterAnimationValidation.txt",output.ToString());Debug.Log(output);
    }
    public static void ValidateAndBuild(){Validate();ValidateCharacterAssets();BuildWindows();}
}
