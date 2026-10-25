using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Cinemachine;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.TestTools;
using UnityEngine.Timeline;

public sealed class IntroSlotSessionTests
{
 const string ScenePath="Assets/_Game/Scenes/Intro.unity";
 static T Read<T>(object o,string field)=>(T)o.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(o);
 static void Set(object o,string field,object value)=>o.GetType().GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(o,value);
 static IEnumerator Seconds(float t) { float end=Time.realtimeSinceStartup+t;while(Time.realtimeSinceStartup<end)yield return null; }
 static SlotMachineSprites Bank=>AssetDatabase.LoadAssetAtPath<SlotMachineSprites>("Assets/_Game/SlotMachine/SlotMachineSprites.asset");

 [TestCase(0,1,2,9)] [TestCase(0,0,0,12)] [TestCase(6,6,6,19)]
 public void RewardsAreExactlyOnceAndIndependent(int a,int b,int c,int expected) {
 var chips=new IntroSlotChips();Assert.That(chips.Balance,Is.EqualTo(10));
 Assert.That(chips.DebitAcceptedSpin(1),Is.True);Assert.That(chips.DebitAcceptedSpin(2),Is.False);
 Assert.That(chips.Refill(),Is.False);var result=new SlotSpinResult(a,b,c,Bank);
 Assert.That(chips.Settle(2,result,out _,out _),Is.False);
 Assert.That(chips.Settle(1,result,out _,out _),Is.True);Assert.That(chips.Balance,Is.EqualTo(expected));
 Assert.That(chips.Settle(1,result,out _,out _),Is.False);Assert.That(chips.Refund(1),Is.False);
 Assert.That(chips.Balance,Is.EqualTo(expected));
 }
 [Test]
 public void CancellationRefundAndRefillCannotCreateExtraCredit() {
 var chips=new IntroSlotChips();chips.DebitAcceptedSpin(1);Assert.That(chips.Refund(1),Is.True);Assert.That(chips.Refund(1),Is.False);Assert.That(chips.Balance,Is.EqualTo(10));
 var loss=new SlotSpinResult(0,1,2,Bank);
 for(int i=1;i<=10;i++){Assert.That(chips.DebitAcceptedSpin(i),Is.True);chips.Settle(i,loss,out _,out _);}
 Assert.That(chips.Balance,Is.Zero);Assert.That(chips.DebitAcceptedSpin(11),Is.False);
 Assert.That(chips.Refill(),Is.True);Assert.That(chips.Balance,Is.EqualTo(10));Assert.That(chips.Refill(),Is.False);
 Set(chips,"spinCost",11);Assert.That(chips.Refill(),Is.True);Assert.That(chips.CanSpin,Is.True);
 }
 [Test]
 public void SceneContainsFourPlayerStationsAndOriginalSlotNpc() {
 EditorSceneManager.OpenScene(ScenePath);
 var stations=Object.FindObjectsOfType<IntroSlotStation>();Assert.That(stations.Length,Is.EqualTo(4));
 Assert.That(Object.FindObjectsOfType<SlotMachineController>().Length,Is.EqualTo(5));
 Assert.That(Object.FindObjectsOfType<SlotMachineNpc>(true).Length,Is.EqualTo(1));
 foreach(var s in stations){Assert.That(s.Machine.IsConfigured,Is.True);Assert.That(s.CameraPose,Is.Not.Null);Assert.That(s.IsSpinControlHit(new Ray(s.CameraPose.position,Read<Collider>(s,"spinControl").bounds.center-s.CameraPose.position)),Is.True);}
 var guests=Object.FindObjectsOfType<IntroSlotNpcPresentation>();Assert.That(guests.Length,Is.EqualTo(1));
 var actor=Read<GameObject>(guests[0],"actor");Assert.That(actor.activeSelf,Is.True);
 Assert.That(actor.GetComponent<Animator>().isHuman,Is.True);Assert.That(actor.GetComponent<DealerAnimationController>(),Is.Null);
 Assert.That(GameObject.Find("PF_Dealer"),Is.Not.Null);
 var hud=Object.FindObjectOfType<IntroSlotHud>();
 Assert.That(hud.GetComponentsInChildren<UnityEngine.UI.Button>(true),Is.Empty);
 Assert.That(hud.GetComponentsInChildren<TMPro.TMP_Text>(true).Length,Is.EqualTo(1));
 var coinText=Read<TMPro.TMP_Text>(hud,"coinText");Assert.That(coinText.rectTransform.anchorMax,Is.EqualTo(Vector2.one));
 Assert.That(Read<GameObject>(Object.FindObjectOfType<IntroFlowController>(),"interactionPrompt").name,Is.EqualTo("Dealer E Prompt"));
 }

 [Test]
 public void BarTimelineControlsTheMovedSlotNpcAndPreservesCameraTrack() {
 EditorSceneManager.OpenScene(ScenePath);
 var npc=Object.FindObjectOfType<SlotMachineNpc>();
 var director=Read<PlayableDirector>(Object.FindObjectOfType<IntroFlowController>(),"barDirector");
 var timeline=(TimelineAsset)director.playableAsset;
 var group=timeline.GetRootTracks().OfType<GroupTrack>().Single(t=>t.name=="Slot Machine NPC");
 Assert.That(group.GetChildTracks().OfType<AnimationTrack>(),Is.Empty,"The existing NPC retains Animator/IK ownership");
 var activation=group.GetChildTracks().OfType<ActivationTrack>().Single();
 Assert.That(director.GetGenericBinding(activation),Is.SameAs(npc.gameObject));
 Assert.That(activation.postPlaybackState,Is.EqualTo(ActivationTrack.PostPlaybackState.Active));
 Assert.That(activation.GetClips().Single().duration,Is.EqualTo(timeline.duration).Within(.001));
 Assert.That(timeline.GetRootTracks().OfType<AnimationTrack>().Count(),Is.EqualTo(1));
 Assert.That(Object.FindObjectsOfType<IntroSlotStation>().All(s=>s.Machine!=npc.Machine),Is.True);
 Assert.That(AssetDatabase.LoadAssetAtPath<TimelineAsset>("Assets/_Game/CutScene/Intro/Intro_DealerAmbient.playable"),Is.Null);
 }
 [Test]
 public void ApproachRequiresDistanceFacingAndClearLineOfSight() {
 EditorSceneManager.OpenScene(ScenePath);
 var station=Object.FindObjectsOfType<IntroSlotStation>()[0];
 var probe=new GameObject("Approach probe");var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
 try {
 wall.SetActive(false);
 probe.transform.position=station.InteractionTarget.position+Vector3.back*4;
 probe.transform.LookAt(station.InteractionTarget);Assert.That(station.CanApproach(probe.transform),Is.False);
 probe.transform.position=station.InteractionTarget.position+Vector3.back*2;
 probe.transform.LookAt(station.InteractionTarget);Assert.That(station.CanApproach(probe.transform),Is.True);
 probe.transform.rotation*=Quaternion.Euler(0,180,0);Assert.That(station.CanApproach(probe.transform),Is.False);
 probe.transform.LookAt(station.InteractionTarget);
 wall.transform.position=(probe.transform.position+station.InteractionTarget.position)*.5f;
 wall.transform.localScale=Vector3.one*.4f;wall.SetActive(true);Physics.SyncTransforms();
 Assert.That(station.CanApproach(probe.transform),Is.False,"Cannot use a station through a wall");
 } finally { Object.DestroyImmediate(probe);Object.DestroyImmediate(wall); }
 }

 [UnityTest]
 public IEnumerator ActualIntro_UnloadDuringSpinResetsSessionAndNpcSurvivesCutsceneStop() {
 EditorSceneManager.OpenScene(ScenePath);yield return new EnterPlayMode();yield return Seconds(.8f);
 var flow=Object.FindObjectOfType<IntroFlowController>();var guest=Object.FindObjectOfType<IntroSlotNpcPresentation>();var actor=Read<GameObject>(guest,"actor");
 Assert.That(flow.BeginBarSequence(),Is.True);yield return Seconds(.3f);Assert.That(actor.activeSelf,Is.True);
 Read<PlayableDirector>(flow,"barDirector").Stop();yield return Seconds(.5f);Assert.That(actor.activeSelf,Is.True);
 var session=Object.FindObjectOfType<IntroSlotSession>();var station=Read<IntroSlotStation[]>(session,"stations")[0];
 var position=new Vector3(station.InteractionTarget.position.x,1.77f,22);
 Object.FindObjectOfType<PlayerMove>().WarpViewPose(position,Quaternion.LookRotation(station.InteractionTarget.position-position));yield return Seconds(.8f);
 Assert.That(flow.HandleInteractionInput(),Is.True);Assert.That(session.TrySpin(),Is.True);Assert.That(session.Chips.Balance,Is.EqualTo(9));
 UnityEngine.SceneManagement.SceneManager.LoadScene("Intro");yield return Seconds(.8f);
 Assert.That(session==null,Is.True);session=Object.FindObjectOfType<IntroSlotSession>();
 Assert.That(session.Chips.Balance,Is.EqualTo(10));Assert.That(session.Chips.HasPendingSpin,Is.False);Assert.That(session.IsActive,Is.False);
 Assert.That(Object.FindObjectOfType<PlayerMove>().IsMoveInputEnabled,Is.True);
 flow=Object.FindObjectOfType<IntroFlowController>();actor=Read<GameObject>(Object.FindObjectOfType<IntroSlotNpcPresentation>(),"actor");
 Assert.That(flow.BeginBarSequence(),Is.True);yield return Seconds(.3f);Assert.That(actor.activeSelf,Is.True);
 flow.enabled=false;yield return null;yield return null;Assert.That(actor.activeSelf,Is.False);
 yield return new ExitPlayMode();
 }

 [UnityTest]
 public IEnumerator ActualIntro_PlaySettlementCancellationExitAndDealerDialogue() {
 EditorSceneManager.OpenScene(ScenePath);yield return new EnterPlayMode();
 yield return RunPlayerSession();
 yield return new ExitPlayMode();
 }
 private IEnumerator RunPlayerSession() {
 UnityEngine.SceneManagement.SceneManager.LoadScene("Intro");yield return Seconds(.8f);
 var flow=Object.FindObjectOfType<IntroFlowController>();var player=Object.FindObjectOfType<PlayerMove>();
 Assert.That(flow,Is.Not.Null,"Intro flow loaded");Assert.That(player,Is.Not.Null,"Intro player loaded");
 var session=Object.FindObjectOfType<IntroSlotSession>();Assert.That(session,Is.Not.Null,"Intro session loaded");
 var stations=Read<IntroSlotStation[]>(session,"stations");Assert.That(stations,Is.Not.Null,"Stations array");Assert.That(stations[0],Is.Not.Null,"Assigned station");var station=stations[0];var machine=station.Machine;
 Assert.That(machine,Is.Not.Null,"Station machine");Debug.Log("SLOT REVIEW: session references valid");
 var guest=Object.FindObjectOfType<IntroSlotNpcPresentation>();Assert.That(guest,Is.Not.Null,"Guest controller");var actor=Read<GameObject>(guest,"actor");Assert.That(actor,Is.Not.Null,"Guest actor");
 var director=Read<PlayableDirector>(flow,"barDirector");var hold=Read<IntroHoldToSkip>(flow,"holdToSkip");
 Assert.That(flow.BeginBarSequence(),Is.True);Debug.Log("SLOT REVIEW: bar started");Assert.That(session.TryEnterNearby(),Is.False);
 yield return Seconds(2.5f);Assert.That(actor.activeSelf,Is.True);Capture("Cutscene-Guest");
 yield return Seconds(1.2f);Capture("Cutscene-Survey");
 float deadline=Time.realtimeSinceStartup+15;
 while(flow.State==IntroFlowController.IntroState.BarCutscene){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
 yield return null;Assert.That(actor.activeSelf,Is.True);Assert.That(flow.CanInteract,Is.True);
 var route=new[]{new Vector3(9.65f,1.77f,16.5f),new Vector3(9.65f,1.77f,21.8f),new Vector3(station.InteractionTarget.position.x,1.77f,22f)};
 Vector3 previous=player.transform.position;
 foreach(var next in route){
 var hits=Physics.CapsuleCastAll(previous+Vector3.up*.2f,previous-Vector3.up*.7f,.25f,(next-previous).normalized,(next-previous).magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
 Assert.That(hits.Where(h=>h.collider.gameObject!=player.gameObject).ToArray(),Is.Empty,"Central aisle must be reachable");
 player.WarpViewPose(next,Quaternion.LookRotation(station.InteractionTarget.position-next));yield return null;previous=next;
 }
 yield return Seconds(.8f);Capture("Approach");
 Assert.That(session.FindNearby(),Is.SameAs(station));
 Assert.That(Read<GameObject>(flow,"interactionPrompt").activeSelf,Is.True,"Reuse the original Intro E keycap");
 var before=player.transform.position;var rotation=player.transform.rotation;var cursor=Cursor.lockState;bool visible=Cursor.visible;
 Assert.That(flow.HandleInteractionInput(),Is.True);Assert.That(session.IsActive,Is.True);Assert.That(player.IsMoveInputEnabled,Is.False);
 Assert.That(flow.TryInteract(),Is.False);Assert.That(flow.HandleInteractionInput(),Is.False);Assert.That(flow.BeginBarSequence(),Is.False);
 hold.enabled=false;hold.SetAvailable(true);
 hold.Tick(true,2);flow.SkipIntro();Assert.That(flow.State,Is.EqualTo(IntroFlowController.IntroState.DealerInteraction));
 yield return Seconds(.8f);Capture("Play-Ready");
 Assert.That(Camera.main.GetComponent<CinemachineBrain>().ActiveVirtualCamera.Name,Is.EqualTo("Slot Play Camera"));
 machine.WinChance=0;
 Assert.That(session.TrySpin(),Is.True);Assert.That(session.Chips.Balance,Is.EqualTo(9));Assert.That(session.TrySpin(),Is.False);
 int ticket=Read<int>(session,"pendingTicket");session.RequestExit();Assert.That(session.IsActive,Is.True);Assert.That(session.IsExitRequested,Is.True);
 yield return SpinFinished(machine);Assert.That(session.IsActive,Is.False);Assert.That(session.Chips.Balance,Is.EqualTo(9));
 session.GetType().GetMethod("Complete",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(session,new object[]{ticket,machine.LastResult});Assert.That(session.Chips.Balance,Is.EqualTo(9));
 Assert.That(player.transform.position,Is.EqualTo(before));Assert.That(Quaternion.Angle(player.transform.rotation,rotation),Is.LessThan(.01));Assert.That(player.IsMoveInputEnabled,Is.True);Assert.That(Cursor.lockState,Is.EqualTo(cursor));Assert.That(Cursor.visible,Is.EqualTo(visible));
 hold.Tick(true,2);Assert.That(flow.State,Is.EqualTo(IntroFlowController.IntroState.DealerInteraction));hold.Tick(false,.01f);hold.enabled=true;
 yield return Seconds(.8f);Assert.That(flow.HandleInteractionInput(),Is.True);
 Assert.That(machine.TrySpin(),Is.True);Assert.That(session.TrySpin(),Is.False);Assert.That(session.Chips.Balance,Is.EqualTo(9));machine.enabled=false;machine.enabled=true;
 Assert.That(session.TrySpin(),Is.True);machine.enabled=false;Assert.That(session.Chips.Balance,Is.EqualTo(9));machine.enabled=true;
 machine.WinChance=1;Assert.That(session.TrySpin(),Is.True);session.enabled=false;Assert.That(player.IsMoveInputEnabled,Is.True);
 yield return SpinFinished(machine);int reward=machine.LastResult.Outcome==SlotOutcome.Jackpot?10:3;Assert.That(session.Chips.Balance,Is.EqualTo(8+reward));
 session.enabled=true;yield return Seconds(.8f);Assert.That(flow.HandleInteractionInput(),Is.True);
 int balanceBeforeWin=session.Chips.Balance;
 Assert.That(session.TrySpin(),Is.True);yield return SpinFinished(machine);
 reward=machine.LastResult.Outcome==SlotOutcome.Jackpot?10:3;
 Assert.That(session.Chips.Balance,Is.EqualTo(balanceBeforeWin-1+reward));Capture("Play-Win");
 Set(machine,"spinDuration",.5f);Set(machine,"stopInterval",.2f);machine.WinChance=0;
 while(session.Chips.Balance>0){Assert.That(session.TrySpin(),Is.True);yield return SpinFinished(machine);}
 Assert.That(session.TrySpin(),Is.False);Assert.That(session.Chips.Balance,Is.Zero);yield return null;Capture("Play-Refill");session.Refill();Assert.That(session.Chips.Balance,Is.EqualTo(10));session.Refill();Assert.That(session.Chips.Balance,Is.EqualTo(10));
 session.RequestExit();yield return Seconds(.8f);
 var second=stations[1];var position=new Vector3(second.InteractionTarget.position.x,1.77f,22f);player.WarpViewPose(position,Quaternion.LookRotation(second.InteractionTarget.position-position));yield return Seconds(.2f);
 Assert.That(session.FindNearby(),Is.SameAs(second));Assert.That(flow.HandleInteractionInput(),Is.True);Assert.That(session.Chips.Balance,Is.EqualTo(10));session.RequestExit();
 var dealer=Read<Transform>(flow,"dealerLookTarget");position=new Vector3(9.65f,1.77f,14.8f);player.WarpViewPose(position,Quaternion.LookRotation(dealer.position-position));yield return Seconds(.8f);
 Assert.That(flow.HandleInteractionInput(),Is.True);Assert.That(session.IsActive,Is.False);Assert.That(flow.State,Is.EqualTo(IntroFlowController.IntroState.Seating));Assert.That(session.TryEnterNearby(),Is.False);
 deadline=Time.realtimeSinceStartup+10;while(flow.State==IntroFlowController.IntroState.Seating){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
 Assert.That(flow.State,Is.EqualTo(IntroFlowController.IntroState.Dialogue));Assert.That(session.TryEnterNearby(),Is.False);yield return Seconds(.8f);Capture("Dealer-Dialogue");
 var dialogue=Read<DialogueController>(flow,"dialogueController");while(dialogue.IsRunning){dialogue.Next();yield return null;}
 Assert.That(flow.State,Is.EqualTo(IntroFlowController.IntroState.Transition));Assert.That(session.TryEnterNearby(),Is.False);
 deadline=Time.realtimeSinceStartup+30;
 while(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Dev_Yujin") { Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null; }
 Assert.That(Object.FindObjectsOfType<IntroSlotSession>(),Is.Empty);
 while(Object.FindObjectOfType<IntroFadeController>()!=null) { Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null; }
 yield return null;
 }

 [UnityTest]
 public IEnumerator ActualIntro_SkipHidesNpcAndDevSceneHasNoSlotNpc() {
 EditorSceneManager.OpenScene(ScenePath);yield return new EnterPlayMode();yield return Seconds(.8f);
 var flow=Object.FindObjectOfType<IntroFlowController>();var actor=Read<GameObject>(Object.FindObjectOfType<IntroSlotNpcPresentation>(),"actor");
 flow.BeginBarSequence();yield return Seconds(1);Assert.That(actor.activeSelf,Is.True);flow.SkipIntro();yield return null;yield return null;Assert.That(actor.activeSelf,Is.False);
 float deadline=Time.realtimeSinceStartup+30;while(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name!="Dev_Yujin"){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
 yield return Seconds(1.5f);Assert.That(Object.FindObjectsOfType<SlotMachineNpc>(),Is.Empty);Assert.That(Object.FindObjectsOfType<IntroSlotSession>(),Is.Empty);
 
 
 yield return new ExitPlayMode();
 }
 [UnityTest]
 public IEnumerator ActualIntro_OriginalNpcCoinsSpinAndPayoutSurviveTimeline() {
 EditorSceneManager.OpenScene(ScenePath);yield return new EnterPlayMode();yield return Seconds(.8f);
 var npc=Object.FindObjectOfType<SlotMachineNpc>();var flow=Object.FindObjectOfType<IntroFlowController>();
 var session=Object.FindObjectOfType<IntroSlotSession>();var origin=npc.transform.position;
 Assert.That(npc.isActiveAndEnabled,Is.True);
 Assert.That(Read<Transform>(npc,"coinContact"),Is.Not.Null);
 Assert.That(Read<Transform>(npc,"payoutOrigin").gameObject.scene,Is.EqualTo(npc.gameObject.scene));
 var coins=Read<Transform[]>(npc,"payoutCoins");Assert.That(coins.Length,Is.EqualTo(6));
 var coin=Read<Transform>(npc,"coinProp");npc.Machine.WinChance=1;
 float deadline=Time.realtimeSinceStartup+45;
 while(!coin.gameObject.activeInHierarchy){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}
 Assert.That(flow.BeginBarSequence(),Is.True);
 float began=Time.realtimeSinceStartup;bool spin=false,payout=false,captured=false;
 while(!payout || flow.State==IntroFlowController.IntroState.BarCutscene){
 Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));
 spin|=npc.IsWaitingForResult;
 payout|=coins.Any(c=>c.gameObject.activeInHierarchy);
 Assert.That(npc.gameObject.activeInHierarchy,Is.True);
 Assert.That(session.Chips.Balance,Is.EqualTo(10),"NPC visual credits never debit player coins");
 Assert.That(Vector3.Distance(npc.transform.position,origin),Is.LessThan(.001f));
 if(!captured && Time.realtimeSinceStartup-began>=2.5f){Capture("Relocated-NPC-Cutscene");captured=true;}
 yield return null;
 }
 Assert.That(spin,Is.True);Assert.That(npc.Machine.LastResult.IsWin,Is.True);
 Assert.That(npc.Machine.IsConfigured,Is.True);
 var pos=new Vector3(npc.transform.position.x+1.5f,1.77f,npc.transform.position.z-2);
 Object.FindObjectOfType<PlayerMove>().WarpViewPose(pos,Quaternion.LookRotation(npc.transform.position+Vector3.up*1.3f-pos));
 yield return Seconds(.8f);Capture("Relocated-NPC-FreeRoam");
 yield return new ExitPlayMode();
 }
 static IEnumerator SpinFinished(SlotMachineController machine) { float deadline=Time.realtimeSinceStartup+10;while(machine.IsSpinning){Assert.That(Time.realtimeSinceStartup,Is.LessThan(deadline));yield return null;}yield return null; }
 [UnityTearDown] public IEnumerator LeavePlayMode() { if(EditorApplication.isPlaying)yield return new ExitPlayMode(); }
 static void Capture(string name) {
 if(SystemInfo.graphicsDeviceType==UnityEngine.Rendering.GraphicsDeviceType.Null)return;
 Directory.CreateDirectory("Logs/IntroSlots/Frames");
 var cam=Camera.main;var canvases=Object.FindObjectsOfType<Canvas>().Where(c=>c.renderMode==RenderMode.ScreenSpaceOverlay).ToArray();
 var rt=new RenderTexture(1280,720,24);var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);var old=RenderTexture.active;var target=cam.targetTexture;float aspect=cam.aspect;
 try {foreach(var c in canvases){c.renderMode=RenderMode.ScreenSpaceCamera;c.worldCamera=cam;c.planeDistance=cam.nearClipPlane+.05f;}cam.aspect=1280f/720;cam.targetTexture=rt;Canvas.ForceUpdateCanvases();cam.Render();RenderTexture.active=rt;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();File.WriteAllBytes("Logs/IntroSlots/Frames/"+name+".png",pixels.EncodeToPNG());}
 finally {foreach(var c in canvases)c.renderMode=RenderMode.ScreenSpaceOverlay;cam.targetTexture=target;cam.aspect=aspect;RenderTexture.active=old;Object.DestroyImmediate(rt);Object.DestroyImmediate(pixels);}
 }
}

