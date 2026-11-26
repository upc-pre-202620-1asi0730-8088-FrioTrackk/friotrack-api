# Referencia de la API FríoTrack

La documentación interactiva está disponible en [Scalar](https://friotrack-api-bce6db1b.azurewebsites.net/scalar/v1). La especificación [OpenAPI](https://friotrack-api-bce6db1b.azurewebsites.net/swagger/v1/swagger.json) describe las rutas, los esquemas tipados y las respuestas de cada operación.

## Autenticación

En `POST /api/auth/login`, envíe las credenciales de una cuenta de demostración del Anexo C del informe. El servidor determina su rol; añadir `role` no concede permisos adicionales.

```json
{
  "email": "cliente.demo@friotrack.example",
  "password": "CONTRASENA_DE_LA_CUENTA_DEMOSTRACION"
}
```

Una respuesta `200` incluye `profile`, `state` y `token`. Copie únicamente el valor de `token` en la autenticación Bearer de Scalar para ejecutar consultas autorizadas. El token es opaco, caduca y se revoca con `POST /api/auth/logout`. No guarde tokens en el código ni en capturas del informe.

Una solicitud con credenciales incorrectas devuelve `401`:

```json
{
  "code": "invalidCredentials",
  "message": "Invalid credentials."
}
```

El texto puede consultarse en español mediante `Accept-Language: es`. Los códigos de error permanecen estables.

## Consultas y comandos

| Operación | Solicitud o parámetros | Resultado y validación |
| --- | --- | --- |
| Consultar envíos | `GET /api/shipments`, sin cuerpo, con Bearer | `200`; el cliente recibe únicamente sus envíos asignados. |
| Abrir detalle | `GET /api/shipments/{id}` | Use un ID devuelto por la consulta anterior. Un ID inexistente o no autorizado devuelve `404`. |
| Programar envío | `POST /api/shipments` | Use el esquema `ShipmentRequest` de Scalar y los IDs de cliente, vehículo y conductor obtenidos en el mismo entorno. Las fechas incluyen zona horaria y la llegada debe ser posterior a la salida. |
| Editar envío | `PUT /api/shipments/{id}` | Envíe el esquema de programación con la `version` actual. Una versión antigua o una reserva incompatible devuelve `409`. |
| Eliminar programación | `DELETE /api/shipments/{id}?version=1` | Solo admite envíos programados sin lecturas; el resultado incluye la proyección actualizada. |
| Cambiar estado | `POST /api/shipments/{id}/transitions` | Cuerpo de ejemplo mostrado debajo. El servidor valida el estado y la versión. |
| Registrar lectura | `POST /api/shipments/{id}/readings` | Temperatura y humedad numéricas, latitud, longitud y fecha opcional. Es una lectura de demostración registrada por un coordinador. |
| Evaluar comunicación | `POST /api/shipments/{id}/signal-evaluations` | Sin cuerpo; evalúa el silencio de comunicación del envío autorizado a solicitud del coordinador. |
| Registrar incidencia | `POST /api/shipments/{id}/incidents` | Nota de la incidencia; conserva fecha y responsable. |
| Atender alerta | `POST /api/alerts/{id}/corrective-actions` | Nota y decisión de notificar al cliente. Reconocer la alerta no implica resolver la desviación. |
| Crear o editar flota | `POST /api/vehicles`, `/api/drivers`, `/api/sensors`; `PUT` con ID | Cada recurso usa su esquema propio. Las capacidades, disponibilidad, asignaciones y reservas se validan en el servidor. |
| Leer notificaciones | `POST /api/notifications/read` | Sin cuerpo; actualiza solo los avisos del destinatario autenticado. |
| Actualizar perfil | `PUT /api/profiles/me` | Usa `ProfileRequest`; el usuario no puede cambiar su rol ni la propiedad de la empresa. |
| Generar reporte | `POST /api/shipments/{id}/reports` | Envío entregado y autorizado; genera una copia JSON persistida. |
| Consultar reporte | `GET /api/reports/{id}` | Devuelve la copia generada; el ID se obtiene del comando anterior. |

Ejemplo de transición de un envío programado, utilizando su versión actual:

```json
{
  "version": 1,
  "status": "in-transit",
  "note": "Salida confirmada por el coordinador"
}
```

Las modificaciones pueden devolver `400` por datos inválidos, `401` por falta de sesión, `403` por permisos, `404` por recurso inexistente o ajeno y `409` por conflicto. Los endpoints de acceso pueden devolver `429` cuando se supera el límite de intentos. Use recursos de demostración para las pruebas; una consulta no modifica el historial ni marca avisos como leídos.
