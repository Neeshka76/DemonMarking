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
    public class DemonMarkingLevelModule : ThunderScript
    {
        public List<DemonMarkingDecals> decalMarkings { get; private set; } = new List<DemonMarkingDecals>();
        private List<GameObject> decalsGO = new List<GameObject>();
        private List<Material> decalsMat = new List<Material>();
        private List<Material> eyesMat = new List<Material>();
        private Material demonEyeMaterial;

        public static ModOptionFloat[] GlowOption()
        {
            List<ModOptionFloat> options = new List<ModOptionFloat>();
            float val = 0.0f;
            while (val <= 2f)
            {
                options.Add(new ModOptionFloat(val.ToString("0.00"), val));
                val += 0.25f;
            }
            return options.ToArray();
        }

        [ModOption(name: "Speed Of Glow", tooltip: "Glowing factor of the Demon Marking", valueSourceName: nameof(GlowOption), defaultValueIndex = 4)]
        private static float speedOfGlow = 1f;
        private Creature creaturePossessed;

        public override void ScriptEnable()
        {
            EventManager.onPossess += EventManager_onPossess;
            Catalog.LoadAssetAsync<Material>("Neeshka.DemonMarking.Mat_Human_InnerEyeDemon", mat => demonEyeMaterial = mat, "demonEyeMaterial");
            base.ScriptEnable();
        }

        public override void ScriptDisable()
        {
            EventManager.onPossess -= EventManager_onPossess;
            base.ScriptDisable();
        }

        private void EventManager_onPossess(Creature creature, EventTime eventTime)
        {
            if (eventTime == EventTime.OnEnd)
            {
                if (!creature.data.id.Contains("PlayerDefault"))
                {
                    Snippet.DebugLog($"DemonMarking : Not a PlayerDefault creature, cannot use this mod", "red", true);
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
                        Snippet.DebugLog($"DemonMarking : decalID is null; No decal can be spawned", "red", true);
                    }
                }
                Player.local.StartCoroutine(ColorGlow());
                Player.local.StartCoroutine(AddNewMatToEye(creature));
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

                Snippet.DebugLog($"DemonMarking : Decal spawned : {decalIDSplit.Last()}", "yellow");
            }
            else
            {
                Snippet.DebugLog($"DemonMarking : No Part called : {partName} for {decalID}; No decal can be spawned", "red", true);
            }
        }

        private IEnumerator AddNewMatToEye(Creature creature)
        {
            yield return new WaitUntil(() => demonEyeMaterial != null && creature.renderers.Count > 0);
            creature.renderers.ForEach(cr =>
            {
                Material mat = cr.renderer.material;
                //Snippet.DebugLog($"DemonMarking : Mat name : {mat.name}", true, "cyan");
                //if (mat != null)
                //{
                //    
                //    if (mat.name.Contains("Human_InnerEye"))
                //    {
                //        mat = demonEyeMaterial;
                //        eyesMat.Add(mat);
                //        cr.renderer.material = mat;
                //        Snippet.DebugLog($"DemonMarking : Added eyes : {demonEyeMaterial} for {mat.name};", true, "yellow");
                //    }
                //}
                Material[] mats = cr.renderer.materials;
                bool found = false;
                if (mats.Length > 0)
                {
                    for (int k = mats.Length - 1; k >= 0; k--)
                    {
                        //Snippet.DebugLog($"DemonMarking : Mats[{k}] name : {mats[k].name}", true, "cyan");
                        if (mats[k].name.Contains("Human_InnerEye"))
                        {
                            Snippet.DebugLog($"DemonMarking : Added eyes : {demonEyeMaterial} for {mats[k].name};", "yellow");
                            mats[k] = demonEyeMaterial;
                            eyesMat.Add(mats[k]);
                            found = true;
                        }
                    }
                    if (found)
                        cr.renderer.materials = mats;
                }
                else
                {
                    Snippet.DebugLog($"DemonMarking : No material containing : Human_InnerEye; Eyes cannot be replaced", "red", true);
                }
            });
            yield return null;
        }

        IEnumerator ColorGlow()
        {
            while (true)
            {
                int i = 0;
                foreach (Material material in decalsMat)
                {
                    material.SetColor("_Color", Snippet.HDRColor(Color.white, Mathf.PingPong(Time.time * 0.5f * speedOfGlow + i, 1.75f)));
                    i++;
                }
                int j = 1;
                foreach (Material material in eyesMat)
                {
                    material.SetColor("_EmissionColor", Snippet.HDRColor(Color.white, Mathf.PingPong(Time.time * 0.5f * speedOfGlow * 2 + j, 6f - 4f) + 4f));
                    j++;
                }
                yield return null;
            }
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
