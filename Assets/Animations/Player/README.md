# Animaciones de Character_VF

El player usa el modelo `Assets/Models/Character_VF.fbx`, articulado en el prefab `Assets/Prefabs/Player/Character_VF_Animated.prefab`. El FBX original permanece intacto. Se conservan sus mallas, materiales y proporciones; las mallas que agrupan ambas manos o ambos zapatos se separan en copias izquierda/derecha para animarlas de forma independiente.

El personaje original tiene piezas flotantes. Sus pivotes permiten animar cuerpo, cabeza, manos y zapatos sin añadir extremidades que el modelo no tiene.

- **Player_Walk.anim:** ciclo de 0.64 s con pasos alternos, elevación de zapatos, balanceo de manos y movimiento suave de cuerpo/cabeza.
- **Player_Hurt.anim:** reacción de 0.38 s con retroceso, inclinación del torso y cabeza, elevación de manos y recuperación.
- **Player_Idle.anim:** pose neutra que restablece todas las piezas.
- **Player.controller:** transiciones Idle/Walk/Hurt; parámetros `IsMoving`, `WalkSpeed` y `Damage`.

En `temp_player`, el personaje está en `EmptyModel/Character_VF`. El Animator controla únicamente los descendientes de `Pose`. El Rigidbody, los colliders, la cámara y el apuntado mantienen su funcionamiento. La cápsula visual añadida por error fue retirada; el antiguo `PlayerPlaceholder` de la escena permanece desactivado.

`p_PlayerAnimation` escucha el evento de instancia `p_PlayerStats.Damaged`, emitido por `TakeDamage` cuando baja la salud. Curarse o recibir cero daño no reproduce la reacción. Los golpes sucesivos pueden reiniciarla.

## Ver y editar

1. Abre `ShigePrueba`, entra en Play y mueve al jugador con sus controles habituales.
2. Para probar daño, usa **Tools > Player Animation > Test Damage in Play Mode**. Resta 10 puntos de vida al player seleccionado, o al primero de la escena. Salir de Play restablece la escena.
3. Para editar keyframes, sal de Play, selecciona `temp_player/EmptyModel/Character_VF` y abre **Window > Animation > Animation**. Elige el clip en el desplegable.
4. También puedes abrir `Character_VF_Animated.prefab` para trabajar directamente sobre el personaje.

## Validación

**Tools > Player Animation > Validate Walk and Damage** ejecuta pruebas en una escena temporal y restaura después las escenas abiertas. Comprueba que el modelo sea Character_VF y no una cápsula, el movimiento independiente de los zapatos, daño desde Idle/Walk, golpes repetidos, recuperación, aislamiento entre jugadores, curación, curvas y continuidad del ciclo.

El informe se guarda en `Temp/PlayerAnimationValidation.result` y las imágenes en `Temp/Player_*-preview.png` y `Temp/Player_Walk-phase-*.png`.

**Set Up Walk and Damage** reconstruye los pivotes y las copias de manos/zapatos, instala el prefab en el player y conserva los clips de Character_VF existentes para mantener ajustes manuales. Guarda primero la escena.
