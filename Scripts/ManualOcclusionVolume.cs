using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using VRC.SDK3.Components;


[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
[RequireComponent(typeof(BoxCollider))]
public class ManualOcclusionVolume : UdonSharpBehaviour {

  private MeshRenderer[] m_meshRenderers;
  private Animator[] m_animators;

  private BoxCollider m_boxCollider;

  private bool m_localPlayerInVolume = false;

  // Cache for current enable-state of components.
  // We assume that all components start out enabled (since we filter out / don't control disabled components)
  private bool m_lastUpdateState = true;

  void Start() {
    m_boxCollider = (BoxCollider) GetComponent(typeof(BoxCollider));
    // Collect objects to occlude
    m_meshRenderers = (MeshRenderer[]) this.gameObject.GetComponentsInChildren(typeof(MeshRenderer));
    m_animators = (Animator[]) this.gameObject.GetComponentsInChildren(typeof(Animator));

    // Remove renderers and animators from the list that are default-disabled so that we don't reenable them.
    // (we just null them out in the array so we don't have to copy/resize it)
    int disabledMeshes = 0;
    if (m_meshRenderers != null) {
      for (int i = 0; i < m_meshRenderers.Length; ++i) {
        if (m_meshRenderers[i].enabled == false) {
          m_meshRenderers[i] = null;
          disabledMeshes += 1;
        }
      }
      Debug.Log(string.Format("ManualOcclusionVolume({0}): Managing {1}/{2} meshes", this.gameObject.name,
        m_meshRenderers.Length - disabledMeshes, m_meshRenderers.Length));
    }

    int disabledAnimators = 0;
    if (m_animators != null) {
      for (int i = 0; i < m_animators.Length; ++i) {
        // Only disable animators that are set to "Cull Completely" -- those should be safe to turn off.
        // (the default is "Always Animate", so culling had to be explicitly turned on on them)
        if (m_animators[i].enabled == false || m_animators[i].cullingMode != AnimatorCullingMode.CullCompletely) {
          m_animators[i] = null;
          disabledAnimators += 1;
        }
      }
      Debug.Log(string.Format("ManualOcclusionVolume({0}): Managing {1}/{2} animators", this.gameObject.name,
        m_animators.Length - disabledAnimators, m_animators.Length));
    }
  }



  public override void OnPlayerTriggerEnter(VRCPlayerApi player) {
    if (!player.isLocal)
      return;
    Debug.Log(string.Format("ManualOcclusionVolume({0}): OnPlayerTriggerEnter", this.gameObject.name));
    m_localPlayerInVolume = true;
    UpdateState();
  }
  public override void OnPlayerTriggerExit(VRCPlayerApi player) {
    if (!player.isLocal)
      return;
    Debug.Log(string.Format("ManualOcclusionVolume({0}): OnPlayerTriggerExit", this.gameObject.name));
    m_localPlayerInVolume = false;
    UpdateState();
  }

  public override void OnPlayerRespawn(VRCPlayerApi player) {
    if (!player.isLocal)
      return;

    Debug.Log(string.Format("ManualOcclusionVolume({0}): OnPlayerRespawn position={1}", this.gameObject.name, player.GetPosition()));

    // Check to see if this volume contains the spawn point and enable components immediately if it does.
    // This helps prevent a flash of skybox when respawning.
    if (m_boxCollider.bounds.Contains(player.GetPosition())) {
      _UpdateObjects(true);
    }
  }

  public override void OnPlayerJoined(VRCPlayerApi player) {
    if (!player.isLocal)
      return;

    // Catching the local player join event will help up turn off everything for the areas that the player doesn't spawn in.

    Debug.Log(string.Format("ManualOcclusionVolume({0}): OnPlayerJoined", this.gameObject.name));

    // Schedule an update of the visibility state after a couple seconds
    // That should give plenty of time for the OnPlayerTriggerEnter event for the spawn region to fire,
    // and prevents a flash of skybox at spawn when a player spawns inside an occlusion volume.
    this.SendCustomEventDelayedSeconds("UpdateState", 3.0f);
  }

  public void WillTeleportIntoVolume() {
    // Incoming notification from another object that we plan to teleport the player into this volume.
    // Turn objects on immediately so that we don't get a flash of skybox during teleport.
    _UpdateObjects(true);
  }

  public void UpdateState() {
    _UpdateObjects(m_localPlayerInVolume);
  }

  private void _UpdateObjects(bool state) {
    // Check state cache to see if we're actually changing anything
    if (m_lastUpdateState == state)
      return;
    m_lastUpdateState = state;

    if (m_meshRenderers != null) {
      foreach (MeshRenderer mr in m_meshRenderers) {
        if (mr == null)
          continue;

        mr.enabled = state;
      }
    }

    if (m_animators != null) {
      foreach (Animator anim in m_animators) {
        if (anim == null)
          continue;

        anim.enabled = state;
      }
    }
  }
}
