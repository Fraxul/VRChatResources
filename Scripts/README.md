## UdonSharp Scripts


### ManualOcclusionVolume
Disables MeshRenderers and cullable Animators below it in the hierarchy when the local player is not inside the attached BoxCollider. Useful for CPU optimization on large worlds.


### MapPlayerCounter
Tracks the number of players inside of a trigger volume (BoxCollider). Runs locally -- no networking required -- and handles annoying edge cases, like players spawning or leaving while inside the trigger volume. Can enable or disable objects depending on whether players are present, and can drive a UI.Text component for a player counter. Should be easy to extend for your purposes.


