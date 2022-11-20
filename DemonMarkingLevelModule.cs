using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using ThunderRoad;
using System.Collections;
using Newtonsoft.Json;
using System.IO;

namespace DemonMarking
{
    public class DemonMarkingLevelModule : LevelModule
    {
        public List<DemonMarkingDecals> decalMarkings { get; private set; } = new List<DemonMarkingDecals>();
        private List<GameObject> decalsGO = new List<GameObject>();
        private List<Material> decalsMat = new List<Material>();
        private float speedOfGlow = 1f;
        private Creature creaturePossessed;
        public override IEnumerator OnLoadCoroutine()
        {
            EventManager.onPossess += EventManager_onPossess;
            return base.OnLoadCoroutine();
        }

        private void EventManager_onPossess(Creature creature, EventTime eventTime)
        {
            if (eventTime == EventTime.OnEnd)
            {

                if (!creature.data.id.Contains("PlayerDefault"))
                {
                    Debug.Log($"DemonMarking : Not a PlayerDefault creature, cannot use this mod");
                    return;
                }
                string gender = creature.data.id.Substring("PlayerDefault".Length);
                decalMarkings = JsonConvert.DeserializeObject<List<DemonMarkingDecals>>(File.ReadAllText(Path.Combine(Application.streamingAssetsPath, "Mods", "DemonMarking", "decals_" + gender + ".json")));
                creaturePossessed = creature;
                creaturePossessed.OnDespawnEvent += Creature_OnDespawnEvent;
                foreach (DemonMarkingDecals markingDecals in decalMarkings)
                {
                    if (!string.IsNullOrEmpty(markingDecals.decalID))
                    {
                        SpawnDecal(markingDecals.decalID, markingDecals.decalPartName, markingDecals.position, markingDecals.rotation, markingDecals.scale);
                    }
                    else
                    {
                        Debug.Log($"DemonMarking : decalID is null; No decal can be spawned");
                    }
                }
                Player.local.StartCoroutine(ColorGlow());
            }
        }

        private void Creature_OnDespawnEvent(EventTime eventTime)
        {
            if (eventTime == EventTime.OnStart)
            {
                if (decalsGO.Count > 0)
                {
                    Player.local.StopCoroutine(ColorGlow());
                    foreach (GameObject GO in decalsGO)
                        UnityEngine.Object.Destroy(GO);
                }
                decalsGO.Clear();
                decalsMat.Clear();
                creaturePossessed.OnDespawnEvent -= Creature_OnDespawnEvent;
            }
        }

        public override void OnUnload()
        {
            base.OnUnload();
            EventManager.onPossess -= EventManager_onPossess;
        }

        private void SpawnDecal(string decalID, string partName, Vector3 decalPosition, Vector3 decalRotation, Vector3 decalScale)
        {
            RagdollPart ragdollPart = GetRagdollPartByName(partName);
            if (ragdollPart != null)
            {
                string[] decalIDSplit = decalID.Split('.');
                Catalog.InstantiateAsync(decalID, Vector3.zero, Quaternion.identity, null, decal =>
                {
                    decal.transform.position = Vector3.zero;
                    decal.transform.rotation = Quaternion.identity;
                    decal.transform.SetParent(ragdollPart.transform);
                    decal.transform.localPosition = decalPosition;
                    decal.transform.localEulerAngles = decalRotation;
                    decal.transform.localScale = decalScale;
                    decalsGO.Add(decal);
                    decalsMat.Add(decal.GetComponent<MeshRenderer>().material);
                }, decalIDSplit.Last());
                Debug.Log($"DemonMarking : Decal spawned : {decalIDSplit.Last()}");
            }
            else
            {
                Debug.Log($"DemonMarking : No Part called : {partName} for {decalID}; No decal can be spawned");
            }
        }

        IEnumerator ColorGlow()
        {
            while (true)
            {
                int i = 0;
                foreach (Material material in decalsMat)
                {
                    material.SetColor("_Color", HDRColor(Color.white, Mathf.PingPong(Time.time * 0.5f * speedOfGlow + i, 1.75f)));
                    i++;
                }
                yield return null;
            }
        }

        private Color HDRColor(Color color, float intensity)
        {
            return color * Mathf.Pow(2, intensity);
        }

        private RagdollPart GetRagdollPartByName(string partName)
        {
            RagdollPart part = null;
            if (!string.IsNullOrEmpty(partName))
            {
                foreach (RagdollPart ragdollPart in Player.local.creature.ragdoll.parts)
                {
                    if (ragdollPart.name == partName)
                    {
                        part = ragdollPart;
                        break;
                    }
                }
            }
            return part;
        }
    }
}
