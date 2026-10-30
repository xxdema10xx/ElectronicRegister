const {execute,root}=require('./sql.cjs'),{contract}=require(root+'/tools/release-additive-contract.cjs'),fs=require('fs');
const differences=[];for(const [table,expected] of Object.entries(contract.tables)){const actual=execute('SHOW CREATE TABLE `'+table+'`').split('\t').slice(1).join('\t');if(actual.replaceAll('\r\n','\n')!==expected.replaceAll('\r\n','\n'))differences.push({table,expected,actual});}
fs.writeFileSync(__dirname+'/diagnostic-additive-shapes.json',JSON.stringify(differences,null,2));console.log(differences.map(d=>d.table));
