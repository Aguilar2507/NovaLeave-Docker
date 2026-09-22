# Propuesta de Incorporación de Inteligencia Artificial en NovaComp y NovaLeave

**Fecha:** 9 de septiembre de 2026  
**Proyecto:** NovaLeave  
**Empresa:** NovaComp  
**Estado:** Propuesta formal conceptual y técnica  
**Alcance:** Documento de propuesta; no incluye implementación de código en esta etapa

---

## 1. Resumen Ejecutivo

NovaLeave es una aplicación empresarial para gestionar solicitudes de vacaciones y permisos. El proyecto utiliza .NET 10, C#, ASP.NET Core Identity, Entity Framework Core, SQL Server, Serilog, FluentValidation, Razor Pages/MVC y una arquitectura limpia dividida en Domain, Application, Infrastructure y Presentation.Web.

El sistema contempla actualmente:

- Usuarios y aprobadores.
- Creación, edición, aprobación, rechazo, cancelación, expiración y anulación de solicitudes.
- Control de saldo disponible.
- Reserva de días para solicitudes pendientes.
- Cálculo de días laborables y feriados.
- Validación de solapamiento de fechas.
- Auditoría de acciones y transiciones.
- Autenticación segura con ASP.NET Core Identity.
- Autorización por roles y revalidación server-side.
- Protección antiforgery, rate limiting, lockout y controles de acceso.
- Interfaz web responsive mediante Razor y componentes reutilizables.

La incorporación de inteligencia artificial debe realizarse de forma gradual, controlada y orientada a resultados. La IA no debe reemplazar las reglas deterministas del dominio ni tomar decisiones finales sobre aprobaciones, saldos, permisos o seguridad.

La propuesta recomienda cinco líneas de evolución:

1. Asistente inteligente para crear y revisar solicitudes.
2. Copiloto de apoyo para aprobadores.
3. Pronóstico de demanda y planificación de ausencias.
4. Detección de anomalías y riesgos operativos.
5. Automatización inteligente de comunicaciones y soporte.

También se recomiendan automatizaciones tradicionales que no necesitan IA, como recordatorios, notificaciones, expiración de solicitudes e informes periódicos.

La primera fase debería concentrarse en automatizaciones deterministas y en el asistente para usuarios. Estas funciones ofrecen valor rápido, tienen riesgo controlable y no requieren delegar decisiones críticas a un modelo de IA.

---

## 2. Contexto Actual de NovaLeave

### 2.1 Propósito del sistema

NovaLeave permite administrar el ciclo de vida de las solicitudes de vacaciones. Una solicitud puede iniciar en estado `Pending` y posteriormente ser aprobada, rechazada, cancelada, anulada o expirada según las reglas del negocio.

La entidad `VacationRequest` concentra información como:

- Propietario de la solicitud.
- Fecha inicial y final.
- Motivo.
- Cantidad de días solicitados.
- Estado actual.
- Fecha de expiración.
- Usuario que resolvió la solicitud.
- Motivo de rechazo o anulación.
- Versión de concurrencia.

La entidad `Employee` mantiene el balance y protege operaciones como reservar, deducir y restaurar días. Los casos de uso de Application coordinan la validación, el cálculo de días laborables, la revisión de solapamientos, la persistencia y la auditoría.

### 2.2 Flujo principal identificado

1. El usuario accede al formulario de solicitud.
2. El sistema valida los datos de entrada.
3. Se calculan los días laborables, excluyendo fines de semana y feriados.
4. Se verifica el saldo disponible.
5. Se comprueba que no exista una solicitud pendiente o aprobada que se solape.
6. Se reservan los días y se crea la solicitud en estado `Pending`.
7. Se registra la operación en auditoría.
8. El aprobador consulta sus solicitudes pendientes.
9. El aprobador aprueba o rechaza la solicitud.
10. El sistema revalida permisos, estado, saldo y concurrencia antes de persistir.

### 2.3 Base técnica relevante

La propuesta utiliza como puntos de integración los siguientes elementos existentes:

- `VacationRequest` y su máquina de estados.
- `Employee` y su lógica de balance.
- `DateRange` y las validaciones de fechas.
- `CreateRequestHandler`.
- `ApproveRequestHandler`.
- `RejectRequestHandler`.
- `ListPendingForApproverHandler`.
- `IWorkingDayCalculator`.
- `IVacationRequestRepository`.
- `IStateTransitionAuditService`.
- `AuditRecord`.
- ASP.NET Core Identity.
- FluentValidation.
- Razor Pages/MVC, ViewModels y componentes reutilizables.

La IA deberá integrarse a través de contratos de Application e implementaciones de Infrastructure. El Domain no debe depender de SDKs, proveedores ni modelos de inteligencia artificial.

---

## 3. Objetivos de la Propuesta

### Objetivo general

Mejorar la experiencia de uso, la eficiencia operativa y la capacidad de planificación de NovaComp mediante IA, automatización y analítica avanzada, manteniendo la seguridad, trazabilidad y autoridad del sistema.

### Objetivos específicos

- Reducir errores al crear solicitudes de vacaciones.
- Disminuir el tiempo que los aprobadores dedican a revisar solicitudes.
- Mejorar la visibilidad sobre periodos de alta demanda de ausencias.
- Detectar comportamientos o eventos anómalos.
- Reducir consultas repetitivas a Recursos Humanos y soporte.
- Mantener las decisiones críticas dentro de reglas verificables.
- Utilizar la auditoría existente como base para observabilidad y mejora continua.
- Preservar la arquitectura limpia y las reglas del dominio de NovaLeave.

---

## 4. Principios de Diseño y Gobernanza

### 4.1 La IA recomienda; el dominio decide

Las reglas de saldo, fechas, solapamientos, estados, permisos y autorizaciones deben seguir siendo deterministas y estar protegidas por la capa Domain y los casos de uso de Application.

Un modelo de IA no debe aprobar directamente una solicitud ni modificar el saldo de un empleado.

### 4.2 Intervención humana

Toda recomendación que tenga impacto laboral debe poder ser revisada y aceptada o rechazada por una persona autorizada.

### 4.3 Explicabilidad

Cada recomendación debe mostrar los factores utilizados, por ejemplo:

- Fecha próxima de inicio.
- Cercanía de la fecha de expiración.
- Antigüedad de la solicitud.
- Posible coincidencia con otras ausencias.
- Impacto estimado sobre la disponibilidad operativa.

### 4.4 Privacidad y minimización de datos

Los modelos deben recibir únicamente los datos necesarios. No se deben enviar contraseñas, tokens, cookies, secretos ni información sensible innecesaria a proveedores externos.

### 4.5 Auditoría

Toda recomendación, automatización o interacción con IA debe registrar, cuando corresponda:

- Fecha y hora.
- Usuario o proceso que la solicitó.
- Tipo de modelo o versión.
- Datos resumidos utilizados.
- Resultado.
- Nivel de confianza.
- Acción humana posterior.
- Correlation ID.

La entidad `AuditRecord` y el mecanismo actual de auditoría pueden evolucionar para cubrir estos eventos.

### 4.6 Degradación segura

Si el proveedor de IA no está disponible, NovaLeave debe continuar funcionando con las reglas tradicionales. La indisponibilidad de la IA nunca debe impedir crear, consultar, aprobar o rechazar una solicitud válida.

### 4.7 No automatizar discriminación o decisiones laborales sensibles

Los modelos no deben inferir productividad, compromiso, rendimiento, salud, embarazo, discapacidad, situación familiar ni ninguna característica sensible para priorizar o resolver solicitudes.

---

## 5. Propuesta 1: Asistente Inteligente para Crear Solicitudes

### 5.1 Resumen

Incorporar un asistente conversacional o guiado que ayude al usuario a preparar una solicitud de vacaciones antes de enviarla.

El usuario podría escribir, por ejemplo:

> Quiero tomar vacaciones durante la primera semana de octubre por motivos personales.

El asistente extraería una intención preliminar y propondría:

- Fecha inicial.
- Fecha final.
- Cantidad estimada de días laborables.
- Saldo disponible.
- Posibles conflictos con solicitudes existentes.
- Información faltante.
- Recomendaciones para corregir la solicitud.

El usuario siempre debería revisar y confirmar los datos antes de enviarlos.

### 5.2 Problema que resuelve

La creación de solicitudes puede generar errores en:

- Fechas de inicio y finalización.
- Días laborables calculados.
- Fechas que se solapan con otra solicitud.
- Saldo insuficiente.
- Motivos incompletos.
- Confusión sobre estados y restricciones del proceso.

Estos errores provocan reintentos, mensajes de validación y mayor carga para los aprobadores o el área de Recursos Humanos.

### 5.3 Justificación y base técnica

La IA es útil para interpretar lenguaje natural y convertirlo en una propuesta estructurada. No debe reemplazar los cálculos oficiales del sistema.

La validación final debe continuar ejecutándose mediante `CreateRequestHandler`, `IWorkingDayCalculator`, `IVacationRequestRepository` y las reglas del dominio.

Componentes existentes aprovechables:

- `CreateRequestCommand`.
- `CreateRequestHandler`.
- `IWorkingDayCalculator`.
- `IVacationRequestRepository`.
- Validadores de FluentValidation.
- Entidades `Employee` y `VacationRequest`.
- Componentes de interfaz `FormField`, `BalanceCard` y `AlertMessage`.

### 5.4 Por qué usar IA

La IA aporta valor en:

- Comprensión de lenguaje natural.
- Solicitud de aclaraciones.
- Conversión de texto libre a datos estructurados.
- Explicación de errores en lenguaje sencillo.
- Adaptación de la ayuda a la intención del usuario.

Una solución basada únicamente en reglas también sería útil para validaciones, pero no interpretaría de forma flexible expresiones como “la primera semana de octubre” o “desde el lunes después del festivo”.

### 5.5 Cómo implementarlo

1. Crear un servicio de aplicación `IVacationRequestAssistant`.
2. Recibir el texto mediante un ViewModel dedicado.
3. Enviar al modelo únicamente el contexto necesario.
4. Obtener una respuesta estructurada mediante JSON Schema o salida tipada.
5. Validar fechas y campos con FluentValidation.
6. Recalcular los días laborables con el componente oficial.
7. Consultar saldo y solapamientos con los repositorios existentes.
8. Presentar al usuario un borrador editable.
9. Exigir confirmación explícita.
10. Ejecutar el caso de uso tradicional de creación.
11. Registrar la interacción y el resultado en auditoría.

### 5.6 Contrato sugerido

```text
Application
  Features/
    VacationRequest/
      Assistance/
        VacationRequestDraft.cs
        IVacationRequestAssistant.cs
        VacationRequestAssistantResult.cs
```

El asistente debe devolver un borrador, no crear directamente la solicitud:

```text
VacationRequestDraft
- StartDate
- EndDate
- Reason
- MissingInformation
- Warnings
- SuggestedCorrections
- Confidence
```

### 5.7 Indicadores de éxito

- Reducción de solicitudes rechazadas por errores de fecha.
- Reducción de solicitudes incompletas.
- Menor tiempo promedio para crear una solicitud.
- Porcentaje de borradores aceptados sin correcciones.
- Número de casos en los que el usuario abandona el flujo.

### 5.8 Riesgos y controles

| Riesgo | Control |
|---|---|
| Interpretación incorrecta de fechas | Confirmación visual y validación server-side |
| Alucinación de reglas | El modelo no calcula saldos ni decide estados |
| Exposición de información | Minimización de contexto y filtrado de datos |
| Dependencia del proveedor | Fallback al formulario tradicional |
| Ambigüedad lingüística | Preguntas de aclaración antes de generar el borrador |

---

## 6. Propuesta 2: Copiloto para Aprobadores

### 6.1 Resumen

Incorporar un copiloto que ayude al aprobador a revisar solicitudes pendientes y organizar su trabajo.

El sistema podría mostrar un resumen de cada solicitud:

- Días solicitados.
- Fecha de inicio.
- Antigüedad de la solicitud.
- Cercanía de la expiración.
- Posibles solapamientos operativos.
- Solicitudes pendientes relacionadas.
- Historial relevante permitido por las políticas de acceso.
- Explicación de por qué una solicitud requiere atención prioritaria.

El copiloto no aprobaría ni rechazaría automáticamente.

### 6.2 Problema que resuelve

El aprobador puede tener varias solicitudes pendientes y necesitar revisar manualmente:

- Fechas.
- Urgencia.
- Expiración.
- Carga de trabajo.
- Posibles conflictos.
- Motivos de rechazo o aprobación.

La lista actual ya ordena las solicitudes por urgencia y fecha de expiración, pero puede evolucionar para presentar un contexto más útil.

### 6.3 Justificación y base técnica

La IA puede resumir información dispersa y priorizar la atención. Esto reduce el tiempo de análisis sin modificar las reglas de autorización.

La decisión final debe continuar ejecutándose mediante comandos explícitos como aprobar o rechazar, con revalidación del rol y del estado de la solicitud.

Componentes existentes:

- `ListPendingForApproverHandler`.
- `ApproveRequestHandler`.
- `RejectRequestHandler`.
- `VacationRequest`.
- `AuditRecord`.
- Paginación de solicitudes pendientes.
- Estados `Pending`, `Approved`, `Rejected`, `Cancelled` y `Expired`.

### 6.4 Por qué usar IA

La IA aporta valor para:

- Resumir solicitudes.
- Explicar por qué un elemento aparece primero.
- Generar preguntas de revisión.
- Identificar información faltante.
- Presentar el contexto en lenguaje natural.

La ordenación básica puede resolverse con SQL y reglas. La IA se justifica cuando se necesita síntesis, explicación y asistencia interactiva.

### 6.5 Cómo implementarlo

1. Mantener el listado determinista existente.
2. Añadir un resumen de revisión bajo demanda.
3. Recuperar únicamente solicitudes que el aprobador esté autorizado a consultar.
4. Generar un resumen estructurado.
5. Mostrar factores y nivel de confianza.
6. Solicitar confirmación humana.
7. Ejecutar el comando de aprobación o rechazo existente.
8. Registrar la recomendación y la decisión real.
9. Medir la diferencia entre la sugerencia y la decisión humana.

### 6.6 Priorización propuesta

La prioridad puede combinar reglas deterministas con analítica:

```text
PriorityScore =
  UrgencyByExpiry
  + StartDateProximity
  + WaitingTime
  + OperationalConflictSignal
```

La IA puede explicar el resultado, pero el cálculo de seguridad y las condiciones de aprobación deben permanecer en Application y Domain.

### 6.7 Indicadores de éxito

- Tiempo promedio desde la apertura hasta la resolución.
- Porcentaje de solicitudes revisadas dentro del plazo.
- Reducción de solicitudes expiradas.
- Tiempo promedio de lectura por solicitud.
- Porcentaje de recomendaciones aceptadas o modificadas.

### 6.8 Riesgos y controles

La interfaz debe evitar que la recomendación parezca una decisión obligatoria. El botón de aprobación debe seguir requiriendo autorización, antiforgery, revalidación de estado y persistencia transaccional.

El modelo no debe utilizar criterios como género, edad, discapacidad, salud, nacionalidad, religión, situación familiar o cualquier otro dato protegido para priorizar solicitudes.

---

## 7. Propuesta 3: Pronóstico de Demanda y Planificación de Ausencias

### 7.1 Resumen

Crear un módulo analítico para que NovaComp pueda anticipar periodos con alta concentración de vacaciones.

El módulo podría mostrar:

- Meses con mayor demanda histórica.
- Tendencias por periodo.
- Días con mayor número de solicitudes.
- Distribución de saldos.
- Solicitudes aprobadas versus rechazadas.
- Concentración de ausencias.
- Proyecciones para los próximos periodos.

### 7.2 Problema que resuelve

Las decisiones de planificación pueden depender demasiado de observaciones manuales. Sin visibilidad histórica, NovaComp puede detectar tarde que un periodo tendrá una alta concentración de ausencias.

### 7.3 Justificación y base técnica

La predicción y la visualización de tendencias son escenarios adecuados para analítica y machine learning, siempre que se utilicen datos suficientes y de calidad.

Este módulo no debería tomar decisiones automáticas. Su función sería apoyar la planificación.

Fuentes de datos posibles:

- `VacationRequest`.
- `Employee`.
- `AuditRecord`.
- Fechas de solicitud y resolución.
- Estado de la solicitud.
- Días solicitados.
- Calendario de feriados.
- Datos agregados por periodo.

Se recomienda un modelo de datos analítico separado de las tablas transaccionales. Inicialmente puede utilizarse un proceso batch diario o semanal.

### 7.4 Por qué usar IA

La analítica descriptiva puede comenzar sin IA. El uso de machine learning se justifica cuando exista suficiente historial para identificar patrones y generar pronósticos útiles.

Se recomienda comenzar con métodos estadísticos simples y compararlos con modelos más avanzados. No se debe introducir un modelo complejo sin demostrar una mejora real.

### 7.5 Cómo implementarlo

1. Definir métricas y dimensiones de análisis.
2. Anonimizar o agregar los datos cuando sea posible.
3. Crear un proceso de extracción controlado.
4. Construir un dashboard descriptivo.
5. Medir la calidad y estabilidad de los datos.
6. Crear un modelo de pronóstico básico.
7. Comparar el pronóstico con periodos reales.
8. Mostrar intervalos de confianza.
9. Incorporar alertas para periodos de alta concentración.
10. Revisar las predicciones con Recursos Humanos antes de usarlas para decisiones.

### 7.6 Indicadores de éxito

- Precisión del pronóstico.
- Anticipación promedio de los periodos críticos.
- Reducción de conflictos operativos.
- Porcentaje de periodos con planificación previa.
- Uso recurrente del dashboard por parte de los responsables.

### 7.7 Riesgos y controles

- No usar predicciones para negar automáticamente vacaciones.
- Mostrar incertidumbre e intervalos, no únicamente un número exacto.
- Evitar que el modelo penalice grupos con menor historial de solicitudes.
- Validar que los datos históricos no reflejen decisiones injustas del pasado.
- Revisar el modelo periódicamente y documentar sus cambios.

---

## 8. Propuesta 4: Detección de Anomalías y Riesgos Operativos

### 8.1 Resumen

Utilizar reglas y modelos de detección de anomalías para identificar comportamientos atípicos en solicitudes, aprobaciones, accesos y cambios de estado.

Ejemplos:

- Volumen inusual de solicitudes en poco tiempo.
- Muchas anulaciones realizadas por una misma cuenta.
- Intentos de acceso fallidos fuera del comportamiento habitual.
- Aprobaciones realizadas en patrones atípicos.
- Cambios repetidos sobre una misma solicitud.
- Uso anormal de funciones administrativas.

### 8.2 Problema que resuelve

La auditoría registra eventos, pero revisar manualmente todos los eventos es costoso. Algunos riesgos podrían detectarse demasiado tarde.

### 8.3 Justificación y base técnica

La detección temprana de anomalías puede mejorar la seguridad y la operación. Es especialmente relevante porque NovaLeave prioriza:

- A01: Broken Access Control.
- A06: Insecure Design.
- A09: Security Logging and Alerting Failures.

La entidad `AuditRecord` ya contiene actor, rol, acción, resultado, entidad afectada, correlation ID, motivo, detalles y timestamp. Estos campos permiten construir reglas de detección y, posteriormente, modelos de comportamiento.

### 8.4 Por qué usar IA

Las reglas deterministas son suficientes para eventos conocidos, como superar un número fijo de intentos. La IA o el machine learning pueden ayudar a detectar combinaciones y patrones que no hayan sido definidos previamente.

La recomendación es utilizar primero reglas y umbrales. El machine learning debería incorporarse después de validar la calidad y cantidad de los datos.

### 8.5 Cómo implementarlo

1. Normalizar los tipos de eventos de auditoría.
2. Definir eventos de seguridad prioritarios.
3. Crear reglas de detección simples.
4. Añadir alertas con severidad.
5. Crear una cola de revisión para responsables autorizados.
6. Registrar el resultado de cada investigación.
7. Entrenar modelos únicamente con datos suficientemente representativos.
8. Evitar acciones automáticas destructivas.
9. Mantener revisión humana antes de bloquear cuentas o modificar permisos.

### 8.6 Indicadores de éxito

- Tiempo medio de detección.
- Tiempo medio de investigación.
- Número de falsos positivos.
- Eventos relevantes detectados.
- Porcentaje de alertas cerradas con evidencia.
- Reducción de incidentes no detectados.

### 8.7 Riesgos y controles

Una alerta no equivale a una prueba de abuso. Las alertas deben orientar una investigación y no desencadenar sanciones automáticas. La información debe estar disponible únicamente para los responsables autorizados.

---

## 9. Propuesta 5: Automatización Inteligente de Comunicaciones y Soporte

### 9.1 Resumen

Automatizar notificaciones y ofrecer un asistente de consulta para preguntas frecuentes sobre el proceso de vacaciones.

Ejemplos de consultas:

- ¿Cuántos días tengo disponibles?
- ¿Cuál es el estado de mi solicitud?
- ¿Qué significa `Pending`?
- ¿Por qué no puedo seleccionar estas fechas?
- ¿Cuándo vence mi solicitud?
- ¿Qué ocurre después de que el aprobador decide?

### 9.2 Problema que resuelve

Muchas consultas son repetitivas y no requieren intervención humana. Además, los usuarios pueden no entender claramente el significado de los estados o mensajes de validación.

### 9.3 Justificación y base técnica

La automatización tradicional es suficiente para notificaciones previsibles. La IA puede mejorar la interacción cuando el usuario formula preguntas de distintas maneras.

El asistente debe consultar casos de uso de solo lectura y nunca acceder directamente al `DbContext` desde la interfaz.

Casos de uso potenciales:

```text
GetMyBalance
GetMyRequests
GetRequestDetails
GetPendingApproverRequests
GetRequestStatusExplanation
```

El asistente debe aplicar el mismo control de autorización que las pantallas web.

### 9.4 Por qué usar IA

La IA aporta comprensión de lenguaje natural y respuestas contextualizadas. La información devuelta debe proceder de datos reales de NovaLeave, no de conocimiento inventado por el modelo.

Para evitar respuestas no verificables se recomienda utilizar un patrón de recuperación aumentada:

1. Interpretar la pregunta.
2. Identificar el caso de uso autorizado.
3. Consultar datos reales.
4. Generar una respuesta basada únicamente en esos datos.
5. Indicar cuando no existe información suficiente.

### 9.5 Cómo implementarlo

1. Crear un catálogo de preguntas frecuentes.
2. Implementar primero respuestas deterministas.
3. Añadir búsqueda semántica sobre documentación aprobada.
4. Integrar consultas de solo lectura.
5. Filtrar la información por identidad y rol.
6. Añadir respuesta de escalamiento a soporte.
7. Registrar consultas sin almacenar información sensible innecesaria.
8. Evaluar periódicamente la calidad de las respuestas.

### 9.6 Indicadores de éxito

- Porcentaje de preguntas resueltas sin soporte.
- Tiempo promedio de respuesta.
- Tasa de respuestas incorrectas.
- Número de escalaciones.
- Satisfacción del usuario.
- Reducción de consultas repetitivas.

### 9.7 Riesgos y controles

- Responder únicamente con datos recuperados y autorizados.
- Mostrar un enlace o acción para escalar a soporte.
- No revelar solicitudes de otros usuarios.
- No inventar políticas que no estén documentadas.
- Informar cuando la respuesta tenga información incompleta.

---

## 10. Agentes de IA: Uso Recomendado y Límites

### 10.1 Qué es un agente en este contexto

Un agente de IA sería un componente capaz de interpretar una solicitud, seleccionar herramientas autorizadas, consultar información y preparar una respuesta o acción propuesta.

En NovaLeave, un agente no debe tener permisos generales ni acceso directo a toda la base de datos. Debe operar mediante herramientas explícitas y limitadas.

### 10.2 Herramientas permitidas

Un agente podría utilizar herramientas de solo lectura como:

- Consultar saldo del usuario autenticado.
- Consultar solicitudes propias.
- Consultar el estado de una solicitud autorizada.
- Consultar feriados.
- Calcular una vista preliminar de días laborables.
- Consultar documentación aprobada.
- Preparar un borrador de solicitud.

### 10.3 Herramientas que requieren confirmación humana

- Enviar una solicitud.
- Cancelar una solicitud.
- Aprobar una solicitud.
- Rechazar una solicitud.
- Anular una solicitud.
- Enviar una comunicación oficial.
- Crear o modificar datos persistentes.

### 10.4 Herramientas que no deben estar disponibles para un agente

- Modificar directamente el balance.
- Asignar roles.
- Cambiar permisos.
- Desactivar cuentas.
- Consultar contraseñas o tokens.
- Ejecutar SQL arbitrario.
- Saltarse validaciones de Application o Domain.

### 10.5 Arquitectura de herramientas

```text
Agente
  |
  +-- Tool: GetMyBalance
  +-- Tool: GetMyRequests
  +-- Tool: GetRequestDetails
  +-- Tool: CalculateWorkingDaysPreview
  +-- Tool: CreateVacationDraft
  |
  v
Application Use Cases
  |
  v
Domain e Infrastructure
```

La autorización debe verificarse tanto antes de exponer una herramienta como dentro del caso de uso que la ejecuta.

---

## 11. Automatizaciones Recomendadas sin IA

No todo problema requiere inteligencia artificial. NovaLeave debería incorporar automatizaciones deterministas antes o junto con las funciones de IA.

### 11.1 Recordatorios de solicitudes pendientes

Enviar recordatorios cuando una solicitud se acerque a su fecha de expiración.

### 11.2 Notificación de cambios de estado

Informar al usuario cuando una solicitud sea aprobada, rechazada, cancelada, anulada o expirada.

### 11.3 Alertas de saldo bajo

Avisar al usuario cuando su saldo disponible sea insuficiente para el periodo seleccionado.

### 11.4 Procesamiento automático de expiraciones

Ejecutar un proceso programado que detecte solicitudes pendientes expiradas y aplique la transición correspondiente de forma transaccional.

### 11.5 Informes periódicos

Generar informes para responsables de NovaComp sobre solicitudes pendientes, tiempos de resolución y periodos de alta demanda.

### 11.6 Por qué no usar IA en estas funciones

Estas operaciones tienen reglas conocidas, resultados verificables y poca ambigüedad. Implementarlas con jobs programados, colas y servicios de aplicación será más económico, predecible, auditable y fácil de probar.

---

## 12. Arquitectura Técnica Propuesta

La incorporación de IA debe respetar la arquitectura existente:

```text
Presentation.Web
    |
    v
Application
    |
    v
Domain

Infrastructure
    |
    +-- SQL Server / EF Core
    +-- ASP.NET Core Identity
    +-- Servicio de IA
    +-- Cola de mensajes
    +-- Servicio de notificaciones
    +-- Telemetría y auditoría
```

### 12.1 Presentation.Web

Responsabilidades:

- Mostrar asistentes y recomendaciones.
- Recibir confirmaciones.
- Presentar niveles de confianza y explicaciones.
- No ejecutar directamente un proveedor de IA.
- No aplicar reglas de dominio.
- Usar ViewModels dedicados.

### 12.2 Application

Responsabilidades:

- Coordinar casos de uso.
- Validar identidad y permisos.
- Preparar contexto mínimo para la IA.
- Validar y normalizar respuestas.
- Aplicar políticas de fallback.
- Registrar recomendaciones y decisiones.

Interfaces sugeridas:

```text
IAssistantClient
IApproverCopilot
IAnomalyDetectionService
ILeaveForecastService
INotificationService
```

### 12.3 Domain

Responsabilidades:

- Mantener invariantes.
- Controlar estados de solicitudes.
- Controlar saldo y reservas.
- Validar transiciones.
- No depender de modelos, proveedores ni SDKs de IA.

### 12.4 Infrastructure

Responsabilidades:

- Implementar clientes de proveedores de IA.
- Administrar secretos mediante configuración segura.
- Implementar colas y tareas programadas.
- Persistir datos analíticos.
- Registrar métricas, trazas y eventos.
- Implementar políticas de reintento y circuit breaker.

### 12.5 Persistencia de recomendaciones

Las recomendaciones de IA deben almacenarse separadas de las entidades transaccionales o mediante una entidad explícita, por ejemplo:

```text
AiRecommendation
- Id
- RecommendationType
- SubjectType
- SubjectId
- ModelProvider
- ModelVersion
- InputSummary
- OutputSummary
- Confidence
- CreatedAt
- AcceptedBy
- AcceptedAt
- CorrelationId
```

No se deben almacenar prompts completos si contienen información sensible innecesaria.

---

## 13. Seguridad, Privacidad y Cumplimiento

Antes de usar datos reales en modelos externos se debe definir:

- Qué datos pueden salir de NovaLeave.
- Qué datos deben anonimizarse.
- Qué proveedor se utilizará.
- Dónde se procesan y almacenan los datos.
- Cuánto tiempo se conservan.
- Cómo se eliminan.
- Si el proveedor utiliza los datos para entrenar modelos.
- Cómo se registran los accesos.
- Cómo se atienden solicitudes de auditoría o eliminación.

Nunca deben enviarse al modelo:

- Contraseñas.
- Tokens de autenticación.
- Cookies.
- Secretos.
- Hashes.
- Información no requerida para la tarea.
- Datos de otros usuarios fuera del alcance autorizado.

Las recomendaciones deben quedar separadas de las decisiones oficiales. Una recomendación de IA no debe considerarse evidencia suficiente para sancionar, bloquear o negar una solicitud.

### 13.1 Controles técnicos obligatorios

- Autorización server-side en cada operación.
- Protección antiforgery para acciones mutantes.
- ViewModels de entrada dedicados.
- Validación de salida estructurada.
- Límites de tamaño y tiempo de respuesta.
- Rate limiting para endpoints expuestos.
- Redacción de datos sensibles en logs.
- Correlation ID en cada interacción.
- Auditoría de recomendaciones y acciones humanas.
- Timeouts, reintentos limitados y circuit breaker.
- Fallback al flujo no asistido.
- Pruebas contra prompt injection y extracción de datos.

### 13.2 Riesgos específicos de IA

| Riesgo | Mitigación |
|---|---|
| Alucinaciones | Respuestas estructuradas, grounding y validación server-side |
| Prompt injection | Separar instrucciones del usuario, herramientas limitadas y filtrado |
| Exfiltración de datos | Contexto mínimo, autorización y redacción |
| Automatización excesiva | Confirmación humana y permisos separados |
| Sesgo | Evaluación por grupos y exclusión de atributos sensibles |
| Indisponibilidad | Fallback y circuit breaker |
| Costos inesperados | Límites de tokens, cuotas y métricas de consumo |
| Respuestas inconsistentes | Versionado, pruebas de evaluación y contratos tipados |

---

## 14. Observaciones Técnicas Previas

Antes de implementar IA conviene resolver o revisar algunos puntos de consistencia técnica:

1. La constitución establece el uso de `TimeProvider`, pero algunas entidades actuales utilizan directamente `DateTime.UtcNow`.
2. La lógica de reserva y deducción de días debe revisarse para garantizar que el saldo no se descuente dos veces durante el ciclo `Pending` a `Approved`.
3. Las reglas actuales del dominio y las reglas descritas en la constitución deben mantenerse alineadas.
4. Las operaciones de aprobación, rechazo, cancelación y anulación deben verificar nuevamente rol, estado activo, propiedad y estado de la solicitud.
5. Las respuestas generadas por IA deben pasar por validación estructural antes de llegar a un caso de uso.
6. El sistema debe mantener pruebas unitarias, de integración y end-to-end para cualquier flujo que se modifique.
7. Los eventos de auditoría deben tener categorías consistentes para facilitar analítica y detección de anomalías.
8. Los datos históricos deben revisarse antes de entrenar modelos para evitar aprender decisiones antiguas que no sean compatibles con la política vigente.
9. Deben definirse métricas base antes de activar cualquier piloto para poder demostrar mejora real.

Estas revisiones no son funcionalidades de IA, pero son requisitos importantes para que las capacidades inteligentes se apoyen en una base confiable.

---

## 15. Plan de Implementación por Fases

### Fase 0: Preparación

- Revisar invariantes y transiciones de dominio.
- Confirmar el modelo de permisos.
- Estandarizar la auditoría.
- Definir clasificación de datos.
- Crear métricas base.
- Documentar decisiones mediante ADRs.
- Definir proveedor o estrategia de modelos.
- Crear un conjunto de casos de evaluación anonimizados.

### Fase 1: Automatización sin IA

- Notificaciones de estado.
- Recordatorios de expiración.
- Jobs de revisión de solicitudes.
- Informes básicos.
- Alertas operativas.
- Métricas de tiempos de resolución.

### Fase 2: Asistente de creación

- Borrador de solicitud.
- Validación determinista.
- Confirmación humana.
- Fallback al formulario tradicional.
- Pruebas de seguridad y privacidad.
- Piloto controlado con usuarios internos.

### Fase 3: Copiloto de aprobadores

- Resúmenes.
- Priorización explicable.
- Indicadores de urgencia.
- Registro de recomendaciones.
- Decisión final manual.
- Evaluación de aceptación y corrección humana.

### Fase 4: Analítica y pronósticos

- Dashboard histórico.
- Métricas de demanda.
- Pronóstico de periodos críticos.
- Evaluación de precisión.
- Revisión de sesgos y estabilidad.

### Fase 5: Detección de anomalías

- Reglas de alerta.
- Cola de investigación.
- Modelos de comportamiento.
- Revisión humana antes de acciones críticas.
- Integración con los procesos de seguridad existentes.

### Criterio de avance entre fases

No se debe avanzar a la siguiente fase únicamente porque el prototipo funcione técnicamente. Cada fase debe demostrar:

- Valor medible.
- Seguridad aceptable.
- Tasa de error conocida.
- Capacidad de auditoría.
- Coste operativo controlado.
- Fallback funcional.
- Aceptación por parte de los usuarios involucrados.

---

## 16. Requisitos de Pruebas y Evaluación

### 16.1 Pruebas funcionales

- Fechas ambiguas o incompletas.
- Solicitudes con saldo insuficiente.
- Solicitudes con solapamiento.
- Solicitudes que contienen días festivos.
- Usuarios con distintos roles.
- Solicitudes fuera del alcance del usuario actual.
- Estados incompatibles con la acción solicitada.

### 16.2 Pruebas de seguridad

- Intentos de acceder a datos de otro usuario.
- Intentos de hacer que el agente apruebe una solicitud sin confirmación.
- Prompt injection.
- Exfiltración de información mediante instrucciones indirectas.
- Ausencia o invalidez de antiforgery.
- Expiración de sesión.
- Cuenta inactiva.
- Rate limiting.

### 16.3 Pruebas de calidad de modelo

- Precisión de extracción de fechas.
- Identificación correcta de información faltante.
- Fidelidad de los resúmenes.
- Tasa de respuestas no sustentadas.
- Tasa de falsos positivos en anomalías.
- Estabilidad entre versiones del modelo.
- Calidad por idioma y variaciones de redacción.

### 16.4 Pruebas de degradación

- Proveedor de IA caído.
- Timeout.
- Respuesta con JSON inválido.
- Respuesta incompleta.
- Exceso de cuota.
- Error de red.
- Cambio de versión del proveedor.

En todos estos casos debe continuar disponible el flujo tradicional o una respuesta controlada.

---

## 17. Indicadores de Éxito Globales

La iniciativa debería considerarse exitosa si logra:

- Disminuir errores de captura.
- Reducir tiempos de resolución.
- Reducir solicitudes expiradas.
- Mejorar la planificación de periodos críticos.
- Mantener o mejorar la seguridad.
- Mantener trazabilidad completa.
- Evitar decisiones injustificadas o no explicables.
- Permitir operar normalmente cuando la IA no esté disponible.
- Obtener una aceptación positiva de usuarios y aprobadores.

La precisión del modelo no debe ser el único criterio. También deben evaluarse:

- Explicabilidad.
- Tasa de rechazo humano.
- Falsos positivos.
- Privacidad.
- Coste por operación.
- Disponibilidad.
- Facilidad de mantenimiento.
- Tiempo de respuesta.
- Impacto real sobre los indicadores de negocio.

---

## 18. Propuesta de Diapositivas Opcionales

### Diapositiva 1: Oportunidad

**NovaLeave + IA**

- Menos errores al solicitar vacaciones.
- Revisiones más rápidas para aprobadores.
- Mejor planificación de ausencias.
- Detección temprana de riesgos.
- Atención más eficiente a usuarios.

### Diapositiva 2: Soluciones propuestas

- Asistente para crear solicitudes.
- Copiloto para aprobadores.
- Pronóstico de demanda.
- Detección de anomalías.
- Automatización de notificaciones y soporte.

### Diapositiva 3: Implementación responsable

- La IA recomienda; el dominio decide.
- Confirmación humana en decisiones críticas.
- Datos mínimos y privacidad.
- Auditoría completa.
- Fallback sin IA.
- Implementación gradual por fases.

---

## 19. Conclusión

NovaLeave tiene una base adecuada para incorporar capacidades inteligentes porque ya cuenta con casos de uso definidos, reglas de dominio, estados explícitos, auditoría, autenticación, autorización y separación arquitectónica.

La estrategia recomendada no es añadir IA de forma generalizada, sino incorporarla donde realmente aporta valor:

- Interpretación de solicitudes.
- Resumen de información.
- Priorización explicable.
- Predicción de demanda.
- Detección de patrones anómalos.
- Atención contextual a usuarios.

Las reglas críticas deben continuar siendo deterministas y verificables. La IA debe funcionar como una capa de asistencia y análisis alrededor del núcleo transaccional de NovaLeave, manteniendo la autoridad del dominio, la seguridad de NovaComp y la intervención humana en las decisiones importantes.

La recomendación final es iniciar con automatizaciones sin IA y un piloto controlado del asistente de creación. Esta ruta permite medir resultados, validar la gobernanza de datos y construir confianza antes de incorporar capacidades más avanzadas.
