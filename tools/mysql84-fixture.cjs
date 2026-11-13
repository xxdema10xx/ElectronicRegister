'use strict';
// A fixture-only binding, never enabled in ordinary candidate/staging bundles.
// The C# capability is issued only after that process creates and attests a new
// container; the original database cannot be registered by connection strings.
function bind(sql,config){
 const f=config.ownedFixture;if(!f)return sql;
 if(config.database!=='electronic_register_test'||config.host!=='127.0.0.1'||![f.uuid,f.host,f.capability].every(x=>typeof x==='string')||
  !/^[a-f0-9]{8}(?:-[a-f0-9]{4}){3}-[a-f0-9]{12}$/.test(f.uuid)||!/^[a-f0-9]{12}$/.test(f.host)||!/^[A-F0-9]{64}$/.test(f.capability)||
  !Number.isInteger(config.port)||config.port<1024||config.port>65535||[3306,3307,13306].includes(config.port))throw Error('Owned fixture binding rejected');
 if(!sql.includes('@er_mt5b_fixture_owners'))return sql;
 const predicate=`VERSION()='8.4.13' AND @@port=3306 AND BINARY @@server_uuid='${f.uuid}' AND BINARY @@hostname='${f.host}' AND BINARY @er_mt5b_fixture_capability='${f.capability}'`;
 return sql.replace(/VERSION\(\)='5\.7\.24'\s+AND\s+@@port=13306/g,predicate)
  .replaceAll("RIGHT(LOWER(REPLACE(@@datadir,CHAR(92),'/')),45)='/migrations/mt4/private/mt5a-mamp-13306/data/'","BINARY @@datadir='/var/lib/mysql/'");
}
module.exports={bind};
