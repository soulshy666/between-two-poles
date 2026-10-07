from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import math
OUT=Path('output/design-update/figures');OUT.mkdir(exist_ok=True)
FONT='C:/Windows/Fonts/msyh.ttc';BOLD='C:/Windows/Fonts/msyhbd.ttc'
RED='#e65360';BLUE='#3292c2';INK='#203545';MUTED='#607587';GROUND='#e8eff3';EDGE='#b5c8d4'
def text(d,xy,t,size=30,fill=INK,bold=False,anchor=None):d.text(xy,t,font=ImageFont.truetype(BOLD if bold else FONT,size),fill=fill,anchor=anchor)
def shade(c,f):
 c=c.lstrip('#');return tuple(int(int(c[i:i+2],16)*f) for i in (0,2,4))
class Scene:
 def __init__(self,d,cx,cy,s=80):self.d=d;self.cx=cx;self.cy=cy;self.s=s
 def p(self,x,y,z):return(self.cx+self.s*(x+.36*z),self.cy+self.s*(.25*x-.58*z-y))
 def poly(self,points,fill,outline=None):self.d.polygon([self.p(*p) for p in points],fill=fill,outline=outline)
 def floor(self,gap=False,wide=False):
  a=1.5 if wide else .9
  pts=[(-a,0,-.9),(a,0,-.9),(a,0,.9),(-a,0,.9)]
  self.poly(pts,'#f7fafc' if gap else GROUND,EDGE)
  if gap:
   self.d.line([self.p(-a,0,-.9),self.p(a,0,.9)],fill='#dae4ea',width=2)
 def box(self,x=0,y=0,z=0,l=1.4,h=.24,w=.24,c=RED):
  a=x-l/2;b=x+l/2;k=z-w/2;j=z+w/2
  self.poly([(a,y,k),(b,y,k),(b,y+h,k),(a,y+h,k)],shade(c,.73))
  self.poly([(b,y,k),(b,y,j),(b,y+h,j),(b,y+h,k)],shade(c,.86))
  self.poly([(a,y+h,k),(b,y+h,k),(b,y+h,j),(a,y+h,j)],c)
 def bar(self,c,pose='flat',x=0,y=0,z=0):
  self.box(x,y,z,l=.24 if pose in ('standing','perp') else 1.4,h=1.4 if pose=='standing' else .24,w=1.4 if pose=='perp' else .24,c=c)
 def arc(self,c,begin=90,end=270,x=0,z=0,standing=False):
  for i in range(40):
   a=math.radians(begin+(end-begin)*i/40);b=math.radians(begin+(end-begin)*(i+1)/40)
   def pt(r,t,y):
    return (x+r*math.cos(t),.60+r*math.sin(t) if standing else y,z+y if standing else z+r*math.sin(t))
   self.poly([pt(.55,a,0),pt(.55,b,0),pt(.55,b,.22),pt(.55,a,.22)],shade(c,.75))
   self.poly([pt(.55,a,.22),pt(.55,b,.22),pt(.38,b,.22),pt(.38,a,.22)],c)
 def item(self,name,c=RED):
  if name in ['flat','perp','standing']:self.bar(c,name)
  elif name=='u':self.arc(c)
  elif name=='u_reverse':self.arc(c,-90,90)
  elif name=='wide':self.bar(BLUE,z=.14);self.bar(RED,z=-.14)
  elif name=='cross':self.bar(BLUE);self.bar(RED,'perp',y=.25)
  elif name=='ring':self.arc(BLUE,90,270);self.arc(RED,-90,90)
  elif name=='ring_up':self.arc(BLUE,90,270,standing=True);self.arc(RED,-90,90,standing=True)
  elif name=='lift':self.arc(BLUE);self.bar(RED,'standing');self.d.line([self.p(.85,.2,0),self.p(.85,1.5,0)],fill='#dea638',width=5)
  elif name in ['half','bridge']:
   self.arc(BLUE,x=-.20)
   self.bar(RED,x=1.5,z=-.45)
   self.d.line([self.p(-.20,.16,-.45),self.p(.8,.16,-.45)],fill='#d5ab43',width=4)
   if name=='bridge':
    self.bar(RED,x=1.5,z=.45);self.d.line([self.p(-.20,.16,.45),self.p(.8,.16,.45)],fill='#d5ab43',width=4)
  elif name=='rotor':
   self.arc(RED,x=-1.65);self.bar(BLUE);self.bar(RED,'perp',y=.25);self.arc(BLUE,-90,90,x=1.65)
   self.d.line([self.p(0,.7,0),self.p(0,2,0)],fill='#dea638',width=5)
def arrow(d,x1,y,x2,color=INK):
 d.line([(x1,y),(x2-10,y)],fill=color,width=5);d.polygon([(x2,y),(x2-22,y-11),(x2-22,y+11)],fill=color)
def save(im,name):im.save(OUT/(name+'.png'));return OUT/(name+'.png')
def recipe(name,rows):
 im=Image.new('RGB',(1400,80+len(rows)*270),'#f6f9fb');d=ImageDraw.Draw(im)
 text(d,(50,20),'组合条件',28,bold=True);text(d,(1150,20),'最终形态',28,bold=True)
 for i,(label,a,b,res,note) in enumerate(rows):
  y=80+i*270
  if i:d.line([(40,y),(1360,y)],fill='#dce5eb',width=2)
  text(d,(42,y+12),label,30,bold=True)
  for cx,item,c in [(270,a,RED),(650,b,BLUE),(1110,res,RED)]:
   s=Scene(d,cx,y+179,74 if res not in ['half','bridge'] or cx!=1110 else 58);s.floor(gap=("无支撑" in label or "边缘外" in label) and cx in (650,1110))
   if item=="cross" and b=="perp":s.bar(BLUE,"perp");s.bar(RED,"flat",y=.25)
   else:s.item(item,c)
  text(d,(450,y+128),'+',38,anchor='mm');arrow(d,850,y+147,935)
  text(d,(50,y+236),note,26,fill=MUTED)
 return save(im,name)
# Six distinct material products.
im=Image.new('RGB',(1400,630),'#f6f9fb');d=ImageDraw.Draw(im)
for i,(name,label,sub) in enumerate([('wide','宽条磁铁','两条并排  长度不增加'),('cross','叠放十字','上下交叉  不能直行'),('ring','圆环磁铁','两个 U 形闭合  可推动'),('half','两格桥半成品','一个 U 形加一条梁'),('bridge','完整两格桥','一个 U 形加两条梁'),('lift','单层磁流升降台','平躺 U 形加竖直条形')]):
 x=230+(i%3)*465;y=145+(i//3)*300;s=Scene(d,x-20,y,75 if name not in ['half','bridge'] else 61);s.floor();s.item(name)
 text(d,(x,y+65),label,31,bold=True,anchor='mm');text(d,(x,y+110),sub,25,fill=MUTED,anchor='mm')
save(im,'gallery')
recipe('flat-bars',[
 ('两条平躺且平行','flat','flat','wide','有支撑或在边缘外  →  都是宽条'),
 ('两条平躺且垂直  接收格有支撑','perp','flat','wide','推入方对齐接收方长轴  →  宽条'),
 ('两条平躺且垂直  接收格无支撑','perp','flat','cross','保留垂直夹角  →  十字，接收方在下')])
recipe('standing-bars',[
 ('两条都竖直','standing','standing','wide','两条倒下并排  →  宽条'),
 ('竖直条推向平躺条  平躺条长轴沿推动方向','standing','flat','wide','陆地与边缘外都成立  →  宽条'),
 ('平躺条推向竖直条  平躺条长轴沿推动方向','flat','standing','wide','陆地与边缘外都成立，接收方顺势倒下  →  宽条'),
 ('其余一竖一躺组合','standing','perp','cross','→  十字，原先竖直的条在上')])
recipe('u-combos',[
 ('平躺长条与平躺 U 形','flat','u','half','哪一块被推入都相同  →  两格桥半成品'),
 ('竖直长条与平躺 U 形','standing','u','lift','哪一块被推入都相同  →  单层磁流升降台'),
 ('两个平躺 U 形','u','u_reverse','ring','异极闭合  →  圆环，初始保持平躺')])
# Two-cell assembly, deliberately showing two separate cells and short energy connectors.
im=Image.new('RGB',(1400,690),'#f6f9fb');d=ImageDraw.Draw(im)
for row,typ in enumerate(['half','bridge']):
 y=65+row*310
 text(d,(45,y),'第一次连接' if row==0 else '补入第二根同色长条',31,bold=True)
 s=Scene(d,615,y+146,125)
 for x in [0,1.5]:
  a=x-.75;b=x+.75;s.poly([(a,0,-.75),(b,0,-.75),(b,0,.75),(a,0,.75)],GROUND,EDGE)
 s.item(typ);text(d,(1030,y+70),'不可通行' if row==0 else '可沿侧梁通行',31,bold=True)
 text(d,(1040,y+125),'占两格  固定不动',25,fill=MUTED)
 text(d,(590,y+220),'第 1 格  U 形桥首',24,anchor='mm');text(d,(845,y+260),'第 2 格  长条侧梁',24,anchor='mm')
save(im,'two-cell')
# Movement states: arrows are stage transitions, no attempt to make them motion trajectories.
im=Image.new('RGB',(1400,650),'#f6f9fb');d=ImageDraw.Draw(im)
for row,(title,a,b,c,sub) in enumerate([
 ('单个 U 形','u','u_reverse','u','每推一次翻面 180° 并前进一格  始终平躺'),
 ('圆环','ring','ring_up','ring','平躺可推立  竖立可推倒  轮缘方向可连续滚动')]):
 y=48+row*310;text(d,(45,y),title,31,bold=True)
 for j,item in enumerate([a,b,c]):
  s=Scene(d,260+j*440,y+145,85);s.floor();s.item(item)
  if j<2:arrow(d,390+j*440,y+111,540+j*440)
 text(d,(50,y+240),sub,27,fill=MUTED)
save(im,'movement')
# Top-view route checks.
def topbar(d,x,y,vertical=False):
 if vertical:
  d.rectangle((x-18,y-105,x,y+105),fill=RED);d.rectangle((x,y-105,x+18,y+105),fill=BLUE)
 else:
  d.rectangle((x-77,y-18,x+77,y),fill=RED);d.rectangle((x-77,y,x+77,y+18),fill=BLUE)
def tile2(d,x,y):d.rectangle((x-80,y-70,x+80,y+70),fill=GROUND,outline=EDGE,width=3)
im=Image.new('RGB',(1400,710),'#f6f9fb');d=ImageDraw.Draw(im)
for col in range(3):
 x=240+col*465;tile2(d,x,135)
 if col!=2:tile2(d,x,485)
 topbar(d,x,310,vertical=col!=1)
 arrow(d,x+110,315,x+175,color='#9aaab5') if False else None
 d.line([(x-60,260),(x-60,325)],fill=INK,width=5);d.polygon([(x-60,340),(x-70,320),(x-50,320)],fill=INK)
 text(d,(x,555),['两端接通  可以通过','横着放  不能上下通过','另一端悬空  不能通过'][col],27,bold=True,anchor='mm')
text(d,(50,635),'俯视图  只有沿宽条长边两端的连续通路才有效；侧面不能当作入口。',27,fill=MUTED)
save(im,'passage')
im=Image.new('RGB',(1400,440),'#f6f9fb');d=ImageDraw.Draw(im)
for i,label in enumerate(['推上桥','在桥上继续推','推到对岸']):
 x=225+i*460;s=Scene(d,x,215,85)
 for xx in [-1.5,1.5]:s.poly([(xx-.75,0,-.6),(xx+.75,0,-.6),(xx+.75,0,.6),(xx-.75,0,.6)],GROUND,EDGE)
 s.item('wide');s.bar(RED,'flat' if i==1 else 'standing',x=(-1.5 if i==0 else 0 if i==1 else 1.5),y=.24 if i==1 else 0)
 text(d,(x,342),label,31,bold=True,anchor='mm')
 if i<2:arrow(d,x+155,175,x+250)
save(im,'transport')
im=Image.new('RGB',(1400,700),'#f6f9fb');d=ImageDraw.Draw(im)
text(d,(45,25),'十字旋转',32,bold=True);s=Scene(d,390,240,100);s.floor(wide=True);s.item('cross');s.bar(RED,'standing',x=1.65);arrow(d,660,184,880);s=Scene(d,1100,240,80);s.item('standing')
text(d,(50,295),'十字中心不动  顺时针转 90°  只将邻格竖直单条向外拨一格',27,fill=MUTED)
text(d,(45,370),'两层磁流装置',32,bold=True);s=Scene(d,650,550,105);s.floor(wide=True);s.item('rotor')
text(d,(1020,430),'升高 2H',34,bold=True);text(d,(50,646),'异极 U 形开口相对  十字居中  三个相邻位置组成装置，不吸成一体',27,fill=MUTED)
save(im,'cross-rotor')
print('Created',len(list(OUT.glob('*.png'))),'figures')
