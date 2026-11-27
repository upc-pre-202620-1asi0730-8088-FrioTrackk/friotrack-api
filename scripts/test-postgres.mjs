import assert from 'node:assert/strict';
import { spawn, execFileSync } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { mkdir, writeFile } from 'node:fs/promises';
import net from 'node:net';
import path from 'node:path';

// The suite creates and drops only its own random database on a loopback server.
const connection = process.env.FRIOTRACK_TEST_DB;
if (!connection) throw new Error('Set FRIOTRACK_TEST_DB to an isolated local PostgreSQL administration connection.');
const settings = Object.fromEntries(connection.split(';').filter(Boolean).map(p => { const i=p.indexOf('='); return [p.slice(0,i).trim().toLowerCase(),p.slice(i+1).trim()]; }));
const host = settings.host || settings.server;
if (!['localhost','127.0.0.1','::1'].includes(host)) throw new Error('Integration tests require a loopback PostgreSQL server, never a cloud or production database.');
const database = 'friotrack_av2_test_' + randomBytes(6).toString('hex');
const environment = { ...process.env, PGHOST:host, PGPORT:settings.port||'5432', PGUSER:settings.username||settings['user id']||settings.user, PGPASSWORD:settings.password||'', PGDATABASE:'postgres' };
const psql = process.env.FRIOTRACK_PSQL || 'psql';
function sql(statement, target=database) {
  return execFileSync(psql,['-X','-q','-v','ON_ERROR_STOP=1','-tA','-d',target],{env:environment,input:statement,encoding:'utf8',windowsHide:true,stdio:['pipe','pipe','pipe']}).trim();
}
const probe=net.createServer();await new Promise(r=>probe.listen(0,'127.0.0.1',r));const port=probe.address().port;await new Promise(r=>probe.close(r));
const base='http://127.0.0.1:'+port;
const password='Temporary!'+randomBytes(20).toString('base64url');
const clientPassword='Temporary!'+randomBytes(20).toString('base64url');
const dbConnection=connection.replace(/(?:Database|Initial Catalog)=[^;]*;?/i,'').replace(/;?$/,';')+'Database='+database;
const messages=[];
const smtp=net.createServer(socket=>{
 socket.on('error',()=>{});
 socket.setEncoding('utf8');socket.write('220 loopback.test ESMTP\r\n');let buffer='',dataMode=false,message='';
 socket.on('data',chunk=>{buffer+=chunk;let index;while((index=buffer.indexOf('\r\n'))>=0){const line=buffer.slice(0,index);buffer=buffer.slice(index+2);if(dataMode){if(line==='.') {messages.push(message);message='';dataMode=false;socket.write('250 queued\r\n');}else message+=line+'\r\n';continue;}if(/^EHLO|^HELO/.test(line))socket.write('250 loopback.test\r\n');else if(/^MAIL|^RCPT|^RSET/.test(line))socket.write('250 OK\r\n');else if(line==='DATA'){dataMode=true;socket.write('354 End with dot\r\n');}else if(line==='QUIT')socket.end('221 bye\r\n');else socket.write('250 OK\r\n');}});
});
await new Promise(r=>smtp.listen(0,'127.0.0.1',r));
const apiEnvironment={...process.env,FRIOTRACK_CONTACT_TO:'team@friotrack.example',FRIOTRACK_MAIL_HOST:'127.0.0.1',FRIOTRACK_MAIL_PORT:String(smtp.address().port),FRIOTRACK_MAIL_TLS:'false',FRIOTRACK_MAIL_FROM:'noreply@friotrack.example',FRIOTRACK_RESET_URL:'http://localhost/#/reset-password',FRIOTRACK_PERSISTENCE:'postgres',FRIOTRACK_DB:dbConnection,FRIOTRACK_MIGRATE:'true',FRIOTRACK_SEED:'true',FRIOTRACK_COORDINATOR_PASSWORD:password,FRIOTRACK_CLIENT_PASSWORD:clientPassword,FRIOTRACK_ORIGINS:'http://127.0.0.1:5173'};
const record={scope:'Isolated real PostgreSQL API integration; not cloud deployment or team acceptance',startedAt:new Date().toISOString(),checks:[],failures:[]};
let server,diagnostic='',created=false;
const check=(name,run)=>{run();record.checks.push(name);console.log('PASS '+name);};
async function start(){
  const published=process.env.FRIOTRACK_TEST_PUBLISH_DIR||'output/api-publish';
  diagnostic='';server=spawn(process.env.DOTNET_EXE||'dotnet',[path.resolve(published,'FrioTrack.Api.dll'),'--urls',base],{cwd:path.resolve(published),env:apiEnvironment,windowsHide:true,stdio:['ignore','pipe','pipe']});
  server.stdout.on('data',d=>diagnostic+=d);server.stderr.on('data',d=>diagnostic+=d);
  for(let i=0;i<120;i++){
    try{const r=await fetch(base+'/api/health',{signal:AbortSignal.timeout(1500)});if(r.ok)return;}catch{}
    if(server.exitCode!==null)throw Error('API exited during startup; inspect the private local server log.');
    await new Promise(r=>setTimeout(r,250));
  }
  throw Error('API startup timed out; inspect the private local server log.');
}
async function stop(){if(server&&server.exitCode===null){server.kill();await Promise.race([new Promise(r=>server.once('exit',r)),new Promise(r=>setTimeout(r,3000))]);}}
async function call(route,{method='GET',body,token,language}={}){
 const response=await fetch(base+(route.startsWith('/swagger/')||route.startsWith('/scalar')?'':'/api')+route,{method,signal:AbortSignal.timeout(15000),headers:{...(body===undefined?{}:{'Content-Type':'application/json'}),...(token?{Authorization:'Bearer '+token}:{}),...(language?{'Accept-Language':language}:{})},...(body===undefined?{}:{body:JSON.stringify(body)})});
 const content=await response.text();let data=null;try{data=JSON.parse(content);}catch{}
 return {status:response.status,data,headers:response.headers,content};
}
async function mutate(route,body,token,method='POST'){
 const response=await call(route,{method,body,token});
 assert.ok(response.status>=200&&response.status<300,`${method} ${route}: ${response.status} ${JSON.stringify(response.data)}`);return response.data;
}
try {
 sql(`CREATE DATABASE "${database}";`,'postgres');created=true;
 await start();
 check('EF migrations create the relational schema',()=>{assert.ok(Number(sql('SELECT count(*) FROM "__EFMigrationsHistory";'))>=1);assert.ok(Number(sql("SELECT count(*) FROM information_schema.tables WHERE table_schema='public' AND table_name <> '__EFMigrationsHistory';"))>=18);});
 const spec=await call('/swagger/v1/swagger.json');check('OpenAPI exposes authenticated domain REST operations',()=>{assert.equal(spec.status,200);for(const endpoint of ['/api/shipments','/api/vehicles','/api/drivers','/api/sensors'])assert.ok(spec.data.paths[endpoint]);});
 const ui=await call('/swagger/index.html');check('Swagger UI is available',()=>assert.equal(ui.status,200));
 const scalar=await call('/scalar/v1');check('Scalar renders the published OpenAPI reference without a preset token',()=>{assert.equal(scalar.status,200);assert.match(scalar.content,/swagger\/v1\/swagger\.json/);assert.match(scalar.content,/FríoTrack API/);assert.doesNotMatch(scalar.content,/Bearer [A-Za-z0-9_-]{20,}/);});
 const guest=await call('/workspace');check('Workspace rejects anonymous access',()=>assert.equal(guest.status,401));
 const bad=await call('/auth/login',{method:'POST',body:{email:'coordinador@friotrack.app',password:'wrong'},language:'es-419'});check('Invalid credentials rejected with a localized message',()=>{assert.equal(bad.status,401);assert.equal(bad.data.code,'invalidCredentials');assert.ok(bad.data.message);});
 const owner=await call('/auth/login',{method:'POST',body:{email:'coordinador@friotrack.app',password}});
 const cargo=await call('/auth/login',{method:'POST',body:{email:'cliente@friotrack.app',password:clientPassword,role:'coordinator'}});
 check('Account roles are determined by PostgreSQL-backed identity',()=>{assert.equal(owner.status,200);assert.equal(cargo.status,200);assert.equal(owner.data.profile.role,'coordinator');assert.equal(cargo.data.profile.role,'cargo-client');});
 const token=owner.data.token,clientToken=cargo.data.token;
 check('Client projection contains only assigned shipments',()=>{assert.ok(cargo.data.state.shipments.length);assert.ok(cargo.data.state.shipments.every(s=>s.customerId===cargo.data.profile.id));assert.equal(cargo.data.state.profiles.length,1);});
 const denied=await call('/vehicles',{method:'POST',body:{plate:'DENIED',capacity:5,enabled:true},token:clientToken});check('Client cannot manage fleet through REST',()=>assert.equal(denied.status,403));
 const escalation=await call('/auth/register',{method:'POST',body:{name:'Forbidden',email:'forbidden@example.invalid',organization:'Test',password,consent:true,role:'coordinator'}});check('Public registration cannot create coordinators',()=>assert.equal(escalation.status,403));
 const nonce=randomBytes(4).toString('hex');
 const registration=await call('/auth/register',{method:'POST',body:{name:'Validation Client',email:`client-${nonce}@example.invalid`,organization:'Demonstration',password,consent:true}});
 check('A newly registered client starts without shipment access',()=>{assert.equal(registration.status,200);assert.equal(registration.data.state.shipments.length,0);});
 const freshToken=registration.data.token;
 const v=await mutate('/vehicles',{plate:'AV2-'+nonce.slice(0,3),capacity:10,enabled:true},token);const vehicle=v.result;
 const d=await mutate('/drivers',{name:'Integration Driver',license:'LIC-'+nonce,enabled:true},token);const driver=d.result;
 const s=await mutate('/sensors',{serialCode:'SEN-'+nonce,vehicleId:null,enabled:true},token);const sensor=s.result;
 check('Sensor is an independent persisted root without a vehicle',()=>{assert.equal(sensor.vehicleId,null);assert.equal(sql(`SELECT count(*) FROM sensors WHERE id='${sensor.id}' AND vehicle_id IS NULL;`),'1');});
 await mutate('/sensors/'+sensor.id,{serialCode:'SEN-'+nonce,vehicleId:vehicle.id,enabled:true},token,'PUT');
 check('Sensor assignment references the independent vehicle',()=>assert.equal(sql(`SELECT vehicle_id FROM sensors WHERE id='${sensor.id}';`),vehicle.id));
 const duplicate=await call('/sensors',{method:'POST',token,body:{serialCode:'OTHER-'+nonce,vehicleId:vehicle.id,enabled:true}});check('A vehicle cannot be assigned two sensors',()=>assert.ok([400,409].includes(duplicate.status)));
 const body={cargo:'Integration test cargo',weight:3,origin:'Lima',destination:'Ica',customerId:'cargo-1',vehicleId:vehicle.id,driverId:driver.id,minTemp:2,maxTemp:6,minHumidity:70,maxHumidity:95,departure:new Date(Date.now()+86400000).toISOString(),arrival:new Date(Date.now()+172800000).toISOString()};
 const countBefore=Number(sql('SELECT count(*) FROM shipments;'));
 const invalid=await call('/shipments',{method:'POST',token,body:{...body,minTemp:6,maxTemp:6}});check('Invalid thermal range leaves no partial shipment or reservation',()=>{assert.equal(invalid.status,400);assert.equal(Number(sql('SELECT count(*) FROM shipments;')),countBefore);assert.equal(sql(`SELECT count(*) FROM fleet_allocations WHERE vehicle_id='${vehicle.id}';`),'0');});
 const simultaneous=await Promise.all([call('/shipments',{method:'POST',body,token}),call('/shipments',{method:'POST',body,token})]);
 const successes=simultaneous.filter(x=>x.status>=200&&x.status<300);const conflicts=simultaneous.filter(x=>x.status===409);
 check('Concurrent overlapping bookings accept exactly one reservation',()=>{assert.equal(successes.length,1);assert.equal(conflicts.length,1);assert.equal(sql(`SELECT count(*) FROM fleet_allocations WHERE vehicle_id='${vehicle.id}';`),'1');});
 let shipment=successes[0].data.result;
 const unauthorized=await call('/shipments/'+shipment.id,{token:freshToken});check('Unassigned clients cannot read a known shipment identifier',()=>assert.ok([403,404].includes(unauthorized.status)));
 const stale=await call('/shipments/'+shipment.id+'/transitions',{method:'POST',body:{version:0,status:'in-transit'},token});check('Stale shipment versions are rejected',()=>assert.equal(stale.status,409));
 const transit=await mutate('/shipments/'+shipment.id+'/transitions',{version:shipment.version,status:'in-transit'},token);shipment=transit.result;
 const cancel=await call('/shipments/'+shipment.id+'/transitions',{method:'POST',body:{version:shipment.version,status:'cancelled',note:'Not allowed'},token});check('An in-transit shipment cannot be cancelled',()=>assert.equal(cancel.status,400));
 const readingsBefore=Number(sql('SELECT count(*) FROM sensor_readings;'));
 const missingReading=await call('/shipments/'+shipment.id+'/readings',{method:'POST',body:{},token});
 const missingPosition=await call('/shipments/'+shipment.id+'/readings',{method:'POST',body:{temperature:4,humidity:82},token});
 check('Missing reading values or coordinates cannot become invented zero measurements',()=>{assert.equal(missingReading.status,400);assert.equal(missingPosition.status,400);assert.equal(Number(sql('SELECT count(*) FROM sensor_readings;')),readingsBefore);});
 const prestart=await call('/shipments/'+shipment.id+'/readings',{method:'POST',body:{temperature:4,humidity:82,lat:-12,lng:-77,at:'2000-01-01T00:00:00Z'},token});
 check('Readings before monitoring started are rejected without persistence',()=>{assert.equal(prestart.status,400);assert.equal(Number(sql('SELECT count(*) FROM sensor_readings;')),readingsBefore);});
 let reading=await mutate('/shipments/'+shipment.id+'/readings',{temperature:9,humidity:82,lat:-12.05,lng:-77.05},token);
 const alert=reading.state.alerts.find(a=>a.shipmentId===shipment.id&&!a.resolved);check('A persisted out-of-range reading raises an alert',()=>{assert.ok(alert);assert.ok(Number(sql('SELECT count(*) FROM sensor_readings;'))>0);});
 const acknowledgement=await mutate('/alerts/'+alert.id+'/corrective-actions',{note:'Inspect cooling equipment',notifyClient:true},token);
 check('A corrective action acknowledges without resolving the excursion',()=>{const a=acknowledgement.state.alerts.find(a=>a.id===alert.id);assert.equal(a.resolved,false);assert.equal(a.acknowledged,true);assert.ok(a.actions.some(x=>x.note==='Inspect cooling equipment'));});
 const humidity=await mutate('/shipments/'+shipment.id+'/readings',{temperature:4,humidity:99,lat:-12.05,lng:-77.05},token);
 check('Normal temperature alone does not resolve abnormal humidity',()=>assert.ok(humidity.state.alerts.some(a=>a.shipmentId===shipment.id&&!a.resolved)));
 const normal=await mutate('/shipments/'+shipment.id+'/readings',{temperature:4,humidity:82,lat:-12.05,lng:-77.05},token);
 check('A later reading normal in both ranges resolves thermal excursions',()=>assert.ok(normal.state.alerts.filter(a=>a.shipmentId===shipment.id).every(a=>a.resolved)));
 const readingCount=Number(sql('SELECT count(*) FROM sensor_readings;'));
 const staleAt=sql(`SELECT min(at) FROM sensor_readings WHERE monitoring_id=(SELECT id FROM shipment_monitorings WHERE shipment_id='${shipment.id}');`);
 const outOfOrder=await call('/shipments/'+shipment.id+'/readings',{method:'POST',body:{temperature:10,humidity:82,lat:-12,lng:-77,at:new Date(staleAt).toISOString()},token});
 const unchanged=await call('/workspace',{token});
 check('An older excursion cannot reopen alerts resolved by a newer measurement',()=>{assert.equal(outOfOrder.status,400);assert.equal(Number(sql('SELECT count(*) FROM sensor_readings;')),readingCount);assert.ok(unchanged.data.state.alerts.filter(a=>a.shipmentId===shipment.id).every(a=>a.resolved));});
 const beforeRead=Number(sql("SELECT count(*) FROM notifications WHERE recipient_id='cargo-1' AND read_at IS NULL;"));
 await call('/workspace',{token:clientToken});
 check('Reading the workspace does not mark notifications read',()=>assert.equal(Number(sql("SELECT count(*) FROM notifications WHERE recipient_id='cargo-1' AND read_at IS NULL;")),beforeRead));
 await mutate('/notifications/read',{},clientToken);
 check('Explicit notification read only updates the authenticated recipient',()=>assert.equal(sql("SELECT count(*) FROM notifications WHERE recipient_id='cargo-1' AND read_at IS NULL;"),'0'));
 const current=(await call('/workspace',{token})).data.state.shipments.find(x=>x.id===shipment.id);
 const delivered=await mutate('/shipments/'+shipment.id+'/transitions',{version:current.version,status:'delivered'},token);shipment=delivered.result;
 check('Delivery releases the persisted fleet reservation',()=>assert.equal(sql(`SELECT status FROM fleet_allocations WHERE shipment_id='${shipment.id}';`),'released'));
 const generated=await mutate('/shipments/'+shipment.id+'/reports',{},clientToken);const reportId=generated.result.id;
 check('An authorized client can create a persisted immutable report snapshot',()=>{assert.ok(reportId);assert.equal(sql(`SELECT count(*) FROM thermal_reports WHERE id='${reportId}';`),'1');});
 const snapshotBefore=await call('/reports/'+reportId,{token:clientToken});
 await mutate('/shipments/'+shipment.id+'/incidents',{note:'Evidence added after snapshot generation'},token);
 const snapshotAfter=await call('/reports/'+reportId,{token:clientToken});
 check('A generated report is not recomputed from later shipment edits',()=>assert.deepEqual(snapshotAfter.data,snapshotBefore.data));
 const foreignReport=await call('/reports/'+reportId,{token:freshToken});check('Report snapshots respect shipment assignment',()=>assert.ok([403,404].includes(foreignReport.status)));
 sql("INSERT INTO organizations(id,name) VALUES ('isolation-organization','Isolated organization'); INSERT INTO users(id,organization_id,name,email,company_name,role,language,enabled,password_hash,password_salt) SELECT 'isolation-coordinator','isolation-organization','Isolation reviewer','isolation@example.invalid','Isolated organization','coordinator','en',true,password_hash,password_salt FROM users WHERE id='coordinator-1';");
 const other=await call('/auth/login',{method:'POST',body:{email:'isolation@example.invalid',password}});
 check('A separate organization has an isolated projection',()=>{assert.equal(other.status,200);assert.equal(other.data.state.shipments.length,0);assert.equal(other.data.state.vehicles.length,0);});
 const crossTenant=await call('/shipments/'+shipment.id,{token:other.data.token});
 check('Even a coordinator cannot read another organization shipment',()=>assert.equal(crossTenant.status,404));
 const crossBooking=await call('/shipments',{method:'POST',body,token:other.data.token});
 check('Cross-organization resource assignment is rejected',()=>assert.equal(crossBooking.status,400));
 check('Passwords and bearer session tokens are not stored in clear text',()=>{assert.notEqual(sql("SELECT password_hash FROM users WHERE id='coordinator-1';"),password);assert.notEqual(sql("SELECT token_hash FROM auth_sessions LIMIT 1;"),token);});
 await stop();await start();
 const restored=await call('/workspace',{token});check('Data and sessions survive an API process restart',()=>{assert.equal(restored.status,200);assert.ok(restored.data.state.shipments.some(x=>x.id===shipment.id&&x.status==='delivered'));});
 const logout=await call('/auth/logout',{method:'POST',body:{},token:clientToken});const revoked=await call('/workspace',{token:clientToken});check('Logout persists bearer session revocation',()=>{assert.equal(logout.status,204);assert.equal(revoked.status,401);});
 const batchBooking=await mutate('/shipments',body,token);const batchId=batchBooking.result.id;await mutate('/shipments/'+batchId+'/transitions',{version:batchBooking.result.version,status:'in-transit'},token);
 const at=new Date().toISOString();const first={temperature:4,humidity:80,lat:-12,lng:-77,at};const badBatch=await call('/shipments/'+batchId+'/readings/batch',{method:'POST',body:{readings:[first,{...first,at:new Date(Date.parse(at)+1).toISOString(),lat:100}]},token});
 check('An invalid reading batch rolls back earlier readings and audit events',()=>{assert.equal(badBatch.status,400);assert.equal(sql(`SELECT count(*) FROM sensor_readings r JOIN shipment_monitorings m ON r.monitoring_id=m.id WHERE m.shipment_id='${batchId}';`),'0');});
 const acceptedBatch=await call('/shipments/'+batchId+'/readings/batch',{method:'POST',body:{readings:[first,{...first,at:new Date(Date.parse(at)+1).toISOString()}]},token});check('A valid ordered batch stores all readings atomically',()=>{assert.equal(acceptedBatch.status,200);assert.equal(acceptedBatch.data.result.accepted,2);});
 const forbiddenBatch=await call('/shipments/'+batchId+'/readings/batch',{method:'POST',body:{readings:[first]},token:freshToken});check('Cargo clients cannot submit telemetry batches',()=>assert.equal(forbiddenBatch.status,403));
 const month=new Date().toISOString().slice(0,7);
 const summary=await call('/analytics/monthly?month='+month,{token});
 const clientSummary=await call('/analytics/monthly?month='+month,{token:(await call('/auth/login',{method:'POST',body:{email:'cliente@friotrack.app',password:clientPassword}})).data.token});
 check('Monthly analytics aggregate the authorized departure month without invented readings',()=>{assert.equal(summary.status,200);assert.ok(summary.data.shipments>=1);assert.ok(summary.data.routes.every(r=>r.readings>0||r.averageTemperature===null));assert.ok(clientSummary.data.shipments<=summary.data.shipments);});
 const isolatedSummary=await call('/analytics/monthly?month='+month,{token:other.data.token});check('Another organization cannot see route analytics',()=>{assert.equal(isolatedSummary.status,200);assert.equal(isolatedSummary.data.shipments,0);});
 const invalidMonth=await call('/analytics/monthly?month=2026-13',{token});check('Invalid calendar months are rejected',()=>assert.equal(invalidMonth.status,400));
 const audit=await call('/audit?month='+month,{token});check('Operational audit preserves actor, timestamp and shipment scope',()=>{assert.equal(audit.status,200);assert.ok(audit.data.some(x=>x.shipmentId===shipment.id));assert.ok(audit.data.every(x=>x.at&&x.actor&&x.shipmentId));});
 const quarter=Math.floor((Number(month.slice(5))-1)/3)+1;const year=Number(month.slice(0,4));
 const quarterly=await call(`/analytics/quarterly?year=${year}&quarter=${quarter}`,{token});
 const quarterStart=new Date(Date.UTC(year,(quarter-1)*3,1)).toISOString();const quarterEnd=new Date(Date.UTC(year,quarter*3,1)).toISOString();
 const allShipments=(await call('/shipments',{token})).data;
 check('Quarterly analytics include exactly the authorized three-month departure interval',()=>{assert.equal(quarterly.status,200);assert.equal(quarterly.data.quarter,`${year}-Q${quarter}`);assert.equal(quarterly.data.shipments,allShipments.filter(s=>s.departure>=quarterStart&&s.departure<quarterEnd).length);});
 const isolatedQuarter=await call(`/analytics/quarterly?year=${year}&quarter=${quarter}`,{token:other.data.token});const badQuarter=await call(`/analytics/quarterly?year=${year}&quarter=5`,{token});
 check('Quarterly queries reject invalid periods and isolate organizations',()=>{assert.equal(isolatedQuarter.data.shipments,0);assert.equal(badQuarter.status,400);});
 check('Audit lists a corrective action once while preserving separate readings in one batch',()=>{assert.equal(audit.data.filter(x=>x.shipmentId===shipment.id&&x.action==='correctiveAction'&&x.note==='Inspect cooling equipment').length,1);assert.equal(audit.data.filter(x=>x.shipmentId===batchId&&x.action==='sampleReading').length,2);});
 const actorName=audit.data[0].actor;const filteredAudit=await call('/audit?'+new URLSearchParams({from:quarterStart,to:quarterEnd,actor:actorName}),{token});const unknownActor=await call('/audit?'+new URLSearchParams({month,actor:'Unknown participant'}),{token});const badAudit=await call('/audit?'+new URLSearchParams({from:quarterEnd,to:quarterStart}),{token});
 check('Audit filters users and exclusive UTC date intervals without exposing other actors',()=>{assert.equal(filteredAudit.status,200);assert.ok(filteredAudit.data.length>0);assert.ok(filteredAudit.data.every(x=>x.actor===actorName&&x.at>=quarterStart&&x.at<quarterEnd));assert.equal(unknownActor.data.length,0);assert.equal(badAudit.status,400);});
 const filteredShipments=await call('/shipments?'+new URLSearchParams({q:batchId,status:'in-transit',vehicleId:body.vehicleId,from:quarterStart,to:quarterEnd,active:'true'}),{token});
 const clientFiltered=await call('/shipments?'+new URLSearchParams({q:batchId}),{token:freshToken});const invalidFilters=await call('/shipments?status=unknown',{token});
 check('Shipment collection combines search, resource, state and date filters within access scope',()=>{assert.equal(filteredShipments.status,200);assert.deepEqual(filteredShipments.data.map(s=>s.id),[batchId]);assert.equal(clientFiltered.data.length,0);assert.equal(invalidFilters.status,400);});
 const inactiveVehicle=(await mutate('/vehicles',{plate:'DEL-'+nonce.slice(0,3),capacity:10,enabled:true},token)).result;
 const inactiveDriver=(await mutate('/drivers',{name:'Inactive driver',license:'DEL-'+nonce,enabled:true},token)).result;
 const inactiveSensor=(await mutate('/sensors',{serialCode:'DEL-'+nonce,enabled:true},token)).result;
 for(const [collection,resource] of [['vehicles',inactiveVehicle],['drivers',inactiveDriver],['sensors',inactiveSensor]]){
   const disabled=await call('/'+collection+'/'+resource.id,{method:'DELETE',token});const again=await call('/'+collection+'/'+resource.id,{method:'DELETE',token});const retained=await call('/'+collection+'/'+resource.id,{token});
   check('Logical DELETE of '+collection+' is idempotent and retains the resource',()=>{assert.equal(disabled.status,200);assert.equal(again.status,200);assert.equal(retained.status,200);assert.equal(retained.data.enabled,false);});
 }
 const reservedDelete=await call('/vehicles/'+body.vehicleId,{method:'DELETE',token});const clientDelete=await call('/drivers/'+inactiveDriver.id,{method:'DELETE',token:freshToken});const crossDelete=await call('/sensors/'+inactiveSensor.id,{method:'DELETE',token:other.data.token});
 check('Fleet deactivation rejects reserved resources, cargo clients and cross-organization identifiers',()=>{assert.equal(reservedDelete.status,409);assert.equal(clientDelete.status,403);assert.equal(crossDelete.status,404);});
 const pdf=await fetch(base+'/api/reports/monthly/pdf?month='+month,{headers:{Authorization:'Bearer '+token}});const pdfBytes=Buffer.from(await pdf.arrayBuffer());
 check('Monthly executive report is a real PDF with a download filename',()=>{assert.equal(pdf.status,200);assert.equal(pdf.headers.get('content-type'),'application/pdf');assert.equal(pdfBytes.subarray(0,5).toString(),'%PDF-');assert.match(pdf.headers.get('content-disposition'),/friotrack-monthly/);});
 await mkdir('tmp',{recursive:true});await writeFile('tmp/monthly-report-test.private.pdf',pdfBytes);
 const thermal=await fetch(base+'/api/reports/'+reportId+'/pdf',{headers:{Authorization:'Bearer '+token}});const thermalBytes=Buffer.from(await thermal.arrayBuffer());
 check('The persisted traceability snapshot can be downloaded as PDF',()=>{assert.equal(thermal.status,200);assert.equal(thermalBytes.subarray(0,5).toString(),'%PDF-');});
 const accounts=await call('/accounts',{token});const deniedAccounts=await call('/accounts',{token:freshToken});
 check('Account administration rejects cargo clients and hides password hashes',()=>{assert.equal(accounts.status,200);assert.equal(deniedAccounts.status,403);assert.ok(accounts.data.every(a=>a.email&&!a.passwordHash&&!a.passwordSalt));});
 const lockout=await call('/accounts/coordinator-1/access',{method:'PUT',body:{role:'cargo-client',enabled:false},token});check('A coordinator cannot remove their own access',()=>assert.equal(lockout.status,400));
 const contactBody={name:'Local tester',company:'Example team',email:'participant@example.invalid',phone:'+51 999 000 000',role:'buyer'};
 const contactStart=messages.length;const commercialContact=await call('/commercial-contacts',{method:'POST',body:contactBody});const invalidContact=await call('/commercial-contacts',{method:'POST',body:{...contactBody,role:'administrator'}});
 check('Commercial contact validates fields and delivers only to the fixed loopback mailbox',()=>{assert.equal(commercialContact.status,202);assert.equal(invalidContact.status,400);assert.equal(messages.length,contactStart+1);assert.match(messages.at(-1),/team@friotrack.example/);assert.match(messages.at(-1),/Reply-To: participant@example.invalid/i);});
 await call('/commercial-contacts',{method:'POST',body:contactBody});const limitedContact=await call('/commercial-contacts',{method:'POST',body:contactBody});check('Public commercial contact is rate limited separately from account authentication',()=>assert.equal(limitedContact.status,429));messages.length=0;
 const unknownRecovery=await call('/auth/forgot-password',{method:'POST',body:{email:'unknown@example.invalid'}});
 const recovery=await call('/auth/forgot-password',{method:'POST',body:{email:registration.data.profile.email}});
 check('Recovery returns the same response for existing and unknown accounts',()=>{assert.equal(recovery.status,202);assert.equal(unknownRecovery.status,202);assert.deepEqual(unknownRecovery.data,recovery.data);assert.equal(messages.length,1);});
 const mail=messages[0];const bodyText=mail.split('\r\n\r\n').slice(1).join('\r\n\r\n');const decoded=/Content-Transfer-Encoding: base64/i.test(mail)?Buffer.from(bodyText.replace(/\s/g,''),'base64').toString('utf8'):bodyText.replace(/=\r\n/g,'').replace(/=3D/gi,'=');
 const resetToken=decoded.match(/token=([A-F0-9]{64})/)[1];
 check('Recovery tokens are delivered through loopback SMTP and stored only as hashes',()=>assert.notEqual(sql('SELECT token_hash FROM password_reset_tokens LIMIT 1;'),resetToken));
 const resetPassword='Replacement!'+randomBytes(18).toString('hex');const reset=await call('/auth/reset-password',{method:'POST',body:{token:resetToken,password:resetPassword}});const repeated=await call('/auth/reset-password',{method:'POST',body:{token:resetToken,password:resetPassword}});
 const priorSession=await call('/workspace',{token:freshToken});
 check('A recovery token is single-use and revokes previous sessions',()=>{assert.equal(reset.status,204);assert.equal(repeated.status,400);assert.equal(priorSession.status,401);});
 const changed=await call('/auth/login',{method:'POST',body:{email:registration.data.profile.email,password:resetPassword}});const oldPassword=await call('/auth/login',{method:'POST',body:{email:registration.data.profile.email,password}});check('After recovery only the new password can start a session',()=>{assert.equal(changed.status,200);assert.equal(oldPassword.status,401);});
 const expiredToken=randomBytes(32).toString('hex').toUpperCase();const {createHash}=await import('node:crypto');const expiredDigest=createHash('sha256').update(expiredToken).digest('hex').toUpperCase();sql(`INSERT INTO password_reset_tokens(id,user_id,token_hash,expires_at) VALUES ('expired-recovery','${registration.data.profile.id}','${expiredDigest}',now()-interval '1 minute');`);
 const expiredReset=await call('/auth/reset-password',{method:'POST',body:{token:expiredToken,password:resetPassword}});check('Expired recovery tokens cannot change a password',()=>{assert.equal(expiredReset.status,400);assert.equal(expiredReset.data.code,'invalidResetToken');});
 const disabled=await call('/accounts/'+registration.data.profile.id+'/access',{method:'PUT',body:{role:'cargo-client',enabled:false},token});const disabledAccess=await call('/workspace',{token:changed.data.token});check('Disabling an organization account immediately revokes its sessions',()=>{assert.equal(disabled.status,204);assert.equal(disabledAccess.status,401);});
 const invalidRole=await call('/accounts/'+registration.data.profile.id+'/access',{method:'PUT',body:{role:'administrator',enabled:true},token});check('Unknown privileged roles are rejected',()=>assert.equal(invalidRole.status,403));
 const crossAccount=await call('/accounts/isolation-coordinator/access',{method:'PUT',body:{role:'cargo-client',enabled:true},token});check('Account administration cannot cross organization boundaries',()=>assert.equal(crossAccount.status,404));
 const anonSpec=await call('/swagger/v1/swagger.json');check('Scalar contract does not require a bearer token to request recovery',()=>assert.deepEqual(anonSpec.data.paths['/api/auth/forgot-password'].post.security,[]));
 apiEnvironment.FRIOTRACK_MAIL_HOST='';await stop();await start();const noMail=await call('/capabilities');const unavailableRecovery=await call('/auth/forgot-password',{method:'POST',body:{email:'coordinador@friotrack.app'}});check('Unconfigured email recovery is explicitly unavailable without claiming delivery',()=>{assert.equal(noMail.data.passwordRecovery,false);assert.equal(unavailableRecovery.status,503);assert.equal(unavailableRecovery.data.code,'recoveryUnavailable');});
 const unavailableContact=await call('/commercial-contacts',{method:'POST',body:contactBody});check('Unconfigured commercial delivery returns unavailable without a success claim',()=>{assert.equal(unavailableContact.status,503);assert.equal(unavailableContact.data.code,'contactUnavailable');});
 record.tables=sql("SELECT string_agg(table_name,',' ORDER BY table_name) FROM information_schema.tables WHERE table_schema='public' AND table_name <> '__EFMigrationsHistory';").split(',');
 record.status='passed';
} catch(error){record.status='failed';record.failures.push(error.message);console.error('FAIL '+error.message);process.exitCode=1;}
finally{
 await new Promise(r=>smtp.close(r));
 await stop();await mkdir('tmp',{recursive:true});
 // Keep startup diagnostics outside source control; never serialize environment secrets.
 await writeFile('tmp/postgres-server.private.log',diagnostic);
 if(created){try{sql(`SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='${database}' AND pid<>pg_backend_pid();`,'postgres');sql(`DROP DATABASE "${database}";`,'postgres');}catch{record.cleanup='Own temporary database cleanup requires retry';process.exitCode=1;}}
 record.completedAt=new Date().toISOString();record.passed=record.checks.length;
 await writeFile('tmp/postgres-api-verification.json',JSON.stringify(record,null,2)+'\n');console.log(JSON.stringify({status:record.status,passed:record.passed,failures:record.failures}));
}
