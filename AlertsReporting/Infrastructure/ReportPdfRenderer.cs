using System.Globalization;
using System.Text.Json.Nodes;
using FrioTrack.Api.AlertsReporting.Application;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace FrioTrack.Api.AlertsReporting.Infrastructure;

public sealed class ReportFontResolver : IFontResolver {
    public byte[] GetFont(string faceName)=>File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory,"Resources","Fonts","FrioTrackSans-Regular.ttf"));
    public FontResolverInfo? ResolveTypeface(string familyName,bool bold,bool italic)=>familyName=="FrioTrack Sans"?new FontResolverInfo("FrioTrackSans-Regular"):null;
}
public sealed class ReportPdfRenderer : FrioTrack.Api.AlertsReporting.Application.Ports.IReportDocumentRenderer {
    public static void ConfigureFonts()=>GlobalFontSettings.FontResolver=new ReportFontResolver();
    public byte[] Monthly(MonthlySummary summary) {
        var lines=new List<string>{"Mes de salida (UTC): "+summary.Month,"Generado (UTC): "+summary.GeneratedAt.ToString("u"),OperationalReporting.Notice,"","RESUMEN DE OPERACIONES",$"Envíos: {summary.Shipments} | En tránsito: {summary.InTransit} | Entregados: {summary.Delivered}",$"Lecturas: {summary.Readings} | Alertas térmicas: {summary.ThermalAlerts} | Alertas de señal: {summary.SignalAlerts} | Abiertas: {summary.OpenAlerts}","","COMPARACIÓN DE RUTAS"};
        foreach(var route in summary.Routes){lines.Add(route.Origin+" → "+route.Destination);lines.Add($"Envíos: {route.Shipments} | Lecturas: {route.Readings} | Alertas: {route.Alerts} | Abiertas: {route.OpenAlerts}");lines.Add($"Temperatura registrada: mínimo {Number(route.MinimumTemperature)}, máximo {Number(route.MaximumTemperature)}, promedio {Number(route.AverageTemperature)} °C");lines.Add("");}
        if(summary.Routes.Length==0)lines.Add("No existen envíos asignados con salida en este mes.");
        lines.Add("Las alertas y lecturas corresponden a los envíos cuya salida se encuentra en el mes seleccionado. Los promedios describen únicamente las lecturas registradas; no estiman intervalos sin datos.");
        return Render("Informe ejecutivo mensual",lines);
    }
    static string Number(decimal? value)=>value?.ToString("0.##",CultureInfo.InvariantCulture)??"Sin datos";
    public byte[] Shipment(JsonObject snapshot) {
        var s=snapshot["shipment"]!.AsObject();var lines=new List<string>{"Envío: "+s["id"],"Ruta: "+s["origin"]+" → "+s["destination"],"Carga: "+s["cargo"],"Estado: "+s["status"],"Generado: "+snapshot["generatedAt"],OperationalReporting.Notice,"","LECTURAS REGISTRADAS"};
        foreach(var reading in s["readings"]!.AsArray())lines.Add($"{reading!["at"]} | {reading["temperature"]} °C | {reading["humidity"]} % HR");
        if(s["readings"]!.AsArray().Count==0)lines.Add("Sin lecturas registradas.");
        lines.Add("");lines.Add("HISTORIAL DEL ENVÍO");
        foreach(var item in s["history"]!.AsArray())lines.Add($"{item!["at"]} | {item["action"]} | {item["actor"]} | {item["note"]}");
        return Render("Informe de trazabilidad",lines);
    }
    static byte[] Render(string title,IEnumerable<string> lines) {
        using var doc=new PdfDocument();doc.Info.Title="FríoTrack — "+title;doc.Info.Author="BlackStartup";
        var font=new XFont("FrioTrack Sans",10,XFontStyleEx.Regular);var heading=new XFont("FrioTrack Sans",18,XFontStyleEx.Regular);XGraphics? graphics=null;double y=0;int number=0;PdfPage page=null!;
        void NewPage(){graphics?.Dispose();page=doc.AddPage();page.Size=PdfSharp.PageSize.A4;graphics=XGraphics.FromPdfPage(page);number++;graphics.DrawString("FríoTrack",heading,XBrushes.DarkBlue,new XPoint(42,46));graphics.DrawString(title,font,XBrushes.Black,new XPoint(42,67));graphics.DrawLine(XPens.LightGray,42,80,page.Width.Point-42,80);graphics.DrawString("BlackStartup | "+number,font,XBrushes.Gray,new XPoint(42,page.Height.Point-28));y=104;}
        NewPage();
        foreach(var line in lines){
            var words=line.Replace('\r',' ').Replace('\n',' ').Split(' ');var current="";
            foreach(var word in words){var candidate=current.Length==0?word:current+" "+word;if(graphics!.MeasureString(candidate,font).Width>page.Width.Point-84&&current.Length>0){if(y>page.Height.Point-65)NewPage();graphics!.DrawString(current,font,XBrushes.Black,new XPoint(42,y));y+=15;current=word;}else current=candidate;}
            if(y>page.Height.Point-65)NewPage();graphics!.DrawString(current,font,XBrushes.Black,new XPoint(42,y));y+=line.Length==0?9:17;
        }
        graphics!.Dispose();using var output=new MemoryStream();doc.Save(output,false);return output.ToArray();
    }
}
