using System;
using UnityEngine;
using UnityEngine.UI;
namespace Cozy
{
    public sealed partial class CozyGame
    {
        Texture2D[] Frames(string name)
        {
            var frames = new Texture2D[8];
            for(int i=0;i<8;i++) frames[i]=Resources.Load<Texture2D>("Cozy/"+name+i);
            return frames;
        }

        // A relaxed hold, soft breath and brief blink, matching the selected candidate previews.
        static readonly float[] CompanionIdleDurations = { .60f,.23f,.23f,.10f,.16f,.10f,.26f,.32f };
        public static int CompanionIdleFrame(float seconds)
        {
            float time = Mathf.Repeat(seconds,2f);
            for (int i=0;i<CompanionIdleDurations.Length;i++)
            {
                if (time<CompanionIdleDurations[i]) return i;
                time-=CompanionIdleDurations[i];
            }
            return 0;
        }
        Texture2D AnimalIdle(Animal animal,float seconds)
        {
            if (animal==Animal.Penguin) return idle[(int)(seconds/.2075f)%8];
            return (animal==Animal.Capybara?capybaraIdle:catIdle)[CompanionIdleFrame(seconds)];
        }
        RawImage AnimalPortrait(Transform parent,Animal animal,Vector2 size,Vector2 position)
        {
            var view=Image(animal+" idle portrait",parent,AnimalIdle(animal,clock),size,position,Color.white);
            view.material=animal==Animal.Penguin?penguinMaterial:companionMaterial;
            menuPortraits.Add((view,animal));return view;
        }

        Texture2D Icon(int size, Func<float,float,Color> sample)
        {
            var t = new Texture2D(size,size,TextureFormat.RGBA32,false) { filterMode=FilterMode.Point, wrapMode=TextureWrapMode.Clamp };
            var pixels = new Color[size*size];
            for(int y=0;y<size;y++) for(int x=0;x<size;x++) pixels[y*size+x]=sample((x+.5f)/size*2-1,(y+.5f)/size*2-1);
            t.SetPixels(pixels); t.Apply(); ownedAssets.Add(t); return t;
        }

        void CreateIcons()
        {
            disc=Icon(48,(x,y)=>x*x+y*y<1 ? Color.white : Color.clear);
            rounded=Icon(64,(x,y)=>{float a=Mathf.Max(Mathf.Abs(x)-.72f,0), b=Mathf.Max(Mathf.Abs(y)-.72f,0);return a*a+b*b<.0784f?Color.white:Color.clear;});
            heart=Icon(24,(x,y)=>{ y=y*.95f+.1f; float a=x*x+y*y-.55f; return a*a*a-x*x*y*y*y<0?Color.white:Color.clear; });
            snowflake=Icon(40,(x,y)=>{
                float r=Mathf.Sqrt(x*x+y*y); if(r>.95f)return Color.clear;
                float a=Mathf.Atan2(y,x); float branch=Mathf.Abs(Mathf.Sin(a*3));
                bool on=branch*r<.085f || (r>.43f && r<.69f && Mathf.Abs(Mathf.Sin(a*3+r*9))<.28f);
                return on ? (r<.23f?Color.white:new Color(.67f,.91f,1)) : Color.clear;
            });
            spirit=SpiritIcon(false); toughSpirit=SpiritIcon(true);
        }

        Texture2D SpiritIcon(bool tough) => Icon(48,(x,y)=>{
            float r=x*x+(y+.03f)*(y+.03f); if(r>.79f)return Color.clear;
            if(r>.63f)return new Color(.34f,.38f,.63f);
            if(y>.36f && y<.54f && x>-.38f && x<-.17f)return Color.white;
            if(y>-.06f && y<.15f && (Mathf.Abs(x-.26f)<.07f || Mathf.Abs(x+.26f)<.07f))return Ink;
            if(y<-.18f && y>-.27f && Mathf.Abs(x)<.1f)return new Color(.58f,.49f,.67f);
            if(y<-.08f && y>-.24f && Mathf.Abs(x)>.36f && Mathf.Abs(x)<.57f)return new Color(.96f,.66f,.77f);
            if(y<-.32f || (x<-.52f && y<.25f))return tough?new Color(.54f,.59f,.81f):new Color(.77f,.82f,.95f);
            if(r>.44f && y<.36f)return tough?new Color(.61f,.66f,.86f):new Color(.86f,.90f,.99f);
            return tough ? new Color(.72f,.77f,.95f) : new Color(.97f,.98f,1);
        });

        RectTransform Root(string name, Transform parent, Vector2 size, Vector2 pos)
        {
            var rt=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();
            rt.SetParent(parent,false); rt.anchorMin=rt.anchorMax=new Vector2(.5f,.5f); rt.sizeDelta=size;rt.anchoredPosition=pos; return rt;
        }
        RawImage Image(string name, Transform parent, Texture texture, Vector2 size, Vector2 pos, Color color)
        {
            var rt=Root(name,parent,size,pos);var view=rt.gameObject.AddComponent<RawImage>(); view.texture=texture;view.color=color;view.raycastTarget=false;return view;
        }
        Text Label(Transform parent,string value,int size,Vector2 pos,Vector2 area,Color color)
        {
            var rt=Root(value,parent,area,pos);var t=rt.gameObject.AddComponent<Text>();t.text=value;t.font=font;t.fontSize=Mathf.RoundToInt(size*(profile.settings.largeText?1.12f:1));t.color=color;t.alignment=TextAnchor.MiddleCenter;t.verticalOverflow=VerticalWrapMode.Truncate;t.raycastTarget=false;return t;
        }
        RawImage Button(Transform parent,string title,Vector2 pos,Vector2 size,Action action,bool primary=false)
        {
            var v=Image(title,parent,rounded,size,pos,primary?Pink:Milk);v.raycastTarget=true;
            var b=v.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=v;
            var colors=b.colors;colors.highlightedColor=new Color(.94f,.96f,1);colors.selectedColor=new Color(.66f,.77f,.93f);colors.pressedColor=new Color(.70f,.72f,.85f);b.colors=colors;
            Label(v.transform,title,20,Vector2.zero,size,primary?Color.white:Ink);
            b.onClick.AddListener(()=>{if(Time.frameCount <= blockedFrame)return; Tone(chime,.28f); action();});
            return v;
        }

        void CreateAudio()
        {
            music=gameObject.AddComponent<AudioSource>();music.loop=true;music.volume=.17f;music.playOnAwake=false;
            sfx=gameObject.AddComponent<AudioSource>();sfx.playOnAwake=false;
            chime=Sound("Ice chime",.26f,t=>Mathf.Sin(2*Mathf.PI*1174.66f*t)*Mathf.Exp(-t*17)+.35f*Mathf.Sin(2*Mathf.PI*1760*t)*Mathf.Exp(-t*22));
            hurt=Sound("Soft knock",.18f,t=>Mathf.Sin(2*Mathf.PI*(240-340*t)*t)*Mathf.Exp(-t*20));
            float[] notes={587.33f,739.99f,880,1108.73f,880,739.99f,659.25f,880,987.77f,739.99f,659.25f,554.37f,587.33f,739.99f,880,739.99f};
            music.clip=Sound("Original snow music",32,t=>{float local=t%2;float note=notes[Mathf.Min(15,(int)(t/2))];float envelope=(1-Mathf.Exp(-local*25))*Mathf.Exp(-local*2.3f);return .4f*envelope*(Mathf.Sin(2*Mathf.PI*note*t)+.22f*Mathf.Sin(2*Mathf.PI*note*2*t));});
        }
        AudioClip Sound(string name,float duration,Func<float,float> sample)
        {
            const int rate=22050;var data=new float[(int)(duration*rate)];for(int i=0;i<data.Length;i++)data[i]=sample(i/(float)rate)*.45f;
            var clip=AudioClip.Create(name,data.Length,1,rate,false);clip.SetData(data,0);ownedAssets.Add(clip);return clip;
        }
        void Tone(AudioClip clip,float volume){if(!muted && sfx && clip)sfx.PlayOneShot(clip,volume);}

    }
}

