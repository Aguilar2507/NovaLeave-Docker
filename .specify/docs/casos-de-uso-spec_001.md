# Casos de Uso — Gestión de Solicitudes de Vacaciones (MVP)

**Referencia**: spec_001-vacation-request.md  
**Constitución**: NovaLeave v4.0.0  
**Guía de Pantallas**: spec_003-screen-construction-guide.md  
**Fecha**: 2026-07-24  
**Estado**: Borrador — Reorganizado por Rol (2026-07-25)

---

## Tabla de Contenidos

### Resumen de Casos de Uso por Rol

| ID | Nombre del Caso de Uso | Actor Principal | Módulo/Vista | Prioridad |
|----|-----------------------|-----------------|--------------|-----------|
| **CU-001** | Empleado Envía Solicitud de Vacaciones | Empleado (User) | `/Employee/Requests/Create` | P1 |
| **CU-002** | Empleado Edita una Solicitud Pendiente | Empleado (User) | `/Employee/Requests/{id}/Edit` | P2 |
| **CU-003** | Empleado Cancela una Solicitud Pendiente | Empleado (User) | `/Employee/Dashboard` / `/Employee/Requests/{id}` | P2 |
| **CU-004** | Empleado Anula (Void) una Solicitud Aprobada | Empleado (User) | `/Employee/Dashboard` / `/Employee/Requests/{id}` | P2 |
| **CU-005** | Empleado Consulta Historial y Balance | Empleado (User) | `/Employee/Dashboard` | P2 |
| **CU-101** | Aprobador Consulta Dashboard de Solicitudes Pendientes | Aprobador (Approver) | `/Approver/Dashboard` | P1 |
| **CU-102** | Aprobador Aprueba una Solicitud | Aprobador (Approver) | `/Approver/Requests/{id}` | P1 |
| **CU-103** | Aprobador Rechaza una Solicitud | Aprobador (Approver) | `/Approver/Requests/{id}` | P1 |
| **CU-201** | Visualizar Detalle de Solicitud | Empleado/Aprobador | `/Employee/Requests/{id}` / `/Approver/Requests/{id}` | P1 |
| **CU-202** | Usuario Cambia de Rol Activo (Role Switcher) | Empleado/Aprobador (Dual Role) | Sidebar | P2 |
| **CU-301** | Caducidad Automática de Solicitudes Pendientes | Sistema (Background Job) | Background Service | P1 |
| **CU-302** | Registro de Auditoría e Inmutabilidad de Transacciones | Sistema (Subsistema Transversal) | Application/Infrastructure (corte de concernimiento) | P1 |

---

## Narrativa de Flujos por Rol

### EMPLEADO (User Role)

1. [CU-001: Empleado Envía Solicitud de Vacaciones](#cu-001-empleado-envía-solicitud-de-vacaciones)
2. [CU-002: Empleado Edita una Solicitud Pendiente](#cu-002-empleado-edita-una-solicitud-pendiente)
3. [CU-003: Empleado Cancela una Solicitud Pendiente](#cu-003-empleado-cancela-una-solicitud-pendiente)
4. [CU-004: Empleado Anula (Void) una Solicitud Aprobada](#cu-004-empleado-anula-void-una-solicitud-aprobada)
5. [CU-005: Empleado Consulta Historial y Balance](#cu-005-empleado-consulta-historial-y-balance)

### APROBADOR (Approver Role)

6. [CU-101: Aprobador Consulta Dashboard de Solicitudes Pendientes](#cu-101-aprobador-consulta-dashboard-de-solicitudes-pendientes)
7. [CU-102: Aprobador Aprueba una Solicitud](#cu-102-aprobador-aprueba-una-solicitud)
8. [CU-103: Aprobador Rechaza una Solicitud](#cu-103-aprobador-rechaza-una-solicitud)

### TRANSVERSAL (Multi-Rol)

9. [CU-201: Visualizar Detalle de Solicitud](#cu-201-visualizar-detalle-de-solicitud)
10. [CU-202: Usuario Cambia de Rol Activo (Role Switcher)](#cu-202-usuario-cambia-de-rol-activo-role-switcher)

### SISTEMA (System/Background)

11. [CU-301: Caducidad Automática de Solicitudes Pendientes](#cu-301-caducidad-automática-de-solicitudes-pendientes)
12. [CU-302: Registro de Auditoría e Inmutabilidad de Transacciones del Sistema](#cu-302-registro-de-auditoría-e-inmutabilidad-de-transacciones-del-sistema)

---

## CU-001: Empleado Envía Solicitud de Vacaciones

### 1. Identificación

| Campo | Valor |
|-------|-------|
| **ID** | CU-001 |
| **Nombre** | Empleado Envía Solicitud de Vacaciones |
| **Controlador** | `EmployeeController.CreateRequest` (GET/POST) |
| **Prioridad** | P1 |

### 2. Actores

| Actor | Rol |
|-------|-----|
| Empleado (User) | Usuario autenticado con rol `User` y estado `Active` que crea la solicitud de vacaciones |
| Sistema | Valida reglas de negocio, calcula días hábiles, reserva balance y registra auditoría |

### 3. Precondiciones

| ID | Condición |
|----|-----------|
| PRE-001 | El usuario está autenticado mediante ASP.NET Core Identity con cookie de sesión válida |
| PRE-002 | El usuario posee el rol `User` con estado `Active` |
| PRE-003 | El usuario tiene un balance disponible mayor a cero |

### 4. Flujo Principal

| ID | Paso |
|----|------|
| FP-01 | El Empleado navega a la pantalla **Employee Dashboard** (`/Employee/Dashboard`) y hace clic en el botón "New Request" del componente `BalanceCard` o enlace del `Sidebar` |
| FP-02 | El sistema presenta la pantalla **New Request** (`/Employee/Requests/Create`) con el formulario que incluye: componente `BalanceCard` (balance actual), `FormField` para Fecha Inicio (type=date, min=mañana), `FormField` para Fecha Fin (type=date, min=fecha inicio), área de cálculo de días hábiles (`computed-days`), y `FormField` para Razón (textarea, obligatorio) |
| FP-03 | El Empleado selecciona la Fecha de Inicio (estrictamente futura, no puede ser hoy) |
| FP-04 | El Empleado selecciona la Fecha de Fin (debe ser igual o posterior a la Fecha de Inicio) |
| FP-05 | El script `create-request.js` ejecuta `computeWorkingDaysPreview()` y muestra la previsualización de días hábiles (solo Lunes–Viernes, sin considerar feriados) en el elemento `#computed-days` con `aria-live="polite"` |
| FP-06 | El script ejecuta `validateBalance()` para comparar los días calculados con el balance mostrado en `BalanceCard`; si es insuficiente muestra advertencia visual (esto es solo orientativo, el servidor es autoritativo) |
| FP-07 | El Empleado ingresa la Razón de la solicitud en el campo textarea |
| FP-08 | El script `validateReason()` valida en blur que la razón no esté vacía y no exceda la longitud máxima |
| FP-09 | El Empleado presiona el botón "Submit Request" (`btn btn--primary`) |
| FP-10 | El script `disableOnSubmit()` deshabilita el botón para prevenir doble envío |
| FP-11 | El navegador envía un POST a `/Employee/Requests/Create` incluyendo el token antiforgery (`@Html.AntiForgeryToken()`) |
| FP-12 | El servidor valida el token antiforgery |
| FP-13 | El controlador `EmployeeController.CreateRequest` recibe el ViewModel de entrada (no entidad de dominio) y lo pasa al caso de uso de la capa Application |
| FP-14 | El caso de uso Application invoca FluentValidation (`ValidateAsync`) para validar: fecha inicio > hoy, fecha inicio ≤ fecha fin, razón no vacía, longitud máxima de campos |
| FP-15 | El sistema calcula los días hábiles del rango server-side usando el calendario de feriados configurado (excluyendo sábados, domingos y festivos) — no acepta valor del cliente como autoritativo |
| FP-16 | El sistema verifica que el balance disponible del Empleado sea suficiente para los días calculados (balance – días reservados por solicitudes Pending existentes ≥ días solicitados) |
| FP-17 | El sistema verifica que no exista solapamiento con solicitudes existentes en estado `Pending`, `Approved` o `Voided` del mismo empleado, respaldado por un constraint de exclusión a nivel de base de datos (D-003) |
| FP-18 | El sistema crea la solicitud en estado `Pending`, reserva temporalmente los días (sin deducir del balance), y registra actor, timestamp y datos enviados en el registro de auditoría — todo dentro de una transacción atómica |
| FP-19 | El sistema aplica concurrencia optimista (`rowversion`) |
| FP-20 | El controlador ejecuta Post/Redirect/Get: redirige al **Employee Dashboard** (`/Employee/Dashboard`) |
| FP-21 | La pantalla Employee Dashboard muestra un componente `AlertMessage` de tipo `success` confirmando la creación exitosa (auto-dismiss a los 5 segundos con fade-out 180ms ease-out) |

### 5. Flujos Alternos

#### FA-001: Validación de Fechas Falla en Cliente

| ID | Paso |
|----|------|
| FA-001-01 | El Empleado ingresa una fecha de inicio que es hoy o en el pasado, o una fecha de fin anterior a la fecha de inicio |
| FA-001-02 | El script `validateDates()` detecta el error en el evento `change` del campo de fecha |
| FA-001-03 | Se muestra error inline debajo del `FormField` correspondiente (clase `form-field--error`, elemento `form-field__error` con `role="alert"`) |
| FA-001-04 | El botón "Submit Request" permanece habilitado pero la validación de lado servidor rechazará igualmente si el usuario elude la validación del cliente |

#### FA-002: Balance Insuficiente (Advertencia Cliente)

| ID | Paso |
|----|------|
| FA-002-01 | El script `validateBalance()` detecta que los días calculados exceden el balance disponible mostrado en `BalanceCard` |
| FA-002-02 | Se muestra una advertencia visual (no bloquea envío, es orientativo) |
| FA-002-03 | Si el Empleado envía de todas formas, el servidor rechaza en FP-16 |

### 6. Flujos de Excepción

#### FE-001: Usuario No Autenticado

| ID | Paso |
|----|------|
| FE-001-01 | Un usuario no autenticado intenta acceder a `/Employee/Requests/Create` |
| FE-001-02 | El middleware de autenticación deniega el acceso y redirige a la pantalla **Login** (`/Account/Login`) |

#### FE-002: Fecha de Inicio Inválida (Servidor)

| ID | Paso |
|----|------|
| FE-002-01 | El servidor detecta que la fecha de inicio es hoy o una fecha pasada |
| FE-002-02 | El caso de uso retorna error de validación |
| FE-002-03 | El controlador mapea el error a `ModelState` mediante adaptador compartido |
| FE-002-04 | Se re-renderiza el formulario con los campos poblados y errores inline en los `FormField` correspondientes, más un `AlertMessage` de tipo `error` (persistente, no auto-dismiss) |

#### FE-003: Fecha Fin Anterior a Fecha Inicio (Servidor)

| ID | Paso |
|----|------|
| FE-003-01 | El servidor detecta que fecha fin < fecha inicio |
| FE-003-02 | Se retorna error de validación identificando el campo inválido |
| FE-003-03 | Se re-renderiza formulario con errores (mismo comportamiento que FE-002) |

#### FE-004: Balance Insuficiente (Servidor)

| ID | Paso |
|----|------|
| FE-004-01 | El cálculo server-side de días hábiles determina que el balance disponible es insuficiente |
| FE-004-02 | El sistema rechaza la creación antes de crear la solicitud |
| FE-004-03 | Se re-renderiza con mensaje de error indicando balance insuficiente |

#### FE-005: Solapamiento de Fechas

| ID | Paso |
|----|------|
| FE-005-01 | El sistema detecta solapamiento con solicitud existente en estado `Pending`, `Approved` o `Voided` |
| FE-005-02 | El sistema rechaza la creación sin crear duplicado ni solicitud conflictiva |
| FE-005-03 | Aplica tanto la validación de aplicación como el constraint de exclusión de base de datos (D-003) |
| FE-005-04 | Se re-renderiza con mensaje de error indicando conflicto de fechas |

#### FE-006: Envío Simultáneo (Doble-clic, Múltiples Pestañas)

| ID | Paso |
|----|------|
| FE-006-01 | Se detectan envíos simultáneos o casi simultáneos para el mismo rango de fechas |
| FE-006-02 | El constraint de base de datos asegura que solo una solicitud se crea exitosamente |
| FE-006-03 | El segundo intento es rechazado por solapamiento |

#### FE-007: Usuario Inactivo

| ID | Paso |
|----|------|
| FE-007-01 | Un usuario con estado `Inactive` intenta crear una solicitud |
| FE-007-02 | El sistema revalida el estado activo antes de la operación (Constitución §5 invariante 12) |
| FE-007-03 | Se deniega la acción |

### 7. Postcondiciones

| ID | Estado del Sistema |
|----|-------------------|
| POST-001 | La solicitud existe en estado `Pending` con las fechas, razón, días hábiles calculados y referencia al empleado |
| POST-002 | Los días solicitados están reservados temporalmente en el balance (no deducidos) |
| POST-003 | Un registro de auditoría documenta la creación: actor, timestamp, datos enviados |
| POST-004 | La solicitud es visible en el historial del Empleado y en la cola de pendientes del Aprobador |

### 8. Reglas de Negocio Aplicadas

| ID | Descripción |
|----|-------------|
| RN-001 | Solo se gestionan solicitudes de vacaciones en esta iteración |
| RN-002 | Balance único global acumulativo (1 día por mes completo trabajado) |
| RN-003 | No se permiten balances negativos |
| RN-004 | Mínimo de solicitud: 1 día |
| RN-005 | Máximo de solicitud: balance disponible al momento de solicitar |
| RN-006 | Solo se cuentan días hábiles (excluyendo sábados, domingos y feriados) |
| RN-007 | La fecha de inicio debe ser estrictamente futura (no puede ser hoy) |
| RN-008 | No se permite solapamiento con solicitudes `Pending` o `Approved` |
| RN-009 | Los días en estado `Pending` se reservan pero no se deducen |
| RN-010 | El cálculo de días debe ser server-side; el valor del cliente no es autoritativo |
| RN-011 | Se requiere token antiforgery en toda solicitud POST |
| RN-012 | Fecha inicio ≤ fecha fin |

### 9. Criterios de Aceptación

| ID | Criterio |
|----|----------|
| CA-001 | Dado un Empleado autenticado, cuando envía una solicitud con fecha inicio válida, fecha fin válida (inicio < fin) y razón, entonces el sistema crea la solicitud en estado `Pending`, reserva los días, y registra la auditoría |
| CA-002 | Dado un Empleado autenticado, cuando la fecha inicio es posterior a la fecha fin, entonces el sistema rechaza y muestra error de validación identificando el campo inválido |
| CA-003 | Dado un Empleado autenticado, cuando la fecha de inicio es hoy o en el pasado, entonces el sistema rechaza sin crear solicitud |
| CA-004 | Dado un Empleado con solicitud `Pending` o `Approved` existente, cuando envía un rango que se solapa (incluyendo envíos simultáneos), entonces el sistema rechaza sin crear duplicado, respaldado por constraint de exclusión en BD |
| CA-005 | Dado un Empleado con balance insuficiente, cuando envía una solicitud, entonces el sistema rechaza antes de crear la solicitud indicando balance insuficiente |
| CA-006 | Dada una solicitud creada exitosamente, el sistema calcula los días hábiles server-side excluyendo sábados, domingos y festivos, y no acepta valor del cliente |
| CA-007 | Dado un usuario no autenticado, cuando intenta enviar una solicitud, entonces es redirigido a autenticación |
| CA-008 | Tras creación exitosa, se aplica Post/Redirect/Get y se muestra AlertMessage de éxito en el Dashboard |

---

## CU-002: Empleado Edita una Solicitud Pendiente

### 1. Identificación

| Campo | Valor |
|-------|-------|
| **ID** | CU-002 |
| **Nombre** | Aprobador Aprueba una Solicitud Pendiente |
| **Controlador** | `ApproverController.Approve` (POST) |
| **Prioridad** | P1 |

### 2. Actores

| Actor | Rol |
|-------|-----|
| Aprobador (Approver) | Usuario autenticado con rol `Approver`, estado `Active`, que aprueba solicitudes de empleados asignados |
| Sistema | Valida estado, transiciona solicitud, deduce balance y registra auditoría |

### 3. Precondiciones

| ID | Condición |
|----|-----------|
| PRE-001 | El Aprobador está autenticado con cookie válida |
| PRE-002 | El Aprobador tiene rol `Approver` con estado `Active` |
| PRE-003 | La solicitud existe en estado `Pending` |
| PRE-004 | La solicitud NO pertenece al Aprobador (no puede aprobar su propia solicitud) |
| PRE-005 | El Aprobador tiene una relación de aprobación activa con el dueño de la solicitud |

### 4. Flujo Principal

| ID | Paso |
|----|------|
| FP-01 | El Aprobador navega al **Approver Dashboard** (`/Approver/Dashboard`) desde el enlace en el `Sidebar` |
| FP-02 | El sistema presenta la tabla/cards de solicitudes pendientes ordenadas por urgencia (más próximas a expirar primero), mostrando: nombre del empleado, fechas, días hábiles, fecha de envío, días hasta expiración, y `StatusBadge` |
| FP-03 | El script `approver-dashboard.js` ejecuta `highlightUrgent()` para resaltar solicitudes a 2 días o menos de expirar (borde naranja) |
| FP-04 | El Aprobador hace clic en "Review" para abrir el detalle de la solicitud |
| FP-05 | El sistema presenta la pantalla **Request Detail (Approver)** (`/Approver/Requests/{id}`) mostrando: información del empleado (nombre, balance actual), datos de la solicitud (estado via `StatusBadge`, fechas, días hábiles, razón, fecha envío, fecha expiración), y formularios de decisión con tokens antiforgery y `concurrencyToken` (rowversion) |
| FP-06 | El Aprobador hace clic en el botón "Approve" (`btn btn--primary`, `data-action="approve"`) |
| FP-07 | El script `approver-decision.js` ejecuta `confirmApprove(requestId)` abriendo un `ConfirmationModal` con mensaje: "This will deduct {days} days from {employee}'s balance. This action is final." (foco NO en el botón confirmar, focus trap activo, cierre con Escape) |
| FP-08 | El Aprobador confirma la acción en el modal haciendo clic en el botón de confirmar |
| FP-09 | El script `disableOnSubmit()` previene doble envío |
| FP-10 | Se envía POST a `/Approver/Approve` con `requestId`, `concurrencyToken` y token antiforgery |
| FP-11 | El servidor revalida: identidad, rol activo, estado de la solicitud (`Pending`), que el aprobador no sea dueño, y que el balance no se vuelva negativo (Constitución §5 invariante 12) |
| FP-12 | El sistema transiciona la solicitud a `Approved`, deduce los días reservados del balance del empleado, y registra la transición en auditoría — todo dentro de una transacción atómica |
| FP-13 | Se aplica concurrencia optimista |
| FP-14 | El controlador ejecuta Post/Redirect/Get: redirige al **Approver Dashboard** |
| FP-15 | Se muestra `AlertMessage` de tipo `success` confirmando la aprobación |

### 5. Flujos Alternos

#### FA-001: Solicitud Ya No Está en Estado Pending

| ID | Paso |
|----|------|
| FA-001-01 | Al cargar el detalle, la solicitud ya no está en `Pending` (fue resuelta por otro medio) |
| FA-001-02 | El sistema muestra la pantalla en modo solo lectura sin botones de acción |

### 6. Flujos de Excepción

#### FE-001: Aprobador Intenta Aprobar Su Propia Solicitud

| ID | Paso |
|----|------|
| FE-001-01 | El servidor detecta que el Aprobador es dueño de la solicitud |
| FE-001-02 | Se deniega la acción (botones ocultos en UI + validación server-side) |

#### FE-002: Balance Se Volvería Negativo

| ID | Paso |
|----|------|
| FE-002-01 | El servidor detecta que la aprobación haría el balance negativo |
| FE-002-02 | Se rechaza la aprobación antes de comprometer la transición |
| FE-002-03 | Se muestra `AlertMessage` de error explicando la situación |

#### FE-003: Conflicto de Concurrencia

| ID | Paso |
|----|------|
| FE-003-01 | Dos aprobadores (o un aprobador + sistema de expiración) intentan resolver la misma solicitud simultáneamente |
| FE-003-02 | La concurrencia optimista permite solo una operación exitosa |
| FE-003-03 | El segundo intento recibe error explícito indicando que la solicitud ya fue resuelta |

#### FE-004: Aprobador Sin Relación con el Empleado

| ID | Paso |
|----|------|
| FE-004-01 | Un Aprobador sin asignación al dueño de la solicitud intenta aprobar |
| FE-004-02 | El sistema deniega la acción sin revelar si la solicitud existe (ocultamiento de existencia per SR-003) |

#### FE-005: Aprobador Inactivo

| ID | Paso |
|----|------|
| FE-005-01 | Un Aprobador con estado `Inactive` intenta aprobar |
| FE-005-02 | El sistema revalida el estado activo y deniega la operación |

### 7. Postcondiciones

| ID | Estado del Sistema |
|----|-------------------|
| POST-001 | La solicitud está en estado `Approved` |
| POST-002 | Los días han sido deducidos del balance del empleado |
| POST-003 | Se registró la transición en el audit trail (actor, timestamp, acción) |
| POST-004 | La solicitud ya no aparece en la cola de pendientes del Aprobador |

### 8. Reglas de Negocio Aplicadas

| ID | Descripción |
|----|-------------|
| RN-001 | Un Aprobador no puede aprobar su propia solicitud |
| RN-002 | La aprobación, deducción de balance y auditoría se ejecutan en una transacción atómica |
| RN-003 | La aprobación no puede hacer el balance negativo |
| RN-004 | Solo solicitudes en `Pending` pueden ser aprobadas |
| RN-005 | Se aplica concurrencia optimista para conflictos |
| RN-006 | `Approved` es un estado final (no modificable excepto Void antes de inicio) |
| RN-007 | El servidor revalida identidad, rol, estado activo, ownership y estado de solicitud antes de operar |

### 9. Criterios de Aceptación

| ID | Criterio |
|----|----------|
| CA-001 | Dado un Aprobador autorizado viendo una solicitud `Pending` de un empleado asignado, cuando aprueba, entonces la solicitud transiciona a `Approved`, se deducen los días, y se registra auditoría en operación atómica |
| CA-002 | Dado un usuario viendo su propia solicitud, cuando intenta aprobarla, el sistema deniega la acción sin importar si tiene rol Approver |
| CA-003 | Dado un usuario sin relación de aprobación con el dueño, cuando intenta aprobar, el sistema deniega y no revela existencia del recurso |
| CA-004 | Dada una solicitud que no está en `Pending`, cuando se intenta aprobar, el sistema la rechaza con error explícito |
| CA-005 | Dada una solicitud `Pending` con dos acciones concurrentes, solo una tiene éxito vía concurrencia optimista |
| CA-006 | Si la aprobación haría el balance negativo, se rechaza antes de comprometer la transición |

---

## CU-003: Empleado Cancela una Solicitud Pendiente

### 1. Identificación

| Campo | Valor |
|-------|-------|
| **ID** | CU-003 |
| **Nombre** | Aprobador Rechaza una Solicitud Pendiente |
| **Controlador** | `ApproverController.Reject` (POST) |
| **Prioridad** | P1 |

### 2. Actores

| Actor | Rol |
|-------|-----|
| Aprobador (Approver) | Usuario autenticado con rol `Approver`, estado `Active`, que rechaza una solicitud proporcionando una razón obligatoria |
| Sistema | Valida estado, transiciona solicitud, libera reserva de días y registra auditoría |

### 3. Precondiciones

| ID | Condición |
|----|-----------|
| PRE-001 | El Aprobador está autenticado con cookie válida |
| PRE-002 | El Aprobador tiene rol `Approver` con estado `Active` |
| PRE-003 | La solicitud existe en estado `Pending` |
| PRE-004 | La solicitud NO pertenece al Aprobador |
| PRE-005 | El Aprobador tiene relación de aprobación activa con el dueño |

### 4. Flujo Principal

| ID | Paso |
|----|------|
| FP-01 | El Aprobador se encuentra en la pantalla **Request Detail (Approver)** (`/Approver/Requests/{id}`) |
| FP-02 | El Aprobador hace clic en el botón "Reject" (`btn btn--danger`, `data-action="reject"`) |
| FP-03 | El script `approver-decision.js` ejecuta `showRejectReason()`: se revela el campo `FormField` de Razón de Rechazo (oculto por defecto) y se le da foco |
| FP-04 | El Aprobador ingresa la razón del rechazo (campo obligatorio) |
| FP-05 | El script `validateRejectReason()` verifica que la razón no esté vacía — el botón de confirmación permanece deshabilitado hasta que se proporcione |
| FP-06 | El Aprobador confirma su intención de rechazar |
| FP-07 | El script `confirmReject(requestId)` abre un `ConfirmationModal` con mensaje: "This request will be permanently rejected. The employee will see your reason." |
| FP-08 | El Aprobador confirma en el modal |
| FP-09 | `disableOnSubmit()` previene doble envío |
| FP-10 | Se envía POST a `/Approver/Reject` con `requestId`, `concurrencyToken`, razón de rechazo y token antiforgery |
| FP-11 | El servidor revalida: identidad, rol activo, estado `Pending`, no-ownership, y que la razón esté proporcionada |
| FP-12 | El sistema transiciona la solicitud a `Rejected`, libera los días reservados (sin cambio en balance), almacena la razón de rechazo, y registra la transición en auditoría |
| FP-13 | Post/Redirect/Get: redirige al **Approver Dashboard** con `AlertMessage` de éxito |

### 5. Flujos Alternos

#### FA-001: Aprobador Cancela Intención de Rechazo

| ID | Paso |
|----|------|
| FA-001-01 | El Aprobador hace clic en "Reject" y se revela el campo de razón |
| FA-001-02 | El Aprobador decide no rechazar y navega de vuelta o cierra sin confirmar |
| FA-001-03 | No se realiza ninguna acción en el sistema |

### 6. Flujos de Excepción

#### FE-001: Razón de Rechazo No Proporcionada

| ID | Paso |
|----|------|
| FE-001-01 | El servidor recibe un POST sin razón de rechazo |
| FE-001-02 | Se rechaza la operación y se retorna error de validación indicando que la razón es obligatoria |

#### FE-002: Solicitud No en Estado Pending

| ID | Paso |
|----|------|
| FE-002-01 | La solicitud ya fue resuelta por otro medio |
| FE-002-02 | El sistema rechaza con error explícito (no silencioso) |

#### FE-003: Conflicto de Concurrencia

| ID | Paso |
|----|------|
| FE-003-01 | Operaciones concurrentes sobre la misma solicitud |
| FE-003-02 | Solo una tiene éxito; la otra recibe error indicando que ya fue resuelta |

#### FE-004: Aprobador Es Dueño de la Solicitud

| ID | Paso |
|----|------|
| FE-004-01 | El servidor detecta auto-rechazo |
| FE-004-02 | Se deniega la acción |

### 7. Postcondiciones

| ID | Estado del Sistema |
|----|-------------------|
| POST-001 | La solicitud está en estado `Rejected` |
| POST-002 | Los días reservados han sido liberados (balance no cambia) |
| POST-003 | La razón de rechazo está almacenada y visible para el Empleado |
| POST-004 | Registro de auditoría generado con actor, timestamp y razón |

### 8. Reglas de Negocio Aplicadas

| ID | Descripción |
|----|-------------|
| RN-001 | La razón de rechazo es obligatoria |
| RN-002 | Un Aprobador no puede rechazar su propia solicitud |
| RN-003 | Solo solicitudes en `Pending` pueden ser rechazadas |
| RN-004 | Los días reservados se liberan al rechazar |
| RN-005 | `Rejected` es un estado terminal |
| RN-006 | Se aplica concurrencia optimista |

### 9. Criterios de Aceptación

| ID | Criterio |
|----|----------|
| CA-001 | Dado un Aprobador autorizado con solicitud `Pending` asignada, cuando rechaza con razón proporcionada, entonces la solicitud transiciona a `Rejected`, los días se liberan, la razón se almacena, y se registra auditoría |
| CA-002 | Dado un intento de rechazo sin razón, el sistema lo rechaza y retorna error de validación |
| CA-003 | Dado un usuario intentando rechazar su propia solicitud, el sistema deniega |
| CA-004 | Dada una solicitud no en `Pending`, un intento de rechazo genera error explícito |
| CA-005 | Dadas dos acciones concurrentes, solo una tiene éxito |

---

## CU-004: Empleado Anula (Void) una Solicitud Aprobada

### 1. Identificación

| Campo | Valor |
|-------|-------|
| **ID** | CU-004 |
| **Nombre** | Empleado Cancela una Solicitud Pendiente |
| **Controlador** | `EmployeeController.CancelRequest` (POST) |
| **Prioridad** | P2 |

### 2. Actores

| Actor | Rol |
|-------|-----|
| Empleado (User) | Usuario autenticado dueño de la solicitud que desea cancelarla |
| Sistema | Valida ownership y estado, transiciona y libera reserva |

### 3. Precondiciones

| ID | Condición |
|----|-----------|
| PRE-001 | El Empleado está autenticado con cookie válida |
| PRE-002 | El Empleado tiene rol `User` con estado `Active` |
| PRE-003 | La solicitud existe en estado `Pending` |
| PRE-004 | La solicitud pertenece al Empleado autenticado |

### 4. Flujo Principal

| ID | Paso |
|----|------|
| FP-01 | El Empleado se encuentra en el **Employee Dashboard** (`/Employee/Dashboard`) o en **Request Detail** (`/Employee/Requests/{id}`) |
| FP-02 | El Empleado identifica la solicitud `Pending` que desea cancelar (visible en la tabla desktop con `StatusBadge` "pending" o en `RequestCard` en mobile); el botón "Cancel" es visible solo para solicitudes `Pending` propias |
| FP-03 | El Empleado hace clic en "Cancel" |
| FP-04 | El script `confirmCancel(requestId)` abre un `ConfirmationModal` con icono ⚠️, título de confirmación y mensaje: "This action is irreversible. The reserved days will be released." (focus trap, cierre con Escape, botón confirmar NO pre-enfocado) |
| FP-05 | El Empleado confirma la cancelación en el modal |
| FP-06 | Se envía POST con `requestId`, token antiforgery |
| FP-07 | El servidor revalida: identidad, ownership, estado `Pending`, estado activo del usuario |
| FP-08 | El sistema transiciona la solicitud a `Cancelled`, libera los días reservados, y registra en auditoría |
| FP-09 | La solicitud ya no aparece en la cola del Aprobador |
| FP-10 | Se actualiza la vista: el `StatusBadge` de la solicitud cambia a `cancelled` inmediatamente en la fila/card |
| FP-11 | Se muestra `AlertMessage` de éxito |

### 5. Flujos Alternos

#### FA-001: Cancelación desde Pantalla de Detalle

| ID | Paso |
|----|------|
| FA-001-01 | El Empleado está en `/Employee/Requests/{id}` |
| FA-001-02 | El botón "Cancel" es visible en la sección `request-detail__actions` |
| FA-001-03 | Mismo flujo de confirmación que el principal (FP-04 a FP-11) |

### 6. Flujos de Excepción

#### FE-001: Solicitud No en Estado Pending

| ID | Paso |
|----|------|
| FE-001-01 | La solicitud ya cambió de estado |
| FE-001-02 | El sistema rechaza la cancelación |

#### FE-002: Empleado No es Dueño

| ID | Paso |
|----|------|
| FE-002-01 | Un usuario intenta cancelar solicitud de otro empleado |
| FE-002-02 | El sistema deniega la acción |

#### FE-003: Estado Terminal — Intento de Reapertura

| ID | Paso |
|----|------|
| FE-003-01 | Se intenta transicionar una solicitud ya en `Cancelled` |
| FE-003-02 | El sistema lo previene; `Cancelled` es estado final |

### 7. Postcondiciones

| ID | Estado del Sistema |
|----|-------------------|
| POST-001 | La solicitud está en estado `Cancelled` (estado terminal) |
| POST-002 | Los días reservados han sido liberados |
| POST-003 | Registro de auditoría generado |
| POST-004 | La solicitud está excluida de la cola de pendientes del Aprobador |

### 8. Reglas de Negocio Aplicadas

| ID | Descripción |
|----|-------------|
| RN-001 | Solo el dueño puede cancelar su solicitud |
| RN-002 | Solo solicitudes en `Pending` pueden ser canceladas por el empleado |
| RN-003 | `Cancelled` es un estado terminal sin posibilidad de reapertura |
| RN-004 | Los días reservados se liberan al cancelar |
| RN-005 | Se registra auditoría de la transición |

### 9. Criterios de Aceptación

| ID | Criterio |
|----|----------|
| CA-001 | Dado un Empleado dueño de una solicitud `Pending`, cuando la cancela, transiciona a `Cancelled`, libera los días, y registra auditoría |
| CA-002 | Dado un usuario viendo solicitud de otro empleado, cuando intenta cancelarla, el sistema deniega |
| CA-003 | Dada una solicitud no en `Pending`, la cancelación es rechazada |
| CA-004 | Dada una solicitud `Cancelled`, no se permite ninguna transición posterior |

---

## CU-005: Empleado Consulta Historial y Balance

### 1. Identificación

| Campo | Valor |
|-------|-------|
| **ID** | CU-005 |
| **Nombre** | Empleado Anula una Solicitud Aprobada (Void) |
| **Controlador** | `EmployeeController.VoidRequest` (POST) |
| **Prioridad** | P2 |

### 2. Actores

| Actor | Rol |
|-------|-----|
| Empleado (User) | Usuario autenticado dueño de la solicitud aprobada que desea anularla antes de su inicio |
| Sistema | Valida ownership, estado, fecha de inicio, restaura balance y registra auditoría |

### 3. Precondiciones

| ID | Condición |
|----|-----------|
| PRE-001 | El Empleado está autenticado con cookie válida |
| PRE-002 | El Empleado tiene rol `User` con estado `Active` |
| PRE-003 | La solicitud existe en estado `Approved` |
| PRE-004 | La solicitud pertenece al Empleado autenticado |
| PRE-005 | La fecha de inicio de la solicitud es estrictamente futura (no ha comenzado) |

### 4. Flujo Principal

| ID | Paso |
|----|------|
| FP-01 | El Empleado se encuentra en el **Employee Dashboard** o **Request Detail** (`/Employee/Requests/{id}`) |
| FP-02 | El Empleado identifica una solicitud `Approved` con fecha de inicio futura; el botón "Void" es visible solo bajo estas condiciones |
| FP-03 | El Empleado hace clic en "Void" |
| FP-04 | El script `confirmVoid(requestId)` abre un `ConfirmationModal` con mensaje: "This will return {days} days to your balance. This action cannot be undone." |
| FP-05 | El Empleado confirma en el modal |
| FP-06 | Se envía POST con `requestId`, token antiforgery |
| FP-07 | El servidor revalida: identidad, ownership, estado `Approved`, que la fecha de inicio no haya pasado, estado activo del usuario |
| FP-08 | El sistema transiciona la solicitud a `Voided`, retorna los días deducidos al balance del empleado, y registra en auditoría — transacción atómica |
| FP-09 | Se actualiza la vista y se muestra `AlertMessage` de éxito |

### 5. Flujos Alternos

Ninguno.

### 6. Flujos de Excepción

#### FE-001: Fecha de Inicio Ya Pasó

| ID | Paso |
|----|------|
| FE-001-01 | El servidor detecta que la fecha de inicio ya pasó o es hoy |
| FE-001-02 | Se rechaza la anulación con mensaje: "Cancellation is not allowed once the vacation period has started" |

#### FE-002: Empleado No es Dueño

| ID | Paso |
|----|------|
| FE-002-01 | Un usuario intenta anular solicitud de otro |
| FE-002-02 | Se deniega la acción |

#### FE-003: Solicitud No en Estado Approved

| ID | Paso |
|----|------|
| FE-003-01 | La solicitud no está en `Approved` |
| FE-003-02 | Se rechaza la operación |

### 7. Postcondiciones

| ID | Estado del Sistema |
|----|-------------------|
| POST-001 | La solicitud está en estado `Voided` |
| POST-002 | Los días deducidos han sido retornados al balance del empleado |
| POST-003 | Registro de auditoría generado |

### 8. Reglas de Negocio Aplicadas

| ID | Descripción |
|----|-------------|
| RN-001 | Solo solicitudes `Approved` con fecha de inicio futura pueden ser anuladas |
| RN-002 | No se permite anulación una vez que la fecha de inicio ha pasado |
| RN-003 | No se permiten anulaciones parciales; solo completas |
| RN-004 | Los días deducidos se retornan al balance |
| RN-005 | Solo el dueño puede anular su solicitud |
| RN-006 | `Voided` es un estado terminal |

### 9. Criterios de Aceptación

| ID | Criterio |
|----|----------|
| CA-001 | Dado un Empleado dueño de solicitud `Approved` con fecha inicio futura, cuando la anula, transiciona a `Voided`, retorna los días al balance, y registra auditoría |
| CA-002 | Dado un Empleado con solicitud `Approved` cuya fecha inicio ya pasó, cuando intenta anular, el sistema rechaza con mensaje explicativo |
| CA-003 | Dado un usuario intentando anular solicitud de otro, el sistema deniega |

---

## CU-006: Empleado Consulta Historial y Balance

### 1. Identificación

| Campo | Valor |
|-------|-------|
| **ID** | CU-006 |
| **Nombre** | Empleado Consulta Historial de Solicitudes y Balance |
| **Controlador** | `EmployeeController.Dashboard` (GET) |
| **Prioridad** | P2 |

### 2. Actores

| Actor | Rol |
|-------|-----|
| Empleado (User) | Usuario autenticado que consulta su propio historial y balance |
| Sistema | Recupera y presenta datos paginados del empleado |

### 3. Precondiciones

| ID | Condición |
|----|-----------|
| PRE-001 | El Empleado está autenticado con cookie válida |
| PRE-002 | El Empleado tiene rol `User` con estado `Active` |

### 4. Flujo Principal

| ID | Paso |
|----|------|
| FP-01 | El Empleado navega al **Employee Dashboard** (`/Employee/Dashboard`) desde el `Sidebar` |
| FP-02 | El sistema presenta el componente `BalanceCard` con: días disponibles (valor prominente H2 24px/600), etiqueta "days available", y días reservados en solicitudes Pending |
| FP-03 | El sistema presenta el filtro de estado (`filter-select` con opciones: All, Pending, Approved, Rejected, Cancelled, Voided, Expired) |
| FP-04 | El sistema presenta la lista de solicitudes del empleado (solo sus propias solicitudes) ordenadas por fecha de creación más reciente primero |
| FP-05 | En desktop/tablet (≥768px): se muestra tabla con columnas Dates, Working Days, Reason, Shipped, Status (`StatusBadge`), Actions |
| FP-06 | En mobile (<768px): se muestra la lista de componentes `RequestCard` con la misma información en formato de tarjeta |
| FP-07 | Si no hay solicitudes, se muestra el componente `EmptyState` con CTA para crear nueva solicitud |
| FP-08 | Si hay más de 50 registros, se muestra el componente `PaginationControl` (paginación server-side, tamaño fijo 50 registros) |
| FP-09 | El Empleado puede filtrar por estado usando el select; el script `filterByStatus()` envía el formulario y recarga la página con parámetro de query |

### 5. Flujos Alternos

#### FA-001: Filtrado por Estado

| ID | Paso |
|----|------|
| FA-001-01 | El Empleado selecciona un estado del filtro |
| FA-001-02 | Se recarga la página con el query parameter de filtro |
| FA-001-03 | Solo se muestran solicitudes con el estado seleccionado |

### 6. Flujos de Excepción

#### FE-001: Intento de Acceso a Datos de Otro Empleado

| ID | Paso |
|----|------|
| FE-001-01 | Un empleado intenta acceder al historial de otro (manipulación de URL) |
| FE-001-02 | El sistema deniega y oculta la existencia del recurso (retorna 404 per SR-003) |

### 7. Postcondiciones

| ID | Estado del Sistema |
|----|-------------------|
| POST-001 | No hay cambio de estado; es una operación de solo lectura |
| POST-002 | Se presentan solo los datos del empleado autenticado |

### 8. Reglas de Negocio Aplicadas

| ID | Descripción |
|----|-------------|
| RN-001 | Un empleado solo puede ver sus propias solicitudes y balance |
| RN-002 | El balance muestra días disponibles y días reservados (Pending) por separado |
| RN-003 | Listas con más de 50 registros usan paginación server-side |
| RN-004 | El acceso a datos ajenos se oculta (no revela existencia) |

### 9. Criterios de Aceptación

| ID | Criterio |
|----|----------|
| CA-001 | Dado un Empleado consultando su historial, el sistema retorna solo sus solicitudes con estado actual y balances correctos |
| CA-002 | Dado un Empleado intentando acceder a datos de otro, el sistema deniega y oculta existencia del recurso |
| CA-003 | Dado un historial con más de 50 registros, se aplica paginación server-side en chunks de 50 |
| CA-004 | El balance muestra días disponibles y días reservados (Pending) por separado |

---


## CU-002: Empleado Edita una Solicitud Pendiente

### 1. Identificación

| Campo | Valor |
|-------|-------|
| **ID** | CU-002 |
| **Nombre** | Empleado Edita una Solicitud Pendiente |
| **Controlador** | `EmployeeController.EditRequest` (GET/POST) |
| **Prioridad** | P2 |

### 2. Actores

| Actor | Rol |
|-------|-----|
| Empleado (User) | Usuario autenticado dueño de la solicitud en estado `Pending` que desea modificarla |
| Sistema | Revalida todas las reglas como si fuera una solicitud nueva, aplica concurrencia optimista |

### 3. Precondiciones

| ID | Condición |
|----|-----------|
| PRE-001 | El Empleado está autenticado con cookie válida |
| PRE-002 | El Empleado tiene rol `User` con estado `Active` |
| PRE-003 | La solicitud existe en estado `Pending` |
| PRE-004 | La solicitud pertenece al Empleado autenticado |

### 4. Flujo Principal

| ID | Paso |
|----|------|
| FP-01 | El Empleado se encuentra en **Request Detail** (`/Employee/Requests/{id}`) y hace clic en "Edit" (enlace visible solo para solicitudes `Pending` propias) |
| FP-02 | El sistema presenta la pantalla **Edit Pending Request** (`/Employee/Requests/{id}/Edit`) con: `BalanceCard`, formulario pre-poblado con valores actuales (fechas, razón), campo oculto con `id` y `concurrencyToken` (rowversion), y previsualización de días hábiles |
| FP-03 | El Empleado modifica los campos deseados (fecha inicio, fecha fin, razón) |
| FP-04 | Los scripts `validateDates()`, `computeWorkingDaysPreview()`, `validateBalance()` (considerando los días ya reservados por esta solicitud) y `validateReason()` se ejecutan en tiempo real |
| FP-05 | El Empleado presiona "Save Changes" (`btn btn--primary`) |
| FP-06 | `disableOnSubmit()` previene doble envío |
| FP-07 | Se envía POST a `/Employee/Requests/{id}/Edit` con `id`, `concurrencyToken`, nuevas fechas, nueva razón, y token antiforgery |
| FP-08 | El servidor revalida completamente como si fuera nueva solicitud: identidad, ownership, estado `Pending`, fecha inicio > hoy, fecha inicio ≤ fecha fin, balance suficiente (descontando la reserva actual de esta solicitud), sin solapamiento, razón proporcionada |
| FP-09 | El sistema actualiza la solicitud, ajusta la reserva de días si cambió el rango, y registra en auditoría |
| FP-10 | Se aplica concurrencia optimista con el `concurrencyToken` |
| FP-11 | Post/Redirect/Get: redirige a **Request Detail** (`/Employee/Requests/{id}`) con `AlertMessage` de éxito |

### 5. Flujos Alternos

Ninguno.

### 6. Flujos de Excepción

#### FE-001: Conflicto de Concurrencia

| ID | Paso |
|----|------|
| FE-001-01 | El `concurrencyToken` no coincide (solicitud modificada por otro medio mientras se editaba) |
| FE-001-02 | Se muestra error explicando que la solicitud fue modificada en otro lugar |

#### FE-002: Solicitud Ya No Está en Pending

| ID | Paso |
|----|------|
| FE-002-01 | La solicitud cambió de estado entre la carga del formulario y el envío |
| FE-002-02 | Se redirige al detalle con error indicando que ya no es editable |

#### FE-003: Validaciones de Fechas/Balance/Solapamiento

| ID | Paso |
|----|------|
| FE-003-01 | Cualquier validación falla (mismas reglas que CU-001) |
| FE-003-02 | Se re-renderiza el formulario con errores y campos poblados |

#### FE-004: Empleado No es Dueño

| ID | Paso |
|----|------|
| FE-004-01 | Un usuario intenta editar solicitud de otro |
| FE-004-02 | Se retorna 404 (ocultamiento de existencia) |

### 7. Postcondiciones

| ID | Estado del Sistema |
|----|-------------------|
| POST-001 | La solicitud permanece en estado `Pending` con los datos actualizados |
| POST-002 | La reserva de días se ajustó al nuevo rango (si cambió) |
| POST-003 | Registro de auditoría generado documentando la edición |
| POST-004 | El `concurrencyToken` se actualizó |

### 8. Reglas de Negocio Aplicadas

| ID | Descripción |
|----|-------------|
| RN-001 | Solo solicitudes en `Pending` son editables |
| RN-002 | Solo el dueño puede editar su solicitud |
| RN-003 | La edición aplica revalidación completa (mismas reglas que creación) |
| RN-004 | Se usa concurrencia optimista para prevenir actualizaciones perdidas |
| RN-005 | La validación de balance debe considerar los días ya reservados por esta misma solicitud |
| RN-006 | El servidor es autoritativo; valores del cliente no se aceptan como verdad |

### 9. Criterios de Aceptación

| ID | Criterio |
|----|----------|
| CA-001 | Dado un Empleado dueño de solicitud `Pending`, cuando edita con datos válidos, la solicitud se actualiza manteniendo estado `Pending`, se ajusta la reserva, y se registra auditoría |
| CA-002 | Dado un conflicto de concurrencia, el sistema muestra error explicativo sin perder los datos editados |
| CA-003 | Dada una solicitud que ya no está en `Pending` al momento del POST, se redirige con error |
| CA-004 | La pantalla de edición solo es accesible si la solicitud está en `Pending` y pertenece al usuario |
| CA-005 | Todas las validaciones de creación aplican igualmente (fechas futuras, sin solapamiento, balance suficiente, razón obligatoria) |

---

## CU-002: Aprobador Consulta Dashboard de Solicitudes Pendientes

### 1. Identificación

| Campo | Valor |
|-------|-------|
| **ID** | CU-101 |
| **Nombre** | Aprobador Consulta Dashboard de Solicitudes Pendientes |
| **Controlador** | `ApproverController.Dashboard` (GET) |
| **Prioridad** | P1 |

### 2. Actores

| Actor | Rol |
|-------|-----|
| Aprobador (Approver) | Usuario autenticado con rol `Approver` y estado `Active` que revisa solicitudes pendientes de empleados asignados |
| Sistema | Recupera, filtra, ordena y pagina solicitudes pendientes; calcula urgencia por proximidad a expiración |

### 3. Precondiciones

| ID | Condición |
|----|-----------|
| PRE-001 | El Aprobador está autenticado mediante ASP.NET Core Identity con cookie de sesión válida |
| PRE-002 | El Aprobador posee el rol `Approver` con estado `Active` |
| PRE-003 | Existen empleados asignados a este Aprobador en la tabla de aprobación |

### 4. Flujo Principal

| ID | Paso |
|----|------|
| FP-01 | El Aprobador hace clic en el enlace "Pending Approvals" del componente `Sidebar` (sección de enlaces de Approver) |
| FP-02 | El sistema procesa GET a `/Approver/Dashboard` |
| FP-03 | El servidor revalida identidad, rol `Approver` y estado `Active` (Constitución §5, invariante 12) |
| FP-04 | El caso de uso Application consulta todas las solicitudes en estado `Pending` pertenecientes a empleados asignados al Aprobador actual, usando proyección y `AsNoTracking()` |
| FP-05 | El sistema ordena las solicitudes por urgencia: las más próximas a expirar primero (orden ascendente por fecha de expiración) |
| FP-06 | Si hay más de 50 registros, se aplica paginación server-side (tamaño fijo 50 registros) |
| FP-07 | El sistema presenta la pantalla **Approver Dashboard** (`/Approver/Dashboard`) con: sección resumen (`stat-card` con total de pendientes y `stat-card--urgent` con solicitudes próximas a expirar) |
| FP-08 | En desktop/tablet (≥768px): se muestra tabla con columnas Employee, Dates, Working Days, Shipped, Status (`StatusBadge`), Expires In, Actions — dentro de un wrapper `table-responsive` con `overflow-x: auto` |
| FP-09 | En mobile (<768px): se muestra lista de componentes `RequestCard` (variante approver con nombre del empleado) con touch targets mínimos de 44x44px |
| FP-10 | El script `approver-dashboard.js` ejecuta `highlightUrgent()` para aplicar estilo de urgencia (borde naranja) a solicitudes dentro de 2 días de expiración |
| FP-11 | Cada fila/card muestra un enlace "Review" que dirige a `/Approver/Requests/{id}` |
| FP-12 | Si hay paginación, se renderiza el componente `PaginationControl` con navegación prev/next y touch targets de 44x44px |

### 5. Flujos Alternos

#### FA-001: No Hay Solicitudes Pendientes

| ID | Paso |
|----|------|
| FA-001-01 | El sistema determina que no existen solicitudes `Pending` asignadas al Aprobador |
| FA-001-02 | Se muestra el componente `EmptyState` con título, descripción "No pending requests to review" e ilustración decorativa (`alt=""`, `img-fluid`, `srcset`/`sizes` para responsive) |
| FA-001-03 | No se muestra tabla, cards, ni paginación |

#### FA-002: Paginación

| ID | Paso |
|----|------|
| FA-002-01 | El Aprobador hace clic en "Next" o "Previous" en el componente `PaginationControl` |
| FA-002-02 | El sistema recarga la página con el parámetro de query de página correspondiente |
| FA-002-03 | Se muestran los registros de la página solicitada manteniendo el orden por urgencia |

### 6. Flujos de Excepción

#### FE-001: Aprobador No Autenticado

| ID | Paso |
|----|------|
| FE-001-01 | Un usuario no autenticado intenta acceder a `/Approver/Dashboard` |
| FE-001-02 | El middleware de autenticación redirige a `/Account/Login` |

#### FE-002: Aprobador Inactivo

| ID | Paso |
|----|------|
| FE-002-01 | Un usuario con rol `Approver` pero estado `Inactive` intenta acceder al dashboard |
| FE-002-02 | El sistema revalida estado activo y deniega el acceso |

#### FE-003: Usuario Sin Rol Approver

| ID | Paso |
|----|------|
| FE-003-01 | Un usuario autenticado sin rol `Approver` intenta acceder a `/Approver/Dashboard` |
| FE-003-02 | El sistema deniega con política de autorización deny-by-default |

### 7. Postcondiciones

| ID | Estado del Sistema |
|----|-------------------|
| POST-001 | No hay cambio de estado; es una operación de solo lectura |
| POST-002 | Se presentan exclusivamente solicitudes de empleados asignados al Aprobador |

### 8. Reglas de Negocio Aplicadas

| ID | Descripción |
|----|-------------|
| RN-001 | El Aprobador solo ve solicitudes de empleados que le están asignados |
| RN-002 | Las solicitudes se ordenan por urgencia (proximidad a expiración) |
| RN-003 | Listas con más de 50 registros usan paginación server-side |
| RN-004 | Un Aprobador `Inactive` no puede acceder a la funcionalidad de aprobación |
| RN-005 | Las consultas usan `AsNoTracking()` y proyección (sin N+1) |
| RN-006 | Solicitudes propias del Aprobador no aparecen en esta vista (no puede auto-resolver) |

### 9. Criterios de Aceptación

| ID | Criterio |
|----|----------|
| CA-001 | Dado un Aprobador activo con empleados asignados que tienen solicitudes `Pending`, cuando accede al dashboard, se muestran ordenadas por proximidad a expiración |
| CA-002 | Dado un Aprobador activo sin solicitudes pendientes asignadas, se muestra el componente `EmptyState` |
| CA-003 | Dado un Aprobador que es también User, sus propias solicitudes NO aparecen en la cola de aprobación |
| CA-004 | Dadas más de 50 solicitudes pendientes, se aplica paginación server-side con control de navegación |
| CA-005 | Las solicitudes a 2 días o menos de expirar se resaltan visualmente con estilo de urgencia |
| CA-006 | En mobile (<768px) se muestran `RequestCard`; en desktop/tablet (≥768px) se muestra tabla |
| CA-007 | Un usuario sin rol Approver o con estado Inactive no puede acceder al dashboard |

---

## CU-009: Usuario Cambia de Rol Activo (Role Switcher)

### 1. Identificación

| Campo | Valor |
|-------|-------|
| **ID** | CU-009 |
| **Nombre** | Usuario Cambia de Rol Activo (Role Switcher) |
| **Controlador** | N/A — Comportamiento client-side en `shared.js` (navegación a dashboard del rol seleccionado) |
| **Prioridad** | P2 |

### 2. Actores

| Actor | Rol |
|-------|-----|
| Usuario Dual (User + Approver) | Usuario autenticado que posee ambos roles y desea cambiar el contexto de vista activo |
| Sistema | Reconstruye navegación del `Sidebar`, redirige al dashboard correspondiente |

### 3. Precondiciones

| ID | Condición |
|----|-----------|
| PRE-001 | El usuario está autenticado con cookie válida |
| PRE-002 | El usuario posee AMBOS roles: `User` y `Approver` |
| PRE-003 | El componente `Role Switcher` es visible en el `sidebar__footer` (solo se renderiza para usuarios con múltiples roles) |

### 4. Flujo Principal

| ID | Paso |
|----|------|
| FP-01 | El Usuario Dual observa el componente `Role Switcher` en el footer del `Sidebar`, mostrando el rol activo actual con icono y texto "Viewing as: **{activeRoleLabel}**" |
| FP-02 | El usuario hace clic en el botón `role-switcher__trigger` (atributos `aria-haspopup="listbox"`, `aria-expanded="false"`) |
| FP-03 | El script `shared.js` ejecuta `toggleRoleSwitcher(event)`: actualiza `aria-expanded` a `true`, muestra el menú de opciones de rol |
| FP-04 | Se presentan las opciones disponibles (Employee, Approver) con indicador visual del rol actualmente activo (checkmark) |
| FP-05 | El usuario selecciona el rol deseado haciendo clic en la opción |
| FP-06 | El script ejecuta `switchRole(role)`: actualiza la etiqueta del rol activo, reconstruye los enlaces de navegación del `Sidebar` (Employee ve rutas de empleado, Approver ve rutas de aprobador) |
| FP-07 | El script navega al dashboard correspondiente al nuevo rol: `/Employee/Dashboard` si seleccionó Employee, `/Approver/Dashboard` si seleccionó Approver |
| FP-08 | El script ejecuta `closeRoleSwitcher()`: cierra el menú y resetea `aria-expanded` a `false` |
| FP-09 | El `Sidebar` muestra los enlaces actualizados según el nuevo rol; el enlace activo se destaca con `sidebar__link--active` (función `highlightActiveLink()`) |

### 5. Flujos Alternos

#### FA-001: Cierre sin Selección

| ID | Paso |
|----|------|
| FA-001-01 | El usuario abre el Role Switcher pero hace clic fuera del componente, presiona Escape, o no selecciona ningún rol |
| FA-001-02 | El script `closeRoleSwitcher()` cierra el menú sin cambiar el contexto |
| FA-001-03 | El Sidebar mantiene su estado actual sin navegación |

#### FA-002: Usuario con Un Solo Rol

| ID | Paso |
|----|------|
| FA-002-01 | Un usuario con un solo rol (solo User o solo Approver) accede a una pantalla autenticada |
| FA-002-02 | El componente `Role Switcher` permanece oculto (`style="display:none;"`) |
| FA-002-03 | El Sidebar muestra solo los enlaces correspondientes al único rol |

### 6. Flujos de Excepción

#### FE-001: Intento de Acceso a Ruta de Rol No Poseído

| ID | Paso |
|----|------|
| FE-001-01 | Un usuario manipula la URL y navega a una ruta de un rol que no posee (ej: usuario solo Employee navega a `/Approver/Dashboard`) |
| FE-001-02 | El servidor aplica política de autorización deny-by-default y deniega el acceso |
| FE-001-03 | El cambio de vista es solo cosmético; la autorización real se valida server-side en cada request |

### 7. Postcondiciones

| ID | Estado del Sistema |
|----|-------------------|
| POST-001 | No hay cambio de autenticación ni permisos; el cambio es puramente de contexto de vista |
| POST-002 | El Sidebar refleja los enlaces del rol activo seleccionado |
| POST-003 | El usuario se encuentra en el dashboard correspondiente al rol seleccionado |

### 8. Reglas de Negocio Aplicadas

| ID | Descripción |
|----|-------------|
| RN-001 | El Role Switcher cambia solo el contexto de vista, NO modifica autenticación ni permisos reales |
| RN-002 | La autorización se evalúa por acción y recurso en el servidor, no por el rol visual activo |
| RN-003 | Un usuario con rol `Approver` no puede resolver sus propias solicitudes sin importar la vista activa |
| RN-004 | El Role Switcher solo es visible para usuarios con múltiples roles |
| RN-005 | Ocultar un botón o menú NO constituye control de seguridad; es solo UX |

### 9. Criterios de Aceptación

| ID | Criterio |
|----|----------|
| CA-001 | Dado un usuario con ambos roles (User + Approver), el Role Switcher es visible en el footer del Sidebar |
| CA-002 | Dado un usuario con un solo rol, el Role Switcher NO es visible |
| CA-003 | Cuando el usuario cambia de rol, se navega al dashboard correspondiente y el Sidebar se reconstruye con los enlaces del nuevo rol |
| CA-004 | El cambio de rol NO altera los permisos server-side; un Approver viendo como Employee sigue sin poder aprobar sus propias solicitudes |
| CA-005 | Se puede cerrar el Role Switcher con Escape, clic fuera, o sin seleccionar opción |
| CA-006 | Los atributos ARIA (`aria-haspopup`, `aria-expanded`) se actualizan correctamente en cada interacción |

---

## CU-010: Visualizar Detalle de Solicitud

### 1. Identificación

| Campo | Valor |
|-------|-------|
| **ID** | CU-010 |
| **Nombre** | Visualizar Detalle de una Solicitud de Vacaciones |
| **Controlador** | `EmployeeController.RequestDetail` (GET) / `ApproverController.RequestDetail` (GET) |
| **Prioridad** | P1 |

### 2. Actores

| Actor | Rol |
|-------|-----|
| Empleado (User) | Usuario autenticado que consulta el detalle de su propia solicitud |
| Aprobador (Approver) | Usuario autenticado con rol `Approver` que consulta el detalle de una solicitud asignada para revisión |
| Sistema | Valida ownership/asignación, recupera datos y presenta vista condicional según estado y rol |

### 3. Precondiciones

| ID | Condición |
|----|-----------|
| PRE-001 | El usuario está autenticado con cookie válida |
| PRE-002 | El usuario tiene estado `Active` |
| PRE-003 | La solicitud existe en la base de datos |
| PRE-004 | El usuario es el dueño de la solicitud (vista Employee) O tiene rol `Approver` asignado al dueño (vista Approver) |

### 4. Flujo Principal

| ID | Paso |
|----|------|
| FP-01 | El usuario hace clic en una solicitud desde el **Employee Dashboard** (enlace en fila de tabla o `RequestCard`) o desde el **Approver Dashboard** (enlace "Review") |
| FP-02 | El sistema procesa GET a `/Employee/Requests/{id}` o `/Approver/Requests/{id}` según el contexto |
| FP-03 | El servidor revalida identidad, estado activo, y autorización de recurso: ownership (Employee) o relación de aprobación (Approver) |
| FP-04 | Si la autorización es exitosa, el caso de uso Application recupera la solicitud con proyección |
| FP-05 | **Vista Employee** (`/Employee/Requests/{id}`): el sistema presenta la pantalla con sección `request-detail__summary` conteniendo `detail-list` (definición list `<dl>`) con: Status (`StatusBadge`), Start Date, End Date, Working Days, Reason, Submitted (fecha creación), Expires (fecha expiración), Resolved By (o "—"), Rejection Reason (o "—") |
| FP-06 | **Vista Approver** (`/Approver/Requests/{id}`): el sistema presenta adicionalmente la sección `request-detail__employee-info` con nombre del empleado y balance actual, más la sección `request-detail__decision` con formularios de Approve/Reject (si está en `Pending`) |
| FP-07 | **Acciones condicionales (Employee)**: si la solicitud está en `Pending`, se muestran botones "Cancel" y enlace "Edit"; si está en `Approved` con fecha inicio futura, se muestra botón "Void" |
| FP-08 | **Acciones condicionales (Approver)**: si la solicitud está en `Pending`, se muestran botones "Approve" y "Reject" con sus respectivos formularios (antiforgery + concurrencyToken); si no está en `Pending`, se muestra en modo solo lectura sin botones de acción |
| FP-09 | La sección de Rejection Reason se oculta cuando no es aplicable (solicitud no rechazada) |
| FP-10 | Se incluye enlace "Back to Dashboard" (`btn btn--secondary`) que retorna al dashboard correspondiente |

### 5. Flujos Alternos

#### FA-001: Solicitud en Estado Terminal (Solo Lectura)

| ID | Paso |
|----|------|
| FA-001-01 | La solicitud se encuentra en estado terminal (`Cancelled`, `Rejected`, `Voided`, `Expired`) |
| FA-001-02 | No se muestran botones de acción (ni Cancel, ni Void, ni Edit, ni Approve, ni Reject) |
| FA-001-03 | Se muestra toda la información de la solicitud en modo solo lectura incluyendo razón de rechazo si aplica |

#### FA-002: Solicitud Approved sin Posibilidad de Void

| ID | Paso |
|----|------|
| FA-002-01 | La solicitud está en `Approved` pero la fecha de inicio ya pasó o es hoy |
| FA-002-02 | El botón "Void" NO se muestra (la cancelación post-inicio no está permitida) |

### 6. Flujos de Excepción

#### FE-001: Solicitud No Existe o No Pertenece al Usuario

| ID | Paso |
|----|------|
| FE-001-01 | El usuario navega a `/Employee/Requests/{id}` con un ID inexistente o de una solicitud que no le pertenece |
| FE-001-02 | El sistema retorna HTTP 404 (ocultamiento de existencia per SR-003) — no revela si el recurso existe o no |
| FE-001-03 | Se presenta la pantalla **Not Found (404)** |

#### FE-002: Aprobador Sin Relación con el Dueño

| ID | Paso |
|----|------|
| FE-002-01 | Un Aprobador navega a `/Approver/Requests/{id}` para una solicitud cuyo dueño no le está asignado |
| FE-002-02 | El sistema retorna HTTP 404 (ocultamiento de existencia) |

#### FE-003: Usuario No Autenticado

| ID | Paso |
|----|------|
| FE-003-01 | Un usuario no autenticado intenta acceder a cualquiera de las rutas de detalle |
| FE-003-02 | Redirección a `/Account/Login` |

### 7. Postcondiciones

| ID | Estado del Sistema |
|----|-------------------|
| POST-001 | No hay cambio de estado; operación de solo lectura |
| POST-002 | Las acciones disponibles reflejan correctamente el estado actual de la solicitud y el rol del usuario |

### 8. Reglas de Negocio Aplicadas

| ID | Descripción |
|----|-------------|
| RN-001 | Un Empleado solo puede ver el detalle de sus propias solicitudes |
| RN-002 | Un Aprobador solo puede ver solicitudes de empleados asignados |
| RN-003 | Si el recurso no es accesible, se oculta su existencia retornando 404 (SR-003) |
| RN-004 | Las acciones mostradas dependen del estado de la solicitud y del rol del usuario |
| RN-005 | La razón de rechazo solo es visible cuando la solicitud fue rechazada |
| RN-006 | Los formularios de acción incluyen antiforgery token y concurrency token |

### 9. Criterios de Aceptación

| ID | Criterio |
|----|----------|
| CA-001 | Dado un Empleado dueño de una solicitud, cuando accede a su detalle, se muestra toda la información con acciones apropiadas según el estado |
| CA-002 | Dado un Aprobador asignado al dueño, cuando accede al detalle de una solicitud `Pending`, se muestran los formularios de Approve/Reject |
| CA-003 | Dado un Aprobador viendo una solicitud no-Pending, se presenta en modo solo lectura sin botones de acción |
| CA-004 | Dado un usuario accediendo a una solicitud que no le pertenece o no existe, se retorna 404 sin revelar existencia |
| CA-005 | Las acciones Cancel/Void (Employee) solo aparecen cuando el estado y las condiciones de fecha lo permiten |
| CA-006 | El enlace Edit solo aparece para solicitudes `Pending` propias del Employee |

---

## CU-011: Caducidad Automática de Solicitudes Pendientes

### 1. Identificación

| Campo | Valor |
|-------|-------|
| **ID** | CU-011 |
| **Nombre** | Caducidad Automática de Solicitudes Pendientes (Auto-Expiry) |
| **Controlador** | N/A — Proceso del sistema (Background Service / Scheduled Job) |
| **Prioridad** | P1 |

### 2. Actores

| Actor | Rol |
|-------|-----|
| Sistema (Actor Automático) | Proceso programado que evalúa solicitudes `Pending` y expira las que superan el plazo configurado |

### 3. Precondiciones

| ID | Condición |
|----|-----------|
| PRE-001 | El parámetro de configuración `N` (días hábiles máximos sin resolución) está definido y es mayor a cero |
| PRE-002 | Existen solicitudes en estado `Pending` en la base de datos |
| PRE-003 | El servicio de background o job programado está activo y ejecutándose |

### 4. Flujo Principal

| ID | Paso |
|----|------|
| FP-01 | El proceso programado del sistema se activa según su cadencia configurada (ej: diariamente o en intervalos definidos) |
| FP-02 | El sistema consulta todas las solicitudes en estado `Pending` cuya fecha de creación supera `N` días hábiles (calculados excluyendo sábados, domingos y feriados usando el calendario de feriados configurado) |
| FP-03 | Para cada solicitud candidata a expiración, el sistema inicia una transacción atómica |
| FP-04 | El sistema revalida que la solicitud sigue en estado `Pending` (verificación de estado antes de operar, concurrencia optimista) |
| FP-05 | El sistema transiciona la solicitud al estado `Expired` |
| FP-06 | El sistema libera los días reservados (la reserva temporal se elimina; el balance disponible del empleado se restaura) |
| FP-07 | El sistema registra la transición en el audit trail: actor = "System", timestamp, acción = "Auto-Expiry", solicitud ID, días liberados |
| FP-08 | El sistema confirma la transacción atómica |
| FP-09 | El proceso repite FP-03 a FP-08 para cada solicitud candidata |
| FP-10 | La próxima vez que el Empleado acceda a su **Employee Dashboard**, la solicitud aparecerá con `StatusBadge` en estado `expired`; si hay notificación visual pendiente, se puede mostrar un `AlertMessage` de tipo `info` |
| FP-11 | La solicitud expirada ya no aparece en la cola de pendientes del Aprobador |

### 5. Flujos Alternos

#### FA-001: No Hay Solicitudes Para Expirar

| ID | Paso |
|----|------|
| FA-001-01 | El proceso se ejecuta pero no encuentra solicitudes `Pending` que superen el plazo |
| FA-001-02 | El proceso termina sin realizar cambios |
| FA-001-03 | Se registra log informativo de ejecución exitosa sin solicitudes afectadas |

#### FA-002: Solicitud Resuelta Concurrentemente

| ID | Paso |
|----|------|
| FA-002-01 | Entre la consulta y la transición, la solicitud fue aprobada/rechazada/cancelada por otro actor |
| FA-002-02 | La verificación de estado en FP-04 detecta que ya no está en `Pending` |
| FA-002-03 | El sistema omite esa solicitud sin error y continúa con la siguiente |

### 6. Flujos de Excepción

#### FE-001: Error de Transacción

| ID | Paso |
|----|------|
| FE-001-01 | Una transacción atómica falla (error de BD, timeout, etc.) |
| FE-001-02 | Se ejecuta rollback de la transacción; la solicitud permanece en `Pending` |
| FE-001-03 | Se registra log de error con detalles (structured logging via Serilog) |
| FE-001-04 | El proceso continúa con las demás solicitudes candidatas |

#### FE-002: Conflicto de Concurrencia

| ID | Paso |
|----|------|
| FE-002-01 | El `rowversion` no coincide al intentar la transición |
| FE-002-02 | El sistema detecta el conflicto de concurrencia optimista |
| FE-002-03 | Se omite la solicitud (será evaluada en la próxima ejecución) y se registra log informativo |

#### FE-003: Configuración de N No Disponible

| ID | Paso |
|----|------|
| FE-003-01 | El parámetro `N` no está configurado o es inválido |
| FE-003-02 | El proceso no ejecuta expiraciones y registra log de error crítico |
| FE-003-03 | Se alerta al equipo de operaciones (observabilidad) |

### 7. Postcondiciones

| ID | Estado del Sistema |
|----|-------------------|
| POST-001 | Las solicitudes que superaron el plazo están en estado `Expired` |
| POST-002 | Los días reservados por solicitudes expiradas han sido liberados al balance de cada empleado |
| POST-003 | Cada transición generó un registro de auditoría con actor "System" |
| POST-004 | Las solicitudes expiradas no aparecen en colas de aprobación |
| POST-005 | `Expired` es un estado terminal — no se permiten transiciones posteriores |

### 8. Reglas de Negocio Aplicadas

| ID | Descripción |
|----|-------------|
| RN-001 | El plazo de expiración `N` es parametrizable y se mide en días hábiles (excluyendo sábados, domingos y feriados) |
| RN-002 | Solo solicitudes en estado `Pending` son candidatas a expiración |
| RN-003 | La liberación de días reservados y la transición de estado se ejecutan en transacción atómica |
| RN-004 | El actor registrado en auditoría es "System" (proceso automático) |
| RN-005 | `Expired` es un estado terminal sin posibilidad de reapertura |
| RN-006 | El proceso es idempotente: ejecutarlo múltiples veces no produce efectos secundarios |
| RN-007 | Se aplica concurrencia optimista; conflictos se resuelven omitiendo y reintentando en próxima ejecución |
| RN-008 | El cálculo de días hábiles usa el mismo calendario de feriados que el cálculo de duración de solicitudes |

### 9. Criterios de Aceptación

| ID | Criterio |
|----|----------|
| CA-001 | Dada una solicitud `Pending` cuya antigüedad supera N días hábiles, el sistema la transiciona a `Expired`, libera los días, y registra auditoría con actor "System" |
| CA-002 | Dada una solicitud `Pending` con antigüedad menor a N días hábiles, el sistema no la modifica |
| CA-003 | Dada una solicitud que fue resuelta (aprobada/rechazada/cancelada) entre la consulta y la transición, el sistema la omite sin error |
| CA-004 | Dado un fallo en una transacción individual, el proceso continúa con las demás solicitudes sin afectar el lote |
| CA-005 | El empleado ve su solicitud con `StatusBadge` "expired" en su dashboard tras la expiración |
| CA-006 | La solicitud expirada ya no aparece en la cola del Aprobador |
| CA-007 | `Expired` es un estado terminal; no se permite ninguna transición posterior |
| CA-008 | El parámetro N es configurable sin requerir cambios de código |

---

## CU-302: Registro de Auditoría e Inmutabilidad de Transacciones del Sistema

### 1. Identificación

| Campo | Valor |
|-------|-------|
| **ID** | CU-302 |
| **Nombre** | Registro de Auditoría e Inmutabilidad de Transacciones del Sistema |
| **Controlador** | N/A — Subsistema transversal (corte de concernimiento) ejecutado por la capa Application/Infrastructure en cada operación de negocio |
| **Prioridad** | P1 |

### 2. Actores

| Actor | Rol |
|-------|-----|
| Sistema (Actor Automático) | Registra inmediatamente cada transición de estado y cambio crítico en la tabla de auditoría de forma atómica con la operación de negocio |
| Empleado (User) | Consulta el historial de auditoría de sus propias solicitudes |
| Aprobador (Approver) | Consulta el historial de auditoría de solicitudes que le están asignadas |

### 3. Precondiciones

| ID | Condición |
|----|-----------|
| PRE-001 | La tabla de auditoría (`AuditLog` o equivalente) existe en la base de datos con esquema versionado |
| PRE-002 | La capa Application/Infrastructure está configurada para invocar el registro de auditoría en cada operación de negocio que involucre transición de estado o cambio crítico |
| PRE-003 | El mecanismo de protección (restricción a nivel de base de datos contra UPDATE/DELETE) está activo |

### 4. Flujo Principal

| ID | Paso |
|----|------|
| FP-01 | Una operación de negocio (creación, aprobación, rechazo, cancelación, caducidad, edición, anulación) se ejecuta dentro de una transacción atómica |
| FP-02 | Dentro de la misma transacción, el subsistema de auditoría genera un registro con los campos obligatorios: `timestamp_utc`, `actor_id`, `actor_role`, `action`, `entity_type`, `entity_id`, `result`, `correlation_id`, `request_id` |
| FP-03 | Cuando es aplicable, el registro incluye valores `before` y `after` (ej: estado previo → estado nuevo, balance previo → balance nuevo); campos sensibles (razón médica, datos PII innecesarios) son enmascarados o excluidos |
| FP-04 | La transacción se confirma; el registro de auditoría y la operación de negocio se persisten atómicamente |
| FP-05 | El registro queda permanentemente almacenado e inmutable: no puede ser modificado, eliminado ni anulado por ningún usuario o proceso del sistema |
| FP-06 | Un usuario autorizado (Empleado dueño o Aprobador asignado) solicita consultar el historial de auditoría de una solicitud específica |
| FP-07 | El sistema valida la autorización del solicitante (ownership o relación de aprobación) y retorna los registros de auditoría de la solicitud en orden cronológico inverso (más reciente primero) |

### 5. Flujos Alternos

#### FA-001: Múltiples Transiciones en una Transacción Atómica

| ID | Paso |
|----|------|
| FA-001-01 | Una operación de negocio ejecuta múltiples transiciones en una sola transacción atómica (ej: aprobación que deduce balance) |
| FA-001-02 | Cada transición genera su propio registro de auditoría independiente dentro de la misma transacción |
| FA-001-03 | Los registros comparten el mismo `correlation_id` y `request_id` para vinculación lógica |

#### FA-002: Datos Sensibles en Valores Antes/Después

| ID | Paso |
|----|------|
| FA-002-01 | Una transición involucra datos sensibles (razón de solicitud que contiene información médica o PII) |
| FA-002-02 | El sistema enmascara los campos sensibles en los valores `before`/`after` del registro de auditoría |
| FA-002-03 | Se preserva la trazabilidad de la transición sin exponer información innecesaria |

### 6. Flujos de Excepción

#### FE-001: Fallo del Registro de Auditoría

| ID | Paso |
|----|------|
| FE-001-01 | La transacción de negocio se confirma pero el intento de insertar el registro de auditoría falla (error de BD, constraint, etc.) |
| FE-001-02 | La transacción principal realiza rollback (la Constitución §6 exige atomicidad entre operación y auditoría) |
| FE-001-03 | La operación de negocio no se persiste; se registra log de error crítico |
| FE-001-04 | Se alerta al equipo de operaciones (observabilidad) |

#### FE-002: Intento de Modificación de Registro de Auditoría

| ID | Paso |
|----|------|
| FE-002-01 | Un usuario, proceso o script intenta ejecutar UPDATE o DELETE sobre la tabla de auditoría |
| FE-002-02 | La restricción a nivel de base de datos (trigger, policy, o CHECK constraint) deniega la operación |
| FE-002-03 | Se genera una alerta de seguridad con los detalles del intento no autorizado |

#### FE-003: Acceso No Autorizado a Registros de Auditoría

| ID | Paso |
|----|------|
| FE-003-01 | Un Empleado intenta consultar registros de auditoría de solicitudes que no le pertenecen |
| FE-003-02 | El sistema valida ownership y deniega el acceso |
| FE-003-03 | Se registra el intento denegado en logs de seguridad |

#### FE-004: Paginación de Resultados Grandes

| ID | Paso |
|----|------|
| FE-004-01 | Una consulta de auditoría retorna más de 50 registros |
| FE-004-02 | El sistema aplica paginación server-side (tamaño fijo 50 registros) |
| FE-004-03 | Se renderiza componente `PaginationControl` con navegación prev/next |

### 7. Postcondiciones

| ID | Estado del Sistema |
|----|-------------------|
| POST-001 | Cada transición de estado y cambio crítico tiene un registro de auditoría atómico e inmutable |
| POST-002 | Los registros de auditoría no pueden ser modificados ni eliminados por ningún actor |
| POST-003 | Los usuarios autorizados pueden consultar el historial de auditoría de sus solicitudes/asignaciones |
| POST-004 | Los datos sensibles aparecen enmascarados en los registros de auditoría |

### 8. Reglas de Negocio Aplicadas

| ID | Descripción |
|----|-------------|
| RN-001 | Cada registro de auditoría se crea dentro de la misma transacción atómica que la operación de negocio (Constitución §6) |
| RN-002 | Campos obligatorios: `timestamp_utc`, `actor_id`, `actor_role`, `action`, `entity_type`, `entity_id`, `result`, `correlation_id`, `request_id` (Constitución §8) |
| RN-003 | Cada registro es inmutable una vez persistido; no se permite UPDATE ni DELETE (Constitución §8, §6) |
| RN-004 | La eliminación de registros de auditoría por ley o política sigue un proceso aprobado y auditable (Constitución §6) |
| RN-005 | Los registros se retienen por un mínimo de siete años (Constitución §13) |
| RN-006 | El acceso a registros de auditoría se valida por ownership (Empleado) o relación de aprobación (Approver) |
| RN-007 | Campos sensibles (razón médica, PII) se enmascaran en valores `before`/`after` |
| RN-008 | El fallo del registro de auditoría provoca rollback de la transacción principal |
| RN-009 | Listas con más de 50 registros usan paginación server-side |

### 9. Criterios de Aceptación

| ID | Criterio |
|----|----------|
| CA-001 | Dada cualquier transición de estado (creación, aprobación, rechazo, cancelación, caducidad, edición, anulación), el sistema genera un registro de auditoría con todos los campos obligatorios en la misma transacción atómica |
| CA-002 | Dada una operación que ejecuta múltiples transiciones en una transacción, cada transición genera su propio registro con el mismo `correlation_id` |
| CA-003 | Dado un registro de auditoría persistido, ningún usuario ni proceso del sistema puede modificarlo ni eliminarlo; el intento es denegado y genera alerta de seguridad |
| CA-004 | Dado un Empleado consultando auditoría de sus propias solicitudes, el sistema retorna los registros en orden cronológico inverso |
| CA-005 | Dado un Empleado intentando acceder a registros de solicitudes de otro empleado, el sistema deniega el acceso |
| CA-006 | Dado un Aprobador consultando auditoría de una solicitud asignada, el sistema retorna los registros correspondientes |
| CA-007 | Dada una transición con datos sensibles, los valores `before`/`after` enmascaran los campos PII |
| CA-008 | Dado un fallo en el registro de auditoría, la transacción de negocio realiza rollback y no se persiste |
| CA-009 | Dada una consulta de auditoría con más de 50 registros, se aplica paginación server-side |
| CA-010 | Los registros de auditoría se retienen por un mínimo de siete años sin eliminación física |
| CA-011 | El intento de ejecutar UPDATE o DELETE sobre la tabla de auditoría es denegado a nivel de base de datos y genera alerta de seguridad |
