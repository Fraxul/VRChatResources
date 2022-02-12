using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

class FraxulPrefabUtils : MonoBehaviour {

  static void RevertProperty(SerializedObject so, string propertyName) {
    var prop = so.FindProperty(propertyName);
    PrefabUtility.RevertPropertyOverride(prop, InteractionMode.AutomatedAction);
  }

  [MenuItem("Tools/Fraxul/Recursive Revert Static Flags")]
  static void RevertStaticFlags() {
    var selectedTransforms = Selection.GetTransforms(SelectionMode.Deep | SelectionMode.Editable);
    foreach (Transform xf in selectedTransforms) {
      GameObject obj = xf.gameObject;
      if (!PrefabUtility.IsPartOfPrefabInstance(obj)) {
        Debug.Log(string.Format("{0}: not part of a prefab instance", obj.name));
        continue;
      }
      Undo.RecordObject(obj, "Revert Static Flags");

      var so = new SerializedObject(obj);
      RevertProperty(so, "m_StaticEditorFlags");
      if (so.ApplyModifiedProperties())
        PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
    }
  }


  [MenuItem("Tools/Fraxul/Recursive Revert MeshRenderer Lighting Settings")]
  static void RevertMeshRendererLightingSettings() {
    var selectedTransforms = Selection.GetTransforms(SelectionMode.Deep | SelectionMode.Editable);
    foreach (Transform xf in selectedTransforms) {
      GameObject obj = xf.gameObject;
      if (!PrefabUtility.IsPartOfPrefabInstance(obj)) {
        //Debug.Log(string.Format("{0}: not part of a prefab instance", obj.name));
        continue;
      }

      MeshRenderer mr = obj.GetComponent<MeshRenderer>();
      if (!mr)
        continue;

      Undo.RecordObject(mr, "Revert MeshRenderer Lighting Settings");

      var so = new SerializedObject(mr);
      RevertProperty(so, "m_LightProbeUsage");
      RevertProperty(so, "m_ReflectionProbeUsage");
      RevertProperty(so, "m_ReceiveGI");
      if (so.ApplyModifiedProperties())
        PrefabUtility.RecordPrefabInstancePropertyModifications(mr);
    }
  }


  static bool ClearStaticFlag(GameObject go, StaticEditorFlags flag) {
    StaticEditorFlags sf = GameObjectUtility.GetStaticEditorFlags(go);
    if ((sf & flag) != 0) {
      GameObjectUtility.SetStaticEditorFlags(go, sf ^ flag);
      return true; // modified
    }
    return false; // not modified
  }

  static bool SetStaticFlags(GameObject go, StaticEditorFlags flags) {
    if (GameObjectUtility.GetStaticEditorFlags(go) == flags)
      return false;

    GameObjectUtility.SetStaticEditorFlags(go, flags);
    return true;
  }

  static bool RecursiveApply(GameObject go, Func<GameObject, bool> modificationFn) {
    bool dirty = modificationFn(go);
    for (int childIdx = 0; childIdx < go.transform.childCount; ++childIdx) {
      dirty |= RecursiveApply(go.transform.GetChild(childIdx).gameObject, modificationFn);
    }
    return dirty;
  }

  static void BatchModifyPrefabs(Func<GameObject, bool> modificationFn) {
    foreach (string guid in Selection.assetGUIDs) {
      
      string assetPath = AssetDatabase.GUIDToAssetPath(guid);
      var assetType = AssetDatabase.GetMainAssetTypeAtPath(assetPath);
      if (assetType != typeof(GameObject)) {
        Debug.LogWarning(string.Format("Selected asset {0} is not a Prefab (main asset type: {1})", assetPath, assetType.ToString()));
        continue;
      }

      GameObject contentsRoot = PrefabUtility.LoadPrefabContents(assetPath);
      if (!contentsRoot) {
        Debug.LogWarning(string.Format("Can't open Prefab {0}", assetPath));
        continue;
      }

      bool dirty = RecursiveApply(contentsRoot, modificationFn);

      if (dirty)
        PrefabUtility.SaveAsPrefabAsset(contentsRoot, assetPath);

      PrefabUtility.UnloadPrefabContents(contentsRoot);
    }
  }
    

  [MenuItem("Tools/Fraxul/Prefab Editing/Remove Static Batching Flags")]
  static void RemoveStaticBatchingFlags() {
    BatchModifyPrefabs( (GameObject go) => {
      return ClearStaticFlag(go, StaticEditorFlags.BatchingStatic);
    });

  }

  [MenuItem("Tools/Fraxul/Prefab Editing/Set Static for Medium Props (Occludee, Reflection, GI Light Probe)")]
  static void SetMediumPropStatic() {
    BatchModifyPrefabs( (GameObject go) => {
      bool dirty = SetStaticFlags(go, StaticEditorFlags.OccludeeStatic | StaticEditorFlags.ReflectionProbeStatic | StaticEditorFlags.ContributeGI);
      var mr = go.GetComponent<MeshRenderer>();
      if (mr && mr.receiveGI != ReceiveGI.LightProbes) {
        mr.receiveGI = ReceiveGI.LightProbes;
        dirty = true;
      }

      return dirty;
    });

  }


}

