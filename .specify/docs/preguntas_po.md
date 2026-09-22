# Preguntas para el Product Owner (PO) — NovaLeave MVP

Este documento recopila las preguntas sobre políticas de negocio y decisiones funcionales que **requieren la validación del Product Owner** antes de que el equipo pueda implementar el MVP. Cada pregunta incluye contexto, alternativas y una recomendación del equipo técnico.

**Plazo recomendado**: Antes de iniciar la fase de planificación (`plan.md`), para evitar retrasos.

---

## 1. Política de fechas y cálculo de días

### P1 — ¿Permitimos solicitudes que comiencen el mismo día?

**Contexto**: La especificación actual (D-002, ahora eliminada) considera válido que un empleado solicite un permiso que comience el día actual (fecha de inicio = día de hoy). La alternativa es exigir que la fecha de inicio sea siempre futura (mañana o después).

**Pregunta**: ¿Permitimos solicitudes de permisos que empiecen hoy, o exigimos un día de antelación mínima?

**Alternativas**:
- **Opción A**: Permitir el mismo día (mayor flexibilidad, útil para enfermedades o emergencias).
- **Opción B**: Exigir que la fecha de inicio sea siempre futura (mejor para planificación y control).

**Recomendación del equipo**: Opción A (permitir mismo día), porque refleja casos reales de ausencias imprevistas y no añade complejidad técnica.

---

### P2 — ¿Cómo calculamos los días de duración en el futuro (cuando se implemente el calendario laboral)?

**Contexto**: Para el MVP, D-004 definió que se cuentan días calendario inclusive (sin excluir fines de semana ni festivos). Esto es una simplificación que se documentó como deuda técnica. El cálculo definitivo dependerá de la política de la empresa.

**Pregunta**: ¿Cuál será la política de cálculo de días para el futuro?
- ¿Contamos días hábiles (excluyendo sábados, domingos y festivos)?
- ¿Contamos días naturales (incluyendo fines de semana)?
- ¿Los festivos locales se excluyen automáticamente?

**Alternativas**:
- **Opción A**: Días hábiles (solo laborables) — común en muchas empresas.
- **Opción B**: Días naturales (todos los días) — más simple y similar al MVP.
- **Opción C**: Híbrido (días laborables pero con excepciones).

**Recomendación del equipo**: Definir una política clara antes de la iteración 2, pero para el MVP mantenemos días naturales por simplicidad.

---

## 2. Validaciones de entrada (duración, horizonte, preaviso)

### P3 — Duración máxima por solicitud

**Contexto**: FR-022 requiere rechazar solicitudes que excedan un número máximo de días consecutivos. Este valor debe ser definido por política de empresa.

**Pregunta**: ¿Cuál es el número máximo de días consecutivos que se pueden solicitar en una sola petición?

**Alternativas**:
- **Opción A**: 30 días (un mes).
- **Opción B**: 90 días (tres meses).
- **Opción C**: 365 días (un año).
- **Opción D**: Sin límite (no aplicar restricción).

**Recomendación del equipo**: 30 días, por ser un límite común en muchas empresas y evitar acumulaciones excesivas.

---

### P4 — Horizonte máximo de anticipación

**Contexto**: FR-023 requiere rechazar solicitudes con fecha de inicio más allá de un cierto horizonte futuro. Define con cuánta antelación máxima se puede planificar el tiempo libre.

**Pregunta**: ¿Con cuánta antelación máxima se puede pedir un permiso?

**Alternativas**:
- **Opción A**: 6 meses.
- **Opción B**: 12 meses.
- **Opción C**: 18 meses.
- **Opción D**: Sin límite (no aplicar restricción).

**Recomendación del equipo**: 12 meses, alineado con la planificación anual típica.

---

### P5 — Preaviso mínimo

**Contexto**: FR-024 exige un número mínimo de días de antelación entre la fecha de solicitud y la fecha de inicio. Esto evita solicitudes de último momento no planificadas.

**Pregunta**: ¿Cuántos días de antelación mínima se exigen para solicitar un permiso?

**Alternativas**:
- **Opción A**: 0 días (permite solicitudes del mismo día).
- **Opción B**: 1 día.
- **Opción C**: 3 días.
- **Opción D**: 5 días.

**Recomendación del equipo**: 1 día, como equilibrio entre flexibilidad y planificación.

---

## 3. Cadena de aprobación y jerarquía organizacional

### P6 — ¿Quién aprueba las solicitudes de los Managers (incluyendo directores)?

**Contexto**: La especificación requiere que todo empleado tenga un manager asignado (D-011). Esto incluye a los propios managers, que deben reportar a su superior. Sin embargo, esto debe ser validado por RRHH y la dirección.

**Pregunta**: ¿Todos los empleados (incluyendo los managers) tienen un jefe directo registrado en el sistema, o hay excepciones (ej. CEO, VP)?

**Alternativas**:
- **Opción A**: Sí, todos tienen un manager (cadena recursiva). Esto es lo que asume el sistema actualmente.
- **Opción B**: Los managers de alto nivel (ej. CEO) no tienen manager; sus solicitudes se manejan manualmente (fuera del sistema).
- **Opción C**: Se crea un "manager virtual" (ej. una cuenta de sistema) para el nivel más alto.

**Recomendación del equipo**: Opción A, pero necesitamos confirmación de RRHH sobre la estructura real.

---

### P7 — Empleados sin manager (ej. CEO): ¿Leave Administrator o fail‑closed?

**Contexto**: La especificación actual (D-005 y FR-025) propone un **"Leave Administrator"** (rol designado por HR) para aprobar solicitudes de empleados sin manager. La alternativa anterior era **fail‑closed** (bloquear la creación de solicitudes). Esta decisión afecta si el sistema es inclusivo o restrictivo para el nivel más alto de la organización.

**Pregunta**: ¿Preferimos la opción de **"Leave Administrator"** (un rol designado por HR que aprueba sus solicitudes) o la opción de **"fail‑closed"** (no pueden crear solicitudes)?

**Alternativas**:
- **Opción A**: Leave Administrator (más flexible, requiere desarrollo del rol y su interfaz).
- **Opción B**: Fail‑closed (más seguro, sin desarrollo adicional, pero bloquea a empleados sin manager).

**Recomendación del equipo**: Leave Administrator, porque garantiza que todos los empleados puedan usar el sistema. Si se elige esta opción, se necesita desarrollar la interfaz para designar a ese rol (puede ser una funcionalidad básica en el MVP).

---

### P8 — Prevención de ciclos jerárquicos (ya incorporada como FR-020)

**Contexto**: FR-020 prohíbe que un empleado reporte directa o indirectamente a sí mismo. Esto se ha incorporado como decisión técnica (D-006).

**Pregunta**: ¿Confirmamos que el sistema debe rechazar cualquier configuración que cree un ciclo (ej. A → B → A)?

**Alternativas**:
- **Opción A**: Sí, prohibir ciclos (recomendado, evita bucles infinitos en el flujo de aprobación).
- **Opción B**: No, permitir ciclos (no recomendado, podría generar auto‑aprobación indirecta).

**Recomendación del equipo**: Opción A. Ya está documentado en el spec como invariante.

---

## 4. Auto‑escalado y tiempos de respuesta de managers

### P9 — Auto‑escalado por inactividad del manager

**Contexto**: FR-026 y D-004 proponen que si un manager no responde a una solicitud `Pending` en un plazo definido, la solicitud se escale automáticamente al nivel superior (skip‑level manager). Esto evita que las solicitudes queden eternamente pendientes.

**Pregunta**: ¿Debe existir un mecanismo de auto‑escalado para solicitudes que lleven mucho tiempo sin respuesta?

- En caso afirmativo, ¿cuál es el **tiempo de espera (timeout)** en días hábiles? (ej. 5 días, 10 días).
- ¿Queremos que este tiempo sea **configurable por equipo/departamento** o sea un **valor global** para toda la organización?

**Alternativas**:
- **Opción A**: No implementar auto‑escalado (las solicitudes quedan pendientes indefinidamente).
- **Opción B**: Sí, con timeout global (ej. 5 días hábiles).
- **Opción C**: Sí, con timeout configurable por equipo (más flexible, requiere interfaz de administración).

**Recomendación del equipo**: Opción B para el MVP (global), y dejar la configuración por equipo para una iteración futura. Timeout sugerido: 5 días hábiles.

---

### P10 — Delegación temporal de managers

**Contexto**: Originalmente se consideró FR-031, pero se dejó como `[NEEDS CLARIFICATION]`. Permite a un manager designar un delegado temporal para aprobaciones durante su ausencia.

**Pregunta**: ¿La delegación temporal de managers está dentro del alcance del MVP o se deja para una iteración futura?

**Alternativas**:
- **Opción A**: No incluir delegación en el MVP (usar solo auto‑escalado como mecanismo de respaldo).
- **Opción B**: Incluir delegación básica (el manager puede seleccionar un sustituto para un período).

**Recomendación del equipo**: Opción A para el MVP; la delegación añade complejidad y puede posponerse.

---

## 5. Post‑aprobación: cancelación y modificación de solicitudes ya aprobadas

### P11 — ¿Las cancelaciones y modificaciones de solicitudes ya aprobadas están dentro del alcance del MVP?

**Contexto**: US-8 (nueva) y FR-027 permiten que un empleado cancele o modifique un permiso ya aprobado. Esto es común en la realidad, pero añade complejidad en el flujo de aprobación y ajuste de saldos.

**Pregunta**: ¿Las cancelaciones y modificaciones (amendments) de solicitudes ya aprobadas están dentro del alcance del MVP, o se dejan para una iteración futura?

- **Si SÍ**: ¿Quién autoriza estos cambios?
  - **Opción A**: Automáticos (el empleado puede cancelar sin re‑aprobación).
  - **Opción B**: Requieren la aprobación del manager (flujo similar a una nueva solicitud).
- ¿Se permiten si la fecha de inicio ya pasó?
  - **Opción A**: Sí, pero solo con aprobación de manager y ajuste de saldo.
  - **Opción B**: No, se bloquean una vez iniciado el permiso.

**Recomendación del equipo**: Dejar esta funcionalidad para una iteración futura, centrando el MVP en el flujo básico de solicitud → aprobación → consulta. Si se incluye, recomendamos que las cancelaciones de futuros sean automáticas y las de pasado requieran aprobación.

---

### P12 — Cancelación después de la fecha de inicio: ¿días consumidos se devuelven o no?

**Contexto**: Si se permite cancelar un permiso ya iniciado (ej. regreso anticipado), surge la duda de cómo gestionar los días ya consumidos.

**Pregunta**: Si un empleado cancela un permiso que ya ha empezado (ej. regresa antes de lo previsto), ¿los días ya consumidos se devuelven al saldo, o se mantienen como consumidos?

**Alternativas**:
- **Opción A**: Los días consumidos se devuelven al saldo (el empleado recupera el saldo no utilizado).
- **Opción B**: Los días consumidos se mantienen como consumidos (no se devuelven).
- **Opción C**: Se devuelven solo los días futuros, los ya pasados quedan consumidos.

**Recomendación del equipo**: Opción C, porque refleja que los días ya pasados no pueden "no consumirse".

---

## 6. Experiencia de usuario y seguridad

### P13 — ¿Es obligatorio el paso de confirmación explícita para acciones finales (aprobar, rechazar, cancelar)?

**Contexto**: SR-005 requiere un paso extra de confirmación para evitar clics accidentales en acciones irreversibles.

**Pregunta**: ¿Queremos mantener esta confirmación en el MVP o podemos confiar en que el usuario confirmará solo con un clic (sin paso extra)?

**Recomendación del equipo**: Mantener la confirmación, porque reduce errores costosos y es una buena práctica de UX.

---

### P14 — ¿Cómo se maneja el escenario de "empleado desactivado" con solicitudes `Pending`?

**Contexto**: El Edge Case dice "Out of scope" para el MVP, pero debemos decidir si en una iteración futura se gestionará.

**Pregunta**: ¿Queremos que el sistema, al desactivar un empleado, cancele automáticamente todas sus solicitudes `Pending`? ¿O simplemente las bloquee sin notificar?

**Recomendación del equipo**: Para el MVP, dejamos fuera de alcance. Pero es útil que el PO reflexione sobre el comportamiento deseado para futuras iteraciones.

---

## 7. Otros temas

### P15 — ¿Existe un límite de solicitudes pendientes por empleado?

**Contexto**: El spec actual no limita el número de solicitudes `Pending` que un empleado puede tener simultáneamente. Algunas empresas permiten varias, otras solo una.

**Pregunta**: ¿Permitimos que un empleado tenga más de una solicitud `Pending` a la vez? (ej. una para vacaciones de verano y otra para Navidad)

**Alternativas**:
- **Opción A**: Sin límite (cualquier número de pendientes).
- **Opción B**: Máximo una solicitud `Pending` por empleado (debe ser resuelta antes de crear otra).

**Recomendación del equipo**: Opción A para el MVP (sin límite), pero debemos confirmar si la política de RRHH lo permite.

---

### P16 — ¿Se requiere un campo de "comentario del manager" al rechazar?

**Contexto**: La especificación permite comentario opcional en el rechazo.

**Pregunta**: ¿El comentario del manager al rechazar una solicitud debe ser obligatorio o puede ser opcional?

**Alternativas**:
- **Opción A**: Obligatorio (mejora la comunicación, da feedback al empleado).
- **Opción B**: Opcional (más rápido para el manager).

**Recomendación del equipo**: Opción A (obligatorio), porque el rechazo sin motivo puede generar frustración.

---
