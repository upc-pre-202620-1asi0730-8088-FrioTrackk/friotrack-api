# FríoTrack Web Services 0.3.0 — Sprint 4

El incremento incorpora consultas mensuales, bitácora operativa, informes PDF, lecturas por lote y administración de acceso dentro de cada empresa. La recuperación de contraseña utiliza el modelo relacional existente; requiere configurar un servidor SMTP para enviar mensajes fuera del entorno de pruebas.

## Arquitectura

Los controllers de `Iam/Interfaces/Rest`, `Monitoring/Interfaces/Rest` y `AlertsReporting/Interfaces/Rest` reciben recursos HTTP y delegan en servicios de aplicación. Los contratos de comandos y consultas se ubican en `Application/CommandServices` y `Application/QueryServices`. Las entidades de dominio conservan sus reglas y no dependen de ASP.NET Core, EF Core ni PDFsharp.

La persistencia mantiene EF Core/PostgreSQL y las 18 tablas existentes. `DbContext` representa la unidad de trabajo; las operaciones transaccionales usan el bloqueo por empresa para proteger reservas y lecturas por lote. La integración del correo se realiza mediante el puerto `IRecoveryMail` y el adaptador `SmtpRecoveryMail`. El generador de documentos y la fuente con licencia OFL se ubican en infraestructura. Las sesiones continúan siendo tokens opacos persistidos como hash; esta versión no las presenta como JWT.

| Recurso | Comportamiento |
| --- | --- |
| `GET /api/analytics/monthly?month=2026-10` | Resumen y comparación de rutas de los envíos autorizados con salida en ese mes UTC. Los valores sin lecturas son nulos. |
| `GET /api/audit?month=2026-10` | Historial de envíos y acciones correctivas con fecha y responsable; filtrado por el mes UTC del evento. |
| `GET /api/reports/monthly/pdf?month=2026-10` | PDF ejecutivo del mismo conjunto autorizado. |
| `GET /api/reports/{id}/pdf` | PDF de una copia de trazabilidad persistida, con los permisos del envío de origen. |
| `POST /api/shipments/{id}/readings/batch` | Entre 1 y 100 lecturas ordenadas. Un registro inválido revierte el lote entero, incluidas las alertas y el historial. |
| `GET /api/accounts` | Lista de cuentas de la empresa, exclusiva del coordinador; sin hashes de contraseña ni tokens. |
| `PUT /api/accounts/{id}/access` | Cambia el rol o la habilitación y revoca sesiones. No permite bloquearse a sí mismo ni promover a un cliente con envíos asignados. |
| `POST /api/auth/forgot-password` | Respuesta genérica para cuentas existentes y desconocidas. SMTP sin configurar devuelve `503 recoveryUnavailable`. |
| `POST /api/auth/reset-password` | Token de un uso, vigencia de 15 minutos y revocación de sesiones previas. Solo se conserva su hash en PostgreSQL. |
| `GET /api/capabilities` | Disponibilidad pública de funciones, sin credenciales de infraestructura. |

Los informes estadísticos describen lecturas de demostración y no acreditan cumplimiento térmico de intervalos sin datos. Los lotes reciben muestras autorizadas de demostración; no autentican dispositivos físicos ni implementan un receptor IoT autónomo.

## Configuración del correo

| Variable | Uso |
| --- | --- |
| `FRIOTRACK_MAIL_HOST` | Servidor del proveedor. Vacío mantiene la recuperación no disponible. |
| `FRIOTRACK_MAIL_PORT` | Puerto SMTP, `587` por defecto. |
| `FRIOTRACK_MAIL_USERNAME` / `FRIOTRACK_MAIL_PASSWORD` | Credenciales privadas del proveedor. |
| `FRIOTRACK_MAIL_FROM` | Remitente autorizado por el proveedor. |
| `FRIOTRACK_MAIL_TLS` | TLS habilitado por defecto; solo puede desactivarse para pruebas en loopback. |
| `FRIOTRACK_RESET_URL` | URL HTTPS de la pantalla de recuperación de la SPA, sin token; el servicio añade el token del mensaje. |

La configuración real se administra en Azure App Service o en variables privadas. No se introduce en el frontend. La suite usa un servidor SMTP local y no envía correos a participantes ni a direcciones externas.

## Ejemplos

```json
{
  "readings": [
    { "temperature": 4.2, "humidity": 80, "lat": -12.05, "lng": -77.04, "at": "2026-11-27T12:00:00Z" },
    { "temperature": 4.3, "humidity": 81, "lat": -12.06, "lng": -77.03, "at": "2026-11-27T12:00:10Z" }
  ]
}
```

El envío debe estar en tránsito y las fechas deben ser posteriores al inicio y a la última lectura, dentro del límite permitido por el servicio. Los ejemplos representan el formato de la solicitud; sus fechas se adaptan al escenario de prueba.

```json
{ "email": "persona@ejemplo.com" }
```

```json
{ "token": "TOKEN_PRIVADO_RECIBIDO_POR_CORREO", "password": "NUEVA_CONTRASENA_DE_AL_MENOS_12_CARACTERES" }
```

La referencia completa de recursos y esquemas se publica en `/scalar/v1`. Los endpoints públicos de acceso no requieren Bearer en el contrato OpenAPI. Para las demás operaciones, se utiliza el token de una sesión autorizada.
