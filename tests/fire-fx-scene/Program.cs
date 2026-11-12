using System;
using System.Collections.Generic;
using System.Linq;
using MiniJSON;
using UnityEngine;
using UnityEngine.SceneManagement;

var root = new GameObject("board"); var spell = root.Add("ReuseFX_Missile_Fire_Fireball_Medium");
spell.Components["Spell"] = new TestSpell(spell); var particle = spell.Add("FireParticleBall"); particle.Components["ParticleSystem"] = new Component(particle);
var custom = root.Add("JAILFX_JailhouseManastorm_CustomSummon(Clone)");
var manastormParticle = custom.Add("DetachRoot").Add("PS_KeyForming"); manastormParticle.Components["ParticleSystem"] = new Component(manastormParticle);
var immolate = root.Add("Card_Hand_Immolate(Clone)"); immolate.Components["Spell"] = new TestSpell(immolate);
immolate.Add("PersistentFlame").Components["ParticleSystem"] = new Component(immolate);
var dark = root.Add("FireHidden"); dark.activeInHierarchy = false;
SceneManager.Scenes.Add(new Scene(new[] { root }));
var snapshot = FireFxScene.Read(-1, -1); var rows = (JsonList)snapshot["objects"];
Check(rows.Count == 5 && !(bool)snapshot["truncated"], "all active spells and particles, independent of names");
Check(rows.Cast<JsonNode>().Any(r => (string)r["name"] == "PersistentFlame" && (string)r["spell"] == immolate.name),
    "Immolate descendants included even without fire in their names");
Check(rows.Cast<JsonNode>().Any(r => (string)r["name"] == "PS_KeyForming"), "non-fire Manastorm descendants included");
var p = rows.Cast<JsonNode>().Single(r => (string)r["name"] == "FireParticleBall");
Check((string)p["spell"] == spell.name && (string)p["spellState"] == "POWER" && (bool)p["particle"], "particle parent and spell state");
Check((int)p["spellComponentId"] != (int)p["spellInstance"] && (int)p["spellComponentId"] == spell.Components["Spell"].GetInstanceID(), "spell component ID joins telemetry, game object ID stays separate");
Check(((string)p["path"]).Contains("board/ReuseFX_Missile"), "ancestry");
try { FireFxScene.Read(0, 99); throw new Exception("accepted missing root"); } catch (InvalidOperationException) { }
var many = new GameObject("other"); for (int i = 0; i < 100; i++) many.Add("Fire" + i);
SceneManager.Scenes.Clear(); SceneManager.Scenes.Add(new Scene(new[] { many }));
var bounded = FireFxScene.Read(0,0,"Fire"); Check(((JsonList)bounded["objects"]).Count == 64 && (int)bounded["nextCursor"] > 0 && !(bool)bounded["complete"], "result page exposes continuation");
var next = FireFxScene.Read(-1,-1,null,(int)bounded["nextCursor"]);
Check(((JsonList)next["objects"]).Count == 36 && (bool)next["complete"], "continuation visits every remaining sibling");
var arbitrary = FireFxScene.Read(0,0,"Fire70"); Check(((JsonList)arbitrary["objects"]).Count == 1, "any name selector");
var slow = new GameObject("slow"); for (int i = 0; i < 10; i++) slow.Add("Fire" + i);
SceneManager.Scenes.Clear(); SceneManager.Scenes.Add(new Scene(new[] { slow }));
slow.transform.DelayFirstChild = true;
var timed = FireFxScene.Read(0,0,"Fire");
Check((int)timed["nextCursor"] > 0 && slow.transform.ChildCalls == 1, "child iteration honors cooperative time limit");
var resumed = FireFxScene.Read(-1,-1,null,(int)timed["nextCursor"]);
Check(((JsonList)resumed["objects"]).Count == 10 && (bool)resumed["complete"], "time-limited scan resumes without losing children");
Console.WriteLine("PASS paged read-only spell/particle scene inventory");
static void Check(bool value, string message) { if (!value) throw new Exception(message); }
class TestSpell : Component { public TestSpell(GameObject go) : base(go) {} public string GetActiveState() => "POWER"; }
static class NativeTestControl { public static void AssertMainThread() {} }
namespace MiniJSON { class JsonNode : Dictionary<string, object> {} class JsonList : List<object> {} }
namespace UnityEngine {
 public struct Vector3 { public float x,y,z; }
 public class Component { public GameObject gameObject; public Component(GameObject g) {gameObject=g;} public int GetInstanceID()=>-gameObject.GetInstanceID(); }
 public class Transform { public GameObject gameObject; public Transform parent; public Vector3 position; public string name => gameObject.name; public readonly List<Transform> children=new(); public int childCount=>children.Count; public bool DelayFirstChild; public int ChildCalls; public Transform GetChild(int i){ChildCalls++; if (DelayFirstChild && ChildCalls==1) System.Threading.Thread.Sleep(60); return children[i];} }
 public class GameObject { public string name; public bool activeInHierarchy=true; public Transform transform; public readonly Dictionary<string,Component> Components=new(); public GameObject(string n){name=n;transform=new Transform{gameObject=this};} public GameObject Add(string n){var x=new GameObject(n); x.transform.parent=transform; transform.children.Add(x.transform); return x;} public Component GetComponent(string n)=>Components.TryGetValue(n,out var c)?c:null; public int GetInstanceID()=>GetHashCode(); }
}
namespace UnityEngine.SceneManagement {
 public struct Scene { private GameObject[] roots; public Scene(GameObject[] r){roots=r;} public bool isLoaded=>true; public GameObject[] GetRootGameObjects()=>roots; }
 public static class SceneManager { public static readonly List<Scene> Scenes=new(); public static int sceneCount=>Scenes.Count; public static Scene GetSceneAt(int i)=>Scenes[i]; }
}
