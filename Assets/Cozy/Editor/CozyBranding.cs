using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class CozyBranding
{
    public const string SelectedIconPath="Assets/Cozy/Branding/Icons/01-star-scarf-v1.png";
    public const string SplashPath="Assets/Cozy/Branding/Splash/SecondWindGamesLogo.png";

    public static void Apply()
    {
        var icon=AssetDatabase.LoadAssetAtPath<Texture2D>(SelectedIconPath);
        if(!icon)throw new InvalidOperationException("Selected icon missing: "+SelectedIconPath);
        PlayerSettings.SetIcons(NamedBuildTarget.Unknown,new[]{icon},IconKind.Any);
        // Unity derives density-specific Android launcher icons from this source.
        // Reset platform overrides so a previous selection cannot mask the default icon.
        foreach(var kind in PlayerSettings.GetSupportedIconKindsForPlatform(BuildTargetGroup.Android))
        {
            var slots=PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android,kind);
            foreach(var slot in slots)slot.SetTextures(null);
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android,kind,slots);
        }

        var logo=AssetDatabase.LoadAllAssetsAtPath(SplashPath).OfType<Sprite>().Single();
        PlayerSettings.SplashScreen.show=true;
        PlayerSettings.SplashScreen.showUnityLogo=false;
        PlayerSettings.SplashScreen.backgroundColor=Color.white;
        PlayerSettings.SplashScreen.background=null;
        PlayerSettings.SplashScreen.backgroundPortrait=null;
        PlayerSettings.SplashScreen.blurBackgroundImage=false;
        PlayerSettings.SplashScreen.overlayOpacity=1;
        PlayerSettings.SplashScreen.animationMode=PlayerSettings.SplashScreen.AnimationMode.Static;
        PlayerSettings.SplashScreen.animationBackgroundZoom=1;
        PlayerSettings.SplashScreen.animationLogoZoom=1;
        PlayerSettings.SplashScreen.unityLogoStyle=PlayerSettings.SplashScreen.UnityLogoStyle.DarkOnLight;
        PlayerSettings.SplashScreen.drawMode=PlayerSettings.SplashScreen.DrawMode.UnityLogoBelow;
        PlayerSettings.SplashScreen.logos=new[]{PlayerSettings.SplashScreenLogo.Create(2,logo)};
        // W01 stores AndroidSplashScreenScale=2 (ScaleToFill); no separate Android background.
        var settings=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
        settings.FindProperty("AndroidSplashScreenScale").intValue=2;
        settings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.SaveAssets();
    }
}

public sealed class CozyBrandingImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if(!assetPath.StartsWith("Assets/Cozy/Branding/Icons/"))return;
        var importer=(TextureImporter)assetImporter;
        importer.textureType=TextureImporterType.Default;
        importer.mipmapEnabled=false;
        importer.alphaIsTransparency=true;
        importer.wrapMode=TextureWrapMode.Clamp;
        importer.filterMode=FilterMode.Bilinear;
        importer.npotScale=TextureImporterNPOTScale.None;
        importer.maxTextureSize=2048;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
    }
}
