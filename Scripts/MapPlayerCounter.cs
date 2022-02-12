
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

[RequireComponent(typeof(BoxCollider))] // Player presence volume. You must set isTrigger=true and make sure that the GameObject is on the Default layer.
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]

public class MapPlayerCounter : UdonSharpBehaviour {

  [Tooltip("GameObjects enabled when the volume is occupied, and disabled otherwise. Can be empty.")] public GameObject[] m_OccupiedIndicators;
  [Tooltip("GameObjects enabled when the volume is vacant, and disabled otherwise. Can be empty.")] public GameObject[] m_VacantIndicators;
  [Tooltip("UI Text control for rendering a count of players. Optional, can be null.")] public Text m_PlayerCounterTextMesh;


  // Tracking which player IDs are in this volume to handle join/leave events.
  private int[] m_players;
  private int m_playerCount = 0;

  private void Start() {
    m_players = new int[256];
    m_playerCount = 0;

    UpdateIndicators();
  }

  public int GetPlayerCount() {
    return m_playerCount;
  }
  
  private void RemovePlayer(int playerID) {
    for (int i = 0; i < m_playerCount; ++i) {
      if (m_players[i] == playerID) {
        // swap the last player from the array into this position.
        // (if we're removing the last player from the array this is a no-op, which is fine)
        m_players[i] = m_players[m_playerCount - 1];

        m_playerCount -= 1;
        return;
      }
    }
    // player not found in array, that's OK
  }

  private void AddPlayer(int playerID) {
    for (int i = 0; i < m_playerCount; ++i) {
      if (m_players[i] == playerID) {
        return; // player was already in the array, don't re-add
      }
    }
    m_players[m_playerCount] = playerID;
    m_playerCount += 1;
  }

  public override void OnPlayerJoined(VRCPlayerApi player) {
    //Debug.Log(string.Format("OnPlayerJoined ID={0}", player.playerId));

    // We remove the player from the active arrays when the OnPlayerJoined event fires.
    // VRC has a habit of firing a spurious OnPlayerTriggerEnter event during player init if a player trigger volume overlaps the origin
    // (and not firing a corresponding OnPlayerTriggerExit event when the player join/spawn is finished)
    RemovePlayer(player.playerId);
    UpdateIndicators();
  }

  public override void OnPlayerLeft(VRCPlayerApi player) {
    // Remove departing players from the list. We don't get an OnPlayerTriggerExit for a player that disconnects.
    RemovePlayer(player.playerId);
    UpdateIndicators();
    //Debug.Log(string.Format("OnPlayerLeft ID={0}", player.playerId));
  }

  public override void OnPlayerRespawn(VRCPlayerApi player) {
    // No special action required here -- we get correct player trigger exit and enter notifications for respawning.

    // Debug.Log(string.Format("OnPlayerRespawn ID={0}", player.playerId));
  }

  public override void OnPlayerTriggerEnter(VRCPlayerApi player) {
    //Debug.Log(string.Format("{1} OnPlayerTriggerEnter ID={0}", player.playerId, this.gameObject.name));
    AddPlayer(player.playerId);

    UpdateIndicators();
  }
  public override void OnPlayerTriggerExit(VRCPlayerApi player) {
    //Debug.Log(string.Format("{1} OnPlayerTriggerExit ID={0}", player.playerId, this.gameObject.name));
    RemovePlayer(player.playerId);

    UpdateIndicators();
  }

  private void UpdateIndicators() {
    bool occupied = m_playerCount > 0;
    if (m_OccupiedIndicators != null) {
      foreach (GameObject obj in m_OccupiedIndicators) {
        if (obj == null) continue;
        obj.SetActive(occupied);
      }
    }

    if (m_VacantIndicators != null) {
      foreach (GameObject obj in m_VacantIndicators) {
        if (obj == null) continue;
        obj.SetActive(!occupied);
      }
    }

    if (m_PlayerCounterTextMesh != null) {
      m_PlayerCounterTextMesh.text = m_playerCount.ToString();
    }

  }
}
