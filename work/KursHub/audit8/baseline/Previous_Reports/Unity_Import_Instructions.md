# Importing KursHub

Use Unity 6000.6.3f1 with Universal RP 17.6.0 installed and an active URP renderer. Import KursHub_Final.unitypackage with all dependencies selected; keep the included .meta/GUID files. The package retains the canonical Assets/Environment/KursHub paths, so nested prefab, material and model references remain valid. Open Assets/Environment/KursHub/Scenes/KursHubDemo.unity or place Assets/Environment/KursHub/KursHub.prefab in your scene at unit scale.

The demo contains a static hub, a ground collider, camera and daylight. It does not include a player controller, NPC logic, NavMesh, gameplay or saved-world state. Existing CharacterTest lighting and gameplay are not part of the package. Visual LOD groups do not own colliders; collision remains present at every visual distance.

Loose FBX files are standard geometry interchange exports. The Unity package is the authoritative import route for the configured URP materials, LOD groups, colliders and reusable nested props.
