# Enemy grave return

## Existing gameplay

- `GameEvent.Raise()` broadcasts a parameterless signal to `GameEventListener.Response`. The existing `DebugInspectorEvent` is wired to inspection and spawner prefabs; `PanCollectable` currently has no serialized listeners.
- `CameraAreaSpawner` drops random offering pieces. `PiecePickup` collects them and `PieceCounter` counts them. Reaching the count is not the same as delivering an offering to an enemy.
- `GhostHitTrigger` detects a thrown object hitting a `Ghost` and exposes that enemy through `onGhostHit`.
- `Enemy_FSM` controls patrol, pursuit and attacks using a `NavMeshAgent`. `Practice_Basic_Enemy` handles animation and damage. Running attack/rest coroutines must be cancelled when an offering is accepted.
- `Grave`, `GravePool` and `GraveSpawner` manage enemy-associated, pooled graves. Permanent cemetery graves deliberately have no `Grave` component. Return destinations therefore use explicit scene transforms, not a search for `Grave` components.
- `PlayerOffering` stores completed sets, launches the offering toward the cursor, and consumes one charge per valid throw. `p_PlayerController.PlayerOffer()` calls it through the existing **Offer** input action (`F` / gamepad north button). Ordinary shooting remains separate.

## Ready-to-play scene

`Assets/Scenes/TestingScenes/ShigePrueba.unity` includes the complete example:

- `temp_player` has `PlayerOffering`, a visible capsule placeholder for its missing character model, and the existing cursor as its aim target.
- Three initial pickups surround the player. Touch all three to gain one offering. The existing spawner continues dropping pieces nearby every 15–20 seconds.
- `OfferingProjectile.prefab` uses the bread visual, a trigger/Rigidbody, `GhostHitTrigger`, and lifetime cleanup. Its hit event invokes `OfferingReceived.Raise(GameObject)`.
- `OfferingTestEnemy` uses the animated Bacteria prefab with the `Ghost` tag and a navigation-compatible Rigidbody.
- `OfferingManager` receives the event and chooses among three permanent graves. Their `ReturnPoint` children sit outside the grave geometry.
- The environment has its own baked NavMesh at `Assets/Scenes/TestingScenes/ShigePrueba/NavMesh-OfferingTest.asset`; navigation data belonging to other scenes is unchanged.

Press Play, move with **WASD**, collect three pieces, aim with the **mouse**, and press **F** to offer. The offering must hit the enemy. Once it reaches the selected grave, it sinks smoothly into the ground and is deactivated. Restart Play to repeat the initial scenario.

On `OfferingManager > Enemy Grave Return`, **Disappearance Duration** defaults to 1 second and **Sink Depth** to 3 world units. Existing scene instances use these defaults without additional wiring. The effect is a downward movement, not a material-opacity fade.

## Scene setup

1. Add `EnemyGraveReturn` and `GameEventListener` to a scene manager object.
2. Set the listener's **Event** to `Assets/ScriptableObjects/OfferingReceived.asset`.
3. In **Response With Target**, add the manager and select **EnemyGraveReturn > SendEnemyToRandomGrave (Dynamic GameObject)**. Leave **Response** empty for this event.
4. Create an empty child named `ReturnPoint` beside each eligible grave on walkable ground and add those transforms to **Grave Points**. Avoid positions inside the tomb's collider. For pooled graves, points beneath an inactive grave are automatically skipped.
5. Bake the scene's NavMesh for the enemy agent type. Adjust **Sample Radius** only to bridge small gaps between a point and the NavMesh.
6. On the thrown offering's `GhostHitTrigger`, connect **On Ghost Hit** to the `OfferingReceived` asset and choose **GameEvent > Raise (Dynamic GameObject)**. Use the `Ghost` tag on the enemy root, including when its hit collider is a child. A trigger or collision must have the Rigidbody setup required by Unity physics.

From another offering-delivery script, call `offeringReceived.Raise(enemyGameObject)` only after delivery succeeds. `Raise()` without a target continues to invoke only the original **Response**; it does not select an enemy. The existing **Raise Event** Inspector button also uses this parameterless path.

## Behavior

Only the supplied enemy returns. A random active point with a complete reachable path is selected. The enemy stops attacking and walks to the grave. `EnemyGraveReturn` observes the existing FSM's arrival in `LateUpdate`, then disables the enemy controller, navigation, animation and colliders while moving it downward. Physics bodies are temporarily kinematic. At the end, it deactivates the enemy and restores its pose/component settings while inactive. The grave remains intact. This requires no edits to the existing enemy FSM or the pickup scripts.

Repeated offerings while returning or disappearing are ignored. If no valid point exists, the current AI behavior is preserved and a warning explains the setup issue. If the route becomes invalid while walking, the enemy resumes patrol. Calling `ResumePatrol()` before arrival cancels the pending disappearance. Disabling the return manager clears pending journeys and restores any enemies partway through the effect.

The return script does not reserve graves or create, disable, or destroy them. Multiple enemies may choose the same grave. Scene/prefab event wiring is explicit so the existing inspection and collection events keep their current behavior.

## Validation

With scene changes saved, run **Tools > Gameplay > Validate Enemy Grave Return**. It temporarily enters Play mode in an empty scene, builds a small test NavMesh, checks event routing, actual walking and arrival, invalid destinations, repeated offerings, child colliders and recovery after a lost route, then restores the previously open scenes. Results are written to `Temp/EnemyGraveReturnValidation.result`. It does not run when a custom Play mode start scene is configured.

For the saved example scene, run **Tools > Gameplay > Validate ShigePrueba Offering Flow**. It checks physical pickup collection, the player throw, projectile collision, event delivery, arrival, downward movement with collisions disabled, and enemy deactivation using the actual scene and enemy updates. Results are written to `Temp/ShigePruebaOfferingValidation.result`; Play mode changes are discarded when the test finishes. The disappearance checks have not been run; the final Play mode verification is left to the scene owner.
