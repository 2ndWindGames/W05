using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class CozyStoreCapture
{
    public const string Output="StoreAssets/GooglePlay";
    public static void Prepare()
    {
        Directory.CreateDirectory(Output+"/Screenshots");
        SetSize(1920,1080);
        ExportIcon();
    }
    public static void SetSize(int width,int height)
    {
        var assembly=typeof(Editor).Assembly;
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static|BindingFlags.FlattenHierarchy).GetValue(null);
        var group=sizesType.GetMethod("GetGroup").Invoke(sizes,new[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"),"Android")});
        var sizeType=assembly.GetType("UnityEditor.GameViewSize");
        var typeEnum=assembly.GetType("UnityEditor.GameViewSizeType");
        var entry=Activator.CreateInstance(sizeType,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,new object[]{Enum.ToObject(typeEnum,1),width,height,"Cozy Store "+width+"x"+height},null);
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{entry});
        int count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
        var viewType=assembly.GetType("UnityEditor.GameView");
        var view=EditorWindow.GetWindow(viewType);view.Show();view.Focus();
        viewType.GetProperty("selectedSizeIndex",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).SetValue(view,count-1);
    }
    static void ExportIcon()
    {
        var source=AssetDatabase.LoadAssetAtPath<Texture2D>(CozyBranding.SelectedIconPath);
        var previous=RenderTexture.active;
        var rt=RenderTexture.GetTemporary(512,512,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
        var result=new Texture2D(512,512,TextureFormat.RGBA32,false);
        try
        {
            Graphics.Blit(source,rt);RenderTexture.active=rt;
            result.ReadPixels(new Rect(0,0,512,512),0,0);result.Apply();
            File.WriteAllBytes(Output+"/AppIcon-512.png",result.EncodeToPNG());
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(result);}
    }
}
