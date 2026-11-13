using FrioTrack.Api.Shared.Application;
using FrioTrack.Api.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FrioTrack.Api.Interfaces;

public sealed class ApiErrorMiddleware(RequestDelegate next) {
    public async Task InvokeAsync(HttpContext context) {
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers.XContentTypeOptions = "nosniff";
        try { await next(context); }
        catch (ApiError error) { await Error(context,error.Code,error.Status); }
        catch (DomainRuleException error) { await Error(context,error.Code,error.Code=="forbidden"?403:error.Code=="staleShipment"?409:400); }
        catch (DbUpdateConcurrencyException) { await Error(context,"staleShipment",409); }
        catch (DbUpdateException error) when(error.InnerException is PostgresException postgres) {
            var constraint=postgres.ConstraintName ?? "";
            var code=postgres.SqlState=="23505" ? constraint.Contains("email",StringComparison.OrdinalIgnoreCase)?"duplicateProfile":constraint.Contains("plate",StringComparison.OrdinalIgnoreCase)?"duplicateVehicle":constraint.Contains("license",StringComparison.OrdinalIgnoreCase)?"duplicateDriver":constraint.Contains("serial",StringComparison.OrdinalIgnoreCase)?"duplicateSensor":constraint.Contains("sensors",StringComparison.OrdinalIgnoreCase)?"sensorAssigned":"concurrentUpdate" : "requiredFields";
            await Error(context,code,postgres.SqlState=="23505"?409:400);
        }
        catch (NpgsqlException) { await Error(context,"databaseUnavailable",503); }
        catch (System.Text.Json.JsonException) { await Error(context,"requiredFields",400); }
        catch (BadHttpRequestException) { await Error(context,"requiredFields",400); }
    }
    static async Task Error(HttpContext context,string code,int status) {
        context.Response.StatusCode=status;
        var spanish=context.Request.Headers.AcceptLanguage.ToString().StartsWith("es",StringComparison.OrdinalIgnoreCase);
        var message=code switch {
            "invalidCredentials"=>spanish?"El correo o la contraseña son incorrectos.":"The email or password is incorrect.",
            "sessionExpired"=>spanish?"Inicia sesión nuevamente.":"Please sign in again.",
            "forbidden"=>spanish?"Tu cuenta no puede realizar esta operación.":"Your account cannot perform this operation.",
            "notFound"=>spanish?"No se encontró el registro solicitado.":"The requested record was not found.",
            "staleShipment"=>spanish?"El envío cambió. Actualiza los datos e inténtalo nuevamente.":"The shipment changed. Refresh it and try again.",
            "vehicleBusy"=>spanish?"El vehículo ya tiene una reserva en ese intervalo.":"The vehicle already has a reservation in that interval.",
            "driverBusy"=>spanish?"El conductor ya tiene una reserva en ese intervalo.":"The driver already has a reservation in that interval.",
            "databaseUnavailable"=>spanish?"El servicio de datos no está disponible temporalmente.":"The data service is temporarily unavailable.",
            "duplicateProfile"=>spanish?"El correo ya está registrado.":"The email is already registered.",
            "invalidPassword"=>spanish?"La contraseña debe tener entre 12 y 128 caracteres.":"The password must contain 12 to 128 characters.",
            "invalidRange"=>spanish?"El mínimo de temperatura debe ser menor que el máximo.":"The minimum temperature must be below the maximum.",
            "invalidHumidity"=>spanish?"La humedad debe tener un intervalo válido entre 0 y 100.":"Humidity must have a valid interval between 0 and 100.",
            "reportDeliveredOnly"=>spanish?"El informe se genera después de entregar el envío.":"Generate the report after the shipment is delivered.",
            "invalidReadingDate"=>spanish?"La lectura debe corresponder al monitoreo iniciado, ser posterior a la última lectura y no estar más de un minuto en el futuro.":"The reading must be within the started monitoring period, later than the last reading and no more than one minute in the future.",
            _=>spanish?"La solicitud no cumple las reglas de esta operación.":"The request does not satisfy this operation's rules."
        };
        await context.Response.WriteAsJsonAsync(new ErrorResponse(code,message));
    }
}
