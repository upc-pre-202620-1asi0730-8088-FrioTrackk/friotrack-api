import assert from 'node:assert/strict';
import { spawn } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { mkdir, readFile, writeFile, unlink } from 'node:fs/promises';
import path from 'node:path';
import net from 'node:net';

if (process.argv.length > 2) throw new Error('This suite runs locally; no remote deployment mode is supported.');
const coordinatorPassword = 'LocalCoordinator!' + randomBytes(12).toString('hex');
const clientPassword = 'LocalCargoClient!' + randomBytes(12).toString('hex');
const probe = net.createServer();
await new Promise((resolve, reject) => probe.once('error', reject).listen(0, '127.0.0.1', resolve));
const port = probe.address().port;
await new Promise(resolve => probe.close(resolve));
const host = 'http://127.0.0.1:' + port;
const url = host + '/api';
const dataFile = path.resolve('tmp/api-test-'+randomBytes(6).toString('hex')+'.json');
await mkdir('tmp',{recursive:true});
let server;
 server=spawn('dotnet',[path.resolve('output/api-publish/FrioTrack.Api.dll'),'--urls',host],{cwd:path.resolve('output/api-publish'),env:{...process.env,FRIOTRACK_PERSISTENCE:'json',FRIOTRACK_DATA_FILE:dataFile,FRIOTRACK_COORDINATOR_PASSWORD:coordinatorPassword,FRIOTRACK_CLIENT_PASSWORD:clientPassword},stdio:['ignore','ignore','pipe'],windowsHide:true});
let stderr='';server?.stderr.on('data',d=>stderr+=d);
let passed=0;
async function call(route, body, token) {
 const response=await fetch(url+route,{signal:AbortSignal.timeout(10000),method:body===undefined?'GET':'POST',headers:{...(body===undefined?{}:{'Content-Type':'application/json'}),...(token?{Authorization:'Bearer '+token}:{})},...(body===undefined?{}:{body:JSON.stringify(body)})});
 return {status:response.status,data:response.status===204?null:await response.json().catch(()=>null)};
}
function check(name, action) { action();passed++;console.log('PASS '+name); }
try {
 for(let i=0;i<40;i++){try{if((await call('/health')).status===200)break;}catch{}if(i===39)throw Error('API did not start: '+stderr);await new Promise(r=>setTimeout(r,500));}
  {
  const localOrigin=await fetch(url+'/health',{headers:{Origin:'http://127.0.0.1:5173'}});
  check('Vite development origin is allowed',()=>assert.equal(localOrigin.headers.get('access-control-allow-origin'),'http://127.0.0.1:5173'));
  const foreignOrigin=await fetch(url+'/health',{headers:{Origin:'https://untrusted.example'}});
  check('unknown origin receives no CORS permission',()=>assert.equal(foreignOrigin.headers.get('access-control-allow-origin'),null));
 }
 const guest=await call('/workspace');check('workspace requires authentication',()=>assert.equal(guest.status,401));
 const bad=await call('/auth/login',{email:'coordinador@friotrack.app',password:'incorrect-password'});check('wrong password is rejected',()=>assert.equal(bad.status,401));
 const coordinator=await call('/auth/login',{email:'coordinador@friotrack.app',password:coordinatorPassword});
 check('coordinator login with server role',()=>{assert.equal(coordinator.status,200);assert.equal(coordinator.data.profile.role,'coordinator');assert.ok(coordinator.data.token);});
 const client=await call('/auth/login',{email:'cliente@friotrack.app',password:clientPassword,role:'coordinator'});
 check('client cannot select coordinator role',()=>{assert.equal(client.status,200);assert.equal(client.data.profile.role,'cargo-client');});
 check('client response contains only its shipments and profile',()=>{assert.ok(client.data.state.shipments.length>0);assert.ok(client.data.state.shipments.every(s=>s.customerId===client.data.profile.id));assert.equal(client.data.state.profiles.length,1);assert.ok(!JSON.stringify(client.data).includes('password'));assert.ok(!client.data.state.shipments.some(s=>s.id==='FT-0003'));});
 const refresh=await call('/workspace',undefined,client.data.token);check('session can restore workspace',()=>assert.equal(refresh.data.profile.id,'cargo-1'));
 const forged=await call('/workspace',undefined,'forged-token');check('forged token is rejected',()=>assert.equal(forged.status,401));
 const attack=await call('/commands',{type:'createShipment',payload:{role:'coordinator'}},client.data.token);check('client management operation forbidden on server',()=>assert.equal(attack.status,403));
 const roleAttack=await call('/commands',{type:'updateProfile',payload:{...client.data.profile,role:'coordinator'}},client.data.token);check('profile update cannot escalate role',()=>assert.equal(roleAttack.data.profile.role,'cargo-client'));
 const adminRegistration=await call('/auth/register',{name:'Test',email:'test-admin@example.invalid',organization:'Test',password:'RegistrationPass!123',role:'coordinator',consent:true});check('public registration cannot create coordinators',()=>assert.equal(adminRegistration.status,403));
  {
  const weak=await call('/auth/register',{password:'weak'});check('weak password rejected',()=>assert.equal(weak.data.code,'invalidPassword'));
  const account={name:'Registered Client',email:'registered@example.invalid',organization:'Registered Organization',password:'RegistrationPass!123',consent:true};
  const registration=await call('/auth/register',account);check('client registration creates authenticated account',()=>{assert.equal(registration.status,200);assert.equal(registration.data.profile.role,'cargo-client');assert.equal(registration.data.state.shipments.length,0);});
  const duplicate=await call('/auth/register',account);check('duplicate registration rejected',()=>assert.equal(duplicate.data.code,'duplicateProfile'));
  const p={cargo:'Test cargo',weight:3,origin:'Lima',destination:'Ica',customerId:'cargo-1',vehicleId:'vehicle-1',driverId:'driver-1',minTemp:2,maxTemp:6,minHumidity:70,maxHumidity:95,departure:new Date(Date.now()+86400000).toISOString(),arrival:new Date(Date.now()+172800000).toISOString()};
  const create=await call('/commands',{type:'createShipment',payload:p},coordinator.data.token);check('coordinator creates validated shipment',()=>{assert.equal(create.status,200);assert.equal(create.data.result.status,'scheduled');});
  const overlap=await call('/commands',{type:'createShipment',payload:p},coordinator.data.token);check('API rejects overlapping vehicle assignment',()=>assert.equal(overlap.data.code,'vehicleBusy'));
  const shipment=create.data.result;
  const stale=await call('/commands',{type:'transition',payload:{id:shipment.id,version:0,status:'in-transit'}},coordinator.data.token);check('stale version rejected',()=>assert.equal(stale.data.code,'staleShipment'));
  const transit=await call('/commands',{type:'transition',payload:{id:shipment.id,version:shipment.version,status:'in-transit'}},coordinator.data.token);check('shipment transition works',()=>assert.equal(transit.data.result.status,'in-transit'));
  const reading=await call('/commands',{type:'addReading',payload:{id:shipment.id,temperature:8,humidity:82,lat:-12.05,lng:-77.05}},coordinator.data.token);check('out of range reading creates alert',()=>assert.ok(reading.data.state.alerts.some(a=>a.shipmentId===shipment.id&&!a.resolved)));
  const normal=await call('/commands',{type:'addReading',payload:{id:shipment.id,temperature:4,humidity:82,lat:-12.05,lng:-77.05}},coordinator.data.token);check('normal reading resolves excursion',()=>assert.ok(normal.data.state.alerts.filter(a=>a.shipmentId===shipment.id).every(a=>a.resolved)));
  const fresh=await call('/workspace',undefined,client.data.token);check('client sees new shared shipment',()=>assert.ok(fresh.data.state.shipments.some(s=>s.id===shipment.id)));
  const disk=JSON.parse(await readFile(dataFile,'utf8'));check('passwords and session tokens not stored in clear text',()=>{const text=JSON.stringify(disk);assert.ok(!text.includes(coordinatorPassword));assert.ok(!text.includes(clientPassword));assert.ok(!text.includes(coordinator.data.token));assert.ok(disk.credentials['coordinator-1'].salt);});
 }
 const logout=await call('/auth/logout',{},client.data.token);check('logout revokes server session',()=>assert.equal(logout.status,204));
 const revoked=await call('/workspace',undefined,client.data.token);check('revoked session cannot access workspace',()=>assert.equal(revoked.status,401));
 await call('/auth/logout',{},coordinator.data.token);
 {let throttled=false;for(let i=0;i<20;i++){const r=await call('/auth/login',{email:'unknown@example.invalid',password:'incorrect'});if(r.status===429){throttled=true;break;}}check('login is rate limited',()=>assert.ok(throttled));}
 await writeFile('tmp/local-api-verification.json',JSON.stringify({verifiedAt:new Date().toISOString(),api:url,passed,scope:'local authentication, registration, persistence and shipment commands'},null,2));
 console.log(passed+' API checks passed.');
} finally {server?.kill();{await new Promise(r=>setTimeout(r,500));await unlink(dataFile).catch(()=>{});}}
