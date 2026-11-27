using System.Text.Json.Nodes;
namespace FrioTrack.Api.AlertsReporting.Application.Ports;
public interface IReportDocumentRenderer {
    byte[] Monthly(MonthlySummary summary);
    byte[] Shipment(JsonObject snapshot);
}
