from pathlib import Path
from copy import deepcopy
import hashlib,json
from docx import Document
from docx.shared import Cm,Pt,RGBColor
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parent
SRC=Path('D:/unity/资源/自制资源/Between Two Poles/两极之间 最新规则图解策划案修正.docx')
OUT=SRC.parent/'两极之间 最新规则图解策划案 2026年10月11日 修订3.docx'
d=Document(SRC)
ROOT.joinpath('artifact.md').write_text(f'''# 更新合同
Reference: {SRC}
SHA256: {hashlib.sha256(SRC.read_bytes()).hexdigest()}
Reference render: reference-render, 10 pages, A4 portrait, one section.
Preserve source styles, section, footer, existing combinations and figures except outdated passage figure.
Microsoft YaHei 11pt body, Title 25pt, Heading1 18pt, Heading2 12.5pt, Caption 9pt. Margins 1.7/1.6/2/2 cm.
Edit body paragraph slots by original indices; preserve table format. Insert five themed pages before former section10, rename it15.
Correct layer definition, bridge junctions, portal landing exceptions. Add height, portal sequence, exit assembly, repel/bounce/return, rocks/testing.
New figures use existing red/blue schematic palette. No game source modifications.
Native Word export substitutes unavailable LibreOffice; packaged render_docx.py rasterization retained.
''',encoding='utf-8')

FONT='C:/Windows/Fonts/msyh.ttc'
def fig(name,h=390):
 im=Image.new('RGB',(1400,h),'#f3f8fb');return im,ImageDraw.Draw(im)
def txt(g,p,s,size=25,color='#172d3e'):g.text(p,s,font=ImageFont.truetype(FONT,size),fill=color)
def arrow(g,a,b,c='#233e52'):
 import math
 g.line([a,b],fill=c,width=5);v=math.atan2(b[1]-a[1],b[0]-a[0]);g.polygon([b,(b[0]-18*math.cos(v-.5),b[1]-18*math.sin(v-.5)),(b[0]-18*math.cos(v+.5),b[1]-18*math.sin(v+.5))],fill=c)
def hole(g,x,y):
 g.ellipse((x-36,y-36,x+36,y+36),fill='#36224c',outline='#9872cc',width=7)
 g.arc((x-21,y-21,x+21,y+21),20,310,fill='#c3a4ee',width=5)
def bar(g,x,y,vertical=False,red=True,wide=False):
 col='#e9485e' if red else '#268eb8'
 g.rectangle((x-12,y-55,x+12,y+55) if vertical else (x-55,y-12,x+55,y+12),fill=col)
 if wide:g.rectangle((x+13,y-55,x+36,y+55) if vertical else (x-55,y+13,x+55,y+36),fill='#268eb8')
def person(g,x,y):
 g.ellipse((x-13,y-32,x+13,y-6),fill='#bfa972');g.line((x,y-5,x,y+23),fill='#4d5a61',width=10)
def save(im,name):
 p=ROOT/(name+'.png');im.save(p);return p

im,g=fig('junction',400)
txt(g,(35,20),'宽条仍须两端接通   有效横桥可作为支桥的接点',30)
for x in (135,315):g.rectangle((x,105,x+70,175),fill='#dbe9ee',outline='#aabfc9',width=2)
bar(g,260,140,wide=True);g.rectangle((225,303,295,375),fill='#dbe9ee');bar(g,260,248,True,True,True)
g.line((247,176,247,193),fill='#ddb447',width=5);g.line((274,176,274,193),fill='#ddb447',width=5)
arrow(g,(327,210),(327,300));txt(g,(475,115),'横桥先独立连接两岸',26);txt(g,(475,165),'支桥一端接横桥中部  另一端接岸',26)
txt(g,(475,215),'等高并通过连通判定后  玩家与磁铁可转入支桥',26)
txt(g,(475,275),'接缝用两条电磁波连接  与磁铁同一水平高度',26)
junction=save(im,'junction')

im,g=fig('height',330)
g.line((65,260,1290,260),fill='#68889b',width=3);g.rectangle((160,225,300,260),fill='#268eb8');g.polygon([(380,260),(395,179),(445,166),(510,178),(530,260)],fill='#718286')
g.rectangle((730,110,1040,260),fill='#bbd4df');txt(g,(744,130),'一层平台 H',29);person(g,440,147)
arrow(g,(535,153),(710,76));txt(g,(65,22),'H = 格子边长的一半     2H = 一个格子的边长',31)
txt(g,(100,278),'平躺磁铁 / 矮石头顶面',25);txt(g,(695,278),'站在物体上后  自动蹬腿跳上相邻一层平台',24)
height=save(im,'height')

im,g=fig('sequence',345)
for x,t in [(60,'① 磁铁先入洞'),(420,'② 玩家跳入并选择'),(920,'③ 磁铁先出 玩家后出')]:txt(g,(x,24),t,27)
bar(g,115,172);arrow(g,(175,172),(250,172));hole(g,300,172)
hole(g,530,205);person(g,495,111);arrow(g,(535,102),(545,165));hole(g,760,205)
hole(g,985,205);bar(g,1190,205);arrow(g,(1035,180),(1120,180));person(g,1120,98)
txt(g,(65,282),'进入方向保留到目标出口  向右进入就从目标右侧出洞',26)
sequence=save(im,'sequence')

im,g=fig('assembly',390)
txt(g,(42,18),'异极平躺条形长轴垂直  按出口接收格支撑决定',29)
for y,label,gap in [(132,'岸外无支撑 → 十字',True),(280,'陆地有支撑 → 宽条',False)]:
 txt(g,(45,y-35),label,27);hole(g,480,y);bar(g,620,y,False,False);txt(g,(710,y-20),'+',32);bar(g,825,y,True)
 arrow(g,(910,y),(1020,y))
 if gap:bar(g,1160,y,True);bar(g,1160,y,False,False)
 else:bar(g,1160,y,True,True,True)
 person(g,1150,y-55)
assembly=save(im,'assembly')

im,g=fig('obstacles',430)
txt(g,(45,18),'同极挤占',30);hole(g,115,125);bar(g,280,125,True);arrow(g,(340,125),(450,125));bar(g,525,125);person(g,280,64)
txt(g,(650,80),'出口同极磁铁保持姿态向外平移一格',26);txt(g,(650,125),'传送磁铁落入腾出的格子  玩家随后落下',26)
txt(g,(45,224),'碰石反弹',30);hole(g,115,335);g.polygon([(360,375),(370,300),(445,280),(480,375)],fill='#718286');bar(g,275,325)
arrow(g,(230,315),(329,315));arrow(g,(315,358),(160,358));person(g,421,258)
txt(g,(650,285),'磁铁撞石后立即反向回洞  从原入口返回',26);txt(g,(650,333),'磁铁返回完毕后  玩家从目标洞抛出落在石顶',26)
obstacles=save(im,'obstacles')

repl={
26:'宽条形成不等于通路成立。两端须接通有效支撑；一般沿长轴通过，已连通横桥承接支桥时可在有效接点转向，详见第 8 节。十字不作为普通跨岛桥。',
2:'程序与美术共用　｜　2026 年 10 月 11 日　修订 3',
4:'太空浮岛上的格子解谜游戏。玩家通过站位、推动、翻面、组合与黑洞传送，把磁铁变成通路或升降装置，前往新的岛屿与高处。没有主动跳跃按键；入洞、出洞及从物体顶面上平台时会自动播放跳跃动作。',
9:'第 2 至 7 节保留磁铁组合规则；第 8 至 9 节更新通路与十字；第 10 至 14 节新增高度、黑洞出口、石头及测试工具；第 15 节用于验收与关卡规划。配图表达规则，不作为建模尺寸。',
39:'U 型平躺作底座，长条在上方竖直，合成后持续产生磁流。玩家由同层侧面进入可升高 H，也可从高处下降。出口须与相邻地面衔接，不穿过长条实体。H 改为一层高度，即格子边长的一半；详见第 10 节。',
49:'第二根必须是平躺条形，按剩余接口配方补齐，不触发与第一根侧梁的普通同极排斥。当前补料接口接受任意极性；首次条形与 U 型组合仍须异极。补齐后固定在原锚点，不可拆开或整体推动。',
43:'标准材料为一个 U 型与两根平躺条形。先用异极条形与 U 型形成半成品，再用第二根平躺条形补齐另一接口。第二根的极性按当前补料接口规则处理，见本页下文。',
63:'图 7  宽条两端连通仍是基础条件。新增例外是有效横桥承接支桥，接缝以同高度双电磁波表示。',
64:'宽条沿长轴两端接岸或有效桥段。横桥自身先独立接通两岸，支桥一端可接横桥中部、另一端接岸；同高且通过连通判定后可转入支桥。不能让两个无岸支撑的桥互相循环认定为通路。',
67:'桥上材料与桥面分层占位，有效接点也允许运输磁铁。接缝的两条电磁波位于磁铁厚度内，不悬在桥面上方。普通横摆且无连通支撑的宽条、半成品、十字和石头仍不作为跨岛桥。',
69:'十字固定在接收格，不是普通步行桥。黑洞可将玩家抛落在十字实体顶面，这不改变其桥梁属性。未激活磁流时，从侧面推动会原地顺时针旋转 90°，不交换上下层和颜色。',
77:'15  制作验收与四章规划',
81:'修订 3 保留原组合分类，更新桥分支与高度定义，新增黑洞出口、石头主题和测试地块编辑。四章内容仍属于关卡编排规划；功能已实现不代表每个正式关卡均已部署。'
}
for i,t in repl.items():
 p=d.paragraphs[i];p.text=t
# Replace only the obsolete passage diagram, retaining its original physical dimensions.
shape=d.inline_shapes[6];rid=shape._inline.graphic.graphicData.pic.blipFill.blip.embed
d.part.related_parts[rid]._blob=junction.read_bytes()
shape.height=int(shape.width*400/1400)
# Update the existing bridge figure's obsolete polarity-only label.
import io
shape5=d.inline_shapes[4];rid5=shape5._inline.graphic.graphicData.pic.blipFill.blip.embed
part5=d.part.related_parts[rid5];im5=Image.open(io.BytesIO(part5.blob)).convert('RGB');g5=ImageDraw.Draw(im5)
g5.rectangle((35,370,450,424),fill=im5.getpixel((10,10)))
txt(g5,(44,382),'补入第二根平躺长条',32)
buf=io.BytesIO();im5.save(buf,format='PNG');part5._blob=buf.getvalue()
d.tables[4].rows[4].cells[1].text='宽条须两端接通；已接通横桥允许支桥接入；两格桥随原 U 型开口延伸；半成品不通行。'
d.tables[4].rows[7].cells[1].text='有效桥面及分支可运材料；站在物体顶面可跳上一层平台；H 为半格边长。'

# Append source-styled pages, then move them before the original acceptance chapter.
anchor=d.paragraphs[77]._p
before=set(d.element.body)
def p(t,style=None):return d.add_paragraph(t,style)
def h(t):
 x=p(t,'Heading 1');x.paragraph_format.page_break_before=True;return x
def sub(t):return p(t,'Heading 2')
def pic(path,caption):
 x=p('');x.paragraph_format.keep_with_next=True;x.add_run().add_picture(str(path),width=Cm(17));p(caption,'Caption')
def table(headers,rows):
 t=d.add_table(rows=1,cols=len(headers));t._tbl.remove(t._tbl.tblPr);t._tbl.insert(0,deepcopy(d.tables[0]._tbl.tblPr))
 for c,v in zip(t.rows[0].cells,headers):c.text=v
 for row in rows:
  for c,v in zip(t.add_row().cells,row):c.text=v
 t.autofit=False
 for col,w in zip(t.columns,[5,12]):col.width=Cm(w)
 for ri,row in enumerate(t.rows):
  for ci,c in enumerate(row.cells):
   c.width=Cm([5,12][ci])
   pr=c._tc.get_or_add_tcPr();sh=OxmlElement('w:shd');sh.set(qn('w:fill'),'DCEAF1' if ri==0 else 'FFFFFF');pr.append(sh)
   borders=OxmlElement('w:tcBorders')
   for edge in ['top','left','bottom','right']:
    b=OxmlElement('w:'+edge);b.set(qn('w:val'),'single');b.set(qn('w:sz'),'4');b.set(qn('w:color'),'D9D9D9');borders.append(b)
   pr.append(borders)
   for x in c.paragraphs:
    x.paragraph_format.space_after=Pt(5);x.paragraph_format.space_before=Pt(5)
    for r in x.runs:r.font.size=Pt(10);r.bold=ri==0
  row._tr.get_or_add_trPr().append(OxmlElement('w:cantSplit'))
 return t

h('10  层高与物体顶面行动')
p('本节修正上一版 H 的定义，并区分格子长度、平台层高和物体自身厚度。高度影响落点与通行，动画弧线不改变可达范围。')
pic(height,'图 10  一层等于半格边长。站上磁铁或矮石头后，可自动跳向相邻的一层平台。')
table(['对象','当前定义'],[
('格子边长 L','当前默认 1.5 个世界单位。'),('一层 H','H = L / 2，默认 0.75；两层 2H = L，默认 1.5。'),
('矮石头','基准高度 0.55；原款、4 号、5 号保持同一高度规格。'),('磁流','条形与 U 型升降台升高 H；双 U 型围绕十字的装置升高 2H。')])
sub('如何到达顶面')
p('普通从地面走向石头仍被阻挡，石头不可推动。黑洞的抛出落点可落在磁铁或石头实体顶面。玩家已经站在物体上后，可以继续走向可达顶面，或跳上相邻且不超过所在基础地面一层高的空平台。')
sub('动作表现')
p('上平台先蹬腿再腾空落地；没有独立的自由跳跃输入。石头顶部保留自然起伏与落脚区，实际落点取实体表面，不能按放大的显示包围盒或装饰轮廓估算。')

h('11  黑洞进入与传送顺序')
p('黑洞是同一玩法场景内的传送入口。先推入磁铁，随后玩家进入；玩家选择已探索区域中的目标黑洞。也允许玩家不携带磁铁单独进入，但出口必须存在可落脚表面。')
pic(sequence,'图 11  先存入磁铁，再进入选择。确认后先结算磁铁出洞，最后才抛出玩家。')
sub('方向与选择')
p('以玩家入洞的移动方向作为出洞方向：从入口右边向左进入，便从目标左边出来；向右进入则从目标右边出来，上下同理。选择期间可拖动镜头、滚轮缩放，点击可用目标直接确认。')
sub('完整时序')
p('每个入口一次储存一块磁铁，满载时不能继续推入。磁铁保持入洞姿态。玩家朝洞口跳起并吸入，选洞期间完全隐藏。选定目标后，磁铁以正常大小从洞口下方抛出；其落地、组合、挤占或反弹完成后，玩家才出洞。')
p('玩家出洞向上抛起，空中摆臂蹬腿，沿出洞方向落在目标实体表面，摔倒后起身。磁铁不能先缩小再放大，也不能等玩家落地后突然显现。跨岛切换镜头时同样必须完整显示磁铁出洞动画。')
sub('出口检查')
p('目标黑洞本身须可用且未储存其他磁铁。出口格、组合占格及竖直磁铁倒下所需格须满足占位与高度要求；不可落地时不确认该出口。空地之外的悬空格，不作为普通携带磁铁落地格；已有磁铁可组合的情况单独结算。')

h('12  黑洞出口的磁铁落点与组合')
p('出洞组合按磁铁储存时的姿态和出口原有接收方判断，沿用第 3 至 7 节的配方。先生成组合，再寻找实体顶面作为玩家落点。')
pic(assembly,'图 12  同样是异极平躺垂直条形，出口岸外成十字，出口陆地成宽条；玩家都可由黑洞抛落在其上。')
table(['出口情况','结算结果'],[
('空出口 平躺磁铁','保持平躺先抛出，玩家随后落在实体顶面。'),
('空出口 竖直条形或竖立圆环','先保持竖直出洞；玩家出洞将其向前踢倒并挤远一格，自己落在原出口格。前方必须能容纳倒下的磁铁。'),
('异极平躺条形 长轴平行','组成宽条，玩家落在宽条上。'),
('异极平躺条形 长轴垂直','陆地接收格成宽条；岸外悬空接收格成十字。'),
('其他可接受组合','当前出口支持宽条、十字、圆环及补齐后的完整两格桥；最终姿态仍遵守对应配方。')])
sub('组合限制与顶面例外')
p('出口暂不接受新生成两格桥半成品或磁流升降台作为本次携带传送结果；不要把常规接触配方全部视为可传送配方。十字可承接抛落玩家，但仍不能当普通跨岛桥。圆环落脚在实体环边，不能落进圆心空洞。')

h('13  黑洞出口挤占反弹与返回')
pic(obstacles,'图 13  同极沿出洞方向让位；石头则使磁铁反弹返回入口。玩家始终在磁铁结算后出来。')
sub('同极挤占')
p('两个未组合的同极磁铁相遇，出口原有磁铁保持姿态向外平移一格，传送来的磁铁落入腾出的出口格，玩家随后落在其上。让位随出洞过程连续完成，不合成、不瞬移。')
p('当前要求出口格有同高陆地；远端可进入空格或边缘外第一格，但不能有石头、磁铁、其他黑洞、残骸或不匹配高差。不触发多块连锁挤占；传入竖直条形会与玩家后续踢倒占格冲突，当前不接受此类同极出口。')
sub('碰石反弹')
p('磁铁正常大小抛出，撞到石头后立即反向弹回洞里，再从原入口抛回入洞前的位置。返回完成后，玩家从所选目标黑洞抛出，落在石头顶面。入口返回位置若被占用或无有效地面，则不能选此出口。')
sub('点击入口原路返回')
p('点击自己进入的黑洞，与“返回入口”按钮或 Esc 相同：出洞方向反转，磁铁先从入口返回，玩家随后飞回。平躺磁铁回到入口外格，玩家落在其上；竖直磁铁仍执行玩家出洞踢倒规则，因此不承诺所有姿态都精确恢复入洞前占格。')

h('14  石头美术与机制测试工具')
p('石头分为矮石头、圆石头和高石头，均为固定障碍。矮石头支持原款、4 号、5 号；高度规格均为 0.55，轮廓自然，顶部略有起伏但保留落脚区域。')
table(['主题选项','对应美术元素'],[
('原石 无装饰','保留岩石本体，不覆盖积雪或其他场景装饰。'),('场景一 冰雪世界','雪面覆盖、岩脚积雪、冰雪细节。'),('场景二 丛林世界','青苔、藤蔓与植被附着。'),('场景三 星环沙岩','暖色沙岩、积沙与风化层次。'),('场景四 熔岩世界','暗色火山岩、熔岩裂纹与火山环境细节。')])
sub('正式场景配置')
p('四个章节的石头使用对应主题的原款矮石头。各实例保存不同的水平旋转角，形成固定变化；每次运行不重新随机。主题与装饰只改变美术，不额外增加推动、组合或通路能力。')
sub('测试场景操作')
p('左侧直接选择矮石头、圆石头或高石头，再在下方选择场景装饰；矮石头额外选择款式。黑洞同样可选择放置，用于传送出口、组合和反弹验证。')
p('地块工具支持增加与删除，新增默认为零层，可继续设为一层或两层平台。主角、磁铁、障碍或黑洞占用的位置不可直接删除；先移开主角或清除占用物。地形编辑完成后作为当前测试起点，预设重载会重建测试布局。')
sub('新增与修正分类索引')
p('组合：保留原配方，补齐接口按现实现注明任意极性。通路：增加横桥接支桥与双电磁波。高度：改为半格一层并加入自动上台动作。黑洞：新增出入、出口组合、同极挤占、碰石返回及点击入口返回。美术与工具：新增矮石头款式、五种主题选项和地块增删。')

new=[e for e in d.element.body if e not in before and e.tag!=qn('w:sectPr')]
for e in new:anchor.addprevious(e)
# Keep chapter headings black and preserve existing page break system.
for n in ['Title','Subtitle','Heading 1','Heading 2']:d.styles[n].font.color.rgb=RGBColor(0,0,0)
d.core_properties.title='两极之间 最新规则图解策划案 修订3'
d.core_properties.subject='磁铁组合 通路高度 黑洞出口 石头与测试工具'
d.save(OUT)
print(OUT)
