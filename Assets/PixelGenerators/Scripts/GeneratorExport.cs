using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
namespace BetweenPoles.Generators {
 // Incremental export: one frame per editor/player tick, with cancellable file output.
 public sealed class GeneratorExport : IDisposable {
  readonly PixelGeneratorLab lab;readonly string kind;readonly float savedPhase;
  readonly int count,columns,margin;int index;Texture2D sheet;BinaryWriter gif;
  string partial;bool disposed;public string OutputPath {get;private set;}
  public string Progress {get{return index+" / "+count;}}
  public GeneratorExport(PixelGeneratorLab owner,string format){
   lab=owner;kind=format;savedPhase=lab.phase;count=kind=="png"?1:Mathf.Clamp(lab.frames,2,600);columns=Mathf.Clamp(lab.columns,1,100);margin=Mathf.Clamp(lab.margin,0,32);
   int w=lab.spaceMode?lab.width:Mathf.RoundToInt(lab.pixels*lab.catalog.models[lab.model].relativeScale),h=lab.spaceMode?lab.height:w;
   if((long)w*h>16000000)throw new InvalidOperationException("单帧过大，请降低像素精度或输出尺寸。");
   if(kind=="sheet"){
    int rows=(count+columns-1)/columns;long sw=(long)columns*(w+margin)+margin,sh=(long)rows*(h+margin)+margin;
    if(sw>SystemInfo.maxTextureSize||sh>SystemInfo.maxTextureSize||sw*sh>64000000)throw new InvalidOperationException("图集过大，请减少帧数、列数或像素精度（最多 6400 万像素）。");
    sheet=new Texture2D((int)sw,(int)sh,TextureFormat.RGBA32,false);sheet.SetPixels32(new Color32[(int)(sw*sh)]);
   }
   Directory.CreateDirectory(lab.ExportDirectory);OutputPath=Path.Combine(lab.ExportDirectory,(lab.spaceMode?"太空":lab.catalog.models[lab.model].name)+"_"+DateTime.Now.ToString("yyyyMMdd_HHmmss_fff")+"_"+Guid.NewGuid().ToString("N").Substring(0,4)+(kind=="gif"?".gif":kind=="sheet"?"_图集.png":".png"));partial=OutputPath+".partial";
   if(kind=="gif"){gif=new BinaryWriter(new FileStream(partial,FileMode.CreateNew));gif.Write(System.Text.Encoding.ASCII.GetBytes("GIF89a"));gif.Write((ushort)w);gif.Write((ushort)h);gif.Write((byte)0x70);gif.Write((byte)0);gif.Write((byte)0);gif.Write(new byte[]{0x21,0xff,11});gif.Write(System.Text.Encoding.ASCII.GetBytes("NETSCAPE2.0"));gif.Write(new byte[]{3,1,0,0,0});}
  }
  public bool Step(){lab.phase=savedPhase+index*lab.duration*lab.speed/count;lab.exportCycle=!lab.spaceMode&&kind!="png"?(float)index/count:-1;Texture2D image=lab.Capture();try{
   if(kind=="png")File.WriteAllBytes(OutputPath,image.EncodeToPNG());
   else if(kind=="sheet"){int col=index%columns,row=index/columns;sheet.SetPixels32(margin+col*(image.width+margin),sheet.height-margin-(row+1)*image.height-row*margin,image.width,image.height,image.GetPixels32());}
   else WriteGifFrame(image,Math.Max(1,Mathf.RoundToInt(lab.duration/count*100)));
  }finally{PixelGeneratorLab.Release(image);}index++;
   if(index<count)return false;
   if(kind=="sheet"){sheet.Apply();File.WriteAllBytes(OutputPath,sheet.EncodeToPNG());}
   if(gif!=null){gif.Write((byte)0x3b);gif.Dispose();gif=null;File.Move(partial,OutputPath);}
   return true;
  }
  static int Bin(Color32 c){return (c.r>>3)<<10|(c.g>>3)<<5|(c.b>>3);}
  void WriteGifFrame(Texture2D image,int delay){
   var data=image.GetPixels32();var hist=new Dictionary<int,int>();foreach(var c in data){if(c.a<128)continue;int bin=Bin(c);int n;hist.TryGetValue(bin,out n);hist[bin]=n+1;}
   var bins=hist.OrderByDescending(p=>p.Value).Take(255).Select(p=>p.Key).ToArray();var palette=new Color32[256];
   for(int i=0;i<bins.Length;i++){int b=bins[i];palette[i]=new Color32((byte)(((b>>10)&31)*255/31),(byte)(((b>>5)&31)*255/31),(byte)((b&31)*255/31),255);}
   var lookup=new Dictionary<int,byte>();foreach(int b in hist.Keys){int r=((b>>10)&31)*255/31,g=((b>>5)&31)*255/31,bl=(b&31)*255/31,best=0;int distance=int.MaxValue;for(int i=0;i<bins.Length;i++){var c=palette[i];int d=(r-c.r)*(r-c.r)+(g-c.g)*(g-c.g)+(bl-c.b)*(bl-c.b);if(d<distance){distance=d;best=i;}}lookup[b]=(byte)best;}
   gif.Write(new byte[]{0x21,0xf9,4,9});gif.Write((ushort)Math.Min(65535,delay));gif.Write((byte)255);gif.Write((byte)0);
   gif.Write((byte)0x2c);gif.Write((ushort)0);gif.Write((ushort)0);gif.Write((ushort)image.width);gif.Write((ushort)image.height);gif.Write((byte)0x87);
   foreach(var c in palette){gif.Write(c.r);gif.Write(c.g);gif.Write(c.b);}gif.Write((byte)8);
   // Clear before the dictionary reaches 512 entries, keeping all codes at nine bits.
   // This trades compression ratio for a small, dependency-free and deterministic encoder.
   using(var stream=new MemoryStream()){
    int accumulator=0,bits=0,run=0;Action<int> code=n=>{accumulator|=n<<bits;bits+=9;while(bits>=8){stream.WriteByte((byte)(accumulator&255));accumulator>>=8;bits-=8;}};
    code(256);for(int y=image.height-1;y>=0;y--)for(int x=0;x<image.width;x++){if(run==200){code(256);run=0;}var c=data[y*image.width+x];code(c.a<128?255:lookup[Bin(c)]);run++;}code(257);if(bits>0)stream.WriteByte((byte)accumulator);
    byte[] bytes=stream.ToArray();for(int i=0;i<bytes.Length;i+=255){int n=Math.Min(255,bytes.Length-i);gif.Write((byte)n);gif.Write(bytes,i,n);}gif.Write((byte)0);
   }
  }
  public void Cancel(){Dispose();}
  public void Dispose(){if(disposed)return;disposed=true;if(gif!=null){gif.Dispose();gif=null;}PixelGeneratorLab.Release(sheet);sheet=null;lab.phase=savedPhase;lab.exportCycle=-1;lab.Apply();}
 }
}
