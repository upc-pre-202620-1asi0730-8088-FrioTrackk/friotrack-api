using System.Text.Json.Nodes;
using FrioTrack.Api.AlertsReporting.Application;
using Xunit;
namespace FrioTrack.Api.Tests;
public sealed class OperationalSummaryTests {
    [Fact] public void Quarter_uses_all_readings_without_averaging_rounded_monthly_averages() {
        var state=JsonNode.Parse("""
        {"shipments":[
          {"id":"a","origin":"Ica","destination":"Lima","departure":"2026-10-01T00:00:00Z","status":"delivered","readings":[{"temperature":2}]},
          {"id":"b","origin":"Ica","destination":"Lima","departure":"2026-12-31T23:59:59Z","status":"in-transit","readings":[{"temperature":8},{"temperature":8},{"temperature":8}]},
          {"id":"c","origin":"Ica","destination":"Lima","departure":"2027-01-01T00:00:00Z","status":"scheduled","readings":[{"temperature":99}]}
        ],"alerts":[{"shipmentId":"a","type":"temperature","resolved":true},{"shipmentId":"b","type":"signal","resolved":false}]}
        """)!.AsObject();
        var result=OperationalReporting.SummarizeQuarter(state,2026,4);
        Assert.Equal(2,result.Shipments);Assert.Equal(4,result.Readings);Assert.Equal(6.5m,Assert.Single(result.Routes).AverageTemperature);Assert.Equal(1,result.OpenAlerts);Assert.Equal(1,result.ThermalAlerts);Assert.Equal(1,result.SignalAlerts);
    }
    [Fact] public void Empty_quarter_does_not_invent_a_temperature() {
        var state=JsonNode.Parse("""{"shipments":[],"alerts":[]}""")!.AsObject();
        var result=OperationalReporting.SummarizeQuarter(state,2026,1);Assert.Equal(0,result.Readings);Assert.Empty(result.Routes);
    }
}
