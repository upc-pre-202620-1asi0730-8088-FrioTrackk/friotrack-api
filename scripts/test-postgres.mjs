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
const apiEnvironment={...process.env,FRIOTRACK_PERSISTENCE:'postgres',FRIOTRACK_DB:dbConnection,FRIOTRACK_MIGRATE:'true',FRIOTRACK_SEED:'true',FRIOTRACK_COORDINATOR_PASSWORD:password,FRIOTRACK_CLIENT_PASSWORD:clientPassword,FRIOTRACK_ORIGINS:'http://127.0.0.1:5173'};
const record={scope:'Isolated real PostgreSQL API integration; not cloud deployment or team acceptance',startedAt:new Date().toISOString(),checks:[],failures:[]};
let server,diagnostic='',created=false;
const check=(name,run)=>{run();record.checks.push(name);console.log('PASS '+name);};
async function start(){
  diagnostic='';server=spawn(process.env.DOTNET_EXE||'dotnet',[path.resolve('output/api-publish/FrioTrack.Api.dll'),'--urls',base],{cwd:path.resolve('output/api-publish'),env:apiEnvironment,windowsHide:true,stdio:['ignore','pipe','pipe']});
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
 const response=await fetch(base+(route.startsWith('/swagger/')?'':'/api')+route,{method,signal:AbortSignal.timeout(15000),headers:{...(body===undefined?{}:{'Content-Type':'application/json'}),...(token?{Authorization:'Bearer '+token}:{}),...(language?{'Accept-Language':language}:{})},...(body===undefined?{}:{body:JSON.stringify(body)})});
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
 record.tables=sql("SELECT string_agg(table_name,',' ORDER BY table_name) FROM information_schema.tables WHERE table_schema='public' AND table_name <> '__EFMigrationsHistory';").split(',');
 record.status='passed';
} catch(error){record.status='failed';record.failures.push(error.message);console.error('FAIL '+error.message);process.exitCode=1;}
finally{
 await stop();await mkdir('tmp',{recursive:true});
 // Keep startup diagnostics outside source control; never serialize environment secrets.
 await writeFile('tmp/postgres-server.private.log',diagnostic);
 if(created){try{sql(`SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='${database}' AND pid<>pg_backend_pid();`,'postgres');sql(`DROP DATABASE "${database}";`,'postgres');}catch{record.cleanup='Own temporary database cleanup requires retry';process.exitCode=1;}}
 record.completedAt=new Date().toISOString();record.passed=record.checks.length;
 await writeFile('tmp/postgres-api-verification.json',JSON.stringify(record,null,2)+'\n');console.log(JSON.stringify({status:record.status,passed:record.passed,failures:record.failures}));
}
