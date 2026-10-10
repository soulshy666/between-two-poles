from PIL import Image, ImageDraw, ImageFont
import numpy as np
from pathlib import Path

OUT=Path(__file__).parent
S=2
im=Image.new('RGB',(1800*S,1390*S),'#ecefe9');d=ImageDraw.Draw(im)
def font(n,bold=False):return ImageFont.truetype('C:/Windows/Fonts/'+('msyhbd.ttc' if bold else 'msyh.ttc'),n*S)
def text(x,y,s,n=22,fill='#233936',bold=False):d.text((x*S,y*S),s,font=font(n,bold),fill=fill)
def line(points,fill='#708881',width=2):d.line([(int(x*S),int(y*S)) for x,y in points],fill=fill,width=width*S)
def panel(x,y,w,h):d.rounded_rectangle((x*S,y*S,(x+w)*S,(y+h)*S),radius=22*S,fill='#fafbf7')
def rings(kind):
    if kind==0:
        xy=np.array([[-.63,-.32],[-.35,-.57],[.34,-.55],[.64,-.28],[.59,.34],[.30,.56],[-.4,.53],[-.65,.22]])
        specs=[(.87,0,0,0),(1,.11,0,0),(.97,.39,.025,0),(.80,.55,.02,.01)]
    else:
        a=np.arange(10)*2*np.pi/10
        xy=np.array([[np.cos(t)*(.69+.025*np.sin(i*2)),np.sin(t)*(.56+.03*np.cos(i*3))] for i,t in enumerate(a)])
        specs=[(.86,0,0,0),(1,.09,0,0),(.91,.23,-.035,.005),(.97,.27,-.015,.0),(.83,.42,.025,.015),(.71,.55,.035,.01)]
    return [np.array([[p[0]*scale+dx,p[1]*scale+dy,z] for p in xy]) for scale,z,dx,dy in specs]
def render(kind,cx,cy,scale,snow=False):
    rr=rings(kind)
    camera=np.array([3,-5,4.5]);camera=camera/np.linalg.norm(camera)
    right=np.array([5,3,0]);right=right/np.linalg.norm(right);up=np.cross(camera,right)
    def project(v):return ((cx+np.dot(v,right)*scale)*S,(cy-np.dot(v,up)*scale)*S)
    # Soft contact shadow on the ground.
    for radius in range(12,0,-1):
        d.ellipse(((cx-scale*.71-radius)*S,(cy+scale*.1-radius*.3)*S,(cx+scale*.71+radius)*S,(cy+scale*.37+radius*.3)*S),fill=(226-radius//3,232-radius//3,224-radius//3))
    faces=[]
    for k in range(len(rr)-1):
        for i in range(len(rr[k])):
            j=(i+1)%len(rr[k]);faces.append(([rr[k][i],rr[k][j],rr[k+1][j],rr[k+1][i]],k,i,False))
    faces.append((list(rr[-1]),len(rr),0,True))
    light=np.array([-2,-4,8]);light=light/np.linalg.norm(light)
    for points,k,i,top in sorted(faces,key=lambda f:np.mean(f[0],axis=0)@camera):
        p=np.array(points);normal=np.cross(p[1]-p[0],p[2]-p[0]);normal/=np.linalg.norm(normal)
        if top:normal=np.array([0,0,1])
        elif normal@np.array([p[:,0].mean(),p[:,1].mean(),0])<0:normal=-normal
        base=np.array([137,154,159]) if kind==0 else np.array([151,157,147])
        if snow and (top or k>=len(rr)-2):base=np.array([220,239,244])
        shade=.73+.30*max(0,normal@light)+.025*np.sin(i*3+k)
        color=tuple(np.clip(base*shade,0,255).astype(int))
        d.polygon([project(v) for v in p],fill=color)
    # Shallow side seams remain below the uninterrupted landing surface.
    if kind==1:
        for k in [2,4]:
            for i in range(len(rr[k])):
                j=(i+1)%len(rr[k]);middle=(rr[k][i]+rr[k][j])*.5
                if middle[:2]@camera[:2]>0:d.line([project(rr[k][i]),project(rr[k][j])],fill='#76847e',width=2*S)
    return project

text(70,38,'矮石头 · 两种新增造型提案',42,bold=True)
text(72,108,'设计预览  /  高度固定 0.55  /  顶部连续平坦，可供黑洞抛出后落脚',23)
text(1450,52,'BETWEEN TWO POLES',17,fill='#607b71')
for x in [60,930]:panel(x,168,810,1140)
for kind,x in [(0,60),(1,930)]:
    text(x+35,196,'A  切角岩台' if kind==0 else 'B  层叠岩盘',34,bold=True)
    text(x+35,250,'整体厚实 · 大块切面 · 不规则八角轮廓' if kind==0 else '扁圆轮廓 · 错层侧壁 · 连续平顶',21)
    render(kind,x+405,560,330)
    text(x+35,681,'原石造型 / 主视图',19,fill='#647c72')
    line([(x+35,731),(x+775,731)],'#dce3da',1)
    text(x+35,754,'冰雪装饰示意',22,bold=True)
    render(kind,x+220,980,230,True)
    text(x+425,754,'俯视 / 落脚面',22,bold=True)
    r=rings(kind)
    def plan(p):return ((x+593+p[0]*210)*S,(920+p[1]*210)*S)
    d.polygon([plan(v) for v in r[1]],fill='#b9c4bf')
    d.polygon([plan(v) for v in r[-1]],fill='#758f84')
    text(x+513,899,'连续平顶',20,fill='#ffffff',bold=True)
    line([(x+35,1070),(x+775,1070)],'#dce3da',1)
    text(x+35,1090,'高度约束',21,bold=True)
    # Side elevation uses the same 0.55 / footprint proportions.
    basey=1237;scale=175
    pts=[(x+140,basey),(x+118,basey-19),(x+130,basey-67),(x+153,basey-.55*scale),(x+315,basey-.55*scale),(x+346,basey-65),(x+355,basey-20),(x+334,basey)]
    d.polygon([(a*S,b*S) for a,b in pts],fill='#91a29a')
    line([(x+95,basey),(x+376,basey)],'#a5b4ab',1)
    line([(x+390,basey),(x+390,basey-.55*scale)],width=2)
    for y in [basey,basey-.55*scale]:line([(x+382,y),(x+398,y)],width=2)
    text(x+407,basey-72,'0.55',26,bold=True)
    text(x+535,1128,'平顶不增加尖峰',19)
    text(x+535,1169,'装饰保持轻薄、贴边',19)
    text(x+535,1210,'不改变现有落脚高度',19)
text(70,1335,'仅为造型评审图，尚未添加到游戏。冰雪示意沿用当前场景风格；原石也可用于其他星球装饰。',21,fill='#526b60')
im.resize((1800,1390),Image.Resampling.LANCZOS).save(OUT/'low-rock-two-concepts.png')
