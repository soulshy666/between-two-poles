const fs=require('fs'),vm=require('vm'),path=require('path');
const html=fs.readFileSync(process.argv[2],'utf8');
const data=vm.runInNewContext(html.match(/<script>([\s\S]*?)<\/script>/)[1]+'\nAstronautDesign;');
fs.writeFileSync(path.join(__dirname,'unity-geometry.json'),JSON.stringify(data));
console.log('Exported '+data.parts.length+' authored parts without coordinate conversion.');
