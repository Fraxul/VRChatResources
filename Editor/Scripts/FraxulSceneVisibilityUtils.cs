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
  static void Reset() {
    SceneVisibilityManager.instance.ShowAll();

    // Cleanup collision proxies, if they exist
    GameObject colliderProxyRoot = GameObject.Find(proxyContainerName);
    if (colliderProxyRoot != null) {
      UnityEngine.Object.DestroyImmediate(colliderProxyRoot);
    }
  }

  static void Invert() {
    var scene = EditorSceneManager.GetActiveScene();
    var rootGameObjects = scene.GetRootGameObjects();
    foreach (var go in rootGameObjects) {
      RecursiveInvertVisibility(go);
    }
  }


  static void AdjustSelection(bool hide, bool includeDescendants) {
    var selectedTransforms = Selection.GetTransforms(SelectionMode.Unfiltered);
    foreach (Transform xf in selectedTransforms) {
      if (hide) {
        SceneVisibilityManager.instance.Hide(xf.gameObject, includeDescendants);
      } else {
        SceneVisibilityManager.instance.Show(xf.gameObject, includeDescendants);
      }
    }

  }
  static GameObject generateProxyLocator(Transform proxyContainerXf, GameObject refGO) {
    var res = new GameObject(refGO.name + "_proxyLocator");
    res.tag = "EditorOnly";
    res.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
    res.transform.SetParent(proxyContainerXf);
    res.transform.SetPositionAndRotation(refGO.transform.position, refGO.transform.rotation);
    res.transform.localScale = refGO.transform.lossyScale;
    return res;
  }


  static void setupProxy(Transform proxyLocatorXf, GameObject proxy) {
    proxy.tag = "EditorOnly";
    proxy.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
    proxy.transform.SetParent(proxyLocatorXf);
    proxy.transform.localPosition = Vector3.zero;
    proxy.transform.localRotation = Quaternion.identity;
    proxy.transform.localScale = Vector3.one;
  }

  const string proxyContainerName = "_FraxulSceneVisibilityColliderProxies";
  static void VisualizeCollision() {
    // Delete old collision proxies, if they exist
    GameObject colliderProxyRoot = GameObject.Find(proxyContainerName);
    if (colliderProxyRoot != null) {
      UnityEngine.Object.DestroyImmediate(colliderProxyRoot);
    }

    // Create a container for collision proxies.
    // The whole hierarchy is marked editor-only and "don't save", so they won't make it into builds or clutter up the scene file.
    colliderProxyRoot = new GameObject(proxyContainerName);
    colliderProxyRoot.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor; // | HideFlags.HideInHierarchy;
    colliderProxyRoot.tag = "EditorOnly";

    // Get a handle to the "Cube" default resource mesh; we use this for our "fast path" for visualizing BoxColliders on Cube meshes.
    var cubeGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
    Mesh cubeSharedMesh = cubeGO.GetComponent<MeshFilter>().sharedMesh;
    Material defaultSharedMaterial = cubeGO.GetComponent<MeshRenderer>().sharedMaterial;
    UnityEngine.Object.DestroyImmediate(cubeGO);

    ApplyVisibility( (GameObject go) => {
      if (go.CompareTag("EditorOnly"))
        return false;

      if (!go.activeInHierarchy)
        return false; // inactive objects have no collision

      Collider[] colliders = go.GetComponents<Collider>();
      if (colliders.Length == 0)
        return false;


      MeshFilter mf = null;
      MeshRenderer mr = null;
      go.TryGetComponent<MeshFilter>(out mf);
      go.TryGetComponent<MeshRenderer>(out mr);

      foreach (Collider c in colliders) {
        if ((!c.enabled)|| c.isTrigger) 
          continue; // only enabled, non-trigger colliders
        if (c.GetType() == typeof(MeshCollider)) {
          MeshCollider mc = (MeshCollider) c;
          if (mr != null && mf != null && mf.sharedMesh == mc.sharedMesh && mr.enabled) {
            // Fast path: mesh renderer enabled and mesh filter matches MeshCollider mesh
            return true;
          }
          // Generate a proxy using the collision mesh and the default material
          var proxyLocation = generateProxyLocator(colliderProxyRoot.transform, go);
          var proxy = new GameObject("MeshColliderProxy", typeof(MeshFilter), typeof(MeshRenderer));
          setupProxy(proxyLocation.transform, proxy);
          MeshFilter proxyMf = proxy.GetComponent<MeshFilter>();
          MeshRenderer proxyMr = proxy.GetComponent<MeshRenderer>();
          proxyMf.sharedMesh = mc.sharedMesh;
          proxyMr.material = defaultSharedMaterial;
          
        } else if (c.GetType() == typeof(BoxCollider)) {
          BoxCollider bc = (BoxCollider) c;
          if (mf != null && mr != null && mr.enabled && mf.sharedMesh == cubeSharedMesh && bc.center == Vector3.zero && bc.size == Vector3.one) {
            // fast-path
            return true;
          } else {
            var proxyLocation = generateProxyLocator(colliderProxyRoot.transform, go);
            var proxy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            setupProxy(proxyLocation.transform, proxy);

            proxy.transform.localPosition = bc.center;
            proxy.transform.localScale = bc.size;
          }
        } else if (c.GetType() == typeof(CapsuleCollider)) {
          CapsuleCollider cc = (CapsuleCollider) c;
          var proxyLocation = generateProxyLocator(colliderProxyRoot.transform, go);
          var proxy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
          setupProxy(proxyLocation.transform, proxy);

          proxy.transform.localPosition = cc.center;
          proxy.transform.localScale = new Vector3(cc.radius * 2.0f, cc.height * 0.5f, cc.radius * 2.0f);
          switch (cc.direction) {
            case 0: // X-axis
              proxy.transform.localEulerAngles = new Vector3(0, 0, 90);
              break;
            case 1: // Y-axis
              proxy.transform.localEulerAngles = Vector3.zero;
              break;
            case 2: // Z-axis
              proxy.transform.localEulerAngles = new Vector3(90, 0, 0);
              break;
          }

        } else {
          Debug.LogWarning(string.Format("GameObject {0}: Unhandled collider type {1}, can't generate proxy", go.name, c.GetType().ToString()));
          // just make the object visible anyway
          return true;
        }
      }
      return false;
    });

    SceneVisibilityManager.instance.Show(colliderProxyRoot, /*includeDescendants=*/ true);
  }


  static void ShowGPUInstancingConflicts() {
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

  static void ShowGPUInstancingOpportunities() {
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



  [MenuItem("Tools/Fraxul/Scene Visibility Tool...", priority = 10)]
  public static void ShowWindow() {
    EditorWindow.GetWindow(typeof(FraxulSceneVisibilityUtils), /*utilityWindow=*/true, "Scene Visibility");
  }

  void OnGUI() {
    Rect contentRect = EditorGUILayout.BeginVertical();

    if (GUILayout.Button("Reset")) Reset();
    if (GUILayout.Button("Invert")) Invert();
    if (GUILayout.Button("Hide EditorOnly")) {
      foreach (GameObject go in GameObject.FindGameObjectsWithTag("EditorOnly")) SceneVisibilityManager.instance.Hide(go, /*includeDescendants=*/ true);
    }

    EditorGUILayout.Space();

    if (GUILayout.Button("Only Selected")) {
      SceneVisibilityManager.instance.HideAll();
      AdjustSelection(/*hide=*/ false, /*includeDescendants=*/ false);
    }
    if (GUILayout.Button("Only Selected + Descendants")) {
      SceneVisibilityManager.instance.HideAll();
      AdjustSelection(/*hide=*/ false, /*includeDescendants=*/ true);
    }
    if (GUILayout.Button("Hide Selected")) AdjustSelection(/*hide=*/ true, /*includeDescendants=*/ false);
    if (GUILayout.Button("Hide Selected + Descendants")) AdjustSelection(/*hide=*/ true, /*includeDescendants=*/ true);

    EditorGUILayout.Space();

    if (GUILayout.Button("No Static Occlusion")) {
      ApplyVisibility( (GameObject go) => !(GameObjectUtility.AreStaticEditorFlagsSet(go, StaticEditorFlags.OccludeeStatic) || GameObjectUtility.AreStaticEditorFlagsSet(go, StaticEditorFlags.OccluderStatic) || go.CompareTag("EditorOnly")) );
    }
    EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("Occluder Static")) {
        // We don't filter out EditorOnly occluder-static objects during visualization, since adding EditorOnly occlusion "helper objects" is a common strategy.
        ApplyVisibility( (GameObject go) => GameObjectUtility.AreStaticEditorFlagsSet(go, StaticEditorFlags.OccluderStatic) );
      }
      if (GUILayout.Button("Not Occluder Static")) {
        // We do filter out EditorOnly when filtering for non-Occluder Static, though, since large non-Occluder EditorOnly objects are common for light baking and such
        ApplyVisibility( (GameObject go) => (!GameObjectUtility.AreStaticEditorFlagsSet(go, StaticEditorFlags.OccluderStatic)) && !go.CompareTag("EditorOnly") );
      }
    EditorGUILayout.EndHorizontal();
    EditorGUILayout.BeginHorizontal();
      if (GUILayout.Button("Occludee Static")) {
        ApplyVisibility( (GameObject go) => GameObjectUtility.AreStaticEditorFlagsSet(go, StaticEditorFlags.OccludeeStatic) && !go.CompareTag("EditorOnly") );
      }
      if (GUILayout.Button("Not Occludee Static")) {
        ApplyVisibility( (GameObject go) => (!GameObjectUtility.AreStaticEditorFlagsSet(go, StaticEditorFlags.OccludeeStatic)) && !go.CompareTag("EditorOnly") );
      }
    EditorGUILayout.EndHorizontal();

    EditorGUILayout.Space();

    if (GUILayout.Button("Visualize Collision")) VisualizeCollision();

    EditorGUILayout.Space();

    if (GUILayout.Button("GPU Instancing-Static Batching conflicts")) ShowGPUInstancingConflicts();
    if (GUILayout.Button("GPU Instancing Opportunities")) ShowGPUInstancingOpportunities();
    if (GUILayout.Button("Odd Negative Scaling")) {
      ApplyVisibility( (GameObject go) => (go.transform.lossyScale.x < 0.0f || go.transform.lossyScale.y < 0.0f || go.transform.lossyScale.z < 0.0f) );
    }

    
    EditorGUILayout.EndVertical(); // contentRect

    // Compute minimum size to show all controls
    int minContentHeightPadded = ((int) contentRect.height) + GUI.skin.button.padding.top + GUI.skin.button.padding.bottom;
    if (((int) this.minSize.y) != minContentHeightPadded)
      this.minSize = new Vector2(200, minContentHeightPadded);
  }
}
