using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

class FraxulMiscUtils {
  [MenuItem("Tools/Fraxul/Move Camera to Scene View")]
  static void MoveCameraToSceneView() {
    var view = SceneView.lastActiveSceneView;
    Camera targetCamera = null;

    // Check the selection for a Camera
    var cameras = Selection.GetFiltered(typeof(Camera), SelectionMode.TopLevel | SelectionMode.Editable);
    if (cameras.Length > 0) {
      targetCamera = (Camera) cameras[0];
    }

    // Try the MainCamera tag
    if (!targetCamera) {
      var o = GameObject.FindGameObjectWithTag("MainCamera");
      targetCamera = o.GetComponent<Camera>();
    }

    if (!targetCamera) {
      Debug.LogError("Can't find a camera to move, either via selection or the MainCamera tag");
      return;
    }

    targetCamera.gameObject.transform.SetPositionAndRotation(view.camera.transform.position, view.camera.transform.rotation);
  }

  [MenuItem("Tools/Fraxul/Bake Scale to Children")]
  static void BakeScaleToChildren() {
    const string kUndoName = "Bake Scale to Children";
    var selection = Selection.GetTransforms(SelectionMode.TopLevel | SelectionMode.Editable);
    foreach (Transform tr in selection) {
      Undo.RecordObject(tr, kUndoName);
      Vector3 parentScale = tr.localScale;

      Vector3[] childPositions = new Vector3[tr.childCount];
      Quaternion[] childRotations = new Quaternion[tr.childCount];
      Vector3[] childScales = new Vector3[tr.childCount];

      for (int i = 0; i < tr.childCount; ++i) {
        var child = tr.GetChild(i);
        Undo.RecordObject(child, kUndoName);
        childPositions[i] = child.position;
        childRotations[i] = child.rotation;
        childScales[i] = child.localScale;
      }

      tr.localScale = Vector3.one;

      for (int i = 0; i < tr.childCount; ++i) {
        var child = tr.GetChild(i);
        child.position = childPositions[i];
        child.rotation = childRotations[i];
        child.localScale = new Vector3(
          childScales[i].x * parentScale.x,
          childScales[i].y * parentScale.y,
          childScales[i].z * parentScale.z);
      }

    }
  }

  [MenuItem("Tools/Fraxul/Absolute Value Scale")]
  static void AbsoluteValueScale(bool preserveChildPositions = false) {
    const string kUndoName = "Absolute Value Scale";
    var selection = Selection.GetTransforms(SelectionMode.TopLevel | SelectionMode.Editable);
    foreach (Transform tr in selection) {
      if (tr.localScale.x >= 0.0f && tr.localScale.y >= 0.0f && tr.localScale.z >= 0.0f)
        continue; // ignore any objects with positive scale so we don't accidentally modify prefab components or whatever

      Undo.RecordObject(tr, kUndoName);
      if (preserveChildPositions && tr.childCount > 0) {
        Vector3[] childPositions = new Vector3[tr.childCount];
        Quaternion[] childRotations = new Quaternion[tr.childCount];

        for (int i = 0; i < tr.childCount; ++i) {
          var child = tr.GetChild(i);
          Undo.RecordObject(child, kUndoName);
          childPositions[i] = child.position;
          childRotations[i] = child.rotation;
        }

        tr.localScale = new Vector3(
          Mathf.Abs(tr.localScale.x),
          Mathf.Abs(tr.localScale.y),
          Mathf.Abs(tr.localScale.z));

        for (int i = 0; i < tr.childCount; ++i) {
          var child = tr.GetChild(i);
          // Avoid writing the fields (and dirtying prefab instances) if the children don't need to move.
          // Note that Unity correctly implements epsilon comparison in Vector3 and Quaternion operator==
          if (!(child.position == childPositions[i]))
            child.position = childPositions[i];
          if (!(child.rotation == childRotations[i]))
            child.rotation = childRotations[i];
        }
      } else {
        tr.localScale = new Vector3(
          Mathf.Abs(tr.localScale.x),
          Mathf.Abs(tr.localScale.y),
          Mathf.Abs(tr.localScale.z));
      }
    }
  }

  [MenuItem("Tools/Fraxul/Absolute Value Scale + preserve child positions")]
  static void AbsoluteValueScalePCP() {
    AbsoluteValueScale(true);
  }

}
