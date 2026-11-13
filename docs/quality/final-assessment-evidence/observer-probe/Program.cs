using System.Reflection;
using System.Text.Json;
using MySqlConnector;

var evidence=Path.GetFullPath("docs/quality/final-assessment-evidence");
var i=JsonDocument.Parse(File.ReadAllText(Path.Combine(evidence,"instance.json")).TrimStart('\uFEFF')).RootElement;
var run=i.GetProperty("Run").GetString()!;var port=i.GetProperty("Port").GetInt32();
if(!i.GetProperty("New").GetBoolean()||port!=13449||!Path.GetFullPath(run).StartsWith(Path.GetFullPath(".staff-private/instance-"),StringComparison.OrdinalIgnoreCase))throw new Exception("Target guard");
var cs=new MySqlConnectionStringBuilder{Server="127.0.0.1",Port=(uint)port,UserID="root",Password=File.ReadAllText(run+"/password.txt"),Database="electronic_register_test",SslMode=MySqlSslMode.None,Pooling=false}.ConnectionString;
await using var blocker=new MySqlConnection(cs);await blocker.OpenAsync();
await using(var identity=new MySqlCommand("SELECT VERSION(),@@port,REPLACE(@@datadir,CHAR(92),'/')",blocker)){
 await using var r=await identity.ExecuteReaderAsync();await r.ReadAsync();
 if(r.GetString(0)!="5.7.24"||r.GetInt32(1)!=port||!r.GetString(2).TrimEnd('/').Equals((run+"/data").Replace('\\','/'),StringComparison.OrdinalIgnoreCase))throw new Exception("Identity mismatch");}
await using var tx=await blocker.BeginTransactionAsync();
await using(var locked=new MySqlCommand("SELECT version FROM er_schema_release WHERE version='er-quality-1' FOR UPDATE",blocker,tx))await locked.ExecuteScalarAsync();
await using var unrelated=new MySqlConnection(cs);await unrelated.OpenAsync();
await using var waiter=new MySqlCommand("SELECT version FROM er_schema_release WHERE version='er-quality-1' FOR UPDATE",unrelated){CommandTimeout=20};
var waiting=waiter.ExecuteScalarAsync();
var type=typeof(ElectronicRegisterAPI.Tests.Integration.InstitutionClassCrudTests).Assembly.GetType("ElectronicRegisterAPI.Tests.Integration.SqlLockEvidence")!;
var method=type.GetMethod("WaitAsync",BindingFlags.NonPublic|BindingFlags.Static,null,new[]{typeof(string),typeof(Task),typeof(long)},null)!;
bool accepted=false;
// The actual contender has already completed. Only an unrelated waiter exists.
// Poll actual InnoDB edges first, not a sleep-based inference.
await using var observer=new MySqlConnection(cs);await observer.OpenAsync();
try{
 var watch=System.Diagnostics.Stopwatch.StartNew();
 while(true){
  await using var check=new MySqlCommand("SELECT COUNT(*) FROM information_schema.INNODB_LOCK_WAITS w JOIN information_schema.INNODB_TRX b ON b.trx_id=w.blocking_trx_id WHERE b.trx_mysql_thread_id=@id",observer);check.Parameters.AddWithValue("@id",blocker.ServerThread);
  if(Convert.ToInt64(await check.ExecuteScalarAsync())>0)break;
  if(watch.Elapsed>TimeSpan.FromSeconds(15))throw new Exception("Missing real wait");await Task.Delay(150);
 }
 await (Task)method.Invoke(null,new object[]{cs,Task.CompletedTask,(long)blocker.ServerThread})!;
 accepted=true;
}finally{await tx.RollbackAsync();await waiting;}
File.WriteAllText(Path.Combine(evidence,"observer-negative-control.json"),JsonSerializer.Serialize(new{CompletedContender=true,UnrelatedWaiter=true,ActualInnoDbEdge=true,ObserverAccepted=accepted,ExpectedAcceptance=false},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine("Real SqlLockEvidence accepted an already completed contender because an unrelated waiter shared its blocker.");
