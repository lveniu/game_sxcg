// FE-20: 成就面板 — 展示成就列表、进度、领取奖励 | 竖屏720x1280 | 后端BE-17完成后替换Mock
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace Game.UI
{
    // TODO: 后端 BE-17 完成后替换
    [System.Serializable] public class AchievementData
    { public string id, name, description, category, iconId, statKey, rewardType; public int tier, targetValue, rewardAmount; public bool isHidden; }
    [System.Serializable] public class AchievementProgress
    { public string id; public int currentValue; public bool isUnlocked, isRewardClaimed; public long unlockTimestamp; }

    #region Mock
    public static class MockAchievementManager
    {
        public static event Action<AchievementData> OnAchievementUnlocked;
        static List<AchievementData> D; static Dictionary<string, AchievementProgress> P;
        public static List<AchievementData> GetAllAchievements() => D ??= new() {
            new(){id="first_kill",name="初次击杀",description="完成第一次击杀",category="combat",iconId="sword",tier=1,targetValue=1,statKey="total_kills",rewardType="gold",rewardAmount=50},
            new(){id="kill_100",name="百人斩",description="累计击杀100个敌人",category="combat",iconId="skull",tier=2,targetValue=100,statKey="total_kills",rewardType="gold",rewardAmount=500},
            new(){id="triple_master",name="三条大师",description="累计打出10次三条",category="combat",iconId="dice",tier=2,targetValue=10,statKey="triples_count",rewardType="gold",rewardAmount=300},
            new(){id="relic_collector",name="遗物收藏家",description="单局收集8个遗物",category="collection",iconId="gem",tier=3,targetValue=8,statKey="max_relics_one_run",rewardType="gold",rewardAmount=800},
            new(){id="speed_demon",name="速通达人",description="600秒内通关",category="special",iconId="clock",tier=3,targetValue=600,statKey="fastest_clear_time",rewardType="gold",rewardAmount=1000,isHidden=true},
            new(){id="boss_slayer",name="屠龙者",description="累计击杀10个Boss",category="combat",iconId="dragon",tier=3,targetValue=10,statKey="boss_kills",rewardType="gold",rewardAmount=1000},
            new(){id="streak_5",name="五连胜",description="达成5连胜",category="combat",iconId="fire",tier=2,targetValue=5,statKey="max_win_streak",rewardType="gold",rewardAmount=400},
            new(){id="straight_flush",name="顺子之王",description="累计打出5次顺子",category="combat",iconId="shield",tier=2,targetValue=5,statKey="straights_count",rewardType="gold",rewardAmount=300},
            new(){id="perfect_boss",name="完美击杀",description="无伤击杀Boss",category="special",iconId="star",tier=3,targetValue=1,statKey="perfect_boss_kills",rewardType="gold",rewardAmount=1500,isHidden=true},
            new(){id="all_heroes",name="全英雄精通",description="使用3个不同英雄通关",category="collection",iconId="coin",tier=3,targetValue=3,statKey="unique_hero_clears",rewardType="gold",rewardAmount=2000},
            new(){id="rich",name="富甲天下",description="单局累计获得2000金币",category="collection",iconId="coin",tier=2,targetValue=2000,statKey="max_gold_earned",rewardType="gold",rewardAmount=500},
            new(){id="level_20",name="深渊探索者",description="到达第20层",category="exploration",iconId="map",tier=3,targetValue=20,statKey="max_level_reached",rewardType="gold",rewardAmount=1000}
        };
        public static Dictionary<string, AchievementProgress> GetAllProgress() => P ??= new() {
            ["first_kill"]=new(){id="first_kill",currentValue=1,isUnlocked=true,isRewardClaimed=true},
            ["kill_100"]=new(){id="kill_100",currentValue=47}, ["triple_master"]=new(){id="triple_master",currentValue=3},
            ["relic_collector"]=new(){id="relic_collector",currentValue=8,isUnlocked=true},
            ["speed_demon"]=new(){id="speed_demon"}, ["boss_slayer"]=new(){id="boss_slayer",currentValue=7},
            ["streak_5"]=new(){id="streak_5",currentValue=5,isUnlocked=true},
            ["straight_flush"]=new(){id="straight_flush",currentValue=2}, ["perfect_boss"]=new(){id="perfect_boss"},
            ["all_heroes"]=new(){id="all_heroes",currentValue=2}, ["rich"]=new(){id="rich",currentValue=1500},
            ["level_20"]=new(){id="level_20",currentValue=15}
        };
        public static (int total, int unlocked, int claimable) GetSummary() {
            int u=0,c=0; foreach(var kv in GetAllProgress()) if(kv.Value.isUnlocked){u++;if(!kv.Value.isRewardClaimed)c++;} return (GetAllAchievements().Count,u,c); }
        public static void SimulateUnlock(string id) {
            if(GetAllProgress().TryGetValue(id,out var p)&&!p.isUnlocked){p.isUnlocked=true;p.unlockTimestamp=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var d=D.Find(a=>a.id==id);if(d!=null)OnAchievementUnlocked?.Invoke(d);} }
        public static void ClaimReward(string id) { if(GetAllProgress().TryGetValue(id,out var p)&&p.isUnlocked&&!p.isRewardClaimed)p.isRewardClaimed=true; }
    }
    #endregion

    public class AchievementPanel : UIPanel
    {
        // 颜色常量
        static Color HC(string h){ColorUtility.TryParseHtmlString(h,out var c);return c;}
        static readonly Color HDR=HC("#1a1a2e"),CARD=new(.12f,.12f,.18f,.92f);
        static readonly Color BD_D=HC("#4CAF50"),BD_C=HC("#FFD700"),BD_P=HC("#2196F3"),BD_H=HC("#555555");
        static readonly Color TA=new(.25f,.55f,.95f),TN=new(.2f,.2f,.25f,.9f),PB=new(.2f,.2f,.25f),PF=new(.2f,.55f,.95f);
        static readonly Color TB=HC("#CD7F32"),TS=HC("#C0C0C0"),TG=HC("#FFD700");
        static readonly Dictionary<string,Color> IC=new(){["sword"]=HC("#E64A4A"),["skull"]=HC("#808080"),["dice"]=HC("#E6B82B"),["gem"]=HC("#66B3FF"),
            ["clock"]=HC("#4DE680"),["dragon"]=HC("#CC33CC"),["fire"]=HC("#FF8019"),["shield"]=HC("#B3B3E6"),["star"]=HC("#FFD933"),["coin"]=HC("#FFCC19"),["map"]=HC("#996633")};
        static readonly string[] CAT={"all","combat","collection","exploration","special"},CLB={"全部","战斗","收集","探索","特殊"};
        static Color TC(int t)=>t switch{1=>TB,2=>TS,_=>TG};
        static string TE(int t)=>t switch{1=>"\U0001F949",2=>"\U0001F948",_=>"\U0001F947"};
        static Font DF=>Resources.GetBuiltinResource<Font>("Arial.ttf");

        Text sTxt; readonly List<Button> tabs=new(); RectTransform lc; string cat="all";

        protected override void Awake(){base.Awake();panelId="Achievement";slideInAnimation=false;Build();}
        protected override void OnShow(){
            tabs.ForEach(b=>b.onClick.RemoveAllListeners());
            for(int i=0;i<CAT.Length;i++){string c=CAT[i];tabs[i].onClick.AddListener(()=>Sel(c));}
            Sum();cat="all";Hi("all");RL("all");
            rectTransform.anchoredPosition=new(0,-Screen.height);rectTransform.DOAnchorPosY(0,.4f).SetEase(Ease.OutBack);
        }
        protected override void OnHide(){tabs.ForEach(b=>b.onClick.RemoveAllListeners());rectTransform.DOAnchorPosY(-Screen.height,.3f).SetEase(Ease.InBack);DOTween.Kill(gameObject);}

        void Build(){
            var bg=GetComponent<Image>()??gameObject.AddComponent<Image>();bg.color=new(.08f,.08f,.12f,.97f);bg.raycastTarget=true;
            rectTransform.anchorMin=Vector2.zero;rectTransform.anchorMax=Vector2.one;rectTransform.offsetMin=rectTransform.offsetMax=Vector2.zero;
            var c=New("Content",transform);var cr=R(c);cr.anchorMin=new(.5f,0);cr.anchorMax=new(.5f,1);cr.pivot=new(.5f,1);cr.sizeDelta=new(640,0);cr.offsetMin=cr.offsetMax=Vector2.zero;
            float y=0;
            // 标题栏
            var h=New("Hdr",c.transform);var hr=R(h);hr.sizeDelta=new(0,64);hr.anchoredPosition=new(0,-y);h.AddComponent<Image>().color=HDR;
            Tx("T",h,(.05f,0,.8f,1),"🏆 成就",26,Color.white,TA.MiddleLeft,true);
            var cb=New("X",h.transform);var xr=R(cb);xr.anchorMin=new(.85f,.15f);xr.anchorMax=new(.95f,.85f);xr.offsetMin=xr.offsetMax=Vector2.zero;
            cb.AddComponent<Image>().color=new(.7f,.7f,.75f);var xBtn=cb.AddComponent<Button>();xBtn.targetGraphic=cb.GetComponent<Image>();
            var xt=cb.AddComponent<Text>();xt.text="✕";xt.font=DF;xt.fontSize=22;xt.color=Color.white;xt.alignment=TA.MiddleCenter;
            xBtn.onClick.AddListener(Hide); y+=64;
            // 摘要
            var sg=New("Sum",c.transform);var sr=R(sg);sr.sizeDelta=new(0,40);sr.anchoredPosition=new(0,-y);
            sTxt=sg.AddComponent<Text>();sTxt.font=DF;sTxt.fontSize=16;sTxt.color=new(.85f,.85f,.9f);sTxt.alignment=TA.MiddleCenter; y+=40;
            // Tab
            var tg=New("Tabs",c.transform);R(tg).sizeDelta=new(0,44);tg.GetComponent<RectTransform>().anchoredPosition=new(0,-y); tabs.Clear();
            for(int i=0;i<CAT.Length;i++){var b=New($"Tab_{CAT[i]}",tg.transform);var br=R(b);float w=1f/CAT.Length;
                br.anchorMin=new(i*w+.01f,.1f);br.anchorMax=new((i+1)*w-.01f,.9f);br.offsetMin=br.offsetMax=Vector2.zero;
                b.AddComponent<Image>().color=TN;var btn=b.AddComponent<Button>();btn.targetGraphic=b.GetComponent<Image>();
                var bt=b.AddComponent<Text>();bt.text=CLB[i];bt.font=DF;bt.fontSize=16;bt.color=Color.white;bt.alignment=TA.MiddleCenter;
                tabs.Add(btn);} y+=44;
            // 滚动列表
            var sv=New("SV",c.transform);var svr=R(sv);svr.anchorMin=Vector2.zero;svr.anchorMax=Vector2.one;svr.offsetMin=Vector2.zero;svr.offsetMax=new(0,-y);
            sv.AddComponent<Image>().color=new(.06f,.06f,.1f);
            var vp=New("VP",sv.transform);var vpr=R(vp);vpr.anchorMin=Vector2.zero;vpr.anchorMax=Vector2.one;vpr.offsetMin=vpr.offsetMax=Vector2.zero;vp.AddComponent<RectMask2D>();
            var lg=New("LC",vp.transform);lc=R(lg);lc.anchorMin=new(0,1);lc.anchorMax=new(1,1);lc.pivot=new(.5f,1);lc.offsetMin=lc.offsetMax=Vector2.zero;
            var s=sv.AddComponent<ScrollRect>();s.content=lc;s.viewport=vpr.GetComponent<RectTransform>();s.horizontal=false;s.vertical=true;s.movementType=ScrollRect.MovementType.Elastic;
        }

        void Sum(){var(t,u,c)=MockAchievementManager.GetSummary();if(sTxt)sTxt.text=$"{u}/{t} 已解锁  ⭐ {c} 待领奖";}
        void Sel(string c){cat=c;Hi(c);RL(c);}
        void Hi(string c){for(int i=0;i<CAT.Length;i++){var im=tabs[i].GetComponent<Image>();var tx=tabs[i].GetComponent<Text>();
            bool a=CAT[i]==c;if(im)im.color=a?TA:TN;if(tx)tx.color=a?Color.white:new(.7f,.7f,.75f);}}

        void RL(string c){
            for(int i=lc.childCount-1;i>=0;i--)Destroy(lc.GetChild(i).gameObject);
            var all=MockAchievementManager.GetAllAchievements();var pr=MockAchievementManager.GetAllProgress();
            var list=new List<(AchievementData d,AchievementProgress p)>();
            foreach(var d in all){if(c!="all"&&d.category!=c)continue;pr.TryGetValue(d.id,out var p);list.Add((d,p??new(){id=d.id}));}
            list.Sort((a,b)=>Sc(a).CompareTo(Sc(b)));
            int Sc((AchievementData d,AchievementProgress p)x)=>x switch{var t when t.p.isUnlocked&&!t.p.isRewardClaimed=>0,var t when !t.p.isUnlocked&&!t.d.isHidden=>1,var t when t.p.isUnlocked&&t.p.isRewardClaimed=>2,_=>3};
            float y=0;foreach(var(d,p)in list){Card(d,p,y);y+=108;}lc.sizeDelta=new(0,Mathf.Max(y,0));
        }

        void Card(AchievementData d,AchievementProgress p,float y){
            bool hide=d.isHidden&&!p.isUnlocked,done=p.isUnlocked&&p.isRewardClaimed,claim=p.isUnlocked&&!p.isRewardClaimed,prog=!p.isUnlocked&&!hide;
            var g=New($"Card_{d.id}",lc.transform);var rt=R(g);
            rt.anchorMin=new(.02f,1);rt.anchorMax=new(.98f,1);rt.pivot=new(.5f,1);rt.sizeDelta=new(0,100);rt.anchoredPosition=new(0,-y);
            g.AddComponent<Image>().color=CARD;
            var ol=g.AddComponent<Outline>();ol.effectColor=claim?BD_C:done?BD_D:prog?BD_P:BD_H;ol.effectDistance=new(2,-2);
            if(claim){var s=DOTween.Sequence();s.Append(DOTween.To(()=>ol.effectColor,c=>ol.effectColor=c,new(1f,.84f,0f),.5f));
                s.Append(DOTween.To(()=>ol.effectColor,c=>ol.effectColor=c,new(.7f,.55f,0f),.5f));s.SetLoops(-1,LoopType.Yoyo).SetTarget(g);}
            // 图标
            var ig=New("Ico",g.transform);var ir=R(ig);ir.anchorMin=ir.anchorMax=new(.03f,.2f);ir.pivot=Vector2.zero;ir.sizeDelta=new(60,60);
            ig.AddComponent<Image>().color=hide?new(.3f,.3f,.35f):IC.GetValueOrDefault(d.iconId,Color.gray);
            var it=ig.AddComponent<Text>();it.font=DF;it.fontSize=24;it.color=Color.white;it.alignment=TA.MiddleCenter;
            it.text=hide?"?":d.iconId switch{"sword"=>"⚔","skull"=>"💀","dice"=>"🎲","gem"=>"💎","clock"=>"⏱","dragon"=>"🐉","fire"=>"🔥","shield"=>"🛡","star"=>"⭐","coin"=>"💰","map"=>"🗺",_=>"?"};
            float L=.17f;
            Tx("N",g,(L,.6f,.72f,.88f),hide?"???":d.name,17,hide?new(.5f,.5f,.55f):Color.white,TA.MiddleLeft,true);
            Tx("D",g,(L,.35f,.95f,.58f),hide?"继续探索以解锁此成就":d.description,13,hide?new(.45f,.45f,.5f):new(.7f,.7f,.75f),TA.MiddleLeft);
            // 进度条
            var bg=New("Bar",g.transform);var br=R(bg);br.anchorMin=new(L,.08f);br.anchorMax=new(.72f,.3f);br.offsetMin=br.offsetMax=Vector2.zero;bg.AddComponent<Image>().color=PB;
            var fg=New("Fill",bg.transform);var fr=R(fg);fr.anchorMin=Vector2.zero;fr.pivot=new(0,.5f);fr.offsetMin=fr.offsetMax=Vector2.zero;
            float fa=p.isUnlocked?1f:hide?0f:Mathf.Clamp01((float)p.currentValue/d.targetValue);fr.anchorMax=new(fa,1f);
            fg.AddComponent<Image>().color=done?BD_D:claim?BD_C:PF;
            if(prog&&fa>0){fr.anchorMax=new(0,1f);fr.DOAnchorMax(new(fa,1f),.6f).SetEase(Ease.OutQuad).SetTarget(g);}
            Tx("P",g,(L,.08f,.72f,.3f),hide?"":p.isUnlocked?"✅ 已完成":$"{Mathf.Min(p.currentValue,d.targetValue)}/{d.targetValue}",12,Color.white,TA.MiddleCenter);
            // Tier徽章
            var tBg=New("Tier",g.transform);var tr=R(tBg);tr.anchorMin=new(.8f,.65f);tr.anchorMax=new(.97f,.95f);tr.offsetMin=tr.offsetMax=Vector2.zero;
            tBg.AddComponent<Image>().color=hide?new(.3f,.3f,.35f):TC(d.tier);
            var tt=tBg.AddComponent<Text>();tt.font=DF;tt.fontSize=18;tt.color=Color.white;tt.alignment=TA.MiddleCenter;tt.text=hide?"?":TE(d.tier);
            if(done)Tx("Ok",g,(.75f,.08f,.97f,.55f),"✅ 已完成",13,Color.white,TA.MiddleCenter,bg:new(.3f,.7f,.4f,.9f));
            if(claim){string cid=d.id;var cg=New("Clm",g.transform);var cr=R(cg);cr.anchorMin=new(.74f,.08f);cr.anchorMax=new(.98f,.55f);cr.offsetMin=cr.offsetMax=Vector2.zero;
                cg.AddComponent<Image>().color=new(.85f,.65f,.1f);var cb=cg.AddComponent<Button>();cb.targetGraphic=cg.GetComponent<Image>();
                var ct=cg.AddComponent<Text>();ct.text=$"领取 {d.rewardAmount}💰";ct.font=DF;ct.fontSize=13;ct.color=new(.2f,.1f,0);ct.alignment=TA.MiddleCenter;
                cb.onClick.AddListener(()=>Claim(cid));}
            if(prog){var dg=New("Dot",g.transform);var dr=R(dg);dr.anchorMin=dr.anchorMax=new(.74f,.15f);dr.pivot=new(.5f,.5f);dr.sizeDelta=new(12,12);dg.AddComponent<Image>().color=TC(d.tier);}
            rt.localScale=Vector3.one*.9f;rt.DOScale(Vector3.one,.25f).SetEase(Ease.OutBack).SetTarget(g);
        }

        void Claim(string id){
            MockAchievementManager.ClaimReward(id);
            for(int i=0;i<lc.childCount;i++){var ch=lc.GetChild(i);if(ch.name!=$"Card_{id}")continue;
                var cr=ch as RectTransform;if(cr)cr.DOScale(.92f,.15f).SetEase(Ease.InQuad).OnComplete(()=>cr.DOScale(Vector3.one,.2f).SetEase(Ease.OutBack));
                var fg=New("GF",ch.gameObject.transform);var fr=R(fg);fr.anchorMin=fr.anchorMax=new(.5f,.5f);fr.pivot=new(.5f,.5f);fr.sizeDelta=new(32,32);
                fg.AddComponent<Image>().color=BD_C;var ft=fg.AddComponent<Text>();ft.text="💰";ft.font=DF;ft.fontSize=24;ft.alignment=TA.MiddleCenter;ft.raycastTarget=false;
                fr.anchoredPosition=Vector2.zero;fr.DOAnchorPos(new(0,80),.6f).SetEase(Ease.OutQuad).OnComplete(()=>Destroy(fg));
                fr.DOScale(1.5f,.6f);fg.GetComponent<Image>().DOFade(0,.6f);break;}
            DOVirtual.DelayedCall(.7f,()=>{Sum();RL(cat);});
        }

        // UI辅助
        static GameObject New(string n,Transform p){var g=new GameObject(n);g.transform.SetParent(p,false);var r=g.AddComponent<RectTransform>();
            r.anchorMin=new(0,1);r.anchorMax=new(1,1);r.pivot=new(.5f,1);r.sizeDelta=Vector2.zero;r.anchoredPosition=Vector2.zero;return g;}
        static RectTransform R(GameObject g)=>g.GetComponent<RectTransform>();
        static Text Tx(string n,GameObject p,(float,float,float,float)a,string t,int s,Color c,TA al,bool b=false,Color?bg=null){
            var g=new GameObject(n);g.transform.SetParent(p.transform,false);var r=g.AddComponent<RectTransform>();
            r.anchorMin=new(a.Item1,a.Item2);r.anchorMax=new(a.Item3,a.Item4);r.offsetMin=r.offsetMax=Vector2.zero;
            if(bg.HasValue){var im=g.AddComponent<Image>();im.color=bg.Value;}
            var tx=g.AddComponent<Text>();tx.text=t;tx.font=DF;tx.fontSize=s;tx.color=c;tx.alignment=al;
            if(b)tx.fontStyle=FontStyle.Bold;return tx;}
    }
}
