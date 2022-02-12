using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public class FraxulSceneVisibilityUtils : EditorWindow {

  static void RecursiveSelectObjects(ref List<GameObject> objs, GameObject go, Func<GameObject, bool> filterFn, bool skipEditorOnly) {
    if (skipEditorOnly && go.CompareTag("EditorOnly")) {
      return; // skip object and descendents
    }
    if (filterFn(go)) {
      objs.Add(go);
    }
    for (int childIdx = 0; childIdx < go.transform.childCount; ++childIdx) {
      RecursiveSelectObjects(ref objs, go.transform.GetChild(childIdx).gameObject, filterFn, skipEditorOnly);
    }
  }

  static List<GameObject> SelectObjects(Func<GameObject, bool> filterFn, bool skipEditorOnly = true) {
    List<GameObject> res = new List<GameObject>();
    var scene = EditorSceneManager.GetActiveScene();
    var rootGameObjects = scene.GetRootGameObjects();
    foreach (var go in rootGameObjects) {
      RecursiveSelectObjects(ref res, go, filterFn, skipEditorOnly);
    }
    return res;
  }


  static void RecursiveApplyVisibility(GameObject go, Func<GameObject, bool> testFn) {
    bool res = testFn(go);
    if (res) {
      SceneVisibilityManager.instance.Show(go, /*includeDescendants=*/ false);
    } else {
      SceneVisibilityManager.instance.Hide(go, /*includeDescendants=*/ false);
    }
    for (int childIdx = 0; childIdx < go.transform.childCount; ++childIdx) {
      RecursiveApplyVisibility(go.transform.GetChild(childIdx).gameObject, testFn);
    }
  }

  static void ApplyVisibility(Func<GameObject, bool> testFn) {
    var scene = EditorSceneManager.GetActiveScene();
    var rootGameObjects = scene.GetRootGameObjects();
    foreach (var go in rootGameObjects) {
      RecursiveApplyVisibility(go, testFn);
    }
  }


  static void RecursiveInvertVisibility(GameObject go) {
    // Test for some fast paths using the visibility manager's cache.
    // The simple solution of calling ToggleVisibility with includeDescendants=false on every object in the scene is surprisingly slow.
    if (SceneVisibilityManager.instance.IsHidden(go, /*includeDescendants=*/false) && SceneVisibilityManager.instance.AreAllDescendantsHidden(go)) {
      SceneVisibilityManager.instance.Show(go, /*includeDescendants=*/ true);
    } else if ((!SceneVisibilityManager.instance.IsHidden(go, /*includeDescendants=*/false)) && SceneVisibilityManager.instance.AreAllDescendantsVisible(go)) {
      SceneVisibilityManager.instance.Hide(go, /*includeDescendants=*/ true);
    } else {
      SceneVisibilityManager.instance.ToggleVisibility(go, /*includeDescendants=*/false);
      for (int childIdx = 0; childIdx < go.transform.childCount; ++childIdx) {
        RecursiveInvertVisibility(go.transform.GetChild(childIdx).gameObject);
      }
    }
  }

  [MenuItem("Tools/Fraxul/Scene Visibility/Reset", priority = 100)]
  static void SceneVisibilityReset() {
    SceneVisibilityManager.instance.ShowAll();
  }

  [MenuItem("Tools/Fraxul/Scene Visibility/Invert", priority = 101)]
  static void SceneVisibilityInvert() {
    var scene = EditorSceneManager.GetActiveScene();
    var rootGameObjects = scene.GetRootGameObjects();
    foreach (var go in rootGameObjects) {
      RecursiveInvertVisibility(go);
    }
  }


  [MenuItem("Tools/Fraxul/Scene Visibility/Only Selected", priority = 102)]
  static void SceneVisibilityOnlySelected() {
    SceneVisibilityManager.instance.HideAll();
    var selectedTransforms = Selection.GetTransforms(SelectionMode.Unfiltered);
    foreach (Transform xf in selectedTransforms) {
      SceneVisibilityManager.instance.Show(xf.gameObject, /*includeDescendants=*/ false);
    }
  }

    [MenuItem("Tools/Fraxul/Scene Visibility/Only Selected + Descendants", priority = 103)]
  static void SceneVisibilityOnlySelectedWithDescendants() {
    SceneVisibilityManager.instance.HideAll();
    var selectedTransforms = Selection.GetTransforms(SelectionMode.Deep);
    foreach (Transform xf in selectedTransforms) {
      SceneVisibilityManager.instance.Show(xf.gameObject, /*includeDescendants=*/ true);
    }
  }

  [MenuItem("Tools/Fraxul/Scene Visibility/Hide Selected", priority = 104)]
  static void SceneVisibilityHideSelected() {
    var selectedTransforms = Selection.GetTransforms(SelectionMode.Unfiltered);
    foreach (Transform xf in selectedTransforms) {
      SceneVisibilityManager.instance.Hide(xf.gameObject, /*includeDescendants=*/ false);
    }
  }
  [MenuItem("Tools/Fraxul/Scene Visibility/Hide Selected + Descendants", priority = 105)]
  static void SceneVisibilityHideSelectedWithDescendants() {
    var selectedTransforms = Selection.GetTransforms(SelectionMode.Deep);
    foreach (Transform xf in selectedTransforms) {
      SceneVisibilityManager.instance.Hide(xf.gameObject, /*includeDescendants=*/ true);
    }
  }

  [MenuItem("Tools/Fraxul/Scene Visibility/No Static Occlusion", priority = 120)]
  static void SceneVisibilityNoStaticOcclusion() {
    ApplyVisibility( (GameObject go) => !(GameObjectUtility.AreStaticEditorFlagsSet(go, StaticEditorFlags.OccludeeStatic) || GameObjectUtility.AreStaticEditorFlagsSet(go, StaticEditorFlags.OccluderStatic) || go.CompareTag("EditorOnly")) );
  }

  [MenuItem("Tools/Fraxul/Scene Visibility/Occluder Static", priority = 121)]
  static void SceneVisibilityOccluderStatic() {
    ApplyVisibility( (GameObject go) => GameObjectUtility.AreStaticEditorFlagsSet(go, StaticEditorFlags.OccluderStatic) && !go.CompareTag("EditorOnly") );
  }

  [MenuItem("Tools/Fraxul/Scene Visibility/Occludee Static", priority = 122)]
  static void SceneVisibilityOccludeeStatic() {
    ApplyVisibility( (GameObject go) => GameObjectUtility.AreStaticEditorFlagsSet(go, StaticEditorFlags.OccludeeStatic) && !go.CompareTag("EditorOnly") );
  }


  [MenuItem("Tools/Fraxul/Scene Visibility/GPU Instancing-Static Batching conflicts", priority = 140)]
  static void SceneVisibilityGPUInstancingConflict() {
    List<GameObject> objs = SelectObjects((GameObject go) => {
      if (!GameObjectUtility.AreStaticEditorFlagsSet(go, StaticEditorFlags.BatchingStatic))
        return false; // only GameObjects that are batching-static
      MeshRenderer mr = go.GetComponent<MeshRenderer>();
      if ((mr == null) || (!mr.enabled)) return false; // require mesh renderer to exist and be enabled
      MeshFilter mf = go.GetComponent<MeshFilter>();
      if (mf == null) return false; // require mesh filter to exist
      foreach (Material mat in mr.sharedMaterials) {
        if ((mat != null) && mat.enableInstancing)
          return true; // conflict: object is static batched, but a material is assigned with GPU instancing enabled
      }
      return false;
    });

    // Filter to ensure that we only display objects using meshes that are referenced more than once in the scene.
    // (No point in raising a GPU instancing conflict for an object that'll only ever draw one instance)
    Dictionary<Mesh, int> meshRefcounts = new Dictionary<Mesh, int>();
    foreach (GameObject obj in objs) {
      MeshFilter mf = obj.GetComponent<MeshFilter>();
      if (meshRefcounts.ContainsKey(mf.sharedMesh)) {
        meshRefcounts[mf.sharedMesh] += 1;
      } else {
        meshRefcounts.Add(mf.sharedMesh, 1);
      }
    }

    SceneVisibilityManager.instance.HideAll();
    foreach (GameObject obj in objs) {
      MeshFilter mf = obj.GetComponent<MeshFilter>();
      if (meshRefcounts[mf.sharedMesh] > 1) {
        SceneVisibilityManager.instance.Show(obj, /*includeDescendants=*/ false);
      }
    }

  }

  [MenuItem("Tools/Fraxul/Scene Visibility/GPU Instancing Opportunities", priority = 141)]
  static void SceneVisibilityGPUInstancingOpportunity() {
    List<GameObject> objs = SelectObjects((GameObject go) => {
      MeshRenderer mr = go.GetComponent<MeshRenderer>();
      if ((mr == null) || (!mr.enabled)) return false; // require mesh renderer to exist and be enabled
      if (mr.sharedMaterial == null) return false; // require first sharedMaterial to be non-NULL for filtering to work
      MeshFilter mf = go.GetComponent<MeshFilter>();
      if (mf == null) return false; // require mesh filter to exist
      foreach (Material mat in mr.sharedMaterials) {
        if ((mat != null) && (!mat.enableInstancing))
          return true; // uses a non-instanced material
      }
      return false;
    });

    // Filter to ensure that we only display objects using mesh-material combos that are referenced more than once in the scene.
    // This only filters based on the first sharedMaterial on the object, but that should be sufficient.
    Dictionary<Mesh, Dictionary<Material, int> > meshRefcounts = new Dictionary<Mesh, Dictionary<Material, int> >();
    foreach (GameObject obj in objs) {
      MeshFilter mf = obj.GetComponent<MeshFilter>();
      MeshRenderer mr = obj.GetComponent<MeshRenderer>();

      if (meshRefcounts.ContainsKey(mf.sharedMesh)) {
        var matDict = meshRefcounts[mf.sharedMesh];
        if (matDict.ContainsKey(mr.sharedMaterial)) {
          matDict[mr.sharedMaterial] += 1;
        } else {
          matDict.Add(mr.sharedMaterial, 1);
        }
      } else {
        Dictionary<Material, int> newMatDict = new Dictionary<Material, int>();
        newMatDict.Add(mr.sharedMaterial, 1);
        meshRefcounts.Add(mf.sharedMesh, newMatDict);
      }
    }

    SceneVisibilityManager.instance.HideAll();
    foreach (GameObject obj in objs) {
      MeshFilter mf = obj.GetComponent<MeshFilter>();
      MeshRenderer mr = obj.GetComponent<MeshRenderer>();

      if (meshRefcounts[mf.sharedMesh][mr.sharedMaterial] > 1) {
        SceneVisibilityManager.instance.Show(obj, /*includeDescendants=*/ false);
      }
    }

  }



  [MenuItem("Tools/Fraxul/Scene Visibility/Show Tool Window", priority = 1000)]
  public static void ShowWindow() {
    EditorWindow.GetWindow(typeof(FraxulSceneVisibilityUtils), /*utilityWindow=*/true, "Scene Visibility");
  }

  void OnGUI() {
    if (GUILayout.Button("Reset")) SceneVisibilityReset();
    if (GUILayout.Button("Invert")) SceneVisibilityInvert();
    EditorGUILayout.Space();
    if (GUILayout.Button("Only Selected")) SceneVisibilityOnlySelected();
    if (GUILayout.Button("Only Selected + Descendants")) SceneVisibilityOnlySelectedWithDescendants();
    if (GUILayout.Button("Hide Selected")) SceneVisibilityHideSelected();
    if (GUILayout.Button("Hide Selected + Descendants")) SceneVisibilityHideSelectedWithDescendants();
    EditorGUILayout.Space();
    if (GUILayout.Button("No Static Occlusion")) SceneVisibilityNoStaticOcclusion();
    if (GUILayout.Button("Occluder Static")) SceneVisibilityOccluderStatic();
    if (GUILayout.Button("Occludee Static")) SceneVisibilityOccludeeStatic();
    EditorGUILayout.Space();
    if (GUILayout.Button("GPU Instancing-Static Batching conflicts")) SceneVisibilityGPUInstancingConflict();
    if (GUILayout.Button("GPU Instancing Opportunities")) SceneVisibilityGPUInstancingOpportunity();
  }
}
