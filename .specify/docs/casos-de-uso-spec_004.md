# Casos de Uso — Login y Autenticación (MVP)

**Referencia**: spec_004-login-authentication.md
**Constitución**: NovaLeave v4.0.0
**Guía de Pantallas**: spec_003-screen-construction-guide.md
**Fecha**: 2026-07-28
**Estado**: Borrador

> **Nota de terminología**: Este documento usa "Usuario (User)" como rol constitucional (Constitución §4), siguiendo el mismo patrón de `casos-de-uso-spec_001.md` ("Empleado (User)"). El actor que inicia sesión puede sostener el rol `User`, el rol `Approver`, o ambos (Constitución §4.4); el login no distingue entre ellos hasta el momento de la redirección post-autenticación.

---

## Tabla de Contenidos

1. [CU-012: Usuario Inicia Sesión con Credenciales Válidas](#cu-012-usuario-inicia-sesión-con-credenciales-válidas)
2. [CU-013: Sistema Deniega Acceso por Credenciales Inválidas o Cuenta Inactiva](#cu-013-sistema-deniega-acceso-por-credenciales-inválidas-o-cuenta-inactiva)
3. [CU-014: Usuario Cierra Sesión](#cu-014-usuario-cierra-sesión)
4. [CU-015: Expiración o Invalidación de Sesión en Curso](#cu-015-expiración-o-invalidación-de-sesión-en-curso)

---

## CU-012: Usuario Inicia Sesión con Credenciales Válidas

### 1. Identificación

| Campo | Valor |
|-------|-------|
| **ID** | CU-012 |
| **Nombre** | Usuario Inicia Sesión con Credenciales Válidas |
| **Controlador** | `AccountController.Login` (GET/POST) |
| **Prioridad** | P1 |

### 2. Actores

| Actor | Rol |
|-------|-----|
| Usuario (User/Approver) | Persona no autenticada que posee una cuenta de Identity pre-aprovisionada con estado `Active` |
| Sistema | Verifica credenciales vía ASP.NET Core Identity, valida estado activo, emite cookie de sesión y registra auditoría |

### 3. Precondiciones

| ID | Condición |
|----|-----------|
| PRE-001 | La cuenta de Identity existe y fue pre-aprovisionada (no hay auto-registro en el MVP) |
| PRE-002 | La cuenta posee el rol `User`, el rol `Approver`, o ambos |
| PRE-003 | El estado de la cuenta es `Active` |
| PRE-004 | El visitante no tiene una sesión autenticada vigente |

### 4. Flujo Principal

| ID | Paso |
|----|------|
| FP-01 | El visitante navega a `/Account/Login` |
| FP-02 | El sistema presenta la pantalla **Login** con layout independiente (sin sidebar), mostrando el componente `login-card`: logo, título "Sign In", formulario con `FormField` para Email y `FormField` para Password, y botón "Sign In" (`btn btn--primary btn--full-width`) |
| FP-03 | El script `login-validation.js` ejecuta auto-focus en el campo Email al cargar la página |
| FP-04 | El visitante ingresa su Email y Password |
| FP-05 | El visitante presiona el botón "Sign In" |
| FP-06 | El script `validateLoginForm()` previene el envío si Email o Password están vacíos, mostrando errores inline (mismo patrón `form-field--error` / `form-field__error` con `role="alert"`) |
| FP-07 | El script `disableOnSubmit()` deshabilita el botón para prevenir doble envío |
| FP-08 | El navegador envía un POST a `/Account/Login` incluyendo el token antiforgery (`@Html.AntiForgeryToken()`) |
| FP-09 | El servidor valida el token antiforgery |
| FP-10 | El controlador `AccountController.Login` recibe un `LoginViewModel` dedicado (solo Email, Password, token) y lo pasa al caso de uso de la capa Application — no se enlaza directamente contra la entidad de Identity ni contra el dominio |
| FP-11 | El sistema verifica la pertenencia al rate limiter del endpoint de login (Constitución §7.2); si no se excede el umbral, continúa |
| FP-12 | ASP.NET Core Identity verifica el Email y el Password contra el hash almacenado |
| FP-13 | El sistema verifica que la cuenta no esté bloqueada (`LockoutEnd`) por intentos fallidos previos |
| FP-14 | El sistema verifica que el estado de la cuenta sea `Active` (Constitución §4.3) |
| FP-15 | El sistema emite la cookie de sesión (`HttpOnly`, `Secure` en producción, `SameSite=Lax` o más estricto, expiración deslizante con vida útil absoluta de 8–12 horas e inactividad máxima de 20 minutos) |
| FP-16 | El sistema registra un registro de auditoría: actor, timestamp, acción = "LoginSuccess", resultado, correlation ID — sin registrar el password |
| FP-17 | El sistema resuelve el destino de redirección: si el visitante llegó a Login mediante un redirect desde una página protegida específica y esta sigue siendo válida y autorizada, redirige allí; en caso contrario, redirige al **User Dashboard** por defecto, o al **Approver Dashboard** si la identidad posee únicamente el rol `Approver` |
| FP-18 | La pantalla destino se renderiza con el contexto de sesión activo (sidebar con enlaces condicionales por rol) |

### 5. Flujos Alternos

#### FA-001: Validación de Campos Vacíos en Cliente

| ID | Paso |
|----|------|
| FA-001-01 | El visitante intenta enviar el formulario con Email o Password vacío |
| FA-001-02 | `validateLoginForm()` detecta el error antes del envío |
| FA-001-03 | Se muestra error inline debajo del `FormField` correspondiente |
| FA-001-04 | El envío no ocurre; si el visitante elude la validación de cliente, el servidor rechaza igualmente por campos requeridos |

#### FA-002: Usuario Ya Autenticado Navega a Login

| ID | Paso |
|----|------|
| FA-002-01 | Un visitante con sesión válida navega a `/Account/Login` (por URL directa o clic en logo) |
| FA-002-02 | El sistema detecta la sesión activa antes de renderizar el formulario |
| FA-002-03 | El sistema redirige automáticamente al dashboard correspondiente a su rol, sin volver a solicitar credenciales |

#### FA-003: Redirección Post-Login a Página Originalmente Solicitada (Deep-Link)

| ID | Paso |
|----|------|
| FA-003-01 | El visitante fue redirigido a Login al intentar acceder directamente a una página protegida (ej. `/Employee/Requests/Create`) |
| FA-003-02 | Tras autenticación exitosa, el sistema evalúa si esa página sigue siendo válida y autorizada para la identidad |
| FA-003-03 | Si es válida, redirige a esa página en lugar del dashboard por defecto; si ya no es válida o autorizada, redirige al dashboard por defecto |

### 6. Flujos de Excepción

#### FE-001: Cuenta con Ambos Roles

| ID | Paso |
|----|------|
| FE-001-01 | La identidad autenticada posee tanto el rol `User` como el rol `Approver` |
| FE-001-02 | El sistema aplica la regla de destino por defecto: redirige al **User Dashboard** |
| FE-001-03 | El `Sidebar` se construye mostrando ambas secciones de navegación y el Role Switcher en el pie |

#### FE-002: Rate Limit Excedido Antes de Verificar Credenciales

| ID | Paso |
|----|------|
| FE-002-01 | El número de intentos hacia el endpoint de login excede el umbral configurado (Constitución §7.2) |
| FE-002-02 | El sistema rechaza la solicitud (p. ej. HTTP 429) antes de ejecutar la verificación de credenciales |
| FE-002-03 | No se genera un intento de autenticación evaluable contra el contador de lockout de Identity para ese request |

### 7. Postcondiciones

| ID | Estado del Sistema |
|----|-------------------|
| POST-001 | Existe una cookie de sesión válida, `HttpOnly` y segura, asociada a la identidad autenticada |
| POST-002 | Un registro de auditoría documenta el login exitoso: actor, timestamp, acción, resultado, correlation ID |
| POST-003 | El visitante se encuentra en la pantalla destino correcta según su rol o el deep-link original |
| POST-004 | El contador de intentos fallidos (`AccessFailedCount`) de la cuenta se reinicia tras el login exitoso |

### 8. Reglas de Negocio Aplicadas

| ID | Descripción |
|----|-------------|
| RN-001 | El login usa exclusivamente ASP.NET Core Identity con autenticación por cookie; no existe mecanismo de sesión o token propio |
| RN-002 | Una identidad autenticada puede sostener el rol `User`, `Approver`, o ambos |
| RN-003 | El destino de redirección por defecto es el User Dashboard; el Approver Dashboard aplica solo cuando la identidad posee únicamente el rol `Approver` |
| RN-004 | Solo cuentas con estado `Active` pueden obtener sesión, incluso con credenciales correctas |
| RN-005 | Se requiere token antiforgery en el POST de login |
| RN-006 | El ViewModel de entrada es dedicado (Email, Password, token) y no se enlaza contra entidades de dominio o de Identity |
| RN-007 | La cookie de sesión usa expiración deslizante con vida absoluta de 8–12 horas e inactividad máxima de 20 minutos |
| RN-008 | El rate limiting del endpoint de login es independiente del lockout de cuenta de Identity |

### 9. Criterios de Aceptación

| ID | Criterio |
|----|----------|
| CA-001 | Dado un visitante no autenticado, cuando envía email y password correctos para una cuenta `Active`, entonces el sistema autentica, emite cookie de sesión y redirige al dashboard correspondiente al rol |
| CA-002 | Dado un login exitoso, se registra un evento de auditoría con actor, timestamp y resultado, sin incluir el password |
| CA-003 | Dado un visitante redirigido a Login desde una página protegida específica, cuando autentica exitosamente, es redirigido a esa página si sigue siendo válida y autorizada |
| CA-004 | Dado un usuario ya autenticado, cuando navega a `/Account/Login`, es redirigido automáticamente sin re-solicitar credenciales |
| CA-005 | Dada una identidad con ambos roles, tras login exitoso aterriza en el User Dashboard con ambas secciones de navegación disponibles |

---

## CU-013: Sistema Deniega Acceso por Credenciales Inválidas o Cuenta Inactiva

### 1. Identificación

| Campo | Valor |
|-------|-------|
| **ID** | CU-013 |
| **Nombre** | Sistema Deniega Acceso por Credenciales Inválidas o Cuenta Inactiva |
| **Controlador** | `AccountController.Login` (POST) |
| **Prioridad** | P1 |

### 2. Actores

| Actor | Rol |
|-------|-----|
| Visitante no autenticado | Persona que intenta iniciar sesión con credenciales incorrectas, email desconocido, o una cuenta `Inactive` |
| Sistema | Rechaza el intento con mensaje genérico, aplica lockout y rate limiting, y registra auditoría |

### 3. Precondiciones

| ID | Condición |
|----|-----------|
| PRE-001 | El visitante no tiene sesión autenticada vigente |
| PRE-002 | Se cumple al menos una de: el email no corresponde a ninguna cuenta, el password no coincide con el hash almacenado, o la cuenta correspondiente tiene estado `Inactive` |

### 4. Flujo Principal

| ID | Paso |
|----|------|
| FP-01 | El visitante envía el formulario de Login con datos que no resultan en autenticación válida |
| FP-02 | El servidor valida el token antiforgery |
| FP-03 | El sistema verifica el rate limiter del endpoint; si no se excede, continúa |
| FP-04 | ASP.NET Core Identity intenta resolver el email y verificar el password |
| FP-05 | El sistema determina la categoría de fallo internamente (email desconocido / password incorrecto / cuenta inactiva / cuenta bloqueada), pero **no** expone esa categoría al visitante |
| FP-06 | El sistema incrementa el contador de intentos fallidos (`AccessFailedCount`) cuando el email corresponde a una cuenta existente |
| FP-07 | El sistema registra un registro de auditoría: actor (si es resoluble), timestamp, acción = "LoginFailure", categoría de razón (interna), correlation ID — sin registrar el password enviado |
| FP-08 | El sistema re-renderiza la pantalla Login con un único mensaje de error genérico (p. ej. "Invalid email or password.") mediante `AlertMessage` de tipo `error` (persistente, sin auto-dismiss) |
| FP-09 | El campo Password se limpia; el campo Email puede conservar el valor ingresado (no es información sensible por sí sola) |

### 5. Flujos Alternos

#### FA-001: Intento con Email Desconocido

| ID | Paso |
|----|------|
| FA-001-01 | El email ingresado no corresponde a ninguna cuenta de Identity |
| FA-001-02 | El sistema no puede incrementar un `AccessFailedCount` (no existe cuenta) |
| FA-001-03 | Solo el rate limiter del endpoint (FP-03) puede frenar intentos repetidos con distintos emails desconocidos |
| FA-001-04 | Se muestra el mismo mensaje genérico que en cualquier otro fallo |

#### FA-002: Intento con Password Incorrecto para Cuenta Existente

| ID | Paso |
|----|------|
| FA-002-01 | El email existe pero el password no coincide |
| FA-002-02 | Se incrementa `AccessFailedCount` para esa cuenta |
| FA-002-03 | Se muestra el mismo mensaje genérico |

#### FA-003: Intento con Cuenta `Inactive`

| ID | Paso |
|----|------|
| FA-003-01 | El email y password son correctos, pero la cuenta asociada tiene estado `Inactive` |
| FA-003-02 | El sistema deniega la emisión de sesión pese a la verificación de credenciales exitosa (Constitución §4.3) |
| FA-003-03 | Se muestra el mismo mensaje genérico que para credenciales incorrectas |
| FA-003-04 | Se registra auditoría con categoría de razón "InactiveAccount" (interna, no expuesta) |

### 6. Flujos de Excepción

#### FE-001: Bloqueo de Cuenta por Intentos Fallidos (Lockout)

| ID | Paso |
|----|------|
| FE-001-01 | `AccessFailedCount` alcanza `MAX_FAILED_ACCESS_ATTEMPTS` |
| FE-001-02 | ASP.NET Core Identity establece `LockoutEnd` según `LOCKOUT_DURATION_MINUTES` |
| FE-001-03 | Intentos posteriores contra esa cuenta — incluso con el password correcto — se rechazan mientras `LockoutEnd` no haya expirado |
| FE-001-04 | Se muestra el mismo mensaje genérico que cualquier otro fallo (no se revela que la cuenta está bloqueada) |
| FE-001-05 | Se registra un evento de auditoría "LockoutTriggered" al momento en que se activa el bloqueo |

#### FE-002: Rate Limit Excedido

| ID | Paso |
|----|------|
| FE-002-01 | El volumen de solicitudes al endpoint de login excede el umbral configurado |
| FE-002-02 | El sistema rechaza la solicitud (p. ej. HTTP 429) antes de evaluar credenciales |
| FE-002-03 | No se incrementa `AccessFailedCount` para ninguna cuenta específica en este camino |

#### FE-003: Token Antiforgery Ausente o Inválido

| ID | Paso |
|----|------|
| FE-003-01 | El POST de login llega sin token antiforgery válido |
| FE-003-02 | El servidor rechaza la solicitud con HTTP 400/403 antes de procesar credenciales |
| FE-003-03 | No se ejecuta verificación de credenciales ni se incrementa el contador de intentos fallidos |

### 7. Postcondiciones

| ID | Estado del Sistema |
|----|-------------------|
| POST-001 | No se emite cookie de sesión |
| POST-002 | Un registro de auditoría documenta el intento fallido con su categoría de razón interna, sin exponer el password |
| POST-003 | Si aplica, `AccessFailedCount` de la cuenta correspondiente queda incrementado |
| POST-004 | Si se alcanzó el umbral, la cuenta queda en estado de bloqueo temporal (`LockoutEnd` establecido) |
| POST-005 | El visitante permanece en la pantalla Login con el mensaje de error genérico visible |

### 8. Reglas de Negocio Aplicadas

| ID | Descripción |
|----|-------------|
| RN-001 | El mensaje de error es idéntico para email desconocido, password incorrecto y cuenta inactiva o bloqueada |
| RN-002 | Ningún estado interno (existencia de cuenta, categoría de fallo, bloqueo) se revela al visitante más allá del mensaje genérico |
| RN-003 | El lockout de Identity y el rate limiting del endpoint son controles independientes y complementarios |
| RN-004 | Una cuenta `Inactive` nunca obtiene sesión, incluso con credenciales correctas |
| RN-005 | `MAX_FAILED_ACCESS_ATTEMPTS` y `LOCKOUT_DURATION_MINUTES` son parámetros configurables |
| RN-006 | Todo intento fallido se audita, sin registrar el password enviado |

### 9. Criterios de Aceptación

| ID | Criterio |
|----|----------|
| CA-001 | Dado un email desconocido, un password incorrecto, o una cuenta `Inactive`, el sistema responde con el mismo mensaje genérico en los tres casos |
| CA-002 | Dada una cuenta que alcanza `MAX_FAILED_ACCESS_ATTEMPTS`, los intentos posteriores se rechazan durante `LOCKOUT_DURATION_MINUTES`, incluso con password correcto |
| CA-003 | Dado un volumen de solicitudes que excede el rate limit configurado, las solicitudes adicionales se rechazan antes de evaluar credenciales |
| CA-004 | Todo intento fallido genera un registro de auditoría con categoría de razón interna y sin el password |
| CA-005 | Un POST de login sin token antiforgery válido es rechazado sin evaluar credenciales |

---

## CU-014: Usuario Cierra Sesión

### 1. Identificación

| Campo | Valor |
|-------|-------|
| **ID** | CU-014 |
| **Nombre** | Usuario Cierra Sesión (Logout) |
| **Controlador** | `AccountController.Logout` (POST) |
| **Prioridad** | P1 |

### 2. Actores

| Actor | Rol |
|-------|-----|
| Usuario autenticado (User/Approver) | Persona con sesión activa que solicita cerrarla |
| Sistema | Invalida la cookie de sesión / security stamp y registra auditoría |

### 3. Precondiciones

| ID | Condición |
|----|-----------|
| PRE-001 | El usuario tiene una sesión autenticada vigente |

### 4. Flujo Principal

| ID | Paso |
|----|------|
| FP-01 | El usuario hace clic en el botón "Sign Out" ubicado en el `sidebar__footer`, visible en toda pantalla autenticada |
| FP-02 | El navegador envía un POST a `/Account/Logout` incluyendo el token antiforgery |
| FP-03 | El servidor valida el token antiforgery |
| FP-04 | El sistema invalida la cookie de sesión actual (y actualiza el security stamp si corresponde) |
| FP-05 | El sistema registra un registro de auditoría: actor, timestamp, acción = "Logout", correlation ID |
| FP-06 | El controlador ejecuta Post/Redirect/Get: redirige a `/Account/Login` |
| FP-07 | La pantalla Login se presenta sin contexto de sesión (sin sidebar) |

### 5. Flujos Alternos

Ninguno identificado — el logout es una operación única sin variantes de negocio.

### 6. Flujos de Excepción

#### FE-001: Token Antiforgery Ausente o Inválido

| ID | Paso |
|----|------|
| FE-001-01 | El POST de logout llega sin token antiforgery válido |
| FE-001-02 | El servidor rechaza la solicitud con HTTP 400/403 |
| FE-001-03 | La sesión permanece activa (el logout no se ejecuta) |

#### FE-002: Intento de Acceso Tras Logout con Cookie Anterior

| ID | Paso |
|----|------|
| FE-002-01 | El mismo navegador reutiliza la cookie de sesión previa (ya invalidada) para acceder a una página protegida |
| FE-002-02 | El middleware de autenticación detecta que la cookie ya no es válida |
| FE-002-03 | Se deniega el acceso y se redirige a `/Account/Login` |

### 7. Postcondiciones

| ID | Estado del Sistema |
|----|-------------------|
| POST-001 | La cookie de sesión anterior deja de ser válida |
| POST-002 | Un registro de auditoría documenta el logout: actor, timestamp, acción, correlation ID |
| POST-003 | El usuario se encuentra en la pantalla Login sin contexto de sesión |
| POST-004 | Cualquier solicitud posterior a una página protegida con la cookie anterior es denegada |

### 8. Reglas de Negocio Aplicadas

| ID | Descripción |
|----|-------------|
| RN-001 | El logout es siempre un POST, nunca un enlace GET |
| RN-002 | Se requiere token antiforgery para ejecutar el logout |
| RN-003 | Todo logout se audita |
| RN-004 | Tras logout, la cookie previa no debe ser honrada en ninguna solicitud posterior |

### 9. Criterios de Aceptación

| ID | Criterio |
|----|----------|
| CA-001 | Dado un usuario autenticado, cuando ejecuta Sign Out, la sesión se invalida y es redirigido a Login |
| CA-002 | Un POST de logout sin token antiforgery válido es rechazado y la sesión permanece activa |
| CA-003 | Tras un logout exitoso, una solicitud posterior con la cookie anterior es denegada y redirigida a Login |
| CA-004 | Todo logout exitoso genera un registro de auditoría con actor, timestamp y acción |

---

## CU-015: Expiración o Invalidación de Sesión en Curso

### 1. Identificación

| Campo | Valor |
|-------|-------|
| **ID** | CU-015 |
| **Nombre** | Expiración o Invalidación de Sesión en Curso |
| **Controlador** | Middleware de autenticación (transversal a todos los controladores protegidos) |
| **Prioridad** | P2 |

### 2. Actores

| Actor | Rol |
|-------|-----|
| Usuario con sesión previamente autenticada | Persona cuya sesión ha quedado inactiva, ha superado su vida útil absoluta, o cuya cuenta cambió de estado/rol durante la sesión |
| Sistema | Revalida identidad, rol y estado activo en cada solicitud protegida o de cambio de estado |

### 3. Precondiciones

| ID | Condición |
|----|-----------|
| PRE-001 | Existe una cookie de sesión emitida previamente para el usuario |
| PRE-002 | Se cumple al menos una de: (a) el tiempo de inactividad supera el timeout configurado, (b) la vida útil absoluta de la cookie fue superada, o (c) el estado `Active`/`Inactive` o el rol de la cuenta cambió después de emitida la cookie |

### 4. Flujo Principal

| ID | Paso |
|----|------|
| FP-01 | El usuario realiza una solicitud a una página o acción protegida usando una cookie de sesión existente |
| FP-02 | El middleware de autenticación evalúa la validez temporal de la cookie (inactividad e vida útil absoluta) |
| FP-03 | Para acciones que cambian estado, el sistema revalida adicionalmente identidad, rol actual y estado `Active` directamente contra la fuente de verdad server-side (Constitución §5 invariante 12), no solo contra los claims cacheados en la cookie |
| FP-04 | Si la sesión sigue siendo válida en todos los aspectos, la solicitud continúa normalmente |
| FP-05 | Si cualquiera de las verificaciones falla, el sistema deniega la solicitud actual |
| FP-06 | El sistema redirige a `/Account/Login`, preservando la URL originalmente solicitada para su uso posterior (ver CU-012, FA-003) |

### 5. Flujos Alternos

#### FA-001: Expiración por Inactividad

| ID | Paso |
|----|------|
| FA-001-01 | El tiempo transcurrido desde la última solicitud supera el timeout de inactividad configurado (20 minutos) |
| FA-001-02 | El sistema trata la sesión como expirada y deniega la solicitud |
| FA-001-03 | Se redirige a Login preservando la página solicitada |

#### FA-002: Expiración por Vida Útil Absoluta

| ID | Paso |
|----|------|
| FA-002-01 | La cookie supera su vida útil absoluta configurada (8–12 horas), independientemente de la actividad reciente |
| FA-002-02 | El sistema deniega la solicitud y redirige a Login |

#### FA-003: Re-autenticación Exitosa Tras Expiración

| ID | Paso |
|----|------|
| FA-003-01 | El usuario, redirigido a Login por expiración, vuelve a autenticarse correctamente (CU-012) |
| FA-003-02 | El sistema lo redirige a la página originalmente solicitada, si sigue siendo válida y autorizada; de lo contrario, al dashboard por defecto |

### 6. Flujos de Excepción

#### FE-001: Cambio de Estado a `Inactive` Durante Sesión Activa

| ID | Paso |
|----|------|
| FE-001-01 | La cuenta del usuario es desactivada (`Active` → `Inactive`) mientras la cookie de sesión sigue siendo técnicamente válida |
| FE-001-02 | En la siguiente solicitud protegida o de cambio de estado, la revalidación server-side (FP-03) detecta el estado `Inactive` |
| FE-001-03 | El sistema deniega la solicitud sin aplicar ningún cambio parcial (fail-closed) |
| FE-001-04 | Se registra un intento de autorización fallido en auditoría (Constitución §7.4) |
| FE-001-05 | Se redirige a Login |

#### FE-002: Cambio de Rol Durante Sesión Activa

| ID | Paso |
|----|------|
| FE-002-01 | El rol asociado a la cuenta cambia (p. ej. se remueve el rol `Approver`) mientras la sesión sigue activa |
| FE-002-02 | La actualización del security stamp de Identity invalida la cookie previamente emitida en su próximo uso (Constitución §7.1, D-005) |
| FE-002-03 | La siguiente solicitud que dependa del rol removido es denegada; el usuario debe re-autenticar para obtener claims actualizados |

#### FE-003: Solicitud de Cambio de Estado Durante Ventana de Revalidación

| ID | Paso |
|----|------|
| FE-003-01 | Una acción de cambio de estado (p. ej. aprobar una solicitud, en el dominio de `spec_001`) llega justo cuando la sesión ya expiró o el estado cambió |
| FE-003-02 | El sistema revalida identidad, rol, estado activo y estado del recurso inmediatamente antes de ejecutar la operación (Constitución §5 invariante 12) |
| FE-003-03 | Si la revalidación falla, la operación se rechaza sin aplicar cambios parciales, y no se compromete ninguna transacción |

### 7. Postcondiciones

| ID | Estado del Sistema |
|----|-------------------|
| POST-001 | Ninguna solicitud protegida o de cambio de estado se procesa con una identidad, rol, o estado activo obsoletos |
| POST-002 | El usuario queda en la pantalla Login cuando su sesión ya no es válida |
| POST-003 | La URL originalmente solicitada se preserva para redirección posterior a una re-autenticación exitosa, cuando aplica |
| POST-004 | Los intentos de autorización fallidos por estado/rol obsoleto quedan auditados |

### 8. Reglas de Negocio Aplicadas

| ID | Descripción |
|----|-------------|
| RN-001 | La expiración se evalúa por inactividad y por vida útil absoluta, lo que ocurra primero |
| RN-002 | Identidad, rol, estado activo y estado del recurso se revalidan server-side inmediatamente antes de cualquier operación de cambio de estado, nunca solo contra claims cacheados |
| RN-003 | Un cambio de rol o de estado `Active`/`Inactive` invalida la cookie previamente emitida mediante actualización del security stamp |
| RN-004 | La denegación por sesión inválida es fail-closed: ninguna operación parcial se aplica |
| RN-005 | La URL originalmente solicitada se preserva a través del ciclo Login → re-autenticación → redirección |

### 9. Criterios de Aceptación

| ID | Criterio |
|----|----------|
| CA-001 | Dada una sesión inactiva por más del timeout configurado, la siguiente solicitud es denegada y redirigida a Login |
| CA-002 | Dada una cookie que supera su vida útil absoluta, la siguiente solicitud es denegada independientemente de la actividad reciente |
| CA-003 | Dada una cuenta desactivada durante una sesión activa, la siguiente solicitud protegida es denegada mediante revalidación server-side, no mediante los claims de la cookie |
| CA-004 | Dado un cambio de rol durante una sesión activa, la siguiente solicitud que dependa del rol removido es denegada |
| CA-005 | Tras una re-autenticación exitosa posterior a una expiración, el usuario es redirigido a la página originalmente solicitada si sigue siendo válida y autorizada |
